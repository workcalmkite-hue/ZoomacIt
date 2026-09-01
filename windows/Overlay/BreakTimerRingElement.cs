using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 위젯의 원 부분을 그린다: 반투명 트랙 링 + 남은 시간만큼 채워진 진행 링 +
/// 가운데 카운트다운 숫자. 원 아래 컨트롤 바 영역은 버튼들이 차지한다.
/// (맥 원본 <c>BreakTimerView.draw(_:)</c>에 대응)
/// </summary>
internal sealed class BreakTimerRingElement : FrameworkElement
{
    private static readonly Stopwatch PulseClock = Stopwatch.StartNew();

    public required BreakTimerState State { get; init; }
    public double Diameter { get; set; } = BreakTimerWidgetMetrics.BaseDiameter;
    public Color RingColor { get; set; } = Color.FromRgb(0xFF, 0x3B, 0x30);
    public double RingOpacity { get; set; } = 1.0;

    private double WidgetScale => BreakTimerWidgetMetrics.Scale(Diameter);
    private double RingLineWidth => 6 * WidgetScale;

    protected override void OnRender(DrawingContext dc)
    {
        var circle = new Rect(0, 0, Diameter, Diameter);
        DrawFill(dc, circle);
        DrawRing(dc, circle);
        DrawTime(dc, circle);
    }

    /// <summary>
    /// 가운데 원판. 맥 원본은 여기를 거의 투명하게 뒀지만(드래그를 잡기 위한 1% 채움만),
    /// 윈도우에서 흰 배경 슬라이드 위에 띄워 보니 흰 숫자가 거의 안 보였다. 교실
    /// 프로젝터에서 읽히는 쪽이 중요하므로 옅은 어두운 원판을 깐다 —
    /// 덕분에 창의 이 부분이 마우스 입력도 확실히 받는다.
    /// </summary>
    private static void DrawFill(DrawingContext dc, Rect circle)
    {
        var inset = new Rect(
            circle.X + circle.Width * 0.08, circle.Y + circle.Height * 0.08,
            circle.Width * 0.84, circle.Height * 0.84);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(97, 0, 0, 0)), null,
            new Point(inset.X + inset.Width / 2, inset.Y + inset.Height / 2),
            inset.Width / 2, inset.Height / 2);
    }

    private void DrawRing(DrawingContext dc, Rect circle)
    {
        double lw = RingLineWidth;
        var ringRect = new Rect(circle.X + lw / 2, circle.Y + lw / 2,
            circle.Width - lw, circle.Height - lw);
        var center = new Point(ringRect.X + ringRect.Width / 2, ringRect.Y + ringRect.Height / 2);
        double radius = ringRect.Width / 2;

        // 트랙(남은 시간이 아닌 부분)
        var trackPen = new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), lw);
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        if (State.IsExpired)
        {
            double alpha = BreakTimerRingGeometry.ExpiredPulseAlpha(PulseClock.Elapsed.TotalSeconds);
            // 만료 경고는 위젯 전체 불투명도 설정을 일부러 무시한다 — 놓치면 안 되는 신호다.
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 59, 48)), lw);
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        double fraction = BreakTimerRingGeometry.RemainingFraction(
            State.RemainingSeconds, State.SessionTotalSeconds);
        if (fraction <= 0) return;

        var progressBrush = new SolidColorBrush(RingColor) { Opacity = RingOpacity };
        var progressPen = new Pen(progressBrush, lw)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        if (fraction >= 0.999)
        {
            dc.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        // 12시 방향에서 시계 방향으로
        double sweep = fraction * 360.0;
        var start = new Point(center.X, center.Y - radius);
        double rad = sweep * Math.PI / 180.0;
        var end = new Point(
            center.X + radius * Math.Sin(rad),
            center.Y - radius * Math.Cos(rad));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0,
                isLargeArc: sweep > 180, SweepDirection.Clockwise,
                isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, progressPen, geometry);
    }

    private void DrawTime(DrawingContext dc, Rect circle)
    {
        string display = State.IsExpired
            ? (State.ShowElapsed ? State.FormattedElapsed : "0:00")
            : State.FormattedTime;

        double fontSize = Diameter * 0.24;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
            FontWeights.SemiBold, FontStretches.Normal);

        var text = new FormattedText(display, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, fontSize, Brushes.White, dpi);

        var origin = new Point(
            circle.X + circle.Width / 2 - text.Width / 2,
            circle.Y + circle.Height / 2 - text.Height / 2);

        // 어두운 배경이 없으므로 밝은 화면 위에서도 읽히도록 그림자를 깐다.
        var shadow = new FormattedText(display, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, fontSize,
            new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), dpi);
        double offset = Math.Max(1, 1.5 * WidgetScale);
        dc.DrawText(shadow, new Point(origin.X + offset, origin.Y + offset));
        dc.DrawText(text, origin);
    }

    public void Refresh() => InvalidateVisual();
}
