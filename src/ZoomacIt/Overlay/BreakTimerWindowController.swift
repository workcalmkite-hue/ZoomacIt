import AppKit
import AudioToolbox

/// Manages the lifecycle of the Break Timer overlay window.
@MainActor
final class BreakTimerWindowController {

    private var timerWindow: BreakTimerWindow?
    private var timerView: BreakTimerView?
    private var countdownTimer: Timer?
    private var pulseTimer: Timer?
    private var state: BreakTimerState
    private var playingSound: NSSound?  // retain while playing

    /// Sound used for test playback from Settings. Static so it can be stopped.
    private static var testSound: NSSound?

    /// Whether a test sound is currently playing.
    static var isTestSoundPlaying: Bool {
        testSound?.isPlaying ?? false
    }

    /// True while the timer is visible.
    var isActive: Bool { timerWindow != nil }

    init() {
        self.state = BreakTimerState()
    }

    // MARK: - Public

    func showTimer() {
        guard let screen = NSScreen.main else {
            NSLog("[BreakTimerController] No main screen available.")
            return
        }

        NSLog("[BreakTimerController] Starting break timer: %d seconds", state.defaultDuration)
        state.reloadFromSettings()
        state.remainingSeconds = state.defaultDuration
        state.elapsedSinceExpiration = 0
        state.isPaused = true
        state.hasStarted = false
        state.sessionTotalSeconds = state.defaultDuration

        let diameter = Settings.shared.breakTimerWidgetDiameter
        let savedPosition = Settings.shared.breakTimerWidgetPosition
        let origin: CGPoint
        if let saved = savedPosition {
            let widgetRect = CGRect(
                origin: saved,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
            )
            // Keep the widget on whichever connected screen the user left it on;
            // fall back to the main screen only if that display is gone.
            let host = NSScreen.screens.first { $0.frame.intersects(widgetRect) } ?? screen
            origin = BreakTimerWidgetMetrics.clamped(origin: saved, in: host.frame, forDiameter: diameter)
        } else {
            origin = BreakTimerWidgetMetrics.defaultOrigin(in: screen.frame, forDiameter: diameter)
        }

        let window = BreakTimerWindow(at: origin, diameter: diameter)
        let view = BreakTimerView(state: state, diameter: diameter)
        view.onDismiss = { [weak self] in
            self?.dismiss()
        }
        view.onResizeRequest = { [weak self] deltaY, isPrecise in
            self?.resizeWidget(scrollDeltaY: deltaY, isPrecise: isPrecise)
        }
        view.onPlayPauseToggle = { [weak self] in
            self?.togglePause()
        }

        window.contentView = view
        window.orderFront(nil)

        timerWindow = window
        timerView = view
        // The countdown does not start here — the widget appears paused and the
        // user presses play to begin.
    }

    /// Pause or resume the countdown. While paused, both the countdown and the
    /// expired-state pulse/count-up are frozen.
    func togglePause() {
        if state.isPaused {
            state.isPaused = false
            state.hasStarted = true
            NSLog("[BreakTimerController] Resumed.")
            startCountdown()
        } else {
            state.isPaused = true
            NSLog("[BreakTimerController] Paused.")
            countdownTimer?.invalidate()
            countdownTimer = nil
            pulseTimer?.invalidate()
            pulseTimer = nil
        }
        timerView?.playPauseStateChanged()
    }

    func dismiss() {
        NSLog("[BreakTimerController] Dismissing break timer.")

        if let window = timerWindow {
            Settings.shared.breakTimerWidgetPosition = window.frame.origin
        }

        countdownTimer?.invalidate()
        countdownTimer = nil
        pulseTimer?.invalidate()
        pulseTimer = nil

        // Stop any expiration sound still playing
        playingSound?.stop()
        playingSound = nil

        timerWindow?.orderOut(nil)
        timerWindow?.close()
        timerWindow = nil
        timerView = nil

        // Notify the app delegate
        if let appDelegate = NSApplication.shared.delegate as? AppDelegate {
            appDelegate.breakTimerDidEnd()
        }
    }

    /// Bring the timer window back to the foreground (e.g. from menu bar click).
    /// Does not activate the app — the widget floats without stealing focus.
    func bringToFront() {
        timerWindow?.orderFront(nil)
    }

    // MARK: - Private

    /// One scroll tick over the widget: grow/shrink around the widget's center,
    /// keep it on its current screen, and persist the chosen size immediately
    /// (position, by contrast, is saved on dismiss).
    private func resizeWidget(scrollDeltaY: CGFloat, isPrecise: Bool) {
        guard let window = timerWindow, let view = timerView else { return }

        let current = view.diameter
        let target = BreakTimerWidgetMetrics.diameter(
            afterScrollDeltaY: scrollDeltaY, isPrecise: isPrecise, from: current)
        guard target != current else { return }

        var origin = BreakTimerWidgetMetrics.resizedOrigin(
            currentOrigin: window.frame.origin, fromDiameter: current, toDiameter: target)
        if let screen = window.screen ?? NSScreen.main {
            origin = BreakTimerWidgetMetrics.clamped(origin: origin, in: screen.frame, forDiameter: target)
        }

        window.setFrame(
            NSRect(origin: origin, size: BreakTimerWidgetMetrics.windowSize(forDiameter: target)),
            display: true
        )
        view.apply(diameter: target)
        Settings.shared.breakTimerWidgetDiameter = target
    }

    private func startCountdown() {
        countdownTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0, repeats: true) { [weak self] _ in
            guard let self else { return }
            let justExpired = self.state.tick()

            if justExpired {
                NSLog("[BreakTimerController] Timer expired!")
                self.playExpirationSound()
            }

            // Covers expiration reached via the − button as well as the normal tick,
            // where justExpired alone would miss it.
            if self.state.isExpired && self.pulseTimer == nil {
                self.startPulsing()
            }

            self.timerView?.needsDisplay = true
        }
        // .common so the countdown keeps ticking while the user is dragging the widget
        // (dragging runs the run loop in .eventTracking mode).
        RunLoop.main.add(timer, forMode: .common)
        countdownTimer = timer
        NSLog("[BreakTimerController] Countdown started.")
    }

    /// Redraws at ~20fps so the expired-state ring pulse animates smoothly. Self-stops
    /// once `state` is no longer expired (e.g. the user clicked +1 min), and gets
    /// restarted by `startCountdown()` the next time the timer expires.
    private func startPulsing() {
        pulseTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0 / 20.0, repeats: true) { [weak self] timer in
            guard let self else {
                timer.invalidate()
                return
            }
            guard self.state.isExpired else {
                timer.invalidate()
                self.pulseTimer = nil
                return
            }
            self.timerView?.needsDisplay = true
        }
        RunLoop.main.add(timer, forMode: .common)
        pulseTimer = timer
    }

    private func playExpirationSound() {
        guard state.playSoundOnExpiration else {
            NSLog("[BreakTimerController] Sound disabled, skipping.")
            return
        }
        if let url = state.soundFileURL {
            if let sound = NSSound(contentsOf: url, byReference: true) {
                playingSound = sound
                sound.play()
                NSLog("[BreakTimerController] Playing custom sound from %@.", url.lastPathComponent)
            } else {
                NSLog("[BreakTimerController] Failed to load sound from %@, playing default.", url.absoluteString)
                Self.playDefaultSound()
            }
        } else {
            Self.playDefaultSound()
        }
    }

    /// Play a test sound from Settings. Stops any previously playing test sound first.
    static func playTestSound(fileURL: URL?) {
        stopTestSound()
        if let url = fileURL {
            if let sound = NSSound(contentsOf: url, byReference: true) {
                testSound = sound
                sound.play()
                NSLog("[BreakTimerController] Test: playing custom sound from %@.", url.lastPathComponent)
            } else {
                NSLog("[BreakTimerController] Test: failed to load sound from %@, playing default.", url.absoluteString)
                playDefaultSound()
            }
        } else {
            playDefaultSound()
        }
    }

    /// Stop the currently playing test sound.
    static func stopTestSound() {
        testSound?.stop()
        testSound = nil
    }

    /// Play the default expiration alert sound.
    private static func playDefaultSound() {
        if let glass = NSSound(named: "Glass") {
            glass.play()
        } else {
            AudioServicesPlayAlertSound(kSystemSoundID_UserPreferredAlert)
        }
        NSLog("[BreakTimerController] Playing default alert sound.")
    }
}
