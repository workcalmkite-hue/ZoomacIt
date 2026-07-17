import XCTest
@testable import ZoomacIt

final class BreakTimerStateTests: XCTestCase {

    // MARK: - Default Values

    func testStartsPaused() {
        let state = BreakTimerState()
        XCTAssertTrue(state.isPaused, "Timer must not auto-start; the user presses play")
    }

    func testDefaultState() {
        let state = BreakTimerState()
        XCTAssertEqual(state.defaultDuration, 600)
        XCTAssertEqual(state.remainingSeconds, 600)
        XCTAssertFalse(state.isExpired)
        XCTAssertEqual(state.elapsedSinceExpiration, 0)
        XCTAssertEqual(state.opacity, 1.0)
        XCTAssertTrue(state.showElapsed)
        XCTAssertFalse(state.playSoundOnExpiration)
        XCTAssertNil(state.soundFileURL)
    }

    func testDefaultColor() {
        let state = BreakTimerState()
        XCTAssertEqual(state.timerColor.nsColor, PenColor.red.nsColor)
    }

    // MARK: - Time Adjustment

    func testAdjustTimePositive() {
        let state = BreakTimerState()
        state.remainingSeconds = 300
        state.adjustTime(byMinutes: 1)
        XCTAssertEqual(state.remainingSeconds, 360)
    }

    func testAdjustTimeNegative() {
        let state = BreakTimerState()
        state.remainingSeconds = 300
        state.adjustTime(byMinutes: -1)
        XCTAssertEqual(state.remainingSeconds, 240)
    }

    func testAdjustTimeClampAtZero() {
        let state = BreakTimerState()
        state.remainingSeconds = 30
        state.adjustTime(byMinutes: -1)
        XCTAssertEqual(state.remainingSeconds, 0)
    }

    func testAdjustTimeFromZero() {
        let state = BreakTimerState()
        state.remainingSeconds = 0
        state.adjustTime(byMinutes: -1)
        XCTAssertEqual(state.remainingSeconds, 0, "Cannot go below 0")
    }

    func testAdjustTimeResetsElapsed() {
        let state = BreakTimerState()
        state.remainingSeconds = 0
        state.elapsedSinceExpiration = 45
        state.adjustTime(byMinutes: 1)
        XCTAssertEqual(state.remainingSeconds, 60)
        XCTAssertEqual(state.elapsedSinceExpiration, 0, "Elapsed should reset when time is added")
    }

    // MARK: - Tick

    func testTickDecrementsTime() {
        let state = BreakTimerState()
        state.remainingSeconds = 10
        let expired = state.tick()
        XCTAssertEqual(state.remainingSeconds, 9)
        XCTAssertFalse(expired)
    }

    func testTickReturnsExpiredOnZero() {
        let state = BreakTimerState()
        state.remainingSeconds = 1
        let expired = state.tick()
        XCTAssertEqual(state.remainingSeconds, 0)
        XCTAssertTrue(expired, "tick() should return true when transitioning to 0")
        XCTAssertTrue(state.isExpired)
    }

    func testTickIncrementsElapsedAfterExpiry() {
        let state = BreakTimerState()
        state.remainingSeconds = 0
        let expired = state.tick()
        XCTAssertFalse(expired, "Already expired, should not return true again")
        XCTAssertEqual(state.elapsedSinceExpiration, 1)

        state.tick()
        XCTAssertEqual(state.elapsedSinceExpiration, 2)
    }

    func testTickDoesNotGoNegative() {
        let state = BreakTimerState()
        state.remainingSeconds = 0
        for _ in 0..<100 {
            state.tick()
        }
        XCTAssertEqual(state.remainingSeconds, 0)
        XCTAssertEqual(state.elapsedSinceExpiration, 100)
    }

    // MARK: - Formatting

    func testFormattedTime10Minutes() {
        let state = BreakTimerState()
        state.remainingSeconds = 600
        XCTAssertEqual(state.formattedTime, "10:00")
    }

    func testFormattedTime1Minute1Second() {
        let state = BreakTimerState()
        state.remainingSeconds = 61
        XCTAssertEqual(state.formattedTime, "1:01")
    }

    func testFormattedTimeZero() {
        let state = BreakTimerState()
        state.remainingSeconds = 0
        XCTAssertEqual(state.formattedTime, "0:00")
    }

    func testFormattedTime59Seconds() {
        let state = BreakTimerState()
        state.remainingSeconds = 59
        XCTAssertEqual(state.formattedTime, "0:59")
    }

    func testFormattedElapsed() {
        let state = BreakTimerState()
        state.elapsedSinceExpiration = 75
        XCTAssertEqual(state.formattedElapsed, "(1:15)")
    }

    func testFormattedElapsedZero() {
        let state = BreakTimerState()
        state.elapsedSinceExpiration = 0
        XCTAssertEqual(state.formattedElapsed, "(0:00)")
    }

    // MARK: - isExpired

    func testIsExpired() {
        let state = BreakTimerState()
        state.remainingSeconds = 1
        XCTAssertFalse(state.isExpired)

        state.remainingSeconds = 0
        XCTAssertTrue(state.isExpired)
    }

    // MARK: - Reload from Settings

    func testReloadFromSettings() {
        let state = BreakTimerState()

        // Change settings
        Settings.shared.breakTimerDefaultDuration = 300
        Settings.shared.breakTimerColor = .blue
        Settings.shared.breakTimerOpacity = 0.5
        Settings.shared.breakTimerShowElapsed = false
        Settings.shared.breakTimerPlaySound = true

        // Before reload, state still has old values
        XCTAssertEqual(state.defaultDuration, 600)

        // After reload, all properties should match Settings
        state.reloadFromSettings()
        XCTAssertEqual(state.defaultDuration, 300)
        XCTAssertEqual(state.timerColor, .blue)
        XCTAssertEqual(state.opacity, 0.5)
        XCTAssertFalse(state.showElapsed)
        XCTAssertTrue(state.playSoundOnExpiration)

        // Clean up
        Settings.shared.resetToDefaults()
    }

}
