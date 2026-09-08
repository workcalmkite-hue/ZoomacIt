using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 마우스 스포트라이트 오버레이 창: 커서 주위 원만 남기고 화면을 어둡게 덮으며,
/// 클릭 링을 그 위에 얹는다. 마우스 이벤트를 전부 통과시키므로(클릭 통과)
/// 스포트라이트를 켠 채로 아래 앱을 평소처럼 조작할 수 있다.
/// (맥 원본 <c>MouseSpotlightOverlayView</c>에 대응)
/// </summary>
internal sealed class MouseSpotlightWindow : Window
{
    /// <summary>클릭 링 색상. 밝은 배경에서도 보이도록 반투명 빨강(원본과 동일한 이유).</summary>
    private static readonly Color RippleColor = Color.FromRgb(0xFF, 0x3B, 0x30);

    private readonly Path _dim = new();
    private readonly EllipseGeometry _hole = new();
    private readonly Canvas _ripples = new() { IsHitTestVisible = false };

    public MouseSpotlightWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsHitTestVisible = false;

        double dim = Math.Clamp(Settings.Shared.MouseSpotlightDim, 0.0, 1.0);
        _dim.Fill = new SolidColorBrush(Color.FromArgb((byte)(dim * 255), 0, 0, 0));
        _dim.IsHitTestVisible = false;

        var grid = new Grid();
        grid.Children.Add(_dim);
        grid.Children.Add(_ripples);
        Content = grid;

        SourceInitialized += (_, _) =>
        {
            WindowInterop.MakeClickThrough(this);
            WindowInterop.HideFromAltTab(this);
        };
    }

    /// <summary>구멍을 <paramref name="center"/>(창 기준 DIP)로 다시 뚫는다.</summary>
    public void UpdateHole(Point center, double radius)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        _hole.Center = center;
        _hole.RadiusX = radius;
        _hole.RadiusY = radius;

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(bounds));
        group.Children.Add(_hole);
        _dim.Data = group;
    }

    /// <summary>
    /// 클릭 지점에 링을 띄워 8 → 40으로 퍼지며 사라지게 한다. 딤 레이어의
    /// 형제로 얹으므로 구멍 마스크의 영향을 받지 않는다(원본과 같은 구조).
    /// </summary>
    public void ShowClickRipple(Point point)
    {
        const double startRadius = 8;
        const double endRadius = 40;

        var geometry = new EllipseGeometry(point, startRadius, startRadius);
        var ring = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(RippleColor),
            StrokeThickness = 6,
            Opacity = 0.75,
            IsHitTestVisible = false
        };
        _ripples.Children.Add(ring);

        var duration = TimeSpan.FromSeconds(0.8);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var grow = new DoubleAnimation(startRadius, endRadius, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(0.75, 0.0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => _ripples.Children.Remove(ring);

        geometry.BeginAnimation(EllipseGeometry.RadiusXProperty, grow);
        geometry.BeginAnimation(EllipseGeometry.RadiusYProperty, grow);
        ring.BeginAnimation(OpacityProperty, fade);
    }
}
