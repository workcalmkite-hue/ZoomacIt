import XCTest
@testable import ZoomacIt

final class VanishingPenFaderTests: XCTestCase {

    func testFullyOpaqueAtCreation() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 100.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 1.0, accuracy: 0.001)
    }

    func testHalfwayThroughLifetime() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 101.5, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.5, accuracy: 0.001)
    }

    func testFullyFadedAtLifetimeEnd() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 103.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.0, accuracy: 0.001)
    }

    func testClampedAfterLifetimeEnd() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 200.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.0, accuracy: 0.001)
    }

    func testClampedBeforeCreation() {
        // now < createdAt shouldn't happen in practice, but must never yield >1.
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 99.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 1.0, accuracy: 0.001)
    }

    func testIsExpiredFalseBeforeLifetimeEnd() {
        XCTAssertFalse(VanishingPenFader.isExpired(createdAt: 100.0, now: 102.9, lifetime: 3.0))
    }

    func testIsExpiredTrueAtLifetimeEnd() {
        XCTAssertTrue(VanishingPenFader.isExpired(createdAt: 100.0, now: 103.0, lifetime: 3.0))
    }

    func testZeroLifetimeIsImmediatelyExpired() {
        XCTAssertEqual(VanishingPenFader.alpha(createdAt: 100.0, now: 100.0, lifetime: 0.0), 0.0, accuracy: 0.001)
        XCTAssertTrue(VanishingPenFader.isExpired(createdAt: 100.0, now: 100.0, lifetime: 0.0))
    }
}
