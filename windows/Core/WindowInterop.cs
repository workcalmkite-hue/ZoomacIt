using System.Windows;
using System.Windows.Interop;

namespace ZoomacItWin.Core;

/// <summary>
/// 맥의 <c>NSWindow.level</c> / <c>ignoresMouseEvents</c> /
/// <c>nonactivatingPanel</c>에 대응하는 확장 스타일 설정과,
/// 물리 픽셀 단위 창 배치.
/// </summary>
internal static class WindowInterop
{
    private static IntPtr Handle(Window w) => new WindowInteropHelper(w).Handle;

    private static void AddExStyle(Window w, int bits)
    {
        var h = Handle(w);
        if (h == IntPtr.Zero) return;
        long current = NativeMethods.GetWindowLongPtr(h, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(h, NativeMethods.GWL_EXSTYLE, new IntPtr(current | (uint)bits));
    }

    /// <summary>마우스 이벤트를 그대로 아래 앱으로 흘려보낸다 (스포트라이트 오버레이용).</summary>
    public static void MakeClickThrough(Window w) =>
        AddExStyle(w, NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE);

    /// <summary>클릭해도 현재 활성 앱의 포커스를 뺏지 않는다 (브레이크 타이머 위젯용).</summary>
    public static void MakeNoActivate(Window w) =>
        AddExStyle(w, NativeMethods.WS_EX_NOACTIVATE);

    /// <summary>Alt+Tab 목록에서 숨긴다.</summary>
    public static void HideFromAltTab(Window w) =>
        AddExStyle(w, NativeMethods.WS_EX_TOOLWINDOW);

    /// <summary>물리 픽셀 좌표로 창을 배치한다 (WPF의 Left/Top은 배율이 섞여 신뢰할 수 없다).</summary>
    public static void SetBoundsPx(Window w, Rect px)
    {
        var h = Handle(w);
        if (h == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(h, IntPtr.Zero,
            (int)Math.Round(px.X), (int)Math.Round(px.Y),
            (int)Math.Round(px.Width), (int)Math.Round(px.Height),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// 이 창을 화면 캡처에서 제외한다. 라이브 줌이 자기 자신을 다시 찍어
    /// 무한 거울이 되는 것을 막는 유일하게 확실한 방법이다(DWM이 켜진 요즘
    /// 윈도우에서는 BitBlt도 레이어드 창을 같이 찍어 온다).
    ///
    /// 부작용: 이 창은 화면 녹화·화상수업 공유에도 보이지 않는다. 교실
    /// 프로젝터(화면 복제)에는 정상적으로 보인다.
    /// </summary>
    public static bool ExcludeFromCapture(Window w)
    {
        var h = Handle(w);
        if (h == IntPtr.Zero) return false;
        return NativeMethods.SetWindowDisplayAffinity(h, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    /// <summary>창의 위치만 물리 픽셀로 옮긴다(크기는 WPF가 DIP로 관리).</summary>
    public static void SetPositionPx(Window w, Point px)
    {
        var h = Handle(w);
        if (h == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(h, IntPtr.Zero,
            (int)Math.Round(px.X), (int)Math.Round(px.Y), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>창의 현재 위치 (물리 픽셀).</summary>
    public static Point GetPositionPx(Window w)
    {
        var h = Handle(w);
        if (h == IntPtr.Zero) return new Point(0, 0);
        NativeMethods.GetWindowRect(h, out var r);
        return new Point(r.Left, r.Top);
    }

    /// <summary>창을 커서가 있는 모니터 전체에 딱 맞춘다.</summary>
    public static Rect FillScreenContainingMouse(Window w)
    {
        var bounds = ScreenHelper.ScreenContainingMouse().BoundsPx();
        SetBoundsPx(w, bounds);
        return bounds;
    }
}
