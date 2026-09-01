using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 정지 줌: 지금 화면을 한 장 찍어 얼어붙은 그림으로 띄우고, 마우스로 훑으며
/// 확대해 보여준다. (맥 원본 <c>StillZoomView</c> + <c>StillZoomWindowController</c>)
///
/// 화면을 통째로 덮는 불투명 창이라 알파 트릭이 필요 없다.
/// </summary>
internal sealed class StillZoomWindow : Window
{
    /// <summary>줌 화면 위에 바로 그리기로 넘어갈 때 (지금 보이는 그림을 넘겨준다).</summary>
    public event Action<ImageSource>? DrawRequested;

    private readonly Rectangle _canvas = new();
    private readonly ImageBrush _brush;
    private readonly Size _imageSize;
    private readonly double _scale;

    private double _zoom;
    private Point _panCenterPx;

    public StillZoomWindow()
    {
        var screen = ScreenHelper.ScreenContainingMouse();
        var boundsPx = screen.BoundsPx();
        _scale = ScreenHelper.ScaleOf(screen);

        // 창을 띄우기 전에 찍는다 — 그래야 자기 자신이 찍히지 않는다.
        var source = ScreenCapture.CaptureRegion(boundsPx);
        _imageSize = new Size(source.PixelWidth, source.PixelHeight);

        var cursor = ScreenHelper.CursorPositionPx();
        _panCenterPx = new Point(cursor.X - boundsPx.X, cursor.Y - boundsPx.Y);
        _zoom = ZoomMath.ClampZoomLevel(Settings.Shared.DefaultZoomLevel);

        _brush = new ImageBrush(source)
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
        };
        _canvas.Fill = _brush;
        RenderOptions.SetBitmapScalingMode(_canvas, BitmapScalingMode.Linear);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Black;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;
        Content = _canvas;

        // 색상/도구 키가 한글 IME에 먹히지 않도록 (드로잉 오버레이와 같은 이유)
        InputMethod.SetIsInputMethodEnabled(this, false);

        SourceInitialized += (_, _) =>
        {
            WindowInterop.SetBoundsPx(this, boundsPx);
            WindowInterop.HideFromAltTab(this);
        };

        Loaded += (_, _) =>
        {
            Activate();
            Keyboard.Focus(this);
            UpdateViewbox();
        };

        MouseMove += OnMouseMoveHandler;
        MouseWheel += OnWheel;
        MouseRightButtonDown += (_, _) => Close();
        KeyDown += OnKeyDownHandler;
    }

    private void UpdateViewbox()
        => _brush.Viewbox = ZoomMath.VisibleContentsRect(_zoom, _panCenterPx, _imageSize);

    private void OnMouseMoveHandler(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        _panCenterPx = new Point(p.X * _scale, p.Y * _scale);
        UpdateViewbox();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom = ZoomMath.ClampZoomLevel(_zoom + Math.Sign(e.Delta) * ZoomMath.ZoomStep);
        UpdateViewbox();
    }

    private void OnKeyDownHandler(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey
                : e.Key == Key.System ? e.SystemKey
                : e.Key;

        switch (key)
        {
            case Key.Escape:
                Close();
                break;

            case Key.Up or Key.OemPlus or Key.Add:
                _zoom = ZoomMath.ClampZoomLevel(_zoom + ZoomMath.ZoomStep);
                UpdateViewbox();
                break;

            case Key.Down or Key.OemMinus or Key.Subtract:
                _zoom = ZoomMath.ClampZoomLevel(_zoom - ZoomMath.ZoomStep);
                UpdateViewbox();
                break;

            case Key.D: // 지금 보이는 확대 화면 위에 바로 그리기 (원본의 Zoom → Draw 전환)
                DrawRequested?.Invoke(RenderCurrentView());
                Close();
                break;
        }
    }

    /// <summary>지금 화면에 보이는 (확대된) 그림을 그대로 한 장으로 만든다.</summary>
    private ImageSource RenderCurrentView()
    {
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)(ActualWidth * _scale)),
            Math.Max(1, (int)(ActualHeight * _scale)),
            96 * _scale, 96 * _scale, PixelFormats.Pbgra32);
        bitmap.Render(_canvas);
        bitmap.Freeze();
        return bitmap;
    }
}
