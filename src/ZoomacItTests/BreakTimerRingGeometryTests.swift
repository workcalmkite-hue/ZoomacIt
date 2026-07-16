import XCTest
@testable import ZoomacIt

final class BreakTimerRingGeometryTests: XCTestCase {

    // MARK: - remainingFraction

    func testFullTimeRemainingIsFullFraction() {
        XCTAssertEqual(BreakTimerRingGeometry.remainingFraction(remainingSeconds: 600, totalSeconds: 600), 1.0)
    }

    func testHalfTimeRemainingIsHalfFraction() {
        XCTAssertEqual(BreakTimerRingGeometry.remainingFraction(remainingSeconds: 300, totalSeconds: 600), 0.5)
    }

    func testZeroRemainingIsZeroFraction() {
        XCTAssertEqual(BreakTimerRingGeometry.remainingFraction(remainingSeconds: 0, totalSeconds: 600), 0.0)
    }

    func testZeroTotalDurationDoesNotDivideByZero() {
        XCTAssertEqual(BreakTimerRingGeometry.remainingFraction(remainingSeconds: 0, totalSeconds: 0), 0.0)
    }

    func testFractionIsClampedToOneWhenTimeWasAddedPastDefault() {
        // adjustTime(byMinutes:) can push remainingSeconds above defaultDuration.
        XCTAssertEqual(BreakTimerRingGeometry.remainingFraction(remainingSeconds: 900, totalSeconds: 600), 1.0)
    }

    // MARK: - expiredPulseAlpha

    func testExpiredPulseAlphaAtZeroIsMidRange() {
        XCTAssertEqual(BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: 0), 0.675, accuracy: 0.001)
    }

    func testExpiredPulseAlphaStaysWithinBounds() {
        var t: TimeInterval = 0
        while t < 5.0 {
            let alpha = BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: t)
            XCTAssertGreaterThanOrEqual(alpha, 0.35)
            XCTAssertLessThanOrEqual(alpha, 1.0)
            t += 0.1
        }
    }

    func testExpiredPulseAlphaRepeatsEveryPeriod() {
        let a = BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: 0.2)
        let b = BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: 0.2 + 0.9)
        XCTAssertEqual(a, b, accuracy: 0.0001)
    }
}
