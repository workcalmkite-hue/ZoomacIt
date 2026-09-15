import AppKit

/// Renders geometric shapes as NSBezierPath for both preview and final compositing.
enum ShapeRenderer {

    // MARK: - Line

    static func linePath(from start: CGPoint, to end: CGPoint) -> NSBezierPath {
        let path = NSBezierPath()
        path.move(to: start)
        path.line(to: end)
        return path
    }

    // MARK: - Rectangle

    static func rectanglePath(from start: CGPoint, to end: CGPoint) -> NSBezierPath {
        let rect = CGRect(
            x: min(start.x, end.x),
            y: min(start.y, end.y),
            width: abs(end.x - start.x),
            height: abs(end.y - start.y)
        )
        return NSBezierPath(rect: rect)
    }

    // MARK: - Ellipse

    static func ellipsePath(from start: CGPoint, to end: CGPoint) -> NSBezierPath {
        let rect = CGRect(
            x: min(start.x, end.x),
            y: min(start.y, end.y),
            width: abs(end.x - start.x),
            height: abs(end.y - start.y)
        )
        return NSBezierPath(ovalIn: rect)
    }

    // MARK: - Arrow

    /// Creates an arrow path where the **start point is the arrowhead tip**
    /// (ZoomIt-specific behavior — the arrow points toward where you started dragging).
    /// For a PowerPoint-style arrow (tip where the drag ends) pass the points swapped.
    ///
    /// The head is a closed triangle so callers can `fill()` it; the shaft stops
    /// at the triangle's base so a thick round cap doesn't poke past the tip.
    static func arrowPath(from start: CGPoint, to end: CGPoint, penWidth: CGFloat = 3.0) -> NSBezierPath {
        let path = NSBezierPath()

        let dx = end.x - start.x
        let dy = end.y - start.y
        let length = hypot(dx, dy)
        let angle = atan2(dy, dx)

        // Head scales with pen width but never outgrows the arrow itself.
        let headLength: CGFloat = min(max(24.0, penWidth * 4.5), max(length * 0.6, 1))
        let headAngle: CGFloat = .pi / 7  // ~26 degrees

        let arrowPoint1 = CGPoint(
            x: start.x + headLength * cos(angle + headAngle),
            y: start.y + headLength * sin(angle + headAngle)
        )
        let arrowPoint2 = CGPoint(
            x: start.x + headLength * cos(angle - headAngle),
            y: start.y + headLength * sin(angle - headAngle)
        )
        let base = CGPoint(
            x: (arrowPoint1.x + arrowPoint2.x) / 2,
            y: (arrowPoint1.y + arrowPoint2.y) / 2
        )

        // Shaft: tail → base of the head
        path.move(to: end)
        path.line(to: base)

        // Head: closed triangle
        path.move(to: start)
        path.line(to: arrowPoint1)
        path.line(to: arrowPoint2)
        path.close()

        return path
    }

    /// Path for an arrow of either direction.
    static func arrowPath(for shapeType: ShapeType, from start: CGPoint, to end: CGPoint, penWidth: CGFloat) -> NSBezierPath {
        shapeType == .arrowForward
            ? arrowPath(from: end, to: start, penWidth: penWidth)
            : arrowPath(from: start, to: end, penWidth: penWidth)
    }
}
