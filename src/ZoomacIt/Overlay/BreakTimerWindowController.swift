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

        let savedPosition = Settings.shared.breakTimerWidgetPosition
        let origin: CGPoint
        if let saved = savedPosition {
            let widgetRect = CGRect(
                origin: saved,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: BreakTimerWidgetMetrics.baseDiameter)
            )
            // Keep the widget on whichever connected screen the user left it on;
            // fall back to the main screen only if that display is gone.
            let host = NSScreen.screens.first { $0.frame.intersects(widgetRect) } ?? screen
            origin = BreakTimerWidgetMetrics.clamped(
                origin: saved, in: host.frame, forDiameter: BreakTimerWidgetMetrics.baseDiameter)
        } else {
            origin = BreakTimerWidgetMetrics.defaultOrigin(
                in: screen.frame, forDiameter: BreakTimerWidgetMetrics.baseDiameter)
        }

        let window = BreakTimerWindow(at: origin)
        let view = BreakTimerView(state: state)
        view.onDismiss = { [weak self] in
            self?.dismiss()
        }

        window.contentView = view
        window.orderFront(nil)

        timerWindow = window
        timerView = view

        startCountdown()
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
