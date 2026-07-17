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

    // MARK: - Key Commands (by hardware key code, input-source independent)

    func testKeyCommandMapping() {
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 0, shift: false), .selectAll)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 8, shift: false), .copy)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 9, shift: false), .paste)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 7, shift: false), .cut)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 6, shift: false), .undo)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 6, shift: true), .redo)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 24, shift: false), .fontBigger)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 27, shift: false), .fontSmaller)
        XCTAssertEqual(StickyNoteKeyCommand.from(keyCode: 29, shift: false), .fontReset)
    }

    func testKeyCommandIgnoresOtherKeys() {
        XCTAssertNil(StickyNoteKeyCommand.from(keyCode: 12, shift: false), "Q must not match")
        XCTAssertNil(StickyNoteKeyCommand.from(keyCode: 49, shift: false), "Space must not match")
    }

    // MARK: - Font Scroll

    func testFontScrollUpGrowsText() {
        let size = StickyNoteMetrics.fontSize(afterScrollDeltaY: 2, isPrecise: false, from: 15)
        XCTAssertEqual(size, 18, "One wheel notch (delta 2) grows the text by 3pt")
    }

    func testFontScrollPreciseIsFiner() {
        let size = StickyNoteMetrics.fontSize(afterScrollDeltaY: 10, isPrecise: true, from: 15)
        XCTAssertEqual(size, 16, accuracy: 0.001)
    }

    func testFontScrollClamps() {
        XCTAssertEqual(
            StickyNoteMetrics.fontSize(afterScrollDeltaY: -1000, isPrecise: false, from: 15),
            StickyNoteMetrics.minFontSize
        )
        XCTAssertEqual(
            StickyNoteMetrics.fontSize(afterScrollDeltaY: 1000, isPrecise: false, from: 15),
            StickyNoteMetrics.maxFontSize
        )
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
