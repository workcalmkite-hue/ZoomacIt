import AppKit

/// Mutable state for the Break Timer feature.
final class BreakTimerState {

    // MARK: - Timer

    /// Default countdown duration in seconds.
    var defaultDuration: Int = Settings.shared.breakTimerDefaultDuration

    /// Seconds remaining in the countdown. Stops at 0.
    var remainingSeconds: Int = Settings.shared.breakTimerDefaultDuration

    /// Whether the timer has reached zero.
    var isExpired: Bool { remainingSeconds <= 0 }

    /// Seconds elapsed after the timer expired (counts up from 0).
    var elapsedSinceExpiration: Int = 0

    /// Whether the countdown is paused. The timer starts paused — the user must
    /// press play to begin, and can pause/resume at any time.
    var isPaused: Bool = true

    /// Whether play has been pressed at least once this session.
    var hasStarted: Bool = false

    /// Total seconds the progress ring measures against: the duration the user
    /// actually chose for this session (10:00 default, adjusted with +/− before
    /// pressing play), not the settings default.
    var sessionTotalSeconds: Int = Settings.shared.breakTimerDefaultDuration

    // MARK: - Appearance

    /// Timer text color — reuses PenColor from Draw.
    var timerColor: PenColor = Settings.shared.breakTimerColor

    /// Widget opacity (0.1 … 1.0).
    var opacity: CGFloat = Settings.shared.breakTimerOpacity

    // MARK: - Options

    /// Show elapsed time after expiration.
    var showElapsed: Bool = Settings.shared.breakTimerShowElapsed

    /// Play a sound when time expires.
    var playSoundOnExpiration: Bool = Settings.shared.breakTimerPlaySound

    /// Custom sound file URL (nil = system default).
    var soundFileURL: URL? = Settings.shared.breakTimerSoundFile

    // MARK: - Methods

    /// Reload all properties from current Settings values.
    func reloadFromSettings() {
        defaultDuration = Settings.shared.breakTimerDefaultDuration
        timerColor = Settings.shared.breakTimerColor
        opacity = Settings.shared.breakTimerOpacity
        showElapsed = Settings.shared.breakTimerShowElapsed
        playSoundOnExpiration = Settings.shared.breakTimerPlaySound
        soundFileURL = Settings.shared.breakTimerSoundFile
    }

    /// Adjust remaining time by the given number of minutes.
    /// Clamps to a minimum of 0 seconds.
    func adjustTime(byMinutes minutes: Int) {
        let newValue = remainingSeconds + (minutes * 60)
        remainingSeconds = max(0, newValue)
        // If time was added after expiration, reset elapsed counter
        if remainingSeconds > 0 {
            elapsedSinceExpiration = 0
        }
        if !hasStarted {
            // Before the first play the user is still choosing the duration,
            // so the ring stays a full circle.
            sessionTotalSeconds = remainingSeconds
        } else if remainingSeconds > sessionTotalSeconds {
            sessionTotalSeconds = remainingSeconds
        }
    }

    /// Tick the timer by one second. Returns `true` if the timer just expired (transition to 0).
    @discardableResult
    func tick() -> Bool {
        if remainingSeconds > 0 {
            remainingSeconds -= 1
            if remainingSeconds == 0 {
                return true // just expired
            }
        } else {
            elapsedSinceExpiration += 1
        }
        return false
    }

    // MARK: - Formatting

    /// Formatted remaining time, e.g. "10:00", "1:01", "0:00".
    var formattedTime: String {
        let minutes = remainingSeconds / 60
        let seconds = remainingSeconds % 60
        return String(format: "%d:%02d", minutes, seconds)
    }

    /// Formatted elapsed time after expiration, e.g. "(1:15)".
    var formattedElapsed: String {
        let minutes = elapsedSinceExpiration / 60
        let seconds = elapsedSinceExpiration % 60
        return String(format: "(%d:%02d)", minutes, seconds)
    }
}
