import AppKit

/// A borderless, transparent window that sits above all other windows
/// and captures input for Draw mode.
final class OverlayWindow: NSWindow {

    convenience init(for screen: NSScreen) {
        self.init(
            contentRect: screen.frame,
            styleMask: .borderless,
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        // popUpMenu (101): above the menu bar, Dock, and all normal windows,
        // but below screenshot-tool overlays like Snipaste's snipper (102)
        // so region selection still works while an overlay is up.
        level = .init(rawValue: Int(CGWindowLevelForKey(.popUpMenuWindow)))
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        acceptsMouseMovedEvents = true
        ignoresMouseEvents = false
    }

    // MARK: - Overrides

    /// Allow the window to become key so it can receive keyboard events.
    override var canBecomeKey: Bool { true }

    /// Allow the window to become main.
    override var canBecomeMain: Bool { true }
}
