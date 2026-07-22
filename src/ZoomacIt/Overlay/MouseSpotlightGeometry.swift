import CoreGraphics

/// Pure geometry for Mouse Spotlight: the cursor-following, click-through
/// screen dimmer. No AppKit dependency, so it's testable without a real
/// screen or window — `MouseSpotlightOverlayView` reads `holePath(...)` on
/// every cursor tick, and `MouseSpotlightWindowController` uses the scroll
/// math to resize the hole live (same shape as `BreakTimerWidgetMetrics`).
enum MouseSpotlightGeometry {

    /// Radius the spotlight ships with.
    static let defaultRadius: CGFloat = 150

    /// Smallest radius the user can scroll down to.
    static let minRadius: CGFloat = 60

    /// Largest radius the user can scroll up to.
    static let maxRadius: CGFloat = 400

    static func clampedRadius(_ radius: CGFloat) -> CGFloat {
        min(max(radius, minRadius), maxRadius)
    }

    /// New radius after a scroll gesture. Precise deltas (trackpads) arrive in much
    /// larger quantities per gesture than mouse-wheel line deltas, so they use a finer
    /// points-per-unit ratio to make both input devices feel similar (same math as
    /// `BreakTimerWidgetMetrics.diameter(afterScrollDeltaY:...)`).
    static func radius(afterScrollDeltaY deltaY: CGFloat, isPrecise: Bool, from current: CGFloat) -> CGFloat {
        let pointsPerUnit: CGFloat = isPrecise ? 0.5 : 5
        return clampedRadius(current + deltaY * pointsPerUnit)
    }

    /// A path covering all of `rect` except a circle of `radius` around `center`.
    /// Must be filled with the `.evenOdd` rule (e.g. `CAShapeLayer.fillRule = .evenOdd`,
    /// or `CGPath.contains(_:using:.evenOdd)`) for the circle to read as a hole
    /// rather than an extra filled shape.
    static func holePath(center: CGPoint, radius: CGFloat, in rect: CGRect) -> CGPath {
        let path = CGMutablePath()
        path.addRect(rect)
        path.addEllipse(in: CGRect(
            x: center.x - radius, y: center.y - radius,
            width: radius * 2, height: radius * 2
        ))
        return path
    }
}
