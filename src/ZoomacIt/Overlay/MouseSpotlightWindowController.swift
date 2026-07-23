import AppKit

/// Manages the lifecycle of Mouse Spotlight: a click-through, cursor-following
/// screen dimmer with a click-ripple effect, toggled by ⌘1. Unlike Draw/Zoom,
/// it never grabs input or activates the app — see
/// docs/superpowers/specs/2026-07-22-mouse-spotlight-design.md.
@MainActor
final class MouseSpotlightWindowController {

    private var overlayWindow: OverlayWindow?
    private var overlayView: MouseSpotlightOverlayView?
    private var currentScreen: NSScreen?
    private var currentDisplayID: CGDirectDisplayID?
    private var followTimer: Timer?
    private var mouseDownMonitor: Any?
    private var scrollMonitor: Any?
    private var radius: CGFloat

    /// True while the overlay is visible.
    var isActive: Bool { overlayWindow != nil }

    init() {
        self.radius = Settings.shared.mouseSpotlightRadius
    }

    // MARK: - Public

    func showSpotlight() {
        guard let screen = NSScreen.screenContainingMouse ?? NSScreen.main else {
            NSLog("[MouseSpotlightController] No screen available.")
            return
        }
        NSLog("[MouseSpotlightController] Showing spotlight, radius=%.0f", radius)
        presentOverlay(on: screen)
        startFollowing()
        startMonitors()
    }

    func dismiss() {
        NSLog("[MouseSpotlightController] Dismissing spotlight.")
        followTimer?.invalidate()
        followTimer = nil
        if let mouseDownMonitor {
            NSEvent.removeMonitor(mouseDownMonitor)
        }
        mouseDownMonitor = nil
        if let scrollMonitor {
            NSEvent.removeMonitor(scrollMonitor)
        }
        scrollMonitor = nil

        overlayWindow?.orderOut(nil)
        overlayWindow?.close()
        overlayWindow = nil
        overlayView = nil
        currentScreen = nil
        currentDisplayID = nil
    }

    // MARK: - Private — window presentation

    private func presentOverlay(on screen: NSScreen) {
        let window = OverlayWindow(for: screen)
        window.ignoresMouseEvents = true

        let view = MouseSpotlightOverlayView(frame: NSRect(origin: .zero, size: screen.frame.size))
        window.contentView = view
        window.orderFront(nil)

        overlayWindow = window
        overlayView = view
        currentScreen = screen
        currentDisplayID = displayID(for: screen)
        updateHole(on: screen)
    }

    /// Extracts the stable physical-display identifier for a screen. `NSScreen`
    /// instances are not stable across display reconfiguration (sleep/wake,
    /// monitor plug/unplug, resolution changes) — AppKit can hand back a fresh
    /// `NSScreen` object for the same physical display — so identity
    /// comparisons (`===`) on `NSScreen` are unreliable. `CGDirectDisplayID`
    /// stays stable for the same physical display, matching the idiom used
    /// elsewhere in this codebase (StillZoomWindowController, LiveZoomWindowController,
    /// DrawingCanvasView).
    private func displayID(for screen: NSScreen) -> CGDirectDisplayID? {
        screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? CGDirectDisplayID
    }

    private func updateHole(on screen: NSScreen) {
        let mouseLocation = NSEvent.mouseLocation
        let localPoint = CGPoint(
            x: mouseLocation.x - screen.frame.minX,
            y: mouseLocation.y - screen.frame.minY
        )
        overlayView?.updateHole(center: localPoint, radius: radius)
    }

    // MARK: - Private — cursor following

    private func startFollowing() {
        followTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0 / 60.0, repeats: true) { [weak self] _ in
            self?.tick()
        }
        RunLoop.main.add(timer, forMode: .common)
        followTimer = timer
    }

    /// Runs every tick: re-centers the hole, and hands the overlay off to a
    /// different screen if the cursor has crossed onto one.
    private func tick() {
        guard let targetScreen = NSScreen.screenContainingMouse else { return }
        let targetDisplayID = displayID(for: targetScreen)
        let screenChanged: Bool
        if let targetDisplayID, let currentDisplayID {
            screenChanged = targetDisplayID != currentDisplayID
        } else {
            // Either side couldn't resolve a display ID — fail safe and treat
            // it as a screen change so the overlay still hands off cleanly.
            screenChanged = true
        }
        if screenChanged {
            overlayWindow?.orderOut(nil)
            overlayWindow?.close()
            presentOverlay(on: targetScreen)
            return
        }
        updateHole(on: targetScreen)
    }

    // MARK: - Private — global monitors (click ripple + scroll resize)

    /// Mouse-event global monitors (unlike keyboard ones) require no special
    /// permission — they work regardless of which app has focus.
    private func startMonitors() {
        mouseDownMonitor = NSEvent.addGlobalMonitorForEvents(
            matching: [.leftMouseDown, .rightMouseDown]
        ) { [weak self] event in
            self?.handleClick(event)
        }
        scrollMonitor = NSEvent.addGlobalMonitorForEvents(matching: .scrollWheel) { [weak self] event in
            self?.handleScroll(event)
        }
    }

    private func handleClick(_ event: NSEvent) {
        guard let screen = currentScreen, let view = overlayView else { return }
        let mouseLocation = NSEvent.mouseLocation
        guard screen.frame.contains(mouseLocation) else { return }
        let localPoint = CGPoint(
            x: mouseLocation.x - screen.frame.minX,
            y: mouseLocation.y - screen.frame.minY
        )
        view.showClickRipple(at: localPoint)
    }

    private func handleScroll(_ event: NSEvent) {
        let target = MouseSpotlightGeometry.radius(
            afterScrollDeltaY: event.scrollingDeltaY,
            isPrecise: event.hasPreciseScrollingDeltas,
            from: radius
        )
        guard target != radius else { return }
        radius = target
        Settings.shared.mouseSpotlightRadius = target
        if let screen = currentScreen {
            updateHole(on: screen)
        }
    }
}
