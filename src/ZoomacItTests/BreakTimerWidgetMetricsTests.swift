import XCTest
@testable import ZoomacIt

final class BreakTimerWidgetMetricsTests: XCTestCase {

    func testWindowSizeMatchesDiameterAndControlBar() {
        XCTAssertEqual(BreakTimerWidgetMetrics.windowSize.width, BreakTimerWidgetMetrics.diameter)
        XCTAssertEqual(
            BreakTimerWidgetMetrics.windowSize.height,
            BreakTimerWidgetMetrics.diameter + BreakTimerWidgetMetrics.controlBarHeight
        )
    }

    func testDefaultOriginIsScreenCentered() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = BreakTimerWidgetMetrics.defaultOrigin(in: screenFrame)
        XCTAssertEqual(origin.x, (1920 - BreakTimerWidgetMetrics.windowSize.width) / 2)
        XCTAssertEqual(origin.y, (1080 - BreakTimerWidgetMetrics.windowSize.height) / 2)
    }

    func testDefaultOriginRespectsScreenNotAtZeroZero() {
        // Secondary displays report frames offset from (0,0).
        let screenFrame = CGRect(x: 1920, y: 0, width: 1920, height: 1080)
        let origin = BreakTimerWidgetMetrics.defaultOrigin(in: screenFrame)
        XCTAssertEqual(origin.x, 1920 + (1920 - BreakTimerWidgetMetrics.windowSize.width) / 2)
    }

    func testClampedOriginWithinBoundsIsUnchanged() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = CGPoint(x: 400, y: 300)
        XCTAssertEqual(BreakTimerWidgetMetrics.clamped(origin: origin, in: screenFrame), origin)
    }

    func testClampedOriginPastRightEdgeIsPulledIn() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = CGPoint(x: 5000, y: 300)
        let clamped = BreakTimerWidgetMetrics.clamped(origin: origin, in: screenFrame)
        XCTAssertEqual(clamped.x, 1920 - BreakTimerWidgetMetrics.windowSize.width)
        XCTAssertEqual(clamped.y, 300)
    }

    func testClampedOriginPastNegativeEdgeIsPulledIn() {
        let screenFrame = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let origin = CGPoint(x: -500, y: -500)
        let clamped = BreakTimerWidgetMetrics.clamped(origin: origin, in: screenFrame)
        XCTAssertEqual(clamped.x, 0)
        XCTAssertEqual(clamped.y, 0)
    }
}
