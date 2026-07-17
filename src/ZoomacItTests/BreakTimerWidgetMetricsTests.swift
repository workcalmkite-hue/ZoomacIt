import XCTest
@testable import ZoomacIt

final class BreakTimerWidgetMetricsTests: XCTestCase {

    // MARK: - Scaling

    func testWindowSizeAtBaseDiameterMatchesLegacyLayout() {
        let size = BreakTimerWidgetMetrics.windowSize(forDiameter: 110)
        XCTAssertEqual(size.width, 110)
        XCTAssertEqual(size.height, 110 + 34)
    }

    func testControlBarHeightScalesLinearly() {
        XCTAssertEqual(BreakTimerWidgetMetrics.controlBarHeight(forDiameter: 110), 34)
        XCTAssertEqual(BreakTimerWidgetMetrics.controlBarHeight(forDiameter: 220), 68)
        XCTAssertEqual(BreakTimerWidgetMetrics.controlBarHeight(forDiameter: 55), 17)
    }

    func testScaleIsRelativeToBaseDiameter() {
        XCTAssertEqual(BreakTimerWidgetMetrics.scale(forDiameter: 110), 1.0)
        XCTAssertEqual(BreakTimerWidgetMetrics.scale(forDiameter: 220), 2.0)
        XCTAssertEqual(BreakTimerWidgetMetrics.scale(forDiameter: 70), 70.0 / 110.0, accuracy: 0.0001)
    }

    // MARK: - Diameter clamping

    func testClampedDiameterInRangeIsUnchanged() {
        XCTAssertEqual(BreakTimerWidgetMetrics.clampedDiameter(110), 110)
        XCTAssertEqual(BreakTimerWidgetMetrics.clampedDiameter(70), 70)
        XCTAssertEqual(BreakTimerWidgetMetrics.clampedDiameter(220), 220)
    }

    func testClampedDiameterOutOfRangeIsClamped() {
        XCTAssertEqual(BreakTimerWidgetMetrics.clampedDiameter(10), 70)
        XCTAssertEqual(BreakTimerWidgetMetrics.clampedDiameter(1000), 220)
    }

    // MARK: - Scroll math

    func testPreciseScrollUsesHalfPointPerUnit() {
        let d = BreakTimerWidgetMetrics.diameter(afterScrollDeltaY: 20, isPrecise: true, from: 110)
        XCTAssertEqual(d, 120)
    }

    func testLineScrollUsesFivePointsPerUnit() {
        let d = BreakTimerWidgetMetrics.diameter(afterScrollDeltaY: 2, isPrecise: false, from: 110)
        XCTAssertEqual(d, 120)
    }

    func testNegativeScrollShrinks() {
        let d = BreakTimerWidgetMetrics.diameter(afterScrollDeltaY: -20, isPrecise: true, from: 110)
        XCTAssertEqual(d, 100)
    }

    func testScrollClampsAtBothEnds() {
        XCTAssertEqual(BreakTimerWidgetMetrics.diameter(afterScrollDeltaY: 10_000, isPrecise: true, from: 110), 220)
        XCTAssertEqual(BreakTimerWidgetMetrics.diameter(afterScrollDeltaY: -10_000, isPrecise: true, from: 110), 70)
    }

    // MARK: - Center-anchored resize

    func testResizedOriginKeepsCenterFixedWhenGrowing() {
        let origin = BreakTimerWidgetMetrics.resizedOrigin(
            currentOrigin: CGPoint(x: 500, y: 400), fromDiameter: 110, toDiameter: 220)
        let oldSize = BreakTimerWidgetMetrics.windowSize(forDiameter: 110)
        let newSize = BreakTimerWidgetMetrics.windowSize(forDiameter: 220)
        let oldCenter = CGPoint(x: 500 + oldSize.width / 2, y: 400 + oldSize.height / 2)
        let newCenter = CGPoint(x: origin.x + newSize.width / 2, y: origin.y + newSize.height / 2)
        XCTAssertEqual(oldCenter.x, newCenter.x, accuracy: 0.0001)
        XCTAssertEqual(oldCenter.y, newCenter.y, accuracy: 0.0001)
    }

    func testResizedOriginKeepsCenterFixedWhenShrinking() {
        let origin = BreakTimerWidgetMetrics.resizedOrigin(
            currentOrigin: CGPoint(x: 500, y: 400), fromDiameter: 200, toDiameter: 90)
        let oldSize = BreakTimerWidgetMetrics.windowSize(forDiameter: 200)
        let newSize = BreakTimerWidgetMetrics.windowSize(forDiameter: 90)
        XCTAssertEqual(origin.x + newSize.width / 2, 500 + oldSize.width / 2, accuracy: 0.0001)
        XCTAssertEqual(origin.y + newSize.height / 2, 400 + oldSize.height / 2, accuracy: 0.0001)
    }

    // MARK: - Placement (parameterized versions of the legacy tests)

    func testDefaultOriginIsScreenCentered() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = BreakTimerWidgetMetrics.defaultOrigin(in: screenFrame, forDiameter: 110)
        let size = BreakTimerWidgetMetrics.windowSize(forDiameter: 110)
        XCTAssertEqual(origin.x, (1920 - size.width) / 2)
        XCTAssertEqual(origin.y, (1080 - size.height) / 2)
    }

    func testDefaultOriginRespectsScreenNotAtZeroZero() {
        // Secondary displays report frames offset from (0,0).
        let screenFrame = CGRect(x: 1920, y: 0, width: 1920, height: 1080)
        let origin = BreakTimerWidgetMetrics.defaultOrigin(in: screenFrame, forDiameter: 110)
        let size = BreakTimerWidgetMetrics.windowSize(forDiameter: 110)
        XCTAssertEqual(origin.x, 1920 + (1920 - size.width) / 2)
    }

    func testClampedOriginWithinBoundsIsUnchanged() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = CGPoint(x: 400, y: 300)
        XCTAssertEqual(
            BreakTimerWidgetMetrics.clamped(origin: origin, in: screenFrame, forDiameter: 110), origin)
    }

    func testClampedOriginPastRightEdgeIsPulledIn() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let clamped = BreakTimerWidgetMetrics.clamped(
            origin: CGPoint(x: 5000, y: 300), in: screenFrame, forDiameter: 110)
        XCTAssertEqual(clamped.x, 1920 - BreakTimerWidgetMetrics.windowSize(forDiameter: 110).width)
        XCTAssertEqual(clamped.y, 300)
    }

    func testClampedOriginPastNegativeEdgeIsPulledIn() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let clamped = BreakTimerWidgetMetrics.clamped(
            origin: CGPoint(x: -500, y: -500), in: screenFrame, forDiameter: 110)
        XCTAssertEqual(clamped.x, 0)
        XCTAssertEqual(clamped.y, 0)
    }

    func testClampedOriginUsesLargerWindowForLargerDiameter() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let clamped = BreakTimerWidgetMetrics.clamped(
            origin: CGPoint(x: 5000, y: 300), in: screenFrame, forDiameter: 220)
        XCTAssertEqual(clamped.x, 1920 - BreakTimerWidgetMetrics.windowSize(forDiameter: 220).width)
    }
}
