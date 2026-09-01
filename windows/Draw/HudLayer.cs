using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

/// <summary>
/// 화면 구석의 상태 표시. 원본의 "Vanishing Pen" 배지와 같은 목적 —
/// 모드를 켠 걸 깜박하고 수업하는 사고를 막는다.
/// </summary>
internal sealed class HudLayer : FrameworkElement
{
    public bool IsVanishing { get; set; }
    public bool IsHighlighter { get; set; }
    public double Lifetime { get; set; } = 3.0;
    public double PenWidth { get; set; } = 5;
    public Color PenColorValue { get; set; } = Colors.Red;

    public HudLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        const double margin = 16;

        // --- 오른쪽 아래: 모드 배지 ---
        string badge = IsVanishing
            ? $"지워지는 펜 · {Lifetime:0.#}초"
            : "일반 펜 (V로 전환)";
        DrawBadge(dc, badge, dpi, alignRight: true, margin);

        // --- 왼쪽 아래: 현재 펜 상태 ---
        var swatchSize = 14.0;
        var swatchRect = new Rect(margin + 10, ActualHeight - margin - 26 + 6, swatchSize, swatchSize);
        string info = $"굵기 {PenWidth:0}{(IsHighlighter ? " · 형광펜" : "")}";
        var text = new FormattedText(info, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Malgun Gothic"), 13, Brushes.White, dpi);

        var bg = new Rect(margin, ActualHeight - margin - 26,
            swatchSize + text.Width + 26, 26);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)), null, bg, 6, 6);
        dc.DrawEllipse(new SolidColorBrush(PenColorValue), null,
            new Point(swatchRect.X + swatchSize / 2, swatchRect.Y + swatchSize / 2),
            swatchSize / 2, swatchSize / 2);
        dc.DrawText(text, new Point(swatchRect.Right + 8, bg.Y + (26 - text.Height) / 2));
    }

    private void DrawBadge(DrawingContext dc, string label, double dpi, bool alignRight, double margin)
    {
        var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.SemiBold,
                FontStretches.Normal), 13, Brushes.White, dpi);

        const double padding = 8;
        double w = text.Width + padding * 2;
        double h = text.Height + padding;
        double x = alignRight ? ActualWidth - w - margin : margin;
        double y = ActualHeight - h - margin;

        var rect = new Rect(x, y, w, h);
        var fill = IsVanishing
            ? new SolidColorBrush(Color.FromArgb(190, 190, 30, 30))
            : new SolidColorBrush(Color.FromArgb(153, 0, 0, 0));
        dc.DrawRoundedRectangle(fill, null, rect, 6, 6);
        dc.DrawText(text, new Point(x + padding, y + padding / 2));
    }

    public void Refresh() => InvalidateVisual();
}
