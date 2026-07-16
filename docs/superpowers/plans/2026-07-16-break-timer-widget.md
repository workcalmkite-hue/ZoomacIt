# Break Timer 원형 위젯 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fullscreen Break Timer (⌃3) overlay with a small, draggable circular widget (progress ring + countdown, hover controls, position memory, red pulse on expiration) that never steals focus from other apps.

**Architecture:** The window shrinks from a screen-sized borderless window to a fixed-size non-activating `NSPanel` (`isMovableByWindowBackground = true` for free dragging). All fullscreen-only concepts (3×3 grid position, desktop-capture background) are deleted. Sizing/position math and ring/pulse math are extracted into two small pure (no-AppKit) files so they stay unit-testable, matching this codebase's existing convention (`ZoomMath`, `VanishingPenFader`) of keeping pure logic separate from `NSView`/`NSWindow` code. AppKit-level code (window/view/controller) has no existing test coverage precedent in this repo and is instead verified by `make test` (regression) + manual build/run steps at the end of each task.

**Tech Stack:** Swift, AppKit (`NSPanel`, `NSView`, `NSBezierPath`, `NSTrackingArea`), XCTest, `xcodegen`-generated Xcode project (already generated — see Global Constraints).

## Global Constraints

- Never edit `src/ZoomacIt.xcodeproj/project.pbxproj` or `src/project.yml` team ID / `PRODUCT_NAME` overrides, and never `git add` them — they hold this machine's personal signing identity. Only `git add` the specific source/test/doc files each task lists.
- `xcodegen` is not installed on this machine — do not run `make generate`. No new target/group needs to be added to the Xcode project for this plan (all new files land inside the existing `ZoomacIt` and `ZoomacItTests` groups), so this shouldn't come up, but if a step ever seems to need `make generate`, stop and ask rather than editing `project.pbxproj` by hand.
- Run the full suite with `make test` after every task (each task's last step) and treat any *new* failure as a blocker — fix before moving on.
- Korean strings in this plan mirror the design spec; all in-code identifiers, comments, and log messages stay in English/Swift-idiomatic style, matching the existing codebase (the existing files under `Overlay/`, `Models/`, `Settings/` are all English-identifier Swift with no Korean in source).

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ZoomacIt/Models/Settings.swift` | *Modify.* Add `breakTimerWidgetPosition`; remove `breakTimerBackground`/`breakTimerBackgroundFadeDarkness`. |
| `src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift` | *Create.* Pure geometry constants (diameter, control-bar height, window size) + default/clamped origin math. No AppKit. |
| `src/ZoomacIt/Overlay/BreakTimerRingGeometry.swift` | *Create.* Pure math for the progress-ring fraction and the expired-state pulse alpha. No AppKit. |
| `src/ZoomacIt/Models/BreakTimerState.swift` | *Modify.* Remove `BreakTimerPosition`/`BreakTimerBackground` enums and the `position`/`background` properties. Timer logic (`tick`, `adjustTime`, formatting) is untouched. |
| `src/ZoomacIt/Overlay/BreakTimerWindow.swift` | *Rewrite.* Small non-activating `NSPanel` instead of a screen-sized borderless window. |
| `src/ZoomacIt/Overlay/BreakTimerView.swift` | *Rewrite.* Draws circle + progress ring + centered time (normal and expired states) and hosts the hover control buttons. No more keyboard handling. |
| `src/ZoomacIt/Overlay/BreakTimerWindowController.swift` | *Rewrite.* Drops the `ScreenCaptureKit` capture path and the forced app-activation call; adds saved-position load/save and the expiration pulse timer. |
| `src/ZoomacIt/Settings/BreakTimerTab.swift` | *Modify.* Remove the Background picker and Fade Darkness slider. |
| `src/ZoomacItTests/SettingsTests.swift` | *Modify.* Add widget-position tests; remove background/fade-darkness tests. |
| `src/ZoomacItTests/BreakTimerStateTests.swift` | *Modify.* Remove position/background tests. |
| `src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift` | *Create.* Tests for the new pure metrics file. |
| `src/ZoomacItTests/BreakTimerRingGeometryTests.swift` | *Create.* Tests for the new pure ring/pulse math file. |

---

### Task 1: Widget position storage in `Settings`

**Files:**
- Modify: `src/ZoomacIt/Models/Settings.swift`
- Test: `src/ZoomacItTests/SettingsTests.swift`

**Interfaces:**
- Produces: `Settings.shared.breakTimerWidgetPosition: CGPoint?` (get/set). `nil` means "no saved position yet — caller should use a default." Setting it to `nil` clears both underlying keys.

- [ ] **Step 1: Write the failing tests**

Add to `src/ZoomacItTests/SettingsTests.swift`, right after `testSoundFileRoundTrip()` (around line 123):

```swift
    func testBreakTimerWidgetPositionDefaultsToNil() {
        XCTAssertNil(Settings.shared.breakTimerWidgetPosition)
    }

    func testBreakTimerWidgetPositionRoundTrip() {
        Settings.shared.breakTimerWidgetPosition = CGPoint(x: 120.5, y: 340)
        XCTAssertEqual(Settings.shared.breakTimerWidgetPosition?.x, 120.5)
        XCTAssertEqual(Settings.shared.breakTimerWidgetPosition?.y, 340)
    }

    func testBreakTimerWidgetPositionClearedBySettingNil() {
        Settings.shared.breakTimerWidgetPosition = CGPoint(x: 10, y: 10)
        Settings.shared.breakTimerWidgetPosition = nil
        XCTAssertNil(Settings.shared.breakTimerWidgetPosition)
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `value of type 'Settings' has no member 'breakTimerWidgetPosition'`

- [ ] **Step 3: Add the keys**

In `src/ZoomacIt/Models/Settings.swift`, inside `enum Keys` (after `static let breakTimerBackgroundFadeDarkness = "breakTimerBackgroundFadeDarkness"`, line 94):

```swift
        static let breakTimerWidgetPositionX = "breakTimerWidgetPositionX"
        static let breakTimerWidgetPositionY = "breakTimerWidgetPositionY"
```

Do **not** add these to `registerDefaults()` — their absence from `UserDefaults` is how `breakTimerWidgetPosition` knows to return `nil`.

- [ ] **Step 4: Add the computed property**

In `src/ZoomacIt/Models/Settings.swift`, in the `// MARK: - Break Timer` section, after `breakTimerBackgroundFadeDarkness` (line 279):

```swift

    /// Last dragged position of the circular widget, in screen coordinates.
    /// `nil` when the user hasn't moved it yet (caller should default to screen center).
    var breakTimerWidgetPosition: CGPoint? {
        get {
            guard let x = defaults.object(forKey: Keys.breakTimerWidgetPositionX) as? Double,
                  let y = defaults.object(forKey: Keys.breakTimerWidgetPositionY) as? Double else {
                return nil
            }
            return CGPoint(x: x, y: y)
        }
        set {
            guard let point = newValue else {
                defaults.removeObject(forKey: Keys.breakTimerWidgetPositionX)
                defaults.removeObject(forKey: Keys.breakTimerWidgetPositionY)
                return
            }
            defaults.set(Double(point.x), forKey: Keys.breakTimerWidgetPositionX)
            defaults.set(Double(point.y), forKey: Keys.breakTimerWidgetPositionY)
        }
    }
```

- [ ] **Step 5: Add the keys to `resetToDefaults()`**

In `resetToDefaults()`, replace the `allKeys` array's last line:

```swift
            Keys.breakTimerSoundFile, Keys.breakTimerBackgroundFadeDarkness
```

with:

```swift
            Keys.breakTimerSoundFile, Keys.breakTimerBackgroundFadeDarkness,
            Keys.breakTimerWidgetPositionX, Keys.breakTimerWidgetPositionY
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all `SettingsTests`, including the 3 new ones)

- [ ] **Step 7: Commit**

```bash
git add src/ZoomacIt/Models/Settings.swift src/ZoomacItTests/SettingsTests.swift
git commit -m "feat: add break timer widget position storage to Settings"
```

---

### Task 2: `BreakTimerWidgetMetrics` — pure sizing/position math

**Files:**
- Create: `src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift`
- Test: `src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift`

**Interfaces:**
- Consumes: nothing (no dependency on Task 1).
- Produces: `BreakTimerWidgetMetrics.diameter: CGFloat`, `.controlBarHeight: CGFloat`, `.windowSize: CGSize`, `.defaultOrigin(in: CGRect) -> CGPoint`, `.clamped(origin: CGPoint, in: CGRect) -> CGPoint`. Task 4 (window), Task 5 (view layout), and Task 4's controller rewrite all read these.

- [ ] **Step 1: Write the failing tests**

Create `src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift`:

```swift
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `cannot find 'BreakTimerWidgetMetrics' in scope`

- [ ] **Step 3: Implement**

Create `src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift`:

```swift
import CoreGraphics

/// Pure geometry for the Break Timer circular widget. No AppKit dependency, so it's
/// testable without a real screen or window — `BreakTimerWindow`/`BreakTimerView`
/// read these constants, and `BreakTimerWindowController` uses the origin helpers
/// to place/restore the widget.
enum BreakTimerWidgetMetrics {

    /// Diameter of the circular widget itself.
    static let diameter: CGFloat = 110

    /// Extra height below the circle reserved for the hover control buttons.
    static let controlBarHeight: CGFloat = 34

    /// Total window content size (circle + control bar strip below it).
    static let windowSize = CGSize(width: diameter, height: diameter + controlBarHeight)

    /// Where to place the widget the first time it's shown (no saved position yet):
    /// centered on the given screen.
    static func defaultOrigin(in screenFrame: CGRect) -> CGPoint {
        CGPoint(
            x: screenFrame.midX - windowSize.width / 2,
            y: screenFrame.midY - windowSize.height / 2
        )
    }

    /// Keeps a (possibly stale, e.g. from a since-disconnected display) origin fully
    /// on the given screen.
    static func clamped(origin: CGPoint, in screenFrame: CGRect) -> CGPoint {
        let maxX = max(screenFrame.minX, screenFrame.maxX - windowSize.width)
        let maxY = max(screenFrame.minY, screenFrame.maxY - windowSize.height)
        let x = min(max(origin.x, screenFrame.minX), maxX)
        let y = min(max(origin.y, screenFrame.minY), maxY)
        return CGPoint(x: x, y: y)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all 6 new tests)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift
git commit -m "feat: add pure sizing/position math for the break timer widget"
```

---

### Task 3: `BreakTimerRingGeometry` — pure ring/pulse math

**Files:**
- Create: `src/ZoomacIt/Overlay/BreakTimerRingGeometry.swift`
- Test: `src/ZoomacItTests/BreakTimerRingGeometryTests.swift`

**Interfaces:**
- Consumes: nothing.
- Produces: `BreakTimerRingGeometry.remainingFraction(remainingSeconds: Int, totalSeconds: Int) -> CGFloat` (used by Task 4's `BreakTimerView`), `.expiredPulseAlpha(elapsedTime: TimeInterval) -> CGFloat` (used by Task 6's expired-state rendering).

- [ ] **Step 1: Write the failing tests**

Create `src/ZoomacItTests/BreakTimerRingGeometryTests.swift`:

```swift
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `cannot find 'BreakTimerRingGeometry' in scope`

- [ ] **Step 3: Implement**

Create `src/ZoomacIt/Overlay/BreakTimerRingGeometry.swift`:

```swift
import CoreGraphics
import Foundation

/// Pure math for the Break Timer widget's progress ring and expiration pulse. No
/// AppKit dependency — `BreakTimerView` is the only consumer, and keeping this here
/// (rather than inline in `draw(_:)`) makes both formulas unit-testable.
enum BreakTimerRingGeometry {

    /// Fraction of the ring to draw as "time remaining" (1.0 = full circle, 0.0 = empty).
    static func remainingFraction(remainingSeconds: Int, totalSeconds: Int) -> CGFloat {
        guard totalSeconds > 0 else { return 0 }
        let fraction = CGFloat(remainingSeconds) / CGFloat(totalSeconds)
        return min(max(fraction, 0), 1)
    }

    /// Alpha for the pulsing red ring while expired, oscillating between 0.35 and 1.0
    /// on a 0.9s cycle. `elapsedTime` is any monotonically increasing clock (the
    /// caller uses `CACurrentMediaTime()`).
    static func expiredPulseAlpha(elapsedTime: TimeInterval) -> CGFloat {
        let period: TimeInterval = 0.9
        let phase = elapsedTime.truncatingRemainder(dividingBy: period) / period
        let wave = (sin(phase * 2 * .pi) + 1) / 2 // 0...1
        return 0.35 + wave * 0.65 // 0.35...1.0
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all 8 new tests)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerRingGeometry.swift src/ZoomacItTests/BreakTimerRingGeometryTests.swift
git commit -m "feat: add pure ring-fraction and expiration-pulse math for the break timer widget"
```

---

### Task 4: Replace the fullscreen overlay with a basic circular widget

This is the big cutover task: the window, view, controller, settings model, and settings UI all change together because they reference each other directly — the project won't compile with only some of them updated. Hover controls and the expired-state pulse are **not** part of this task (Tasks 5 and 6); after this task the widget is a plain circle with a progress ring and a countdown number that you can drag around, nothing more.

**Files:**
- Modify: `src/ZoomacIt/Models/Settings.swift`
- Modify: `src/ZoomacIt/Models/BreakTimerState.swift`
- Modify: `src/ZoomacIt/Overlay/BreakTimerWindow.swift` (full rewrite)
- Modify: `src/ZoomacIt/Overlay/BreakTimerView.swift` (full rewrite)
- Modify: `src/ZoomacIt/Overlay/BreakTimerWindowController.swift` (full rewrite)
- Modify: `src/ZoomacIt/Settings/BreakTimerTab.swift`
- Modify: `src/ZoomacItTests/SettingsTests.swift`
- Modify: `src/ZoomacItTests/BreakTimerStateTests.swift`

**Interfaces:**
- Consumes: `BreakTimerWidgetMetrics` (Task 2), `BreakTimerRingGeometry.remainingFraction` (Task 3), `Settings.shared.breakTimerWidgetPosition` (Task 1).
- Produces: `BreakTimerWindow.init(at origin: CGPoint)` (replaces `init(for screen:)`), `BreakTimerView.init(state:)` (replaces `init(frame:state:capturedImage:)`), `BreakTimerView.onDismiss: (() -> Void)?` (unchanged name/type — Task 5 doesn't need to touch it), `BreakTimerView.needsDisplay` still driven by the controller's 1s countdown timer. Task 5 (hover buttons) and Task 6 (expired pulse) both add to the `BreakTimerView` created here.

- [ ] **Step 1: Remove `BreakTimerBackground`/`breakTimerBackgroundFadeDarkness` from `Settings`**

In `src/ZoomacIt/Models/Settings.swift`:

Remove these two lines from `enum Keys` (lines 90, 94):
```swift
        static let breakTimerBackground = "breakTimerBackground"
```
```swift
        static let breakTimerBackgroundFadeDarkness = "breakTimerBackgroundFadeDarkness"
```

In `registerDefaults()`, replace the `// Break Timer` block:

```swift
            // Break Timer
            Keys.breakTimerDefaultDuration: 600,
            Keys.breakTimerColor: PenColor.red.rawValue,
            Keys.breakTimerOpacity: 1.0,
            Keys.breakTimerBackground: BreakTimerBackground.black.rawValue,
            Keys.breakTimerShowElapsed: true,
            Keys.breakTimerPlaySound: false,
            Keys.breakTimerBackgroundFadeDarkness: 0.6
        ])
```

with:

```swift
            // Break Timer
            Keys.breakTimerDefaultDuration: 600,
            Keys.breakTimerColor: PenColor.red.rawValue,
            Keys.breakTimerOpacity: 1.0,
            Keys.breakTimerShowElapsed: true,
            Keys.breakTimerPlaySound: false
        ])
```
(Leave the trailing comma structure valid — `Keys.breakTimerShowElapsed: true,` and `Keys.breakTimerPlaySound: false` stay adjacent with a comma between them, and `Keys.breakTimerPlaySound: false` is now the last entry before the closing `])`; drop its trailing comma if it had one dangling from the removed line.)

Remove the `breakTimerBackground` and `breakTimerBackgroundFadeDarkness` computed properties (lines 253–256 and 276–279):
```swift
    var breakTimerBackground: BreakTimerBackground {
        get { BreakTimerBackground(rawValue: defaults.string(forKey: Keys.breakTimerBackground) ?? "") ?? .black }
        set { defaults.set(newValue.rawValue, forKey: Keys.breakTimerBackground) }
    }
```
```swift
    var breakTimerBackgroundFadeDarkness: CGFloat {
        get { CGFloat(defaults.double(forKey: Keys.breakTimerBackgroundFadeDarkness)) }
        set { defaults.set(Double(newValue), forKey: Keys.breakTimerBackgroundFadeDarkness) }
    }
```

In `resetToDefaults()`, replace the whole `allKeys` array (after Task 1 it ends with the two widget-position keys) — this version:

```swift
        let allKeys: [String] = [
            Keys.zoomHotkeyKeyCode, Keys.zoomHotkeyModifiers,
            Keys.drawHotkeyKeyCode, Keys.drawHotkeyModifiers,
            Keys.breakHotkeyKeyCode, Keys.breakHotkeyModifiers,
            Keys.liveZoomHotkeyKeyCode, Keys.liveZoomHotkeyModifiers,
            Keys.defaultPenColor, Keys.defaultPenWidth,
            Keys.highlighterOpacity, Keys.highlighterWidthMultiplier,
            Keys.spotlightDarkness, Keys.vanishingPenLifetime,
            Keys.defaultFontSize, Keys.fontWeight,
            Keys.defaultZoomLevel, Keys.zoomAnimationEnabled,
            Keys.breakTimerDefaultDuration, Keys.breakTimerColor,
            Keys.breakTimerOpacity, Keys.breakTimerBackground,
            Keys.breakTimerShowElapsed, Keys.breakTimerPlaySound,
            Keys.breakTimerSoundFile, Keys.breakTimerBackgroundFadeDarkness,
            Keys.breakTimerWidgetPositionX, Keys.breakTimerWidgetPositionY
        ]
```

becomes:

```swift
        let allKeys: [String] = [
            Keys.zoomHotkeyKeyCode, Keys.zoomHotkeyModifiers,
            Keys.drawHotkeyKeyCode, Keys.drawHotkeyModifiers,
            Keys.breakHotkeyKeyCode, Keys.breakHotkeyModifiers,
            Keys.liveZoomHotkeyKeyCode, Keys.liveZoomHotkeyModifiers,
            Keys.defaultPenColor, Keys.defaultPenWidth,
            Keys.highlighterOpacity, Keys.highlighterWidthMultiplier,
            Keys.spotlightDarkness, Keys.vanishingPenLifetime,
            Keys.defaultFontSize, Keys.fontWeight,
            Keys.defaultZoomLevel, Keys.zoomAnimationEnabled,
            Keys.breakTimerDefaultDuration, Keys.breakTimerColor,
            Keys.breakTimerOpacity,
            Keys.breakTimerShowElapsed, Keys.breakTimerPlaySound,
            Keys.breakTimerSoundFile,
            Keys.breakTimerWidgetPositionX, Keys.breakTimerWidgetPositionY
        ]
```

- [ ] **Step 2: Remove `BreakTimerPosition`/`BreakTimerBackground` from `BreakTimerState`**

Replace the full contents of `src/ZoomacIt/Models/BreakTimerState.swift` with:

```swift
import AppKit

/// Mutable state for the Break Timer feature.
final class BreakTimerState {

    // MARK: - Timer

    /// Default countdown duration in seconds.
    var defaultDuration: Int = Settings.shared.breakTimerDefaultDuration

    /// Seconds remaining in the countdown. Stops at 0.
    var remainingSeconds: Int = Settings.shared.breakTimerDefaultDuration

    /// Whether the timer has reached zero.
    var isExpired: Bool { remainingSeconds <= 0 }

    /// Seconds elapsed after the timer expired (counts up from 0).
    var elapsedSinceExpiration: Int = 0

    // MARK: - Appearance

    /// Timer text color — reuses PenColor from Draw.
    var timerColor: PenColor = Settings.shared.breakTimerColor

    /// Widget opacity (0.1 … 1.0).
    var opacity: CGFloat = Settings.shared.breakTimerOpacity

    // MARK: - Options

    /// Show elapsed time after expiration.
    var showElapsed: Bool = Settings.shared.breakTimerShowElapsed

    /// Play a sound when time expires.
    var playSoundOnExpiration: Bool = Settings.shared.breakTimerPlaySound

    /// Custom sound file URL (nil = system default).
    var soundFileURL: URL? = Settings.shared.breakTimerSoundFile

    // MARK: - Methods

    /// Reload all properties from current Settings values.
    func reloadFromSettings() {
        defaultDuration = Settings.shared.breakTimerDefaultDuration
        timerColor = Settings.shared.breakTimerColor
        opacity = Settings.shared.breakTimerOpacity
        showElapsed = Settings.shared.breakTimerShowElapsed
        playSoundOnExpiration = Settings.shared.breakTimerPlaySound
        soundFileURL = Settings.shared.breakTimerSoundFile
    }

    /// Adjust remaining time by the given number of minutes.
    /// Clamps to a minimum of 0 seconds.
    func adjustTime(byMinutes minutes: Int) {
        let newValue = remainingSeconds + (minutes * 60)
        remainingSeconds = max(0, newValue)
        // If time was added after expiration, reset elapsed counter
        if remainingSeconds > 0 {
            elapsedSinceExpiration = 0
        }
    }

    /// Tick the timer by one second. Returns `true` if the timer just expired (transition to 0).
    @discardableResult
    func tick() -> Bool {
        if remainingSeconds > 0 {
            remainingSeconds -= 1
            if remainingSeconds == 0 {
                return true // just expired
            }
        } else {
            elapsedSinceExpiration += 1
        }
        return false
    }

    // MARK: - Formatting

    /// Formatted remaining time, e.g. "10:00", "1:01", "0:00".
    var formattedTime: String {
        let minutes = remainingSeconds / 60
        let seconds = remainingSeconds % 60
        return String(format: "%d:%02d", minutes, seconds)
    }

    /// Formatted elapsed time after expiration, e.g. "(1:15)".
    var formattedElapsed: String {
        let minutes = elapsedSinceExpiration / 60
        let seconds = elapsedSinceExpiration % 60
        return String(format: "(%d:%02d)", minutes, seconds)
    }
}
```

(This drops `BreakTimerPosition`, `BreakTimerBackground`, `position`, `background`, and their two lines in `reloadFromSettings()` — everything else is byte-for-byte the same as before.)

- [ ] **Step 3: Update `BreakTimerStateTests.swift`**

Remove these test methods entirely: `testDefaultPosition`, `testDefaultBackground`, `testPositionAllCases`, `testPositionCenterOrigin`, `testPositionTopLeft`, `testPositionBottomRight`, `testPositionCenterOnUltrawide`, `testPositionCenterOnSmallScreen`, `testBreakTimerBackgroundAllCases`.

In `testReloadFromSettings()`, remove the two background-related lines:
```swift
        Settings.shared.breakTimerBackground = .fadedDesktop
```
and
```swift
        XCTAssertEqual(state.background, .fadedDesktop)
```

- [ ] **Step 4: Update `SettingsTests.swift`**

In `testDefaultBreakTimer()`, remove:
```swift
        XCTAssertEqual(Settings.shared.breakTimerBackground, .black)
```
and
```swift
        XCTAssertEqual(Settings.shared.breakTimerBackgroundFadeDarkness, 0.6, accuracy: 0.001)
```

Remove the `testBreakTimerBackgroundRoundTrip()` method entirely.

- [ ] **Step 5: Rewrite `BreakTimerWindow.swift`**

Replace the full contents of `src/ZoomacIt/Overlay/BreakTimerWindow.swift` with:

```swift
import AppKit

/// A small, non-activating floating panel that hosts the Break Timer circular widget.
/// `level = .screenSaver` keeps it above the Draw overlay; `isMovableByWindowBackground`
/// lets the user drag the widget anywhere without any custom mouse-tracking code —
/// AppKit itself suppresses the drag when the mouse-down lands on a control (the
/// hover buttons added in a later task).
final class BreakTimerWindow: NSPanel {

    convenience init(at origin: CGPoint) {
        self.init(
            contentRect: NSRect(origin: origin, size: BreakTimerWidgetMetrics.windowSize),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        level = .screenSaver
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        isMovableByWindowBackground = true
        hidesOnDeactivate = false
    }
}
```

- [ ] **Step 6: Rewrite `BreakTimerView.swift`**

Replace the full contents of `src/ZoomacIt/Overlay/BreakTimerView.swift` with:

```swift
import AppKit

/// Draws the Break Timer circular widget: a translucent circle, a progress ring for
/// time remaining, and the countdown number centered inside it. Hover controls
/// (added in a later task) live in the empty strip below the circle
/// (`BreakTimerWidgetMetrics.controlBarHeight`).
@MainActor
final class BreakTimerView: NSView {

    let state: BreakTimerState

    /// Called when the user dismisses the timer via the hover close button (added later).
    var onDismiss: (() -> Void)?

    private let ringLineWidth: CGFloat = 6

    init(state: BreakTimerState) {
        self.state = state
        super.init(frame: NSRect(origin: .zero, size: BreakTimerWidgetMetrics.windowSize))
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    /// The circle sits in the upper portion of the view; the strip below it
    /// (`controlBarHeight` tall) is reserved for hover controls.
    private var circleFrame: NSRect {
        NSRect(
            x: 0,
            y: BreakTimerWidgetMetrics.controlBarHeight,
            width: BreakTimerWidgetMetrics.diameter,
            height: BreakTimerWidgetMetrics.diameter
        )
    }

    override func draw(_ dirtyRect: NSRect) {
        let circle = circleFrame
        drawFill(in: circle)
        drawRing(in: circle)
        drawTime(in: circle)
    }

    private func drawFill(in circle: NSRect) {
        let inset = circle.insetBy(dx: circle.width * 0.08, dy: circle.height * 0.08)
        NSColor.black.withAlphaComponent(0.55 * state.opacity).setFill()
        NSBezierPath(ovalIn: inset).fill()
    }

    private func drawRing(in circle: NSRect) {
        let ringRect = circle.insetBy(dx: ringLineWidth / 2, dy: ringLineWidth / 2)
        let center = NSPoint(x: ringRect.midX, y: ringRect.midY)
        let radius = ringRect.width / 2

        NSColor.white.withAlphaComponent(0.14).setStroke()
        let track = NSBezierPath(ovalIn: ringRect)
        track.lineWidth = ringLineWidth
        track.stroke()

        let fraction = BreakTimerRingGeometry.remainingFraction(
            remainingSeconds: state.remainingSeconds,
            totalSeconds: state.defaultDuration
        )
        guard fraction > 0 else { return }

        let progress = NSBezierPath()
        progress.appendArc(
            withCenter: center,
            radius: radius,
            startAngle: 90,
            endAngle: 90 - fraction * 360,
            clockwise: true
        )
        progress.lineWidth = ringLineWidth
        progress.lineCapStyle = .round
        state.timerColor.nsColor.withAlphaComponent(state.opacity).setStroke()
        progress.stroke()
    }

    private func drawTime(in circle: NSRect) {
        let fontSize = BreakTimerWidgetMetrics.diameter * 0.24
        let font = NSFont.monospacedDigitSystemFont(ofSize: fontSize, weight: .semibold)
        let attrs: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: NSColor.white
        ]
        let text = state.formattedTime as NSString
        let size = text.size(withAttributes: attrs)
        let origin = NSPoint(
            x: circle.midX - size.width / 2,
            y: circle.midY - size.height / 2
        )
        text.draw(at: origin, withAttributes: attrs)
    }
}
```

- [ ] **Step 7: Rewrite `BreakTimerWindowController.swift`**

Replace the full contents of `src/ZoomacIt/Overlay/BreakTimerWindowController.swift` with:

```swift
import AppKit
import AudioToolbox

/// Manages the lifecycle of the Break Timer overlay window.
@MainActor
final class BreakTimerWindowController {

    private var timerWindow: BreakTimerWindow?
    private var timerView: BreakTimerView?
    private var countdownTimer: Timer?
    private var state: BreakTimerState
    private var playingSound: NSSound?  // retain while playing

    /// Sound used for test playback from Settings. Static so it can be stopped.
    private static var testSound: NSSound?

    /// Whether a test sound is currently playing.
    static var isTestSoundPlaying: Bool {
        testSound?.isPlaying ?? false
    }

    /// True while the timer is visible.
    var isActive: Bool { timerWindow != nil }

    init() {
        self.state = BreakTimerState()
    }

    // MARK: - Public

    func showTimer() {
        guard let screen = NSScreen.main else {
            NSLog("[BreakTimerController] No main screen available.")
            return
        }

        NSLog("[BreakTimerController] Starting break timer: %d seconds", state.defaultDuration)
        state.reloadFromSettings()
        state.remainingSeconds = state.defaultDuration
        state.elapsedSinceExpiration = 0

        let savedPosition = Settings.shared.breakTimerWidgetPosition
        let origin = BreakTimerWidgetMetrics.clamped(
            origin: savedPosition ?? BreakTimerWidgetMetrics.defaultOrigin(in: screen.frame),
            in: screen.frame
        )

        let window = BreakTimerWindow(at: origin)
        let view = BreakTimerView(state: state)
        view.onDismiss = { [weak self] in
            self?.dismiss()
        }

        window.contentView = view
        window.orderFront(nil)

        timerWindow = window
        timerView = view

        startCountdown()
    }

    func dismiss() {
        NSLog("[BreakTimerController] Dismissing break timer.")

        if let window = timerWindow {
            Settings.shared.breakTimerWidgetPosition = window.frame.origin
        }

        countdownTimer?.invalidate()
        countdownTimer = nil

        // Stop any expiration sound still playing
        playingSound?.stop()
        playingSound = nil

        timerWindow?.orderOut(nil)
        timerWindow?.close()
        timerWindow = nil
        timerView = nil

        // Notify the app delegate
        if let appDelegate = NSApplication.shared.delegate as? AppDelegate {
            appDelegate.breakTimerDidEnd()
        }
    }

    /// Bring the timer window back to the foreground (e.g. from menu bar click).
    /// Does not activate the app — the widget floats without stealing focus.
    func bringToFront() {
        timerWindow?.orderFront(nil)
    }

    // MARK: - Private

    private func startCountdown() {
        countdownTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0, repeats: true) { [weak self] _ in
            guard let self else { return }
            let justExpired = self.state.tick()

            if justExpired {
                NSLog("[BreakTimerController] Timer expired!")
                self.playExpirationSound()
            }

            self.timerView?.needsDisplay = true
        }
        // .common so the countdown keeps ticking while the user is dragging the widget
        // (dragging runs the run loop in .eventTracking mode).
        RunLoop.main.add(timer, forMode: .common)
        countdownTimer = timer
        NSLog("[BreakTimerController] Countdown started.")
    }

    private func playExpirationSound() {
        guard state.playSoundOnExpiration else {
            NSLog("[BreakTimerController] Sound disabled, skipping.")
            return
        }
        if let url = state.soundFileURL {
            if let sound = NSSound(contentsOf: url, byReference: true) {
                playingSound = sound
                sound.play()
                NSLog("[BreakTimerController] Playing custom sound from %@.", url.lastPathComponent)
            } else {
                NSLog("[BreakTimerController] Failed to load sound from %@, playing default.", url.absoluteString)
                Self.playDefaultSound()
            }
        } else {
            Self.playDefaultSound()
        }
    }

    /// Play a test sound from Settings. Stops any previously playing test sound first.
    static func playTestSound(fileURL: URL?) {
        stopTestSound()
        if let url = fileURL {
            if let sound = NSSound(contentsOf: url, byReference: true) {
                testSound = sound
                sound.play()
                NSLog("[BreakTimerController] Test: playing custom sound from %@.", url.lastPathComponent)
            } else {
                NSLog("[BreakTimerController] Test: failed to load sound from %@, playing default.", url.absoluteString)
                playDefaultSound()
            }
        } else {
            playDefaultSound()
        }
    }

    /// Stop the currently playing test sound.
    static func stopTestSound() {
        testSound?.stop()
        testSound = nil
    }

    /// Play the default expiration alert sound.
    private static func playDefaultSound() {
        if let glass = NSSound(named: "Glass") {
            glass.play()
        } else {
            AudioServicesPlayAlertSound(kSystemSoundID_UserPreferredAlert)
        }
        NSLog("[BreakTimerController] Playing default alert sound.")
    }
}
```

(This drops the entire `// MARK: - Screen Capture` section and its `ScreenCaptureKit` import, and the `NSApplication.shared.activate(ignoringOtherApps:)` call. The pulse timer for the expired state is added in Task 6, not here.)

- [ ] **Step 8: Remove the Background/Fade Darkness settings UI**

In `src/ZoomacIt/Settings/BreakTimerTab.swift`, remove these two `@AppStorage` properties:
```swift
    @AppStorage(Settings.Keys.breakTimerBackground) private var backgroundRaw: String = BreakTimerBackground.black.rawValue
```
```swift
    @AppStorage(Settings.Keys.breakTimerBackgroundFadeDarkness) private var fadeDarkness: Double = 0.6
```

Remove the `background` computed `Binding`:
```swift
    private var background: Binding<BreakTimerBackground> {
        Binding(
            get: { BreakTimerBackground(rawValue: backgroundRaw) ?? .black },
            set: { backgroundRaw = $0.rawValue }
        )
    }
```

Remove the `Picker("Background", ...)` block and the `if BreakTimerBackground(rawValue: backgroundRaw) == .fadedDesktop { ... }` block (the whole "Fade Darkness" `HStack` inside it) from the `Section("Appearance")`.

- [ ] **Step 9: Run tests to verify everything still passes**

Run: `make test`
Expected: PASS — full suite green, no references to `BreakTimerPosition`/`BreakTimerBackground` remain anywhere.

- [ ] **Step 10: Manual build & run verification**

Run: `make build`
Then open the built app from `src/build/Build/Products/Debug/` (exact `.app` name depends on this machine's local `PRODUCT_NAME` override — check that folder if unsure) and confirm:
1. Press ⌃3 (or your configured Break Timer hotkey) — a small circle with a countdown and a progress ring appears centered on screen, instead of the old fullscreen overlay.
2. Click-drag anywhere on the circle — it follows the mouse.
3. The app you were previously using stays focused/frontmost the whole time (the widget never steals focus).
4. Press ⌃3 again — the widget disappears.
5. Press ⌃3 a third time — it reappears at the same spot you last dragged it to.

- [ ] **Step 11: Commit**

```bash
git add src/ZoomacIt/Models/Settings.swift src/ZoomacIt/Models/BreakTimerState.swift \
        src/ZoomacIt/Overlay/BreakTimerWindow.swift src/ZoomacIt/Overlay/BreakTimerView.swift \
        src/ZoomacIt/Overlay/BreakTimerWindowController.swift src/ZoomacIt/Settings/BreakTimerTab.swift \
        src/ZoomacItTests/SettingsTests.swift src/ZoomacItTests/BreakTimerStateTests.swift
git commit -m "feat: replace fullscreen break timer with a draggable circular widget"
```

---

### Task 5: Hover controls (close / −1 min / +1 min)

**Files:**
- Modify: `src/ZoomacIt/Overlay/BreakTimerView.swift`

**Interfaces:**
- Consumes: `state.adjustTime(byMinutes:)`, `onDismiss` (both already exist from Task 4).
- Produces: nothing new consumed by later tasks — this is the last piece of `BreakTimerView` besides the expired-state rendering in Task 6.

- [ ] **Step 1: Add the three buttons and hover tracking**

In `src/ZoomacIt/Overlay/BreakTimerView.swift`, add these properties right after `private let ringLineWidth: CGFloat = 6`:

```swift

    private lazy var minusButton = makeControlButton(symbolName: "minus", action: #selector(minusTapped))
    private lazy var closeButton = makeControlButton(symbolName: "xmark", action: #selector(closeTapped))
    private lazy var plusButton = makeControlButton(symbolName: "plus", action: #selector(plusTapped))

    private var trackingArea: NSTrackingArea?
```

Change the `init(state:)` body to also lay out the buttons:

```swift
    init(state: BreakTimerState) {
        self.state = state
        super.init(frame: NSRect(origin: .zero, size: BreakTimerWidgetMetrics.windowSize))
        setUpControlButtons()
    }
```

Add these methods at the end of the class, before the closing brace:

```swift

    // MARK: - Hover Controls

    private func makeControlButton(symbolName: String, action: Selector) -> NSButton {
        let button = NSButton(
            image: NSImage(systemSymbolName: symbolName, accessibilityDescription: nil) ?? NSImage(),
            target: self,
            action: action
        )
        button.bezelStyle = .circular
        button.isBordered = true
        button.imageScaling = .scaleProportionallyDown
        button.contentTintColor = .white
        button.alphaValue = 0
        return button
    }

    private func setUpControlButtons() {
        let buttons = [minusButton, closeButton, plusButton]
        let buttonSize: CGFloat = 22
        let spacing: CGFloat = 6
        let totalWidth = buttonSize * 3 + spacing * 2
        var x = bounds.midX - totalWidth / 2
        let y = (BreakTimerWidgetMetrics.controlBarHeight - buttonSize) / 2

        for button in buttons {
            button.frame = NSRect(x: x, y: y, width: buttonSize, height: buttonSize)
            addSubview(button)
            x += buttonSize + spacing
        }
    }

    @objc private func minusTapped() {
        state.adjustTime(byMinutes: -1)
        needsDisplay = true
    }

    @objc private func plusTapped() {
        state.adjustTime(byMinutes: 1)
        needsDisplay = true
    }

    @objc private func closeTapped() {
        onDismiss?()
    }

    // MARK: - Hover Tracking

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let trackingArea {
            removeTrackingArea(trackingArea)
        }
        let area = NSTrackingArea(
            rect: bounds,
            options: [.mouseEnteredAndExited, .activeAlways],
            owner: self,
            userInfo: nil
        )
        addTrackingArea(area)
        trackingArea = area
    }

    override func mouseEntered(with event: NSEvent) {
        setControlButtons(hidden: false)
    }

    override func mouseExited(with event: NSEvent) {
        setControlButtons(hidden: true)
    }

    private func setControlButtons(hidden: Bool) {
        NSAnimationContext.runAnimationGroup { context in
            context.duration = 0.15
            for button in [minusButton, closeButton, plusButton] {
                button.animator().alphaValue = hidden ? 0 : 1
            }
        }
    }
```

- [ ] **Step 2: Run the full test suite (regression check)**

Run: `make test`
Expected: PASS — this task added no pure-logic changes, so no new automated tests; confirm nothing broke.

- [ ] **Step 3: Manual build & run verification**

Run: `make build`, open the app, press ⌃3, and confirm:
1. Hovering the mouse over the circle fades in three small buttons (−, ×, +) in the strip below it.
2. Moving the mouse away fades them back out.
3. Clicking **−** decreases the countdown by 1 minute (watch the number and the ring both update).
4. Clicking **+** increases it by 1 minute.
5. Clicking **×** dismisses the widget (same as pressing ⌃3 again).
6. Clicking and dragging a button does **not** move the widget (only dragging the empty circle background does).

- [ ] **Step 4: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerView.swift
git commit -m "feat: add hover controls to the break timer widget"
```

---

### Task 6: Expired-state rendering (red pulse + elapsed count-up)

**Files:**
- Modify: `src/ZoomacIt/Overlay/BreakTimerView.swift`
- Modify: `src/ZoomacIt/Overlay/BreakTimerWindowController.swift`

**Interfaces:**
- Consumes: `BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime:)` (Task 3), `state.isExpired`, `state.showElapsed`, `state.formattedElapsed` (all pre-existing on `BreakTimerState`).
- Produces: nothing new consumed elsewhere — this is the last task.

- [ ] **Step 1: Extend `drawRing(in:)` for the expired case**

In `src/ZoomacIt/Overlay/BreakTimerView.swift`, add `import QuartzCore` at the top (needed for `CACurrentMediaTime()`):

```swift
import AppKit
import QuartzCore
```

Change `drawRing(in:)` to branch on `state.isExpired` before drawing the normal progress arc:

```swift
    private func drawRing(in circle: NSRect) {
        let ringRect = circle.insetBy(dx: ringLineWidth / 2, dy: ringLineWidth / 2)
        let center = NSPoint(x: ringRect.midX, y: ringRect.midY)
        let radius = ringRect.width / 2

        NSColor.white.withAlphaComponent(0.14).setStroke()
        let track = NSBezierPath(ovalIn: ringRect)
        track.lineWidth = ringLineWidth
        track.stroke()

        if state.isExpired {
            let alpha = BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: CACurrentMediaTime())
            let full = NSBezierPath(ovalIn: ringRect)
            full.lineWidth = ringLineWidth
            full.lineCapStyle = .round
            NSColor.systemRed.withAlphaComponent(alpha).setStroke()
            full.stroke()
            return
        }

        let fraction = BreakTimerRingGeometry.remainingFraction(
            remainingSeconds: state.remainingSeconds,
            totalSeconds: state.defaultDuration
        )
        guard fraction > 0 else { return }

        let progress = NSBezierPath()
        progress.appendArc(
            withCenter: center,
            radius: radius,
            startAngle: 90,
            endAngle: 90 - fraction * 360,
            clockwise: true
        )
        progress.lineWidth = ringLineWidth
        progress.lineCapStyle = .round
        state.timerColor.nsColor.withAlphaComponent(state.opacity).setStroke()
        progress.stroke()
    }
```

- [ ] **Step 2: Show elapsed time (or a static "0:00") once expired**

Change `drawTime(in:)` to pick the right string:

```swift
    private func drawTime(in circle: NSRect) {
        let fontSize = BreakTimerWidgetMetrics.diameter * 0.24
        let font = NSFont.monospacedDigitSystemFont(ofSize: fontSize, weight: .semibold)
        let attrs: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: NSColor.white
        ]
        let displayText: String
        if state.isExpired {
            displayText = state.showElapsed ? state.formattedElapsed : "0:00"
        } else {
            displayText = state.formattedTime
        }
        let text = displayText as NSString
        let size = text.size(withAttributes: attrs)
        let origin = NSPoint(
            x: circle.midX - size.width / 2,
            y: circle.midY - size.height / 2
        )
        text.draw(at: origin, withAttributes: attrs)
    }
```

- [ ] **Step 3: Add the pulse timer to the controller**

In `src/ZoomacIt/Overlay/BreakTimerWindowController.swift`, add a new property next to `countdownTimer`:

```swift
    private var countdownTimer: Timer?
    private var pulseTimer: Timer?
```

In `startCountdown()`'s timer closure, start pulsing when the timer just expired:

```swift
            if justExpired {
                NSLog("[BreakTimerController] Timer expired!")
                self.playExpirationSound()
                self.startPulsing()
            }
```

Add the new method, right after `startCountdown()`:

```swift

    /// Redraws at ~20fps so the expired-state ring pulse animates smoothly. Self-stops
    /// once `state` is no longer expired (e.g. the user clicked +1 min), and gets
    /// restarted by `startCountdown()` the next time the timer expires.
    private func startPulsing() {
        pulseTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0 / 20.0, repeats: true) { [weak self] timer in
            guard let self else {
                timer.invalidate()
                return
            }
            guard self.state.isExpired else {
                timer.invalidate()
                self.pulseTimer = nil
                return
            }
            self.timerView?.needsDisplay = true
        }
        RunLoop.main.add(timer, forMode: .common)
        pulseTimer = timer
    }
```

Update `dismiss()` to also invalidate the pulse timer, right next to where `countdownTimer` is invalidated:

```swift
        countdownTimer?.invalidate()
        countdownTimer = nil
        pulseTimer?.invalidate()
        pulseTimer = nil
```

- [ ] **Step 4: Run the full test suite (regression check)**

Run: `make test`
Expected: PASS — `BreakTimerRingGeometryTests` already covers `expiredPulseAlpha`'s math; this step only wires it into AppKit drawing, which this repo doesn't unit test.

- [ ] **Step 5: Manual build & run verification**

In Settings, temporarily set Break Timer **Default Duration** to its minimum (1 minute). Then run `make build`, open the app, press ⌃3, and confirm:
1. Wait for the countdown to reach 0:00 — the ring switches to solid red and pulses smoothly (not just once a second).
2. The center number switches to counting up from 0:00 (elapsed time), assuming "Show Elapsed Time After Expiration" is on in Settings.
3. Click **+** once — the widget immediately returns to normal countdown mode (ring back to the user's timer color, number counting down again), and the pulse stops.
4. Toggle "Show Elapsed Time After Expiration" off in Settings, restart the timer, let it expire again — the center number now stays at a static "0:00" instead of counting up, while the ring still pulses red.

Afterwards, restore Default Duration to your preferred value in Settings.

- [ ] **Step 6: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerView.swift src/ZoomacIt/Overlay/BreakTimerWindowController.swift
git commit -m "feat: add red pulse and elapsed count-up to the break timer widget on expiration"
```

---

## Self-Review Notes

**Spec coverage:** every requirement in `docs/superpowers/specs/2026-07-16-break-timer-widget-design.md` maps to a task — circle+ring+countdown (Task 4), drag + position memory (Task 1, Task 4), hover buttons replacing keyboard shortcuts (Task 5, keyboard removal happened in Task 4's `BreakTimerView` rewrite which simply omits `keyDown`/`acceptsFirstResponder`), no-activate (Task 4), expired pulse + elapsed (Task 6), settings UI cleanup (Task 4 Step 8), background-capture/position-grid removal (Task 4 Steps 1–4).

**Placeholder scan:** no TBD/TODO markers; every step shows complete code.

**Type consistency:** `BreakTimerWindow.init(at:)`, `BreakTimerView.init(state:)`, `BreakTimerWidgetMetrics.{diameter, controlBarHeight, windowSize, defaultOrigin, clamped}`, and `BreakTimerRingGeometry.{remainingFraction, expiredPulseAlpha}` are named identically everywhere they're defined and used across Tasks 2–6.
