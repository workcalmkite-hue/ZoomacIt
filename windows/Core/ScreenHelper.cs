using System.Windows;
using ZoomacItWin.Core;
using Forms = System.Windows.Forms;

namespace ZoomacItWin.Core;

/// <summary>
/// 맥의 <c>NSScreen.screenContainingMouse</c> + 좌표 변환에 해당하는 헬퍼.
///
/// 윈도우에서는 창 위치를 "물리 픽셀"로 다루고, 창 안의 레이아웃/그리기는
/// WPF 단위(DIP, 96dpi 기준)로 다룬다. 모니터마다 배율이 다를 수 있으므로
/// (노트북 150% + 프로젝터 100%) 두 좌표계를 섞지 않도록 여기서만 변환한다.
/// </summary>
internal static class ScreenHelper
{
    /// <summary>커서가 올라가 있는 모니터. 못 찾으면 주 모니터.</summary>
    public static Forms.Screen ScreenContainingMouse()
    {
        NativeMethods.GetCursorPos(out var p);
        return Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y))
               ?? Forms.Screen.PrimaryScreen!;
    }

    /// <summary>커서 위치 (물리 픽셀).</summary>
    public static Point CursorPositionPx()
    {
        NativeMethods.GetCursorPos(out var p);
        return new Point(p.X, p.Y);
    }

    /// <summary>모니터의 작업 영역이 아닌 전체 영역 (물리 픽셀).</summary>
    public static Rect BoundsPx(this Forms.Screen screen)
    {
        var b = screen.Bounds;
        return new Rect(b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>작업 표시줄을 제외한 영역 (물리 픽셀). 위젯/메모의 배치 제한에 쓴다.</summary>
    public static Rect WorkingAreaPx(this Forms.Screen screen)
    {
        var b = screen.WorkingArea;
        return new Rect(b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>해당 지점이 속한 모니터의 배율(1.0 = 100%, 1.5 = 150%).</summary>
    public static double ScaleAt(Point px)
    {
        var pt = new NativeMethods.POINT { X = (int)px.X, Y = (int)px.Y };
        var hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero) return 1.0;
        if (NativeMethods.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out _) != 0) return 1.0;
        return dpiX / 96.0;
    }

    /// <summary>모니터 중심 기준 배율.</summary>
    public static double ScaleOf(Forms.Screen screen)
    {
        var b = screen.Bounds;
        return ScaleAt(new Point(b.X + b.Width / 2.0, b.Y + b.Height / 2.0));
    }
}
