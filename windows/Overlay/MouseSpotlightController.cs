using System.Windows;
using System.Windows.Media;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 마우스 스포트라이트의 수명 관리: 커서 추적, 모니터 이동 시 인계, 전역 클릭/휠 감시.
/// (맥 원본 <c>MouseSpotlightWindowController</c>에 대응)
///
/// 참고: 윈도우에는 PowerToys의 Find My Mouse / Mouse Highlighter라는 유사 기능이
/// 있지만, 스크롤로 크기를 즉석에서 바꾸는 조작과 퍼지는 클릭 링은 원본 쪽 동작이라
/// 그대로 옮겼다.
/// </summary>
internal sealed class MouseSpotlightController : IDisposable
{
    private MouseSpotlightWindow? _window;
    private MouseHook? _hook;

    private Rect _boundsPx;
    private double _scale = 1.0;
    private double _radius = MouseSpotlightGeometry.ClampedRadius(Settings.Shared.MouseSpotlightRadius);

    public bool IsActive => _window is not null;

    public void Toggle()
    {
        if (IsActive) Dismiss();
        else Show();
    }

    public void Show()
    {
        if (IsActive) return;

        var screen = ScreenHelper.ScreenContainingMouse();
        _boundsPx = screen.BoundsPx();
        _scale = ScreenHelper.ScaleOf(screen);

        _window = new MouseSpotlightWindow();
        _window.Show();
        WindowInterop.SetBoundsPx(_window, _boundsPx);
        UpdateHole();

        _hook = new MouseHook();
        _hook.MouseDown += OnGlobalMouseDown;
        _hook.MouseWheel += OnGlobalWheel;
        _hook.Install();

        CompositionTarget.Rendering += OnFrame;
    }

    public void Dismiss()
    {
        if (!IsActive) return;

        CompositionTarget.Rendering -= OnFrame;

        if (_hook is not null)
        {
            _hook.MouseDown -= OnGlobalMouseDown;
            _hook.MouseWheel -= OnGlobalWheel;
            _hook.Dispose();
            _hook = null;
        }

        _window?.Close();
        _window = null;

        Settings.Shared.MouseSpotlightRadius = _radius;
        Settings.Save();
    }

    /// <summary>커서 위치를 창 안의 DIP 좌표로.</summary>
    private Point ToLocal(Point cursorPx)
        => new((cursorPx.X - _boundsPx.X) / _scale, (cursorPx.Y - _boundsPx.Y) / _scale);

    private void UpdateHole()
        => _window?.UpdateHole(ToLocal(ScreenHelper.CursorPositionPx()), _radius);

    /// <summary>
    /// 매 프레임: 구멍을 커서에 다시 맞추고, 커서가 다른 모니터로 넘어갔으면
    /// 오버레이를 그 모니터로 옮긴다.
    /// </summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        if (_window is null) return;

        var screen = ScreenHelper.ScreenContainingMouse();
        var bounds = screen.BoundsPx();
        if (bounds != _boundsPx)
        {
            _boundsPx = bounds;
            _scale = ScreenHelper.ScaleOf(screen);
            WindowInterop.SetBoundsPx(_window, _boundsPx);
        }

        UpdateHole();
    }

    private void OnGlobalMouseDown(Point cursorPx)
    {
        if (_window is null) return;
        if (!_boundsPx.Contains(cursorPx)) return;
        _window.ShowClickRipple(ToLocal(cursorPx));
    }

    private void OnGlobalWheel(int delta)
    {
        double target = MouseSpotlightGeometry.RadiusAfterScroll(delta / 120.0, _radius);
        if (Math.Abs(target - _radius) < 0.01) return;
        _radius = target;
        UpdateHole();
    }

    public void Dispose() => Dismiss();
}
