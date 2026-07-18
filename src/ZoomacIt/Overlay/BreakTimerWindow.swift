import AppKit

/// A small, non-activating floating panel that hosts the Break Timer circular widget.
/// `level = .screenSaver` keeps it above the Draw overlay; `isMovableByWindowBackground`
/// lets the user drag the widget anywhere without any custom mouse-tracking code —
/// AppKit itself suppresses the drag when the mouse-down lands on a control (the
/// hover buttons added in a later task).
final class BreakTimerWindow: NSPanel {

    convenience init(at origin: CGPoint, diameter: CGFloat) {
        self.init(
            contentRect: NSRect(
                origin: origin,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
            ),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        // 99: above normal windows but below the zoom/draw overlay (101)
        // and below screenshot-tool overlays like Snipaste's snipper (102).
        level = .init(rawValue: Int(CGWindowLevelForKey(.popUpMenuWindow)) - 2)
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        isMovableByWindowBackground = true
        hidesOnDeactivate = false
    }
}
