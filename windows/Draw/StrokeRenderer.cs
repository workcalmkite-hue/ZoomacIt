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
    /// 화살표. **시작점이 화살촉**이다 — 드래그를 시작한 쪽을 가리키는
    /// ZoomIt 고유 동작을 원본이 따랐고, 여기서도 그대로 유지한다.
    /// </summary>
    public static Geometry ArrowPath(Point start, Point end, double penWidth)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // 몸통: 꼬리(end) → 촉(start)
            ctx.BeginFigure(end, isFilled: false, isClosed: false);
            ctx.LineTo(start, isStroked: true, isSmoothJoin: false);

            double headLength = Math.Max(20.0, penWidth * 3.0);
            const double headAngle = Math.PI / 6; // 30도

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            var barb1 = new Point(
                start.X + headLength * Math.Cos(angle + headAngle),
                start.Y + headLength * Math.Sin(angle + headAngle));
            var barb2 = new Point(
                start.X + headLength * Math.Cos(angle - headAngle),
                start.Y + headLength * Math.Sin(angle - headAngle));

            ctx.BeginFigure(barb1, isFilled: false, isClosed: false);
            ctx.LineTo(start, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(barb2, isStroked: true, isSmoothJoin: false);
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

        dc.DrawGeometry(null, pen, BuildGeometry(stroke));
    }
}
