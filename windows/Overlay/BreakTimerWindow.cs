using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 브레이크 타이머 원형 위젯. 화면을 덮지 않고 한쪽에 떠 있으며, 드래그로 옮기고
/// 스크롤로 크기를 바꾼다. 다른 앱의 포커스를 절대 뺏지 않으므로 PPT를 넘기면서
/// 켜둘 수 있다 — ZoomIt의 전체화면 타이머(Ctrl+3)로는 안 되는 부분이다.
/// (맥 원본 <c>BreakTimerWindow</c> + <c>BreakTimerWindowController</c>에 대응)
/// </summary>
internal sealed class BreakTimerWindow : Window
{
    private readonly BreakTimerState _state = new();
    private readonly BreakTimerRingElement _ring;
    private readonly Canvas _canvas = new();
    private readonly StackPanel _controls = new() { Orientation = Orientation.Horizontal };

    private readonly RoundIconButton _minus = new("−", "1분 빼기");
    private readonly RoundIconButton _playPause = new("▶", "시작 / 일시정지");
    private readonly RoundIconButton _plus = new("＋", "1분 더하기");
    private readonly RoundIconButton _close = new("✕", "닫기");

    private DispatcherTimer? _secondTimer;
    private DispatcherTimer? _pulseTimer;

    private double _diameter;
    private Point _originPx;
    private double _scale = 1.0;

    private bool _dragging;
    private Point _dragStartCursorPx;
    private Point _dragStartOriginPx;

    public BreakTimerWindow()
    {
        _diameter = BreakTimerWidgetMetrics.ClampedDiameter(Settings.Shared.BreakTimerDiameter);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _ring = new BreakTimerRingElement
        {
            State = _state,
            Diameter = _diameter,
            RingColor = ParseColor(Settings.Shared.BreakTimerColor),
            RingOpacity = Math.Clamp(Settings.Shared.BreakTimerOpacity, 0.1, 1.0)
        };

        _canvas.Children.Add(_ring);
        _canvas.Children.Add(_controls);
        foreach (var b in new[] { _minus, _playPause, _plus, _close })
            _controls.Children.Add(b);
        Content = _canvas;

        _minus.Click += () => AdjustTime(-1);
        _plus.Click += () => AdjustTime(+1);
        _playPause.Click += TogglePlayPause;
        _close.Click += Close;

        _ring.MouseLeftButtonDown += OnDragStart;
        _ring.MouseMove += OnDragMove;
        _ring.MouseLeftButtonUp += OnDragEnd;
        PreviewMouseWheel += OnWheel;
        MouseEnter += (_, _) => SetControlsHidden(false);
        MouseLeave += (_, _) => UpdateControlsVisibility();

        SourceInitialized += (_, _) =>
        {
            WindowInterop.MakeNoActivate(this);
            WindowInterop.HideFromAltTab(this);
            RestorePosition();
            ApplyGeometry();
        };

        Loaded += (_, _) =>
        {
            // 멈춘 상태로 시작하므로 재생 버튼은 호버 없이도 보여야 한다.
            SetControlsHidden(false);
            StartSecondTimer();
        };
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return Color.FromRgb(0xFF, 0x3B, 0x30);
        }
    }

    // --- 배치 ---

    private static Rect ToDip(Rect px, double scale)
        => new(px.X / scale, px.Y / scale, px.Width / scale, px.Height / scale);

    private void RestorePosition()
    {
        var screen = ScreenHelper.ScreenContainingMouse();
        _scale = ScreenHelper.ScaleOf(screen);

        if (Settings.Shared.BreakTimerPositionXPx is double x &&
            Settings.Shared.BreakTimerPositionYPx is double y)
        {
            // 저장된 좌표가 지금 붙어 있는 모니터 밖이면 그 모니터 안으로 끌어온다.
            var saved = new Point(x, y);
            var host = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)saved.X, (int)saved.Y));
            _scale = ScreenHelper.ScaleOf(host);
            var workDip = ToDip(host.WorkingAreaPx(), _scale);
            var clamped = BreakTimerWidgetMetrics.Clamped(
                new Point(saved.X / _scale, saved.Y / _scale), workDip, _diameter);
            _originPx = new Point(clamped.X * _scale, clamped.Y * _scale);
            return;
        }

        var work = ToDip(screen.WorkingAreaPx(), _scale);
        var origin = BreakTimerWidgetMetrics.DefaultOrigin(work, _diameter);
        _originPx = new Point(origin.X * _scale, origin.Y * _scale);
    }

    private void ApplyGeometry()
    {
        var size = BreakTimerWidgetMetrics.WindowSize(_diameter);
        Width = size.Width;
        Height = size.Height;

        _ring.Diameter = _diameter;
        _ring.Width = size.Width;
        _ring.Height = size.Height;
        Canvas.SetLeft(_ring, 0);
        Canvas.SetTop(_ring, 0);

        LayoutControls(size);
        WindowInterop.SetPositionPx(this, _originPx);
        _ring.Refresh();
    }

    private void LayoutControls(Size windowSize)
    {
        double scale = BreakTimerWidgetMetrics.Scale(_diameter);
        double buttonSize = 22 * scale;
        double spacing = 4 * scale;

        foreach (var child in _controls.Children.OfType<RoundIconButton>())
        {
            child.SetSize(buttonSize);
            child.Margin = new Thickness(spacing / 2, 0, spacing / 2, 0);
        }

        double totalWidth = buttonSize * 4 + spacing * 3;
        Canvas.SetLeft(_controls, (windowSize.Width - totalWidth) / 2);
        Canvas.SetTop(_controls, _diameter +
            (BreakTimerWidgetMetrics.ControlBarHeight(_diameter) - buttonSize) / 2);
    }

    /// <summary>
    /// 컨트롤 표시. 타이머가 도는 동안에는 시간 조정/재생 버튼을 감추지만
    /// 닫기 버튼만은 항상 남긴다 — 수업 중에 위젯을 끄려고 헤매는 일은 없어야 한다.
    /// </summary>
    private void SetControlsHidden(bool hidden)
    {
        foreach (var b in new[] { _minus, _playPause, _plus })
            b.Opacity = hidden ? 0 : 1;

        // 평소엔 옅게 두고 호버하면 또렷해진다 — 언제든 누를 수 있다는 표시다.
        _close.Opacity = hidden ? 0.55 : 1;
    }

    /// <summary>지금 상태에 맞게 컨트롤을 다시 계산한다. 멈춰 있거나 만료된 뒤는 계속 보여준다.</summary>
    private void UpdateControlsVisibility()
        => SetControlsHidden(!IsMouseOver && !_state.IsPaused && !_state.IsExpired);

    private void SavePosition()
    {
        Settings.Shared.BreakTimerPositionXPx = _originPx.X;
        Settings.Shared.BreakTimerPositionYPx = _originPx.Y;
        Settings.Shared.BreakTimerDiameter = _diameter;
        Settings.Save();
    }

    // --- 드래그 ---

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStartCursorPx = ScreenHelper.CursorPositionPx();
        _dragStartOriginPx = _originPx;
        _ring.CaptureMouse();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var cursor = ScreenHelper.CursorPositionPx();
        _originPx = new Point(
            _dragStartOriginPx.X + (cursor.X - _dragStartCursorPx.X),
            _dragStartOriginPx.Y + (cursor.Y - _dragStartCursorPx.Y));
        WindowInterop.SetPositionPx(this, _originPx);
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _ring.ReleaseMouseCapture();

        // 다른 배율의 모니터로 옮겨졌을 수 있으므로 배율을 다시 읽는다.
        var center = new Point(_originPx.X + ActualWidth * _scale / 2, _originPx.Y + 10);
        _scale = ScreenHelper.ScaleAt(center);
        SavePosition();
    }

    // --- 스크롤 크기 조절 ---

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        double target = BreakTimerWidgetMetrics.DiameterAfterScroll(e.Delta / 120.0, _diameter);
        if (Math.Abs(target - _diameter) < 0.01) return;

        var originDip = new Point(_originPx.X / _scale, _originPx.Y / _scale);
        var resized = BreakTimerWidgetMetrics.ResizedOrigin(originDip, _diameter, target);

        var host = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)_originPx.X, (int)_originPx.Y));
        var workDip = ToDip(host.WorkingAreaPx(), _scale);
        var clamped = BreakTimerWidgetMetrics.Clamped(resized, workDip, target);

        _diameter = target;
        _originPx = new Point(clamped.X * _scale, clamped.Y * _scale);
        ApplyGeometry();
        SavePosition();
        e.Handled = true;
    }

    // --- 타이머 ---

    private void StartSecondTimer()
    {
        _secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _secondTimer.Tick += (_, _) =>
        {
            if (_state.IsPaused || !_state.HasStarted) return;

            bool justExpired = _state.Tick();
            if (justExpired)
            {
                if (_state.PlaySoundOnExpiration) SystemSounds.Exclamation.Play();
                StartPulseTimer();
                // 시간이 다 됐으니 시간을 더하거나 끌 수 있게 컨트롤을 드러낸다.
                UpdateControlsVisibility();
            }
            _ring.Refresh();
        };
        _secondTimer.Start();
    }

    /// <summary>만료 중에만 도는 깜빡임 타이머. 평소에는 초당 1회 갱신으로 충분하다.</summary>
    private void StartPulseTimer()
    {
        if (_pulseTimer is not null) return;
        _pulseTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 30.0)
        };
        _pulseTimer.Tick += (_, _) =>
        {
            if (!_state.IsExpired)
            {
                _pulseTimer?.Stop();
                _pulseTimer = null;
                return;
            }
            _ring.Refresh();
        };
        _pulseTimer.Start();
    }

    private void AdjustTime(int minutes)
    {
        _state.AdjustTime(minutes);
        if (!_state.IsExpired)
        {
            _pulseTimer?.Stop();
            _pulseTimer = null;
        }
        _ring.Refresh();
    }

    private void TogglePlayPause()
    {
        _state.IsPaused = !_state.IsPaused;
        if (!_state.IsPaused) _state.HasStarted = true;

        _playPause.Glyph = _state.IsPaused ? "▶" : "❚❚";
        // 멈춰 있으면 컨트롤을 계속 보여준다(재생 버튼을 찾을 수 있게).
        UpdateControlsVisibility();
    }

    protected override void OnClosed(EventArgs e)
    {
        _secondTimer?.Stop();
        _pulseTimer?.Stop();
        _secondTimer = null;
        _pulseTimer = null;
        SavePosition();
        base.OnClosed(e);
    }
}
