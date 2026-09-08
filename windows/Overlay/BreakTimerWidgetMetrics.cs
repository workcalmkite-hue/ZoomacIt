using System.Windows;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 브레이크 타이머 원형 위젯의 순수 기하 계산. 창/화면 없이도 테스트 가능하다.
/// (맥 원본 <c>Overlay/BreakTimerWidgetMetrics.swift</c>의 직역 이식)
///
/// 위젯은 스크롤로 크기를 바꿀 수 있으므로 모든 치수가 현재 지름의 함수다.
/// <see cref="BaseDiameter"/>는 기본값이자 비례 계산의 기준이다.
///
/// 좌표계 주의: 맥은 y축이 위로 증가하고 윈도우는 아래로 증가하지만,
/// 아래 계산(중앙 배치·화면 안으로 밀어넣기·중심 고정 리사이즈)은 모두
/// 대칭식이라 두 좌표계에서 동일하게 성립한다. 단위는 WPF의 DIP.
/// </summary>
internal static class BreakTimerWidgetMetrics
{
    /// <summary>기본 지름이자 비례 계산의 기준값.</summary>
    public const double BaseDiameter = 110;

    /// <summary>스크롤로 줄일 수 있는 최소 지름(숫자가 읽히는 한계).</summary>
    public const double MinDiameter = 70;

    /// <summary>스크롤로 키울 수 있는 최대 지름.</summary>
    public const double MaxDiameter = 220;

    /// <summary>기준 지름에서의 컨트롤 바 높이. 지름에 비례해 함께 커진다.</summary>
    private const double BaseControlBarHeight = 34;

    public static double ClampedDiameter(double diameter)
        => Math.Min(Math.Max(diameter, MinDiameter), MaxDiameter);

    /// <summary>지름에 대한 비례 계수(기준 지름에서 1.0).</summary>
    public static double Scale(double diameter) => diameter / BaseDiameter;

    /// <summary>원 아래에 호버 버튼용으로 비워두는 높이.</summary>
    public static double ControlBarHeight(double diameter) => BaseControlBarHeight * Scale(diameter);

    /// <summary>창 전체 크기(원 + 아래 컨트롤 바).</summary>
    public static Size WindowSize(double diameter)
        => new(diameter, diameter + ControlBarHeight(diameter));

    /// <summary>저장된 위치가 없을 때의 첫 표시 위치 — 해당 화면의 정중앙.</summary>
    public static Point DefaultOrigin(Rect screen, double diameter)
    {
        var size = WindowSize(diameter);
        return new Point(
            screen.X + screen.Width / 2 - size.Width / 2,
            screen.Y + screen.Height / 2 - size.Height / 2);
    }

    /// <summary>
    /// (예: 지금은 빠진 모니터에 저장돼 있던) 위치를 화면 안으로 완전히 밀어넣는다.
    /// </summary>
    public static Point Clamped(Point origin, Rect screen, double diameter)
    {
        var size = WindowSize(diameter);
        double maxX = Math.Max(screen.X, screen.Right - size.Width);
        double maxY = Math.Max(screen.Y, screen.Bottom - size.Height);
        return new Point(
            Math.Min(Math.Max(origin.X, screen.X), maxX),
            Math.Min(Math.Max(origin.Y, screen.Y), maxY));
    }

    /// <summary>
    /// 스크롤 후의 새 지름. 원본은 트랙패드(정밀 델타)와 휠을 구분했는데,
    /// 윈도우 휠은 한 칸이 120이므로 여기서는 칸 수(delta/120)를 받아
    /// 원본의 휠 비율(칸당 5pt)을 그대로 적용한다.
    /// </summary>
    public static double DiameterAfterScroll(double wheelNotches, double current)
    {
        const double pointsPerNotch = 5;
        return ClampedDiameter(current + wheelNotches * pointsPerNotch);
    }

    /// <summary>지름이 바뀌어도 창의 중심이 그대로 유지되는 새 원점.</summary>
    public static Point ResizedOrigin(Point currentOrigin, double oldDiameter, double newDiameter)
    {
        var oldSize = WindowSize(oldDiameter);
        var newSize = WindowSize(newDiameter);
        return new Point(
            currentOrigin.X + (oldSize.Width - newSize.Width) / 2,
            currentOrigin.Y + (oldSize.Height - newSize.Height) / 2);
    }
}
