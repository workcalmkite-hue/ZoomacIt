import AppKit
import CoreGraphics
import CoreVideo
import ScreenCaptureKit

/// Manages the lifecycle of the overlay window used for Draw mode.
@MainActor
final class OverlayWindowController {

    private var overlayWindow: OverlayWindow?
    private var canvasView: DrawingCanvasView?
    private let backgroundImageOverride: CGImage?
    /// ⌃T pressed while the frozen screenshot is still being captured — the
    /// canvas doesn't exist yet, so remember it and enter text mode on present.
    private var pendingTextMode = false

    init(backgroundImageOverride: CGImage? = nil) {
        self.backgroundImageOverride = backgroundImageOverride
    }

    // MARK: - Public

    func showOverlay() {
        guard let screen = NSScreen.screenContainingMouse ?? NSScreen.main else { return }

        if let backgroundImageOverride {
            // Zoom → Draw transition: use the frozen zoomed snapshot as background
            self.presentOverlay(screen: screen, backgroundImage: backgroundImageOverride)
            return
        }

        guard Settings.shared.freezeScreenOnDraw else {
            // Live mode: transparent canvas over the live desktop — no capture needed.
            // OverlayWindow is already isOpaque=false / backgroundColor=.clear,
            // so the desktop shows through when DrawingCanvasView draws nothing for the background.
            self.presentOverlay(screen: screen, backgroundImage: nil)
            return
        }

        // Frozen mode (default, ⌃2): grab the screen *before* the overlay window
        // activates us. Activating ZoomacIt makes the frontmost app resign
        // active, which closes any open menu (a Sheets custom menu, Drive's
        // 새로 만들기 menu, …) — the very thing the user wants to annotate.
        // Drawing on the still image keeps it on screen.
        let displayID = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? CGDirectDisplayID ?? CGMainDisplayID()
        let scaleFactor = screen.backingScaleFactor
        Task { @MainActor in
            let captured = await Self.captureScreenImage(
                displayID: displayID,
                width: screen.frame.width,
                height: screen.frame.height,
                scaleFactor: scaleFactor
            )
            // captured == nil (permission denied / capture failed) falls back to
            // the live transparent canvas.
            self.presentOverlay(screen: screen, backgroundImage: captured)
        }
    }

    /// Toggle text entry on the live canvas (⌃T). Entering places the text box
    /// at the current cursor position.
    func toggleTextMode() {
        guard let canvasView else {
            pendingTextMode.toggle()
            return
        }
        canvasView.toggleTextMode(at: NSEvent.mouseLocation)
    }

    /// Whether the canvas is currently taking text input.
    var isTextModeActive: Bool {
        canvasView?.isTextModeActive ?? false
    }

    private func presentOverlay(screen: NSScreen, backgroundImage: CGImage?) {
        let window = OverlayWindow(for: screen)
        let canvas = DrawingCanvasView(
            frame: NSRect(origin: .zero, size: screen.frame.size),
            backgroundImage: backgroundImage
        )
        canvas.onDismiss = { [weak self] in
            self?.dismiss()
        }
        canvas.onStickyNoteRequest = {
            (NSApplication.shared.delegate as? AppDelegate)?.spawnStickyNote()
        }

        window.contentView = canvas
        window.makeKeyAndOrderFront(nil)
        window.makeFirstResponder(canvas)

        // Ensure the app is active so the window receives events
        NSApplication.shared.activate(ignoringOtherApps: true)

        overlayWindow = window
        canvasView = canvas

        if pendingTextMode {
            pendingTextMode = false
            canvas.toggleTextMode(at: NSEvent.mouseLocation)
        }
    }

    func dismiss() {
        overlayWindow?.orderOut(nil)
        overlayWindow?.close()
        overlayWindow = nil
        canvasView = nil

        // Notify the app delegate
        if let appDelegate = NSApplication.shared.delegate as? AppDelegate {
            appDelegate.drawModeDidEnd()
        }
    }

    // MARK: - Screen Capture

    static func captureScreenImage(
        displayID: CGDirectDisplayID,
        width: CGFloat,
        height: CGFloat,
        scaleFactor: CGFloat
    ) async -> CGImage? {
        guard CGPreflightScreenCaptureAccess() else {
            NSLog("[OverlayWindowController] Screen Recording not permitted — using blank background.")
            return nil
        }

        do {
            let availableContent = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
            guard let display = availableContent.displays.first(where: { $0.displayID == displayID }) else {
                NSLog("[OverlayWindowController] Display not found.")
                return nil
            }

            let filter = SCContentFilter(display: display, excludingWindows: [])
            let config = SCStreamConfiguration()
            config.width = Int(width * scaleFactor)
            config.height = Int(height * scaleFactor)
            config.pixelFormat = kCVPixelFormatType_32BGRA
            config.showsCursor = false

            return try await SCScreenshotManager.captureImage(
                contentFilter: filter,
                configuration: config
            )
        } catch {
            NSLog("[OverlayWindowController] Screen capture failed: %@", error.localizedDescription)
            return nil
        }
    }
}
