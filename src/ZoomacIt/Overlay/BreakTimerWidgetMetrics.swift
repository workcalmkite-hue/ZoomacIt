import CoreGraphics

/// Pure geometry for the Break Timer circular widget. No AppKit dependency, so it's
/// testable without a real screen or window — `BreakTimerWindow`/`BreakTimerView`
/// read these values, and `BreakTimerWindowController` uses the origin helpers
/// to place/restore/resize the widget.
///
/// The widget is user-resizable (scroll over it), so every metric is a function of
/// the current diameter; `baseDiameter` is both the default and the reference all
/// proportional sizes (ring, buttons, fonts) scale against.
enum BreakTimerWidgetMetrics {

    /// Diameter the widget ships with, and the reference for proportional scaling.
    static let baseDiameter: CGFloat = 110

    /// Smallest diameter the user can scroll down to (countdown stays legible).
    static let minDiameter: CGFloat = 70

    /// Largest diameter the user can scroll up to.
    static let maxDiameter: CGFloat = 220

    /// Control-bar height at `baseDiameter`; scales linearly with diameter.
    private static let baseControlBarHeight: CGFloat = 34

    static func clampedDiameter(_ diameter: CGFloat) -> CGFloat {
        min(max(diameter, minDiameter), maxDiameter)
    }

    /// Proportional-scaling factor for a given diameter (1.0 at `baseDiameter`).
    static func scale(forDiameter diameter: CGFloat) -> CGFloat {
        diameter / baseDiameter
    }

    /// Extra height below the circle reserved for the hover control buttons.
    static func controlBarHeight(forDiameter diameter: CGFloat) -> CGFloat {
        baseControlBarHeight * scale(forDiameter: diameter)
    }

    /// Total window content size (circle + control bar strip below it).
    static func windowSize(forDiameter diameter: CGFloat) -> CGSize {
        CGSize(width: diameter, height: diameter + controlBarHeight(forDiameter: diameter))
    }

    /// Where to place the widget the first time it's shown (no saved position yet):
    /// centered on the given screen.
    static func defaultOrigin(in screenFrame: CGRect, forDiameter diameter: CGFloat) -> CGPoint {
        let size = windowSize(forDiameter: diameter)
        return CGPoint(
            x: screenFrame.midX - size.width / 2,
            y: screenFrame.midY - size.height / 2
        )
    }

    /// Keeps a (possibly stale, e.g. from a since-disconnected display) origin fully
    /// on the given screen.
    static func clamped(origin: CGPoint, in screenFrame: CGRect, forDiameter diameter: CGFloat) -> CGPoint {
        let size = windowSize(forDiameter: diameter)
        let maxX = max(screenFrame.minX, screenFrame.maxX - size.width)
        let maxY = max(screenFrame.minY, screenFrame.maxY - size.height)
        let x = min(max(origin.x, screenFrame.minX), maxX)
        let y = min(max(origin.y, screenFrame.minY), maxY)
        return CGPoint(x: x, y: y)
    }

    /// New diameter after a scroll gesture. Precise deltas (trackpads) arrive in much
    /// larger quantities per gesture than mouse-wheel line deltas, so they use a finer
    /// points-per-unit ratio to make both input devices feel similar.
    static func diameter(afterScrollDeltaY deltaY: CGFloat, isPrecise: Bool, from current: CGFloat) -> CGFloat {
        let pointsPerUnit: CGFloat = isPrecise ? 0.5 : 5
        return clampedDiameter(current + deltaY * pointsPerUnit)
    }

    /// Window origin that keeps the window's center fixed across a diameter change.
    /// Screen clamping is applied separately by the caller (the target screen is an
    /// AppKit concern this pure-geometry type doesn't know about).
    static func resizedOrigin(currentOrigin: CGPoint, fromDiameter old: CGFloat, toDiameter new: CGFloat) -> CGPoint {
        let oldSize = windowSize(forDiameter: old)
        let newSize = windowSize(forDiameter: new)
        return CGPoint(
            x: currentOrigin.x + (oldSize.width - newSize.width) / 2,
            y: currentOrigin.y + (oldSize.height - newSize.height) / 2
        )
    }
}
