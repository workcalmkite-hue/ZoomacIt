# Break Timer Widget Scroll-to-Resize Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scrolling over the Break Timer circular widget resizes it continuously between 70 pt and 220 pt, everything scales proportionally, and the size persists.

**Architecture:** `BreakTimerWidgetMetrics` (pure geometry) becomes diameter-parameterized; `Settings` gains a clamped `breakTimerWidgetDiameter`; `BreakTimerView` forwards `scrollWheel` deltas via a closure (same pattern as `onDismiss`); `BreakTimerWindowController` owns the resize transaction (new diameter → center-anchored origin → screen clamp → `setFrame` → persist).

**Tech Stack:** Swift / AppKit / XCTest. Spec: `docs/superpowers/specs/2026-07-17-break-timer-widget-resize-design.md`.

## Global Constraints

- Diameter range **[70, 220]**, base/default **110**. Scale factor = `diameter / 110`.
- Scroll sensitivity: precise deltas (trackpad) `× 0.5` pt per unit; line deltas (mouse wheel) `× 5` pt per unit. Positive `scrollingDeltaY` = larger.
- Base sizes that scale: control bar 34, ring line width 6, hover buttons 22, button spacing 6, font `0.24 × diameter`.
- Test/build on this machine: run `xcodebuild` directly (not `make`) and append `DEVELOPMENT_TEAM=<local team id>` — the value is in `.superpowers/sdd/progress.md` line "Working CLI". **NEVER commit that value into any file.**
- Full test command (from repo root):
  `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath build test DEVELOPMENT_TEAM=<local team id>`
- All existing tests must stay green after every task (170 tests at baseline).

---

### Task 1: Diameter-parameterized metrics

**Files:**
- Modify: `src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift` (full rewrite, 36 lines)
- Modify: `src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift` (full rewrite)
- Modify (mechanical call-site updates so the target compiles; behavior unchanged):
  - `src/ZoomacIt/Overlay/BreakTimerView.swift:40` (init frame), `:51-58` (circleFrame), `:115` (font size), `:157` (button y)
  - `src/ZoomacIt/Overlay/BreakTimerWindow.swift:12` (contentRect size)
  - `src/ZoomacIt/Overlay/BreakTimerWindowController.swift:46,50,52` (widgetRect / clamped / defaultOrigin)

**Interfaces:**
- Consumes: nothing new.
- Produces (used by Tasks 2–3):
  - `BreakTimerWidgetMetrics.baseDiameter/minDiameter/maxDiameter: CGFloat`
  - `clampedDiameter(_ diameter: CGFloat) -> CGFloat`
  - `scale(forDiameter: CGFloat) -> CGFloat`
  - `controlBarHeight(forDiameter: CGFloat) -> CGFloat`
  - `windowSize(forDiameter: CGFloat) -> CGSize`
  - `defaultOrigin(in: CGRect, forDiameter: CGFloat) -> CGPoint`
  - `clamped(origin: CGPoint, in: CGRect, forDiameter: CGFloat) -> CGPoint`
  - `diameter(afterScrollDeltaY: CGFloat, isPrecise: Bool, from: CGFloat) -> CGFloat`
  - `resizedOrigin(currentOrigin: CGPoint, fromDiameter: CGFloat, toDiameter: CGFloat) -> CGPoint`
  - The old parameterless statics (`diameter`, `controlBarHeight`, `windowSize`, 2-arg `defaultOrigin`/`clamped`) are **removed**.

- [ ] **Step 1: Rewrite the metrics tests (they will fail to compile — that is the failing state)**

Replace the entire contents of `src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift` with:

```swift
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
```

- [ ] **Step 2: Run the metrics tests to verify they fail**

Run (repo root): `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath build test DEVELOPMENT_TEAM=<local team id> 2>&1 | tail -20`
Expected: **BUILD FAILED** — the parameterized functions don't exist yet.

- [ ] **Step 3: Rewrite `BreakTimerWidgetMetrics`**

Replace the entire contents of `src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift` with:

```swift
import CoreGraphics

/// Pure geometry for the Break Timer circular widget. No AppKit dependency, so it's
/// testable without a real screen or window — `BreakTimerWindow`/`BreakTimerView`
/// read these values, and `BreakTimerWindowController` uses the origin helpers
/// to place/restore/resize the widget.
///
/// The widget is user-resizable (scroll over it), so every metric is a function of
/// the current diameter; `baseDiameter` is both the default and the reference all
/// proportional sizes (ring, buttons, fonts) scale against.
enum BreakTimerWidgetMetrics {

    /// Diameter the widget ships with, and the reference for proportional scaling.
    static let baseDiameter: CGFloat = 110

    /// Smallest diameter the user can scroll down to (countdown stays legible).
    static let minDiameter: CGFloat = 70

    /// Largest diameter the user can scroll up to.
    static let maxDiameter: CGFloat = 220

    /// Control-bar height at `baseDiameter`; scales linearly with diameter.
    private static let baseControlBarHeight: CGFloat = 34

    static func clampedDiameter(_ diameter: CGFloat) -> CGFloat {
        min(max(diameter, minDiameter), maxDiameter)
    }

    /// Proportional-scaling factor for a given diameter (1.0 at `baseDiameter`).
    static func scale(forDiameter diameter: CGFloat) -> CGFloat {
        diameter / baseDiameter
    }

    /// Extra height below the circle reserved for the hover control buttons.
    static func controlBarHeight(forDiameter diameter: CGFloat) -> CGFloat {
        baseControlBarHeight * scale(forDiameter: diameter)
    }

    /// Total window content size (circle + control bar strip below it).
    static func windowSize(forDiameter diameter: CGFloat) -> CGSize {
        CGSize(width: diameter, height: diameter + controlBarHeight(forDiameter: diameter))
    }

    /// Where to place the widget the first time it's shown (no saved position yet):
    /// centered on the given screen.
    static func defaultOrigin(in screenFrame: CGRect, forDiameter diameter: CGFloat) -> CGPoint {
        let size = windowSize(forDiameter: diameter)
        return CGPoint(
            x: screenFrame.midX - size.width / 2,
            y: screenFrame.midY - size.height / 2
        )
    }

    /// Keeps a (possibly stale, e.g. from a since-disconnected display) origin fully
    /// on the given screen.
    static func clamped(origin: CGPoint, in screenFrame: CGRect, forDiameter diameter: CGFloat) -> CGPoint {
        let size = windowSize(forDiameter: diameter)
        let maxX = max(screenFrame.minX, screenFrame.maxX - size.width)
        let maxY = max(screenFrame.minY, screenFrame.maxY - size.height)
        let x = min(max(origin.x, screenFrame.minX), maxX)
        let y = min(max(origin.y, screenFrame.minY), maxY)
        return CGPoint(x: x, y: y)
    }

    /// New diameter after a scroll gesture. Precise deltas (trackpads) arrive in much
    /// larger quantities per gesture than mouse-wheel line deltas, so they use a finer
    /// points-per-unit ratio to make both input devices feel similar.
    static func diameter(afterScrollDeltaY deltaY: CGFloat, isPrecise: Bool, from current: CGFloat) -> CGFloat {
        let pointsPerUnit: CGFloat = isPrecise ? 0.5 : 5
        return clampedDiameter(current + deltaY * pointsPerUnit)
    }

    /// Window origin that keeps the window's center fixed across a diameter change.
    /// Screen clamping is applied separately by the caller (the target screen is an
    /// AppKit concern this pure-geometry type doesn't know about).
    static func resizedOrigin(currentOrigin: CGPoint, fromDiameter old: CGFloat, toDiameter new: CGFloat) -> CGPoint {
        let oldSize = windowSize(forDiameter: old)
        let newSize = windowSize(forDiameter: new)
        return CGPoint(
            x: currentOrigin.x + (oldSize.width - newSize.width) / 2,
            y: currentOrigin.y + (oldSize.height - newSize.height) / 2
        )
    }
}
```

- [ ] **Step 4: Update the call sites mechanically (behavior unchanged — everything passes `baseDiameter`)**

In `src/ZoomacIt/Overlay/BreakTimerView.swift`:

Line 40 (init):
```swift
        super.init(frame: NSRect(
            origin: .zero,
            size: BreakTimerWidgetMetrics.windowSize(forDiameter: BreakTimerWidgetMetrics.baseDiameter)
        ))
```

Lines 51–58 (`circleFrame`):
```swift
    private var circleFrame: NSRect {
        NSRect(
            x: 0,
            y: BreakTimerWidgetMetrics.controlBarHeight(forDiameter: BreakTimerWidgetMetrics.baseDiameter),
            width: BreakTimerWidgetMetrics.baseDiameter,
            height: BreakTimerWidgetMetrics.baseDiameter
        )
    }
```

Line 115 (`drawTime` font size):
```swift
        let fontSize = BreakTimerWidgetMetrics.baseDiameter * 0.24
```

Line 157 (`setUpControlButtons` button y):
```swift
        let y = (BreakTimerWidgetMetrics.controlBarHeight(forDiameter: BreakTimerWidgetMetrics.baseDiameter) - buttonSize) / 2
```

In `src/ZoomacIt/Overlay/BreakTimerWindow.swift` line 12:
```swift
            contentRect: NSRect(
                origin: origin,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: BreakTimerWidgetMetrics.baseDiameter)
            ),
```

In `src/ZoomacIt/Overlay/BreakTimerWindowController.swift` lines 43–53:
```swift
        let savedPosition = Settings.shared.breakTimerWidgetPosition
        let origin: CGPoint
        if let saved = savedPosition {
            let widgetRect = CGRect(
                origin: saved,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: BreakTimerWidgetMetrics.baseDiameter)
            )
            // Keep the widget on whichever connected screen the user left it on;
            // fall back to the main screen only if that display is gone.
            let host = NSScreen.screens.first { $0.frame.intersects(widgetRect) } ?? screen
            origin = BreakTimerWidgetMetrics.clamped(
                origin: saved, in: host.frame, forDiameter: BreakTimerWidgetMetrics.baseDiameter)
        } else {
            origin = BreakTimerWidgetMetrics.defaultOrigin(
                in: screen.frame, forDiameter: BreakTimerWidgetMetrics.baseDiameter)
        }
```

- [ ] **Step 5: Run the full suite to verify it passes**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath build test DEVELOPMENT_TEAM=<local team id> 2>&1 | tail -5`
Expected: **TEST SUCCEEDED** (baseline 170 tests minus 6 legacy metrics tests plus 17 new = 181).

- [ ] **Step 6: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerWidgetMetrics.swift src/ZoomacIt/Overlay/BreakTimerView.swift src/ZoomacIt/Overlay/BreakTimerWindow.swift src/ZoomacIt/Overlay/BreakTimerWindowController.swift src/ZoomacItTests/BreakTimerWidgetMetricsTests.swift
git commit -m "refactor: parameterize break timer widget metrics by diameter"
```

---

### Task 2: Persisted widget diameter setting

**Files:**
- Modify: `src/ZoomacIt/Models/Settings.swift` (new key near line 94, new property after `breakTimerWidgetPosition` ~line 288, key added to `resetToDefaults()` list ~line 307)
- Test: `src/ZoomacItTests/SettingsTests.swift` (add after the `breakTimerWidgetPosition` tests, ~line 131)

**Interfaces:**
- Consumes: `BreakTimerWidgetMetrics.baseDiameter`, `clampedDiameter(_:)` (Task 1).
- Produces: `Settings.shared.breakTimerWidgetDiameter: CGFloat` — get returns 110 when unset, clamps stored values to [70, 220]; set clamps then stores. Used by Task 3.

- [ ] **Step 1: Write the failing tests**

Add to `src/ZoomacItTests/SettingsTests.swift` directly after `testBreakTimerWidgetPositionClearedBySettingNil` (line 131):

```swift
    func testBreakTimerWidgetDiameterDefaultsToBaseDiameter() {
        XCTAssertEqual(Settings.shared.breakTimerWidgetDiameter, BreakTimerWidgetMetrics.baseDiameter)
    }

    func testBreakTimerWidgetDiameterRoundTrip() {
        Settings.shared.breakTimerWidgetDiameter = 180
        XCTAssertEqual(Settings.shared.breakTimerWidgetDiameter, 180)
    }

    func testBreakTimerWidgetDiameterClampsStoredOutOfRangeValuesOnRead() {
        // Simulate a stale/hand-edited defaults value.
        UserDefaults.standard.set(1000.0, forKey: "breakTimerWidgetDiameter")
        XCTAssertEqual(Settings.shared.breakTimerWidgetDiameter, BreakTimerWidgetMetrics.maxDiameter)
        UserDefaults.standard.set(10.0, forKey: "breakTimerWidgetDiameter")
        XCTAssertEqual(Settings.shared.breakTimerWidgetDiameter, BreakTimerWidgetMetrics.minDiameter)
    }

    func testBreakTimerWidgetDiameterClampsOnWrite() {
        Settings.shared.breakTimerWidgetDiameter = 9999
        XCTAssertEqual(
            UserDefaults.standard.double(forKey: "breakTimerWidgetDiameter"),
            Double(BreakTimerWidgetMetrics.maxDiameter))
    }

    func testResetClearsBreakTimerWidgetDiameter() {
        Settings.shared.breakTimerWidgetDiameter = 200
        Settings.shared.resetToDefaults()
        XCTAssertEqual(Settings.shared.breakTimerWidgetDiameter, BreakTimerWidgetMetrics.baseDiameter)
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath build test DEVELOPMENT_TEAM=<local team id> 2>&1 | tail -10`
Expected: **BUILD FAILED** — `breakTimerWidgetDiameter` doesn't exist.

- [ ] **Step 3: Implement**

In `src/ZoomacIt/Models/Settings.swift`:

(a) Add the key after `breakTimerWidgetPositionY` (line 94):
```swift
        static let breakTimerWidgetDiameter = "breakTimerWidgetDiameter"
```

(b) Add the property directly after the `breakTimerWidgetPosition` computed property (~line 288):
```swift
    /// User-chosen widget diameter (scroll-to-resize). Clamped on both read and write
    /// so a stale or hand-edited defaults value can't produce a broken widget.
    var breakTimerWidgetDiameter: CGFloat {
        get {
            guard let stored = defaults.object(forKey: Keys.breakTimerWidgetDiameter) as? Double else {
                return BreakTimerWidgetMetrics.baseDiameter
            }
            return BreakTimerWidgetMetrics.clampedDiameter(CGFloat(stored))
        }
        set {
            defaults.set(
                Double(BreakTimerWidgetMetrics.clampedDiameter(newValue)),
                forKey: Keys.breakTimerWidgetDiameter
            )
        }
    }
```

(c) In `resetToDefaults()` (~line 307), extend the position line:
```swift
            Keys.breakTimerWidgetPositionX, Keys.breakTimerWidgetPositionY,
            Keys.breakTimerWidgetDiameter,
```

- [ ] **Step 4: Run to verify pass**

Same command. Expected: **TEST SUCCEEDED** (186 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Models/Settings.swift src/ZoomacItTests/SettingsTests.swift
git commit -m "feat: persist a clamped break timer widget diameter setting"
```

---

### Task 3: Scroll-to-resize wiring (view, window, controller)

**Files:**
- Modify: `src/ZoomacIt/Overlay/BreakTimerView.swift` (diameter property, scaled drawing/layout, `scrollWheel`, `onResizeRequest`, `apply(diameter:)`)
- Modify: `src/ZoomacIt/Overlay/BreakTimerWindow.swift` (init takes diameter)
- Modify: `src/ZoomacIt/Overlay/BreakTimerWindowController.swift` (init from saved diameter, resize transaction, persist)

No new unit tests (all pure math was tested in Tasks 1–2; this task is AppKit wiring, verified by the full suite compiling/passing + human GUI check).

**Interfaces:**
- Consumes: everything from Tasks 1–2.
- Produces (final API):
  - `BreakTimerView(state:diameter:)`, `var diameter: CGFloat { get }`, `func apply(diameter: CGFloat)`, `var onResizeRequest: ((_ scrollDeltaY: CGFloat, _ isPrecise: Bool) -> Void)?`
  - `BreakTimerWindow(at:diameter:)`

- [ ] **Step 1: Update `BreakTimerView`**

Apply these edits to `src/ZoomacIt/Overlay/BreakTimerView.swift`:

(a) Replace `private let ringLineWidth: CGFloat = 6` (line 15) and add state, directly after `var onDismiss`:
```swift
    /// Called when the user scrolls over the widget to resize it.
    /// Parameters are the raw `scrollingDeltaY` and `hasPreciseScrollingDeltas`.
    var onResizeRequest: ((_ scrollDeltaY: CGFloat, _ isPrecise: Bool) -> Void)?

    /// Current widget diameter; drawing and control layout scale with it.
    private(set) var diameter: CGFloat

    private var widgetScale: CGFloat { BreakTimerWidgetMetrics.scale(forDiameter: diameter) }

    private var ringLineWidth: CGFloat { 6 * widgetScale }
```

(b) Replace the initializer (lines 38–42):
```swift
    init(state: BreakTimerState, diameter: CGFloat) {
        self.state = state
        self.diameter = diameter
        super.init(frame: NSRect(
            origin: .zero,
            size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
        ))
        setUpControlButtons()
    }
```

(c) Replace `circleFrame` (lines 51–58):
```swift
    private var circleFrame: NSRect {
        NSRect(
            x: 0,
            y: BreakTimerWidgetMetrics.controlBarHeight(forDiameter: diameter),
            width: diameter,
            height: diameter
        )
    }
```

(d) In `drawTime`, replace the font-size line:
```swift
        let fontSize = diameter * 0.24
```

(e) Replace `setUpControlButtons` (lines 152–164) with an idempotent layout pass plus a one-time setup:
```swift
    private func setUpControlButtons() {
        for button in controlButtons {
            addSubview(button)
        }
        layoutControlButtons()
    }

    private func layoutControlButtons() {
        let buttonSize: CGFloat = 22 * widgetScale
        let spacing: CGFloat = 6 * widgetScale
        let totalWidth = buttonSize * 3 + spacing * 2
        var x = bounds.midX - totalWidth / 2
        let y = (BreakTimerWidgetMetrics.controlBarHeight(forDiameter: diameter) - buttonSize) / 2

        for button in controlButtons {
            button.frame = NSRect(x: x, y: y, width: buttonSize, height: buttonSize)
            x += buttonSize + spacing
        }
    }
```

(f) Add resize application + scroll forwarding, directly after `closeTapped` (before `// MARK: - Hover Tracking`):
```swift
    // MARK: - Resize

    /// Adopt a new diameter after the controller has resized the window. The window's
    /// `setFrame` already resized this view; this re-derives the scaled layout.
    func apply(diameter: CGFloat) {
        self.diameter = diameter
        layoutControlButtons()
        needsDisplay = true
    }

    override func scrollWheel(with event: NSEvent) {
        onResizeRequest?(event.scrollingDeltaY, event.hasPreciseScrollingDeltas)
    }
```

- [ ] **Step 2: Update `BreakTimerWindow`**

Replace the convenience init signature and contentRect (lines 10–16):
```swift
    convenience init(at origin: CGPoint, diameter: CGFloat) {
        self.init(
            contentRect: NSRect(
                origin: origin,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
            ),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
```

- [ ] **Step 3: Update `BreakTimerWindowController`**

(a) In `showTimer()`, replace the position/size block and window/view creation (lines 43–66 as updated by Task 1):
```swift
        let diameter = Settings.shared.breakTimerWidgetDiameter
        let savedPosition = Settings.shared.breakTimerWidgetPosition
        let origin: CGPoint
        if let saved = savedPosition {
            let widgetRect = CGRect(
                origin: saved,
                size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
            )
            // Keep the widget on whichever connected screen the user left it on;
            // fall back to the main screen only if that display is gone.
            let host = NSScreen.screens.first { $0.frame.intersects(widgetRect) } ?? screen
            origin = BreakTimerWidgetMetrics.clamped(origin: saved, in: host.frame, forDiameter: diameter)
        } else {
            origin = BreakTimerWidgetMetrics.defaultOrigin(in: screen.frame, forDiameter: diameter)
        }

        let window = BreakTimerWindow(at: origin, diameter: diameter)
        let view = BreakTimerView(state: state, diameter: diameter)
        view.onDismiss = { [weak self] in
            self?.dismiss()
        }
        view.onResizeRequest = { [weak self] deltaY, isPrecise in
            self?.resizeWidget(scrollDeltaY: deltaY, isPrecise: isPrecise)
        }
```

(b) Add the resize transaction in `// MARK: - Private`, before `startCountdown()`:
```swift
    /// One scroll tick over the widget: grow/shrink around the widget's center,
    /// keep it on its current screen, and persist the chosen size immediately
    /// (position, by contrast, is saved on dismiss).
    private func resizeWidget(scrollDeltaY: CGFloat, isPrecise: Bool) {
        guard let window = timerWindow, let view = timerView else { return }

        let current = view.diameter
        let target = BreakTimerWidgetMetrics.diameter(
            afterScrollDeltaY: scrollDeltaY, isPrecise: isPrecise, from: current)
        guard target != current else { return }

        var origin = BreakTimerWidgetMetrics.resizedOrigin(
            currentOrigin: window.frame.origin, fromDiameter: current, toDiameter: target)
        if let screen = window.screen ?? NSScreen.main {
            origin = BreakTimerWidgetMetrics.clamped(origin: origin, in: screen.frame, forDiameter: target)
        }

        window.setFrame(
            NSRect(origin: origin, size: BreakTimerWidgetMetrics.windowSize(forDiameter: target)),
            display: true
        )
        view.apply(diameter: target)
        Settings.shared.breakTimerWidgetDiameter = target
    }
```

- [ ] **Step 4: Run the full suite**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath build test DEVELOPMENT_TEAM=<local team id> 2>&1 | tail -5`
Expected: **TEST SUCCEEDED** (186 tests).

- [ ] **Step 5: Build the Debug app for GUI verification**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacIt -configuration Debug -derivedDataPath build build DEVELOPMENT_TEAM=<local team id> 2>&1 | tail -3`
Expected: **BUILD SUCCEEDED**. Then relaunch `build/Build/Products/Debug/ZoomacIt.app`.

- [ ] **Step 6: Commit**

```bash
git add src/ZoomacIt/Overlay/BreakTimerView.swift src/ZoomacIt/Overlay/BreakTimerWindow.swift src/ZoomacIt/Overlay/BreakTimerWindowController.swift
git commit -m "feat: scroll over the break timer widget to resize it (70-220pt, persisted)"
```

---

## Human GUI Verification (after Task 3)

- ⌃3 → scroll up over the widget grows it, scroll down shrinks it (trackpad and mouse wheel both feel reasonable)
- Ring thickness, countdown font, and hover −/×/+ buttons scale together
- Widget grows around its center; near a screen edge it stays fully on-screen
- ⌃3 off/on and app relaunch reopen at the last size
- Resize while expired: red pulse + count-up keep running
