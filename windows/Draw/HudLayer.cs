using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

/// <summary>
/// 화면 구석의 상태 표시. 원본의 "Vanishing Pen" 배지와 같은 목적 —
/// 모드를 켠 걸 깜박하고 수업하는 사고를 막는다.
/// 지우개 크기 고리와 칠판 페이지 번호도 여기서 그린다.
/// </summary>
internal sealed class HudLayer : FrameworkElement
{
    public bool IsVanishing { get; set; }
    public bool IsHighlighter { get; set; }
    public bool IsArrowMode { get; set; }
    public bool IsEraser { get; set; }
    public bool IsTextMode { get; set; }
    public double Lifetime { get; set; } = 3.0;
    public double PenWidth { get; set; } = 5;
    public Color PenColorValue { get; set; } = Colors.Red;

    /// <summary>칠판 페이지 (0부터). 페이지가 두 장 이상일 때만 "2 / 3" 배지가 뜬다.</summary>
    public int PageIndex { get; set; }
    public int PageCount { get; set; } = 1;

    /// <summary>
    /// 지우개 고리를 그릴 자리(마우스 위치). null이면 안 그린다.
    /// 커서 이미지 대신 캔버스에 직접 그린다 — 실제로 지워지는 폭과 똑같은
    /// 좌표·크기로 보이게 하려는 것 (맥 원본 drawEraserRing과 같은 이유).
    /// </summary>
    public Point? EraserCenter { get; set; }
    public double EraserDiameter { get; set; } = 20;

    public HudLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        const double margin = 16;

        // --- 오른쪽 아래: 모드 배지 ---
        var parts = new List<string>();
        if (IsTextMode) parts.Add("글상자");
        if (IsEraser) parts.Add("지우개");
        if (IsArrowMode) parts.Add("화살표");
        parts.Add(IsVanishing ? $"지워지는 펜 · {Lifetime:0.#}초" : "일반 펜 (V로 전환)");
        DrawBadge(dc, string.Join(" · ", parts), dpi, margin, highlight: IsVanishing);

        // --- 위쪽 가운데: 칠판 페이지 ---
        if (PageCount > 1)
            DrawPageBadge(dc, $"{PageIndex + 1} / {PageCount}", dpi, margin);

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

        // --- 지우개 고리: 흰 선 + 검은 테두리라 어느 바탕에서도 보인다 ---
        if (IsEraser && EraserCenter is Point c)
        {
            double r = EraserDiameter / 2;
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 3), c, r, r);
            dc.DrawEllipse(null, new Pen(Brushes.White, 1.5), c, r, r);
        }
    }

    private void DrawBadge(DrawingContext dc, string label, double dpi, double margin, bool highlight)
    {
        var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.SemiBold,
                FontStretches.Normal), 13, Brushes.White, dpi);

        const double padding = 8;
        double w = text.Width + padding * 2;
        double h = text.Height + padding;
        double x = ActualWidth - w - margin;
        double y = ActualHeight - h - margin;

        var rect = new Rect(x, y, w, h);
        var fill = highlight
            ? new SolidColorBrush(Color.FromArgb(190, 190, 30, 30))
            : new SolidColorBrush(Color.FromArgb(153, 0, 0, 0));
        dc.DrawRoundedRectangle(fill, null, rect, 6, 6);
        dc.DrawText(text, new Point(x + padding, y + padding / 2));
    }

    private void DrawPageBadge(DrawingContext dc, string label, double dpi, double margin)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.SemiBold,
                FontStretches.Normal), 13, Brushes.White, dpi);

        const double padding = 10;
        double w = text.Width + padding * 2;
        double h = text.Height + padding / 1.5;
        var rect = new Rect((ActualWidth - w) / 2, margin, w, h);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)), null, rect, h / 2, h / 2);
        dc.DrawText(text, new Point(rect.X + padding, rect.Y + (h - text.Height) / 2));
    }

    public void Refresh() => InvalidateVisual();
}
