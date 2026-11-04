using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Tpos.Desktop.Controls;

/// <summary>
/// Independent WPF vector interpretation of the requested ShapeWaves look.
/// No React, WebView, WebGPU, per-frame bitmap, blur or third-party runtime.
/// </summary>
public partial class ShapeWavesBackground
{
    private const double CellSize = 11;
    private const double SplashRadius = 40;
    private const double SplashStrength = 0.4;
    private const double IntroDuration = 1.6;
    private static readonly Brush[] CellBrushes = CreateBrushes();
    private static readonly Brush ContourBrush = CreateBrush("#273B82F6");
    private readonly List<WaveCell> _cells = [];
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastFrame;
    private Point? _pointer;
    private Window? _window;
    private bool _rendering;
    private double _sceneWidth;
    private double _sceneHeight;

    public ShapeWavesBackground()
    {
        InitializeComponent();
        Loaded += ShapeWavesBackground_OnLoaded;
        Unloaded += ShapeWavesBackground_OnUnloaded;
        SizeChanged += (_, _) => RebuildScene();
        MouseMove += (_, e) => _pointer = e.GetPosition(this);
        MouseLeave += (_, _) => _pointer = null;
    }

    private static Brush[] CreateBrushes() =>
    [
        CreateBrush("#FF3B82F6"),
        CreateBrush("#FF60A5FA"),
        CreateBrush("#FF2563EB")
    ];

    private static SolidColorBrush CreateBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private void ShapeWavesBackground_OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.StateChanged += Window_OnStateChanged;
        }

        _startedAt = DateTimeOffset.UtcNow;
        _lastFrame = DateTimeOffset.MinValue;
        RebuildScene();
        UpdateRendering();
    }

    private void ShapeWavesBackground_OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopRendering();
        if (_window is not null)
        {
            _window.StateChanged -= Window_OnStateChanged;
            _window = null;
        }
    }

    private void Window_OnStateChanged(object? sender, EventArgs e) => UpdateRendering();

    private void UpdateRendering()
    {
        var shouldRender = IsLoaded &&
            SystemParameters.ClientAreaAnimation &&
            (_window is null || _window.WindowState != WindowState.Minimized);
        if (shouldRender)
        {
            StartRendering();
        }
        else
        {
            StopRendering();
            DrawFrame(IntroDuration);
        }
    }

    private void StartRendering()
    {
        if (_rendering)
        {
            return;
        }

        _rendering = true;
        _lastFrame = DateTimeOffset.MinValue;
        CompositionTarget.Rendering += CompositionTarget_OnRendering;
    }

    private void StopRendering()
    {
        if (!_rendering)
        {
            return;
        }

        _rendering = false;
        CompositionTarget.Rendering -= CompositionTarget_OnRendering;
    }

    private void CompositionTarget_OnRendering(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastFrame < TimeSpan.FromMilliseconds(33))
        {
            return;
        }

        _lastFrame = now;
        DrawFrame((now - _startedAt).TotalSeconds);
    }

    private void RebuildScene()
    {
        if (!IsLoaded || ActualWidth < 10 || ActualHeight < 10)
        {
            return;
        }

        if (Math.Abs(ActualWidth - _sceneWidth) < 0.5 && Math.Abs(ActualHeight - _sceneHeight) < 0.5)
        {
            return;
        }

        _sceneWidth = ActualWidth;
        _sceneHeight = ActualHeight;
        ContourCanvas.Children.Clear();
        WaveCanvas.Children.Clear();
        _cells.Clear();
        BuildContours();
        BuildCodeCells();
        BuildAmbientCells();
        DrawFrame((DateTimeOffset.UtcNow - _startedAt).TotalSeconds);
    }

    private void BuildContours()
    {
        for (var line = 0; line < 9; line++)
        {
            var y = _sceneHeight * (line + 1) / 10;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(-30, y), true, false);
                context.BezierTo(
                    new Point(_sceneWidth * 0.2, y - 55 + line * 5),
                    new Point(_sceneWidth * 0.55, y + 65 - line * 4),
                    new Point(_sceneWidth + 30, y - 15),
                    true,
                    true);
            }

            geometry.Freeze();
            ContourCanvas.Children.Add(new Path
            {
                Data = geometry,
                Stroke = ContourBrush,
                StrokeThickness = 1,
                Opacity = 0.42
            });
        }
    }

    private void BuildCodeCells()
    {
        string[] left =
        [
            "..##.",
            ".##..",
            "##...",
            "##...",
            ".##..",
            "..##."
        ];
        string[] slash =
        [
            "...##",
            "..##.",
            "..##.",
            ".##..",
            ".##..",
            "##..."
        ];
        string[] right =
        [
            ".##..",
            "..##.",
            "...##",
            "...##",
            "..##.",
            ".##.."
        ];

        const int totalColumns = 17;
        var originX = (_sceneWidth - totalColumns * CellSize) / 2;
        var originY = Math.Max(120, _sceneHeight * 0.42);
        var patterns = new[] { left, slash, right };
        for (var symbol = 0; symbol < patterns.Length; symbol++)
        {
            var pattern = patterns[symbol];
            for (var row = 0; row < pattern.Length; row++)
            {
                for (var column = 0; column < pattern[row].Length; column++)
                {
                    if (pattern[row][column] != '#')
                    {
                        continue;
                    }

                    AddCell(
                        originX + (symbol * 6 + column) * CellSize,
                        originY + row * CellSize,
                        symbol * 41 + row * 7 + column,
                        0.55);
                }
            }
        }
    }

    private void BuildAmbientCells()
    {
        var random = new Random(2306);
        var count = Math.Min(85, Math.Max(35, (int)(_sceneWidth * _sceneHeight / 6500)));
        for (var index = 0; index < count; index++)
        {
            AddCell(
                random.NextDouble() * Math.Max(1, _sceneWidth - CellSize),
                random.NextDouble() * Math.Max(1, _sceneHeight - CellSize),
                200 + index,
                0.12 + random.NextDouble() * 0.18);
        }
    }

    private void AddCell(double x, double y, int index, double opacity)
    {
        var size = 3.4 + index % 3;
        Shape shape = index % 3 switch
        {
            0 => new Ellipse { Width = size, Height = size },
            1 => new Rectangle { Width = size, Height = size, RadiusX = 0.7, RadiusY = 0.7 },
            _ => new Polygon
            {
                Points = [new Point(size / 2, 0), new Point(size, size), new Point(0, size)],
                Width = size,
                Height = size
            }
        };

        shape.Fill = CellBrushes[index % CellBrushes.Length];
        shape.IsHitTestVisible = false;
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1, size / 2, size / 2);
        shape.RenderTransform = new TransformGroup { Children = [translate, scale] };
        WaveCanvas.Children.Add(shape);
        _cells.Add(new WaveCell(shape, translate, scale, x, y, index * 0.37, opacity));
    }

    private void DrawFrame(double seconds)
    {
        var intro = Math.Clamp(seconds / IntroDuration, 0, 1);
        var easedIntro = 1 - Math.Pow(1 - intro, 3);
        foreach (var cell in _cells)
        {
            var x = Math.Sin(seconds * 4 + cell.Phase) * 1.2;
            var y = Math.Cos(seconds * 3.2 + cell.Phase) * 0.9;
            var hover = 0.0;
            if (_pointer is { } pointer)
            {
                var dx = cell.X - pointer.X;
                var dy = cell.Y - pointer.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < SplashRadius)
                {
                    var strength = (1 - distance / SplashRadius) * SplashStrength;
                    var divisor = Math.Max(distance, 1);
                    x += dx / divisor * strength * 18;
                    y += dy / divisor * strength * 18;
                    hover = strength;
                }
            }

            cell.Translate.X = x;
            cell.Translate.Y = y;
            cell.Scale.ScaleX = Math.Max(0.05, easedIntro + hover * 0.35);
            cell.Scale.ScaleY = cell.Scale.ScaleX;
            cell.Shape.Opacity = Math.Min(1, cell.BaseOpacity * easedIntro * (0.86 + Math.Sin(seconds * 2 + cell.Phase) * 0.14) + hover);
            if (hover > 0)
            {
                cell.Shape.Fill = Brushes.White;
            }
            else
            {
                cell.Shape.Fill = CellBrushes[(int)Math.Round(cell.Phase / 0.37) % CellBrushes.Length];
            }
        }
    }

    private sealed record WaveCell(
        Shape Shape,
        TranslateTransform Translate,
        ScaleTransform Scale,
        double X,
        double Y,
        double Phase,
        double BaseOpacity);
}
