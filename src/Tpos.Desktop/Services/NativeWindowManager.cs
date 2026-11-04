using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Tpos.Desktop.Services;

/// <summary>Opaque, square WPF window with native DWM transitions and correct maximized work area.</summary>
internal sealed class NativeWindowManager
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmDpiChanged = 0x02E0;
    private const int DwmwaCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int MonitorDefaultToNearest = 2;
    private const int GwlStyle = -16;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;
    private const int SwpFrameChanged = 0x0020;
    private const int WindowStyleFlags = 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
    private readonly Window _window;
    private HwndSource? _source;
    private bool _closed;

    internal NativeWindowManager(Window window) => _window = window;

    internal void Attach(HwndSource source)
    {
        _source = source;
        _source.AddHook(WindowMessageHook);
        _source.CompositionTarget!.BackgroundColor = Color.FromRgb(5, 13, 22);
        EnableNativeDwmTransitions();
        SetWindowAttribute(DwmwaSystemBackdropType, 1); // DWMWA_SYSTEMBACKDROP_TYPE: none.
        SetWindowAttribute(DwmwaCornerPreference, 1); // DWMWCP_DONOTROUND.
    }

    internal void SetAuthenticationMode(bool enabled)
    {
        // Kept as the single page-mode hook used by MainWindow. No backdrop or corner change.
        UpdateWindowFrame();
    }

    internal void Detach()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
    }

    internal void UpdateWindowFrame()
    {
        if (_source is null || _closed)
        {
            return;
        }

        SetWindowAttribute(DwmwaSystemBackdropType, 1);
        SetWindowAttribute(DwmwaCornerPreference, 1);
    }

    private void SetWindowAttribute(int attribute, int value)
    {
        if (_source is not null)
        {
            DwmSetWindowAttribute(_source.Handle, attribute, ref value, sizeof(int));
        }
    }

    private void EnableNativeDwmTransitions()
    {
        if (_source is null)
        {
            return;
        }

        var style = GetWindowLong(_source.Handle, GwlStyle);
        SetWindowLong(_source.Handle, GwlStyle, style | WindowStyleFlags);
        SetWindowPos(
            _source.Handle,
            IntPtr.Zero,
            0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WmGetMinMaxInfo:
                ApplyWorkingArea(lParam);
                handled = true;
                break;
            case WmDpiChanged:
                _window.Dispatcher.BeginInvoke(new Action(UpdateWindowFrame));
                break;
        }

        return IntPtr.Zero;
    }

    private void ApplyWorkingArea(IntPtr lParam)
    {
        if (_source is null)
        {
            return;
        }

        var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(_source.Handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var work = info.WorkArea;
        var monitorArea = info.MonitorArea;
        minMax.MaxPosition.X = work.Left - monitorArea.Left;
        minMax.MaxPosition.Y = work.Top - monitorArea.Top;
        minMax.MaxSize.X = work.Right - work.Left;
        minMax.MaxSize.Y = work.Bottom - work.Top;
        minMax.MaxTrackSize = minMax.MaxSize;
        Marshal.StructureToPtr(minMax, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { internal int X; internal int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { internal int Left; internal int Top; internal int Right; internal int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        internal Point Reserved;
        internal Point MaxSize;
        internal Point MaxPosition;
        internal Point MinTrackSize;
        internal Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        internal int Size;
        internal Rectangle MonitorArea;
        internal Rectangle WorkArea;
        internal uint Flags;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
