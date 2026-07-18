import XCTest
@testable import ZoomacIt

final class ANSIKeyTests: XCTestCase {

    func testDrawShortcutLetters() {
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 9), "V", "Vanishing pen (ㅍ on Korean layout)")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 15), "R")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 5), "G")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 11), "B")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 31), "O")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 16), "Y")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 35), "P")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 14), "E")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 13), "W")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 40), "K")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 17), "T")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 1), "S")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 46), "M")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 6), "Z")
        XCTAssertEqual(ANSIKey.letter(forKeyCode: 8), "C")
    }

    func testNonLetterKeysReturnNil() {
        XCTAssertNil(ANSIKey.letter(forKeyCode: 53), "Escape")
        XCTAssertNil(ANSIKey.letter(forKeyCode: 48), "Tab")
        XCTAssertNil(ANSIKey.letter(forKeyCode: 49), "Space")
        XCTAssertNil(ANSIKey.letter(forKeyCode: 126), "Up arrow")
        XCTAssertNil(ANSIKey.letter(forKeyCode: 18), "Digit 1")
    }
}
