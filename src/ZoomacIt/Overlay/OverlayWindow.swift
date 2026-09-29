import AppKit

/// A borderless, transparent window that sits above all other windows
/// and captures input for Draw mode.
///
/// It is a non-activating panel so it can take keyboard input without
/// activating ZoomacIt — Keynote hides its slideshow the moment it stops
/// being the active app, which made ⌃1/⌃2 blank out a running presentation.
final class OverlayWindow: NSPanel {

    convenience init(for screen: NSScreen) {
        self.init(
            contentRect: screen.frame,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        // popUpMenu (101): above the menu bar, Dock, and all normal windows,
        // but below screenshot-tool overlays like Snipaste's snipper (102)
        // so region selection still works while an overlay is up.
        // Raised above a running slideshow (Keynote/PowerPoint play mode),
        // which draws at a shielding level far above popUpMenu.
        level = OverlayLevel.overlay()
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
        acceptsMouseMovedEvents = true
        ignoresMouseEvents = false
    }

    // MARK: - Overrides

    /// Allow the window to become key so it can receive keyboard events.
    override var canBecomeKey: Bool { true }

    /// Allow the window to become main.
    override var canBecomeMain: Bool { true }
}

/// Window levels for ZoomacIt's overlays.
///
/// Normally the zoom/draw overlay sits at popUpMenu (101) and the floating
/// widgets (timer, sticky notes) two below it. A slideshow in play mode
/// (Keynote, PowerPoint) covers the display with a window at a shielding
/// level, which hides anything at 101 — so when such a window is on screen
/// the overlays are lifted just above it.
@MainActor
enum OverlayLevel {

    private static let base = Int(CGWindowLevelForKey(.popUpMenuWindow))
    private static let cursor = Int(CGWindowLevelForKey(.cursorWindow))

    /// Level for the zoom/draw/spotlight overlays.
    static func overlay() -> NSWindow.Level {
        .init(rawValue: overlayRawLevel())
    }

    /// Level for the timer widget and sticky notes: just under the overlay.
    static func floating() -> NSWindow.Level {
        .init(rawValue: overlayRawLevel() - 2)
    }

    private static func overlayRawLevel() -> Int {
        guard let top = coveringWindowLevel() else { return base }
        return max(base, top + 3)
    }

    /// Highest level of another app's window that covers a whole display at
    /// or above our floating level, or nil if there is none.
    static func coveringWindowLevel() -> Int? {
        guard let list = CGWindowListCopyWindowInfo(
            [.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID
        ) as? [[String: Any]] else { return nil }

        let myPID = ProcessInfo.processInfo.processIdentifier
        let screenSizes = NSScreen.screens.map { $0.frame.size }
        var top: Int?

        for info in list {
            guard let layer = info[kCGWindowLayer as String] as? Int,
                  layer >= base - 2, layer + 3 < cursor,
                  let pid = info[kCGWindowOwnerPID as String] as? pid_t, pid != myPID,
                  let boundsDict = info[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: boundsDict as CFDictionary)
            else { continue }

            let coversDisplay = screenSizes.contains {
                bounds.width >= $0.width - 1 && bounds.height >= $0.height - 1
            }
            if coversDisplay { top = max(top ?? layer, layer) }
        }
        return top
    }

    // MARK: - Activation

    /// Activates ZoomacIt so its overlay gets input — except while the
    /// frontmost app is showing a full-screen slideshow. Keynote hides its
    /// slideshow as soon as it is deactivated; the overlay is a
    /// non-activating panel, so it still receives keys without activating.
    static func activateUnlessPresenting() {
        if frontmostAppIsPresenting() { return }
        NSApplication.shared.activate(ignoringOtherApps: true)
    }

    /// Whether the frontmost app (not ZoomacIt) has a window above the normal
    /// level covering a whole display — how Keynote's play mode looks (level 25).
    static func frontmostAppIsPresenting() -> Bool {
        guard let front = NSWorkspace.shared.frontmostApplication,
              front.processIdentifier != ProcessInfo.processInfo.processIdentifier,
              let list = CGWindowListCopyWindowInfo(
                [.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID
              ) as? [[String: Any]]
        else { return false }

        let screenSizes = NSScreen.screens.map { $0.frame.size }
        return list.contains { info in
            guard let pid = info[kCGWindowOwnerPID as String] as? pid_t,
                  pid == front.processIdentifier,
                  let layer = info[kCGWindowLayer as String] as? Int, layer > 0,
                  let boundsDict = info[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: boundsDict as CFDictionary)
            else { return false }
            return screenSizes.contains {
                bounds.width >= $0.width - 1 && bounds.height >= $0.height - 1
            }
        }
    }

    // MARK: - Tracking

    private static var trackingTimer: Timer?

    /// Keeps already-open floating windows (timer, sticky notes) above a
    /// slideshow that starts after they were opened.
    static func startTracking() {
        guard trackingTimer == nil else { return }
        trackingTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { _ in
            MainActor.assumeIsolated { refreshFloatingWindows() }
        }
    }

    private static func refreshFloatingWindows() {
        let floatingWindows = NSApp.windows.filter {
            $0.isVisible && ($0 is BreakTimerWindow || $0 is StickyNotePanel)
        }
        guard !floatingWindows.isEmpty else { return }
        let target = floating()
        for window in floatingWindows where window.level != target {
            window.level = target
        }
    }
}
