using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 마우스 스포트라이트(커서를 따라다니는 클릭 통과 화면 딤)의 순수 기하 계산.
/// (맥 원본 <c>Overlay/MouseSpotlightGeometry.swift</c>의 직역 이식)
/// </summary>
internal static class MouseSpotlightGeometry
{
    /// <summary>기본 반지름.</summary>
    public const double DefaultRadius = 150;

    /// <summary>스크롤로 줄일 수 있는 최소 반지름.</summary>
    public const double MinRadius = 60;

    /// <summary>스크롤로 키울 수 있는 최대 반지름.</summary>
    public const double MaxRadius = 400;

    public static double ClampedRadius(double radius)
        => Math.Min(Math.Max(radius, MinRadius), MaxRadius);

    /// <summary>
    /// 스크롤 후의 새 반지름. 브레이크 타이머 위젯과 같은 방식으로,
    /// 윈도우 휠 한 칸(=120)을 원본의 휠 비율(칸당 5pt)에 대응시킨다.
    /// </summary>
    public static double RadiusAfterScroll(double wheelNotches, double current)
    {
        const double pointsPerNotch = 5;
        return ClampedRadius(current + wheelNotches * pointsPerNotch);
    }

    /// <summary>
    /// <paramref name="bounds"/> 전체에서 <paramref name="center"/> 주위 원만
    /// 빠진 도형. 원이 "구멍"으로 보이려면 반드시 EvenOdd 규칙으로 채워야 한다.
    /// </summary>
    public static GeometryGroup HoleGeometry(Point center, double radius, Rect bounds)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(bounds));
        group.Children.Add(new EllipseGeometry(center, radius, radius));
        return group;
    }
}
