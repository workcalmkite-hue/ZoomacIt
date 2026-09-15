import XCTest
@testable import ZoomacIt

final class ShapeRendererTests: XCTestCase {

    func testLinePath() {
        let path = ShapeRenderer.linePath(
            from: CGPoint(x: 0, y: 0),
            to: CGPoint(x: 100, y: 100)
        )
        XCTAssertEqual(path.elementCount, 2) // moveTo + lineTo
    }

    func testRectanglePath() {
        let path = ShapeRenderer.rectanglePath(
            from: CGPoint(x: 10, y: 10),
            to: CGPoint(x: 110, y: 110)
        )
        // NSBezierPath(rect:) creates moveTo + 3 lineTo + closePath = 5 elements
        XCTAssertTrue(path.elementCount >= 4)
    }

    func testEllipsePath() {
        let path = ShapeRenderer.ellipsePath(
            from: CGPoint(x: 0, y: 0),
            to: CGPoint(x: 100, y: 50)
        )
        XCTAssertTrue(path.elementCount > 0)
    }

    func testArrowPath() {
        let path = ShapeRenderer.arrowPath(
            from: CGPoint(x: 50, y: 50),
            to: CGPoint(x: 150, y: 50)
        )
        // Arrow = shaft (moveTo + lineTo) + closed head (moveTo + lineTo + lineTo + close) = 6 elements
        XCTAssertTrue(path.elementCount >= 6)
    }

    func testArrowForwardPutsTipAtEnd() {
        let start = CGPoint(x: 50, y: 50)
        let end = CGPoint(x: 250, y: 50)
        // ZoomIt style: head sits around the start point
        let back = ShapeRenderer.arrowPath(for: .arrow, from: start, to: end, penWidth: 3)
        // PowerPoint style: head sits around the end point
        let forward = ShapeRenderer.arrowPath(for: .arrowForward, from: start, to: end, penWidth: 3)
        XCTAssertLessThan(back.bounds.minX, 51)
        XCTAssertEqual(forward.bounds.maxX, 250, accuracy: 0.5)
        // The filled head should be taller than a bare line
        XCTAssertGreaterThan(forward.bounds.height, 10)
    }
}
