using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;

namespace Tpos.Desktop.Services;

/// <summary>
/// Applies documented DWM backdrop, corner and work-area behavior without a private
/// composition API. Native backdrop patterns are adapted from ModernWpf (MIT);
/// legacy blur-behind interop follows picoe/Eto GlassHelper (BSD-3-Clause).
/// </summary>
internal sealed class NativeWindowManager
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmDwmCompositionChanged = 0x031E;
    private const int WmDpiChanged = 0x02E0;
    private const int WmSettingChange = 0x001A;
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
    private readonly Border _surface;
    private HwndSource? _source;
    private bool _authenticationMode = true;
    private bool _nativeCornerSupport;
    private bool _closed;

    internal NativeWindowManager(Window window, Border surface)
    {
        _window = window;
        _surface = surface;
    }

    internal string EffectiveBackdrop { get; private set; } = "Opaque";

    internal string EffectiveCorners { get; private set; } = "Square";

    internal void Attach(HwndSource source)
    {
        _source = source;
        _source.AddHook(WindowMessageHook);
        _nativeCornerSupport = Environment.OSVersion.Version.Build >= 22000;
        EnableNativeDwmAnimations();
        ApplyBackdrop();
        UpdateWindowFrame();
    }

    internal void SetAuthenticationMode(bool enabled)
    {
        if (_authenticationMode == enabled)
        {
            return;
        }

        _authenticationMode = enabled;
        ApplyBackdrop();
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

        var maximized = _window.WindowState == WindowState.Maximized;
        var radius = _authenticationMode ? 22 : 18;
        _surface.CornerRadius = new CornerRadius(maximized ? 0 : radius);
        SetCornerPreference(maximized ? 1 : 2);

        if (maximized || _nativeCornerSupport)
        {
            SetWindowRegion(IntPtr.Zero);
            EffectiveCorners = maximized ? "Square-Maximized" : "DWM-Rounded";
            return;
        }

        var scale = _source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        GetWindowRect(_source.Handle, out var rectangle);
        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Bottom - rectangle.Top;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var pixelRadius = Math.Max(2, (int)Math.Round(radius * scale));
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, pixelRadius * 2, pixelRadius * 2);
        if (region == IntPtr.Zero)
        {
            EffectiveCorners = "Surface-Fallback";
            return;
        }

        if (SetWindowRgn(_source.Handle, region, true) == 0)
        {
            DeleteObject(region);
            EffectiveCorners = "Surface-Fallback";
        }
        else
        {
            // The system owns a region after SetWindowRgn succeeds.
            EffectiveCorners = "Native-Rounded-Region";
        }
    }

    private void ApplyBackdrop()
    {
        if (_source is null || _closed)
        {
            return;
        }

        var chrome = WindowChrome.GetWindowChrome(_window);
        var canUseGlass = _authenticationMode &&
            !_window.AllowsTransparency &&
            !SystemParameters.HighContrast &&
            IsTransparencyEnabled() &&
            IsCompositionEnabled();

        if (!canUseGlass)
        {
            DisableBackdrop(chrome);
            return;
        }

        _source.CompositionTarget!.BackgroundColor = Colors.Transparent;
        if (chrome is not null)
        {
            chrome.GlassFrameThickness = new Thickness(-1);
        }

        if (Environment.OSVersion.Version.Build >= 22621 &&
            ExtendGlassFrame(true) &&
            SetWindowAttribute(DwmwaSystemBackdropType, 3))
        {
            EffectiveBackdrop = "Windows11-DesktopAcrylic";
            SetGlassSurface();
            return;
        }

        if (ExtendGlassFrame(true) && EnableBlurBehind(true))
        {
            EffectiveBackdrop = "Documented-DWM-BlurBehind";
            SetGlassSurface();
            return;
        }

        DisableBackdrop(chrome);
    }

    private void DisableBackdrop(WindowChrome? chrome)
    {
        if (_source is null)
        {
            return;
        }

        if (Environment.OSVersion.Version.Build >= 22621)
        {
            SetWindowAttribute(DwmwaSystemBackdropType, 1);
        }

        EnableBlurBehind(false);
        ExtendGlassFrame(false);
        if (chrome is not null)
        {
            chrome.GlassFrameThickness = new Thickness(0);
        }

        _source.CompositionTarget!.BackgroundColor = Color.FromRgb(11, 24, 34);
        _surface.Background = CreateBrush(_authenticationMode ? "#F20B1822" : "#FF0B1822");
        EffectiveBackdrop = _authenticationMode ? "Opaque-Fallback" : "Opaque-Dashboard";
    }

    private void SetGlassSurface() => _surface.Background = CreateBrush("#A80B1822");

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static bool IsTransparencyEnabled()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency",
                1);
            return value is null || Convert.ToInt32(value) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsCompositionEnabled()
    {
        try
        {
            return DwmIsCompositionEnabled(out var enabled) >= 0 && enabled;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    private bool ExtendGlassFrame(bool enabled)
    {
        if (_source is null)
        {
            return false;
        }

        var margins = new Margins(enabled ? -1 : 0);
        return DwmExtendFrameIntoClientArea(_source.Handle, ref margins) >= 0;
    }

    private bool EnableBlurBehind(bool enabled)
    {
        if (_source is null)
        {
            return false;
        }

        var blur = new BlurBehind
        {
            Flags = 0x1,
            Enable = enabled,
            Region = IntPtr.Zero,
            TransitionOnMaximized = false
        };
        return DwmEnableBlurBehindWindow(_source.Handle, ref blur) >= 0;
    }

    private bool SetWindowAttribute(int attribute, int value)
    {
        if (_source is null)
        {
            return false;
        }

        return DwmSetWindowAttribute(_source.Handle, attribute, ref value, sizeof(int)) >= 0;
    }

    private void SetCornerPreference(int preference)
    {
        if (_nativeCornerSupport)
        {
            SetWindowAttribute(DwmwaCornerPreference, preference);
        }
    }

    private void SetWindowRegion(IntPtr region)
    {
        if (_source is not null)
        {
            SetWindowRgn(_source.Handle, region, true);
        }
    }

    /// <summary>Retain native DWM transitions with a custom WPF title bar (FluentWpfCore, MIT).</summary>
    private void EnableNativeDwmAnimations()
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
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        switch (message)
        {
            case WmGetMinMaxInfo:
                ApplyWorkingArea(lParam);
                handled = true;
                break;
            case WmDwmCompositionChanged:
            case WmSettingChange:
                _window.Dispatcher.BeginInvoke(new Action(ApplyBackdrop));
                break;
            case WmDpiChanged:
                _window.Dispatcher.BeginInvoke(new Action(UpdateWindowFrame));
                break;
            case 0x0005: // WM_SIZE
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
    private readonly struct Margins(int value)
    {
        private readonly int Left = value;
        private readonly int Right = value;
        private readonly int Top = value;
        private readonly int Bottom = value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlurBehind
    {
        internal uint Flags;
        [MarshalAs(UnmanagedType.Bool)] internal bool Enable;
        internal IntPtr Region;
        [MarshalAs(UnmanagedType.Bool)] internal bool TransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

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
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref BlurBehind blurBehind);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rectangle rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int ellipseWidth,
        int ellipseHeight);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);
}
