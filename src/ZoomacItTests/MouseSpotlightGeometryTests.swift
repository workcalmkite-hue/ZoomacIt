import XCTest
@testable import ZoomacIt

final class MouseSpotlightGeometryTests: XCTestCase {

    // MARK: - Radius clamping

    func testClampedRadiusInRangeIsUnchanged() {
        XCTAssertEqual(MouseSpotlightGeometry.clampedRadius(150), 150)
        XCTAssertEqual(MouseSpotlightGeometry.clampedRadius(MouseSpotlightGeometry.minRadius), MouseSpotlightGeometry.minRadius)
        XCTAssertEqual(MouseSpotlightGeometry.clampedRadius(MouseSpotlightGeometry.maxRadius), MouseSpotlightGeometry.maxRadius)
    }

    func testClampedRadiusOutOfRangeIsClamped() {
        XCTAssertEqual(MouseSpotlightGeometry.clampedRadius(1), MouseSpotlightGeometry.minRadius)
        XCTAssertEqual(MouseSpotlightGeometry.clampedRadius(10_000), MouseSpotlightGeometry.maxRadius)
    }

    // MARK: - Scroll math

    func testPreciseScrollUsesHalfPointPerUnit() {
        let r = MouseSpotlightGeometry.radius(afterScrollDeltaY: 20, isPrecise: true, from: 150)
        XCTAssertEqual(r, 160)
    }

    func testLineScrollUsesFivePointsPerUnit() {
        let r = MouseSpotlightGeometry.radius(afterScrollDeltaY: 2, isPrecise: false, from: 150)
        XCTAssertEqual(r, 160)
    }

    func testNegativeScrollShrinks() {
        let r = MouseSpotlightGeometry.radius(afterScrollDeltaY: -20, isPrecise: true, from: 150)
        XCTAssertEqual(r, 140)
    }

    func testScrollClampsAtBothEnds() {
        XCTAssertEqual(
            MouseSpotlightGeometry.radius(afterScrollDeltaY: 10_000, isPrecise: true, from: 150),
            MouseSpotlightGeometry.maxRadius)
        XCTAssertEqual(
            MouseSpotlightGeometry.radius(afterScrollDeltaY: -10_000, isPrecise: true, from: 150),
            MouseSpotlightGeometry.minRadius)
    }

    // MARK: - Hole path (even-odd fill rule)

    func testHolePathExcludesCircleCenter() {
        let rect = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let center = CGPoint(x: 960, y: 540)
        let path = MouseSpotlightGeometry.holePath(center: center, radius: 100, in: rect)
        XCTAssertFalse(path.contains(center, using: .evenOdd))
    }

    func testHolePathIncludesAreaFarFromCircle() {
        let rect = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let center = CGPoint(x: 960, y: 540)
        let path = MouseSpotlightGeometry.holePath(center: center, radius: 100, in: rect)
        XCTAssertTrue(path.contains(CGPoint(x: 10, y: 10), using: .evenOdd))
    }

    func testHolePathBoundaryJustInsideRadiusIsExcluded() {
        let rect = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let center = CGPoint(x: 960, y: 540)
        let path = MouseSpotlightGeometry.holePath(center: center, radius: 100, in: rect)
        XCTAssertFalse(path.contains(CGPoint(x: center.x + 99, y: center.y), using: .evenOdd))
    }

    func testHolePathBoundaryJustOutsideRadiusIsIncluded() {
        let rect = CGRect(x: 0, y: 0, width: 1920, height: 1080)
        let center = CGPoint(x: 960, y: 540)
        let path = MouseSpotlightGeometry.holePath(center: center, radius: 100, in: rect)
        XCTAssertTrue(path.contains(CGPoint(x: center.x + 101, y: center.y), using: .evenOdd))
    }
}
