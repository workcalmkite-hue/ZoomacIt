import AppKit

/// A small, non-activating floating panel that hosts the Break Timer circular widget.
/// `level = .screenSaver` keeps it above the Draw overlay; `isMovableByWindowBackground`
/// lets the user drag the widget anywhere without any custom mouse-tracking code —
/// AppKit itself suppresses the drag when the mouse-down lands on a control (the
/// hover buttons added in a later task).
final class BreakTimerWindow: NSPanel {

    convenience init(at origin: CGPoint) {
        self.init(
            contentRect: NSRect(
                origin: origin,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: BreakTimerWidgetMetrics.baseDiameter)
            ),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        level = .screenSaver
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        isMovableByWindowBackground = true
        hidesOnDeactivate = false
    }
}
