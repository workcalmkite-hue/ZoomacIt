import XCTest
@testable import ZoomacIt

final class StickyNoteMetricsTests: XCTestCase {

    // MARK: - Size Clamping

    func testClampedSizeWithinBounds() {
        let size = StickyNoteMetrics.clampedSize(CGSize(width: 300, height: 200))
        XCTAssertEqual(size, CGSize(width: 300, height: 200))
    }

    func testClampedSizeBelowMinimum() {
        let size = StickyNoteMetrics.clampedSize(CGSize(width: 10, height: 10))
        XCTAssertEqual(size, StickyNoteMetrics.minSize)
    }

    func testClampedSizeAboveMaximum() {
        let size = StickyNoteMetrics.clampedSize(CGSize(width: 5000, height: 5000))
        XCTAssertEqual(size, StickyNoteMetrics.maxSize)
    }

    // MARK: - Font Size Clamping

    func testClampedFontSizeWithinBounds() {
        XCTAssertEqual(StickyNoteMetrics.clampedFontSize(20), 20)
    }

    func testClampedFontSizeBounds() {
        XCTAssertEqual(StickyNoteMetrics.clampedFontSize(1), StickyNoteMetrics.minFontSize)
        XCTAssertEqual(StickyNoteMetrics.clampedFontSize(500), StickyNoteMetrics.maxFontSize)
    }

    // MARK: - Grip Drag

    func testGripDragGrowsRightAndDown() {
        let initial = CGRect(x: 100, y: 500, width: 240, height: 180)
        let frame = StickyNoteMetrics.frameForGripDrag(initial: initial, dx: 60, dy: -40)
        XCTAssertEqual(frame.width, 300)
        XCTAssertEqual(frame.height, 220)
        XCTAssertEqual(frame.minX, 100, "Left edge stays fixed")
        XCTAssertEqual(frame.maxY, 680, "Top edge stays fixed")
    }

    func testGripDragShrinks() {
        let initial = CGRect(x: 100, y: 500, width: 240, height: 180)
        let frame = StickyNoteMetrics.frameForGripDrag(initial: initial, dx: -50, dy: 30)
        XCTAssertEqual(frame.width, 190)
        XCTAssertEqual(frame.height, 150)
        XCTAssertEqual(frame.maxY, 680, "Top edge stays fixed")
    }

    func testGripDragClampsAtMinimum() {
        let initial = CGRect(x: 100, y: 500, width: 240, height: 180)
        let frame = StickyNoteMetrics.frameForGripDrag(initial: initial, dx: -1000, dy: 1000)
        XCTAssertEqual(frame.size, StickyNoteMetrics.minSize)
        XCTAssertEqual(frame.maxY, 680, "Top edge stays fixed even when clamped")
    }
}
