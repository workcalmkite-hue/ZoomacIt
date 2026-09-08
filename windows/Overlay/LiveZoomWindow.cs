using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 라이브 줌: 화면이 멈추지 않고 계속 움직이는 상태로 커서 주변을 확대해 보여준다.
/// (맥 원본 <c>LiveZoomView</c> + <c>LiveZoomWindowController</c>)
///
/// 창을 레이어드로 만들어 두면 GDI 캡처가 이 창을 건너뛴다 — 자기 자신을 되찍어
/// 무한 거울이 되는 것을 막는 핵심이다.
/// </summary>
internal sealed class LiveZoomWindow : Window
{
    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly ScreenCapture.Repeating _capturer = new();
    private readonly Rect _screenPx;

    private DispatcherTimer? _timer;
    private double _zoom;

    public LiveZoomWindow()
    {
        var screen = ScreenHelper.ScreenContainingMouse();
        _screenPx = screen.BoundsPx();
        _zoom = ZoomMath.ClampZoomLevel(Settings.Shared.DefaultZoomLevel);

        WindowStyle = WindowStyle.None;
        // 불투명한 내용을 그리지만 레이어드 창이어야 캡처에서 제외된다.
        AllowsTransparency = true;
        Background = Brushes.Black;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;

        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.Linear);
        Content = _image;

        InputMethod.SetIsInputMethodEnabled(this, false);

        SourceInitialized += (_, _) =>
        {
            WindowInterop.SetBoundsPx(this, _screenPx);
            WindowInterop.HideFromAltTab(this);

            // 이게 없으면 매 프레임 자기 화면을 다시 찍어 몇 프레임 만에
            // 화면이 뭉개진다(무한 거울).
            if (!WindowInterop.ExcludeFromCapture(this))
                Log.Write("라이브 줌: 캡처 제외 설정 실패 — 화면이 뭉개질 수 있음");
        };

        Loaded += (_, _) =>
        {
            Activate();
            Keyboard.Focus(this);
            StartCaptureLoop();
        };

        MouseWheel += OnWheel;
        MouseRightButtonDown += (_, _) => Close();
        KeyDown += OnKeyDownHandler;
    }

    private void StartCaptureLoop()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 30.0)
        };
        _timer.Tick += (_, _) => Grab();
        _timer.Start();
        Grab();
    }

    private void Grab()
    {
        var cursor = ScreenHelper.CursorPositionPx();
        var region = ZoomMath.LiveCaptureRect(_zoom, cursor, _screenPx);
        var frame = _capturer.Capture(region);
        if (frame is not null) _image.Source = frame;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        _zoom = ZoomMath.ClampZoomLevel(_zoom + Math.Sign(e.Delta) * ZoomMath.ZoomStep);
        Grab();
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
                Grab();
                break;

            case Key.Down or Key.OemMinus or Key.Subtract:
                _zoom = ZoomMath.ClampZoomLevel(_zoom - ZoomMath.ZoomStep);
                Grab();
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        _capturer.Dispose();
        base.OnClosed(e);
    }
}
