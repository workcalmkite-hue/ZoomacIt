import CoreGraphics

/// Pure geometry for the Break Timer circular widget. No AppKit dependency, so it's
/// testable without a real screen or window — `BreakTimerWindow`/`BreakTimerView`
/// read these constants, and `BreakTimerWindowController` uses the origin helpers
/// to place/restore the widget.
enum BreakTimerWidgetMetrics {

    /// Diameter of the circular widget itself.
    static let diameter: CGFloat = 110

    /// Extra height below the circle reserved for the hover control buttons.
    static let controlBarHeight: CGFloat = 34

    /// Total window content size (circle + control bar strip below it).
    static let windowSize = CGSize(width: diameter, height: diameter + controlBarHeight)

    /// Where to place the widget the first time it's shown (no saved position yet):
    /// centered on the given screen.
    static func defaultOrigin(in screenFrame: CGRect) -> CGPoint {
        CGPoint(
            x: screenFrame.midX - windowSize.width / 2,
            y: screenFrame.midY - windowSize.height / 2
        )
    }

    /// Keeps a (possibly stale, e.g. from a since-disconnected display) origin fully
    /// on the given screen.
    static func clamped(origin: CGPoint, in screenFrame: CGRect) -> CGPoint {
        let maxX = max(screenFrame.minX, screenFrame.maxX - windowSize.width)
        let maxY = max(screenFrame.minY, screenFrame.maxY - windowSize.height)
        let x = min(max(origin.x, screenFrame.minX), maxX)
        let y = min(max(origin.y, screenFrame.minY), maxY)
        return CGPoint(x: x, y: y)
    }
}
