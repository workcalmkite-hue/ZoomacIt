/// Maps hardware key codes to the letter on a US (ANSI) keyboard layout.
///
/// Shortcut handling that matches on `charactersIgnoringModifiers` breaks under
/// non-Latin input sources — with a Korean layout the V key arrives as "ㅍ" —
/// so shortcut consumers resolve the physical key through this table instead.
enum ANSIKey {

    /// The uppercase letter printed on the given key of a US layout,
    /// or nil if the key isn't a letter key.
    static func letter(forKeyCode keyCode: UInt16) -> String? {
        switch keyCode {
        case 0: return "A"
        case 1: return "S"
        case 2: return "D"
        case 3: return "F"
        case 4: return "H"
        case 5: return "G"
        case 6: return "Z"
        case 7: return "X"
        case 8: return "C"
        case 9: return "V"
        case 11: return "B"
        case 12: return "Q"
        case 13: return "W"
        case 14: return "E"
        case 15: return "R"
        case 16: return "Y"
        case 17: return "T"
        case 31: return "O"
        case 32: return "U"
        case 34: return "I"
        case 35: return "P"
        case 37: return "L"
        case 38: return "J"
        case 40: return "K"
        case 45: return "N"
        case 46: return "M"
        default: return nil
        }
    }
}
