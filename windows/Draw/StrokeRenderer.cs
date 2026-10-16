using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

/// <summary>
/// 획 → Geometry 변환. 맥 원본의 <c>FreehandRenderer</c>(Catmull-Rom 스무딩) +
/// <c>ShapeRenderer</c>(직선/사각형/타원/화살표)를 합친 이식.
/// </summary>
internal static class StrokeRenderer
{
    public static Geometry BuildGeometry(Stroke stroke) => stroke.ShapeType switch
    {
        ShapeType.Freehand => SmoothedPath(stroke.Points),
        ShapeType.Line => LinePath(stroke.Start, stroke.End),
        ShapeType.Rectangle => new RectangleGeometry(RectFrom(stroke.Start, stroke.End)),
        ShapeType.Ellipse => EllipsePath(stroke.Start, stroke.End),
        ShapeType.Arrow => ArrowPath(stroke.Start, stroke.End, stroke.LineWidth),
        // 촉을 드래그 끝으로 — 시작·끝을 바꿔 넘기면 된다 (원본 arrowPath(for:)와 같음)
        ShapeType.ArrowForward => ArrowPath(stroke.End, stroke.Start, stroke.LineWidth),
        _ => Geometry.Empty
    };

    private static Rect RectFrom(Point a, Point b) => new(
        Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
        Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    private static Geometry EllipsePath(Point a, Point b)
    {
        var r = RectFrom(a, b);
        return new EllipseGeometry(r);
    }

    private static Geometry LinePath(Point a, Point b) => new LineGeometry(a, b);

    /// <summary>
    /// 원시 입력점을 Catmull-Rom 스플라인으로 부드럽게 이은 경로.
    /// 점이 3개 미만이면 그냥 선분으로 잇는다 (원본과 동일한 폴백).
    /// </summary>
    public static Geometry SmoothedPath(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0) return geometry;

        using (var ctx = geometry.Open())
        {
            if (points.Count < 3)
            {
                ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
                for (int i = 1; i < points.Count; i++)
                    ctx.LineTo(points[i], isStroked: true, isSmoothJoin: true);
            }
            else
            {
                ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p0 = points[Math.Max(i - 1, 0)];
                    var p1 = points[i];
                    var p2 = points[Math.Min(i + 1, points.Count - 1)];
                    var p3 = points[Math.Min(i + 2, points.Count - 1)];

                    // Catmull-Rom → 3차 베지에 제어점
                    var cp1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                    var cp2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

                    ctx.BezierTo(cp1, cp2, p2, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// 화살표. **start 쪽이 화살촉**이다 (ZoomIt 고유 동작). 촉을 드래그 끝에 두려면
    /// 두 점을 바꿔 넘긴다(<see cref="ShapeType.ArrowForward"/>).
    ///
    /// 촉은 닫힌 삼각형이라 채워서 그리고, 몸통은 삼각형 밑변에서 멈춘다 —
    /// 굵은 펜의 둥근 끝이 촉 밖으로 삐져나오지 않게 (맥 원본 v1.3과 같은 모양).
    /// </summary>
    public static Geometry ArrowPath(Point start, Point end, double penWidth)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        // 촉은 펜 굵기에 비례하되, 화살표 자체보다 커지지는 않는다.
        double headLength = Math.Min(Math.Max(24.0, penWidth * 4.5), Math.Max(length * 0.6, 1));
        const double headAngle = Math.PI / 7; // 약 26도

        double angle = Math.Atan2(dy, dx);
        var barb1 = new Point(
            start.X + headLength * Math.Cos(angle + headAngle),
            start.Y + headLength * Math.Sin(angle + headAngle));
        var barb2 = new Point(
            start.X + headLength * Math.Cos(angle - headAngle),
            start.Y + headLength * Math.Sin(angle - headAngle));
        var headBase = new Point((barb1.X + barb2.X) / 2, (barb1.Y + barb2.Y) / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // 몸통: 꼬리(end) → 촉의 밑변
            ctx.BeginFigure(end, isFilled: false, isClosed: false);
            ctx.LineTo(headBase, isStroked: true, isSmoothJoin: false);

            // 촉: 닫힌 삼각형 (채움)
            ctx.BeginFigure(start, isFilled: true, isClosed: true);
            ctx.LineTo(barb1, isStroked: true, isSmoothJoin: true);
            ctx.LineTo(barb2, isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// 획 하나를 그린다. 형광펜은 굵기 4배 + 각진 캡 + 반투명(0.35)으로,
    /// 원본의 multiply 블렌드를 알파 합성으로 근사한다
    /// (WPF DrawingContext에는 곱셈 블렌드가 없다).
    /// </summary>
    public static void DrawStroke(DrawingContext dc, Stroke stroke, double alphaMultiplier)
    {
        if (alphaMultiplier <= 0) return;

        double width = stroke.IsHighlighter ? stroke.LineWidth * 4.0 : stroke.LineWidth;
        double alpha = stroke.IsHighlighter ? 0.35 * alphaMultiplier : alphaMultiplier;

        var brush = new SolidColorBrush(stroke.Color) { Opacity = alpha };
        brush.Freeze();

        var pen = new Pen(brush, width)
        {
            StartLineCap = stroke.IsHighlighter ? PenLineCap.Square : PenLineCap.Round,
            EndLineCap = stroke.IsHighlighter ? PenLineCap.Square : PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        // 화살촉은 채운다(형광펜 화살표는 원본처럼 테두리만).
        var fill = stroke.IsArrow && !stroke.IsHighlighter ? brush : null;
        dc.DrawGeometry(fill, pen, BuildGeometry(stroke));
    }
}
