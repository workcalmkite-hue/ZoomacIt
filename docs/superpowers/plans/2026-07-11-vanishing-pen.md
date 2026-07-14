# Vanishing Pen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Vanishing Pen" mode to ZoomacIt's Draw feature — strokes drawn while it's enabled fade out automatically over a configurable duration instead of persisting on screen.

**Architecture:** Reuse the existing-but-unused `Stroke` model to hold live (not-yet-baked) strokes in a `[Stroke]` array on `DrawingCanvasView`, driven by a ~30fps `Timer` that recomputes each stroke's fade alpha via a pure, unit-testable `VanishingPenFader` helper and redraws using the existing `ShapeRenderer`/`FreehandRenderer`/`HighlighterRenderer`. Toggled by the `V` key inside Draw mode (not a global hotkey). Confirmed strokes drawn in this mode are never baked into `finishedLayer`.

**Tech Stack:** Swift 6, AppKit (`draw(_:)`-based rendering, no CALayer), SwiftUI (Settings only), XCTest.

**Spec:** `docs/superpowers/specs/2026-07-11-vanishing-pen-design.md`

## Global Constraints

- macOS 15+ deployment target, Swift 6, no external dependencies (per project README).
- GPL-3.0 licensed project — this is a personal fork (`workcalmkite-hue/ZoomacIt`), origin remote points to the fork, `upstream` remote points to `07JP27/ZoomacIt`.
- Working directory for all tasks: `~/Desktop/ZoomacIt` (repo root), Swift sources under `src/ZoomacIt/`, tests under `src/ZoomacItTests/`.
- Build/test requires **Xcode** (not just Command Line Tools) — `make build` / `make test` / `make run` all shell out to `xcodebuild`. Per README, `src/project.yml` hardcodes the original maintainer's `DEVELOPMENT_TEAM`; before the first local build, edit `src/project.yml` to your own team ID (`security find-certificate -c "Apple Development" -p | openssl x509 -noout -subject | grep -o 'OU=[^,]*' | cut -d= -f2`), then run `make generate`. Do not commit that change.
- Toggle key `V` is confirmed free (existing Draw-mode local keys: R/G/B/O/Y/P color, E clear, W whiteboard, K blackboard, T text, S spotlight, Tab ellipse, Space, ⌘Z/⌘C/⌘S, arrow keys). No global hotkey (Ctrl+1..4) is touched — those remain Zoom/Draw/Break Timer/Live Zoom exactly as before.
- Vanishing Pen's on/off state is **not persisted** — resets to off every time Draw mode is entered, matching `DrawingState.activeTool`'s existing volatile pattern. Only the fade duration (`vanishingPenLifetime`) is a persisted `Settings` value.
- Fade model: single configurable duration (`vanishingPenLifetime`, seconds), linear fade from alpha 1.0 at creation to 0.0 at `lifetime` seconds — no separate "hold" delay.

---

### Task 1: `Stroke` model — add `id` and `createdAt`

**Files:**
- Modify: `src/ZoomacIt/Models/Stroke.swift`
- Test: `src/ZoomacItTests/StrokeTests.swift`

**Interfaces:**
- Produces: `Stroke.id: UUID`, `Stroke.createdAt: CFTimeInterval`, both with defaults so every existing call site (`Stroke()`, `Stroke(lineWidth:shapeType:)`, etc.) keeps compiling unchanged.

- [ ] **Step 1: Write the failing tests**

Add to the bottom of `src/ZoomacItTests/StrokeTests.swift` (add `import QuartzCore` alongside the existing `import XCTest` at the top of the file for `CACurrentMediaTime`):

```swift
import QuartzCore
```

```swift
    // MARK: - Vanishing Pen fields

    func testDefaultIdIsUnique() {
        let a = Stroke()
        let b = Stroke()
        XCTAssertNotEqual(a.id, b.id)
    }

    func testDefaultCreatedAtIsRecent() {
        let before = CACurrentMediaTime()
        let stroke = Stroke()
        let after = CACurrentMediaTime()
        XCTAssertGreaterThanOrEqual(stroke.createdAt, before)
        XCTAssertLessThanOrEqual(stroke.createdAt, after)
    }

    func testCustomCreatedAt() {
        let stroke = Stroke(createdAt: 100.0)
        XCTAssertEqual(stroke.createdAt, 100.0)
    }

    func testCustomId() {
        let id = UUID()
        let stroke = Stroke(id: id)
        XCTAssertEqual(stroke.id, id)
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `value of type 'Stroke' has no member 'id'` / `'createdAt'`

- [ ] **Step 3: Implement**

Replace the full contents of `src/ZoomacIt/Models/Stroke.swift` with:

```swift
import AppKit
import QuartzCore

/// The type of shape being drawn.
enum ShapeType: Sendable {
    case freehand
    case line
    case rectangle
    case ellipse
    case arrow
}

/// Represents a single confirmed drawing stroke.
struct Stroke {
    /// Unique identity — lets a live collection (e.g. Vanishing Pen's fading
    /// strokes) remove a specific stroke without relying on array order.
    let id: UUID

    /// Raw points collected during freehand drawing.
    var points: [CGPoint]

    /// Starting point (used for shape rendering).
    var startPoint: CGPoint

    /// Ending point (used for shape rendering).
    var endPoint: CGPoint

    /// The stroke color.
    var color: NSColor

    /// The line width in points.
    var lineWidth: CGFloat

    /// The shape type.
    var shapeType: ShapeType

    /// Whether the stroke uses highlighter (semi-transparent) mode.
    var isHighlighter: Bool

    /// Creation timestamp (`CACurrentMediaTime()`), used by Vanishing Pen
    /// to compute how far through its fade the stroke is.
    var createdAt: CFTimeInterval

    init(
        id: UUID = UUID(),
        points: [CGPoint] = [],
        startPoint: CGPoint = .zero,
        endPoint: CGPoint = .zero,
        color: NSColor = .red,
        lineWidth: CGFloat = 3.0,
        shapeType: ShapeType = .freehand,
        isHighlighter: Bool = false,
        createdAt: CFTimeInterval = CACurrentMediaTime()
    ) {
        self.id = id
        self.points = points
        self.startPoint = startPoint
        self.endPoint = endPoint
        self.color = color
        self.lineWidth = lineWidth
        self.shapeType = shapeType
        self.isHighlighter = isHighlighter
        self.createdAt = createdAt
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all `StrokeTests` including the 4 new ones, plus the pre-existing `testDefaultInitialization` / `testCustomInitialization` / `testPartialInitialization` which don't reference `id`/`createdAt` and must still pass unchanged)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Models/Stroke.swift src/ZoomacItTests/StrokeTests.swift
git commit -m "feat: add id and createdAt to Stroke for Vanishing Pen"
```

---

### Task 2: `VanishingPenFader` — pure fade-alpha calculation

**Files:**
- Create: `src/ZoomacIt/Draw/VanishingPenFader.swift`
- Test: `src/ZoomacItTests/VanishingPenFaderTests.swift`

**Interfaces:**
- Consumes: nothing (pure functions over primitives — no dependency on `Stroke` or AppKit, so it can't accidentally couple to view state).
- Produces: `VanishingPenFader.alpha(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> CGFloat`, `VanishingPenFader.isExpired(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> Bool`. Task 6/7/8 call these directly.

- [ ] **Step 1: Write the failing tests**

Create `src/ZoomacItTests/VanishingPenFaderTests.swift`:

```swift
import XCTest
@testable import ZoomacIt

final class VanishingPenFaderTests: XCTestCase {

    func testFullyOpaqueAtCreation() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 100.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 1.0, accuracy: 0.001)
    }

    func testHalfwayThroughLifetime() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 101.5, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.5, accuracy: 0.001)
    }

    func testFullyFadedAtLifetimeEnd() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 103.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.0, accuracy: 0.001)
    }

    func testClampedAfterLifetimeEnd() {
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 200.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 0.0, accuracy: 0.001)
    }

    func testClampedBeforeCreation() {
        // now < createdAt shouldn't happen in practice, but must never yield >1.
        let alpha = VanishingPenFader.alpha(createdAt: 100.0, now: 99.0, lifetime: 3.0)
        XCTAssertEqual(alpha, 1.0, accuracy: 0.001)
    }

    func testIsExpiredFalseBeforeLifetimeEnd() {
        XCTAssertFalse(VanishingPenFader.isExpired(createdAt: 100.0, now: 102.9, lifetime: 3.0))
    }

    func testIsExpiredTrueAtLifetimeEnd() {
        XCTAssertTrue(VanishingPenFader.isExpired(createdAt: 100.0, now: 103.0, lifetime: 3.0))
    }

    func testZeroLifetimeIsImmediatelyExpired() {
        XCTAssertEqual(VanishingPenFader.alpha(createdAt: 100.0, now: 100.0, lifetime: 0.0), 0.0, accuracy: 0.001)
        XCTAssertTrue(VanishingPenFader.isExpired(createdAt: 100.0, now: 100.0, lifetime: 0.0))
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `cannot find 'VanishingPenFader' in scope`

- [ ] **Step 3: Implement**

Create `src/ZoomacIt/Draw/VanishingPenFader.swift`:

```swift
import CoreGraphics

/// Pure fade-progress math for Vanishing Pen mode, kept free of AppKit/view
/// state so it's unit-testable without a running NSView or Timer.
enum VanishingPenFader {

    /// The stroke's current opacity multiplier: 1.0 the instant it's drawn,
    /// linearly decreasing to 0.0 by the time `lifetime` seconds have passed.
    /// Clamped to [0, 1] — callers never see negative or >1 values, including
    /// when `now` is (unexpectedly) before `createdAt`.
    static func alpha(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> CGFloat {
        guard lifetime > 0 else { return 0 }
        let age = now - createdAt
        let remaining = 1.0 - (age / lifetime)
        return CGFloat(max(0.0, min(1.0, remaining)))
    }

    /// Whether the stroke has fully faded and should be dropped from the
    /// live collection.
    static func isExpired(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> Bool {
        alpha(createdAt: createdAt, now: now, lifetime: lifetime) <= 0
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all 8 `VanishingPenFaderTests`)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Draw/VanishingPenFader.swift src/ZoomacItTests/VanishingPenFaderTests.swift
git commit -m "feat: add VanishingPenFader pure fade-alpha calculation"
```

---

### Task 3: `DrawingState` — add `isVanishingPenEnabled` toggle flag

**Files:**
- Modify: `src/ZoomacIt/Models/DrawingState.swift`
- Test: `src/ZoomacItTests/DrawingStateTests.swift`

**Interfaces:**
- Produces: `DrawingState.isVanishingPenEnabled: Bool` (default `false`). Task 6 reads/toggles this from `DrawingCanvasView.keyDown` and `mouseUp`.

- [ ] **Step 1: Write the failing tests**

Add to `src/ZoomacItTests/DrawingStateTests.swift`, after `testActiveToolTransitions()`:

```swift

    // MARK: - Vanishing Pen

    func testDefaultVanishingPenState() {
        let state = DrawingState()
        XCTAssertFalse(state.isVanishingPenEnabled)
    }

    func testVanishingPenToggle() {
        let state = DrawingState()
        state.isVanishingPenEnabled.toggle()
        XCTAssertTrue(state.isVanishingPenEnabled)
        state.isVanishingPenEnabled.toggle()
        XCTAssertFalse(state.isVanishingPenEnabled)
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `value of type 'DrawingState' has no member 'isVanishingPenEnabled'`

- [ ] **Step 3: Implement**

In `src/ZoomacIt/Models/DrawingState.swift`, add this new section right after the `// MARK: - Spotlight` block's `decreaseSpotlightDarkness()` method (i.e. right before `// MARK: - Derived`):

```swift

    // MARK: - Vanishing Pen

    /// Whether strokes drawn from now on should fade out automatically
    /// instead of persisting on `finishedLayer`. Resets to `false` every
    /// time a new `DrawingState` is created (i.e. every time Draw mode is
    /// entered) — not persisted, matching `activeTool`'s volatile pattern.
    var isVanishingPenEnabled: Bool = false
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all `DrawingStateTests` including the 2 new ones)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Models/DrawingState.swift src/ZoomacItTests/DrawingStateTests.swift
git commit -m "feat: add isVanishingPenEnabled flag to DrawingState"
```

---

### Task 4: `Settings` — add `vanishingPenLifetime`

**Files:**
- Modify: `src/ZoomacIt/Models/Settings.swift`
- Test: `src/ZoomacItTests/SettingsTests.swift`

**Interfaces:**
- Produces: `Settings.shared.vanishingPenLifetime: TimeInterval` (default `3.0`), key registered in `registerDefaults()` and `resetToDefaults()`. Task 5 binds a slider to this via `@AppStorage(Settings.Keys.vanishingPenLifetime)`; Task 6/7/8 read `Settings.shared.vanishingPenLifetime`.

- [ ] **Step 1: Write the failing tests**

Add to `src/ZoomacItTests/SettingsTests.swift`, in the `// MARK: - Default Values` section, right after `testDefaultSpotlightDarkness()`:

```swift

    func testDefaultVanishingPenLifetime() {
        XCTAssertEqual(Settings.shared.vanishingPenLifetime, 3.0, accuracy: 0.001)
    }
```

Add to the `// MARK: - Round-trip` section, right after `testSpotlightDarknessRoundTrip()` is fine too — but that one already lives in the Default Values section right after its own default test, so mirror that placement:

```swift

    func testVanishingPenLifetimeRoundTrip() {
        Settings.shared.vanishingPenLifetime = 5.5
        XCTAssertEqual(Settings.shared.vanishingPenLifetime, 5.5, accuracy: 0.001)
    }
```

Add to `testResetToDefaults()` — extend the existing test body (don't create a new test) so reset coverage stays centralized as the file already does it:

```swift
    func testResetToDefaults() {
        // Change some values
        Settings.shared.defaultPenColor = .green
        Settings.shared.defaultPenWidth = 20.0
        Settings.shared.breakTimerDefaultDuration = 120
        Settings.shared.vanishingPenLifetime = 7.0

        // Reset
        Settings.shared.resetToDefaults()

        // Verify defaults restored
        XCTAssertEqual(Settings.shared.defaultPenColor, .red)
        XCTAssertEqual(Settings.shared.defaultPenWidth, 3.0)
        XCTAssertEqual(Settings.shared.breakTimerDefaultDuration, 600)
        XCTAssertEqual(Settings.shared.vanishingPenLifetime, 3.0, accuracy: 0.001)
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `make test`
Expected: FAIL — `value of type 'Settings' has no member 'vanishingPenLifetime'`

- [ ] **Step 3: Implement**

In `src/ZoomacIt/Models/Settings.swift`:

1. In `enum Keys`, add right after `static let spotlightDarkness = "drawSpotlightDarkness"`:

```swift
        static let vanishingPenLifetime = "drawVanishingPenLifetime"
```

2. In `registerDefaults()`, add right after `Keys.spotlightDarkness: 0.6,`:

```swift
            Keys.vanishingPenLifetime: 3.0,
```

3. Add the computed property right after `spotlightDarkness` (in the `// MARK: - Draw` section):

```swift

    var vanishingPenLifetime: TimeInterval {
        get { defaults.double(forKey: Keys.vanishingPenLifetime) }
        set { defaults.set(newValue, forKey: Keys.vanishingPenLifetime) }
    }
```

4. In `resetToDefaults()`, add `Keys.vanishingPenLifetime,` to the `allKeys` array, right after `Keys.spotlightDarkness,`:

```swift
            Keys.defaultPenColor, Keys.defaultPenWidth,
            Keys.highlighterOpacity, Keys.highlighterWidthMultiplier,
            Keys.spotlightDarkness, Keys.vanishingPenLifetime,
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `make test`
Expected: PASS (all `SettingsTests`, including the updated `testResetToDefaults` and the 2 new tests)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Models/Settings.swift src/ZoomacItTests/SettingsTests.swift
git commit -m "feat: add vanishingPenLifetime setting"
```

---

### Task 5: `DrawTab` — fade duration slider

**Files:**
- Modify: `src/ZoomacIt/Settings/DrawTab.swift`

**Interfaces:**
- Consumes: `Settings.Keys.vanishingPenLifetime` (Task 4).
- Produces: nothing consumed by later tasks — this is a leaf UI change.

No automated test exists for any `Settings/*Tab.swift` file in this codebase (they're plain SwiftUI forms bound via `@AppStorage`), so this task is verified manually in Task 9 rather than by XCTest.

- [ ] **Step 1: Add the `@AppStorage` binding**

In `src/ZoomacIt/Settings/DrawTab.swift`, add this property right after `@AppStorage(Settings.Keys.defaultPenWidth) private var penWidth: Double = 3.0`:

```swift
    @AppStorage(Settings.Keys.vanishingPenLifetime) private var vanishingPenLifetime: Double = 3.0
```

- [ ] **Step 2: Add the section to the form body**

In the same file, add a new `Section("Vanishing Pen")` right after the closing `}` of `Section("Pen") { ... }` and before `Section("Highlighter") { ... }`:

```swift

            Section("Vanishing Pen") {
                HStack {
                    Text("Fade Duration")
                    Slider(value: $vanishingPenLifetime, in: 1.0...8.0, step: 0.5)
                    Text(String(format: "%.1fs", vanishingPenLifetime))
                        .frame(width: 40, alignment: .trailing)
                        .monospacedDigit()
                }
            }
```

- [ ] **Step 3: Build to verify it compiles**

Run: `make build`
Expected: build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git add src/ZoomacIt/Settings/DrawTab.swift
git commit -m "feat: add Vanishing Pen fade duration slider to Draw settings tab"
```

---

### Task 6: `DrawingCanvasView` — toggle key, live stroke storage, timer lifecycle

**Files:**
- Modify: `src/ZoomacIt/Draw/DrawingCanvasView.swift`

**Interfaces:**
- Consumes: `DrawingState.isVanishingPenEnabled` (Task 3), `Stroke` (Task 1), `VanishingPenFader.isExpired` (Task 2), `Settings.shared.vanishingPenLifetime` (Task 4).
- Produces: `private var vanishingStrokes: [Stroke]` and `private func startVanishingTimerIfNeeded()` — Task 7 reads `vanishingStrokes` in `draw(_:)` and in `renderFinalImage()`.

This task only wires state/data plumbing (no visible rendering yet — that's Task 7). There's no existing unit-test file for `DrawingCanvasView` anywhere in the suite (it's a full `NSView` driving mouse/keyboard/screen-capture — the codebase's existing convention is to keep pure logic testable elsewhere, e.g. `VanishingPenFader`, and verify the view manually). Verify this task by building; full interactive verification happens in Task 9.

- [ ] **Step 1: Add `import QuartzCore` and the new stored properties**

In `src/ZoomacIt/Draw/DrawingCanvasView.swift`, change the import block at the top from:

```swift
import AppKit
import CoreGraphics
import ScreenCaptureKit
```

to:

```swift
import AppKit
import CoreGraphics
import QuartzCore
import ScreenCaptureKit
```

Then add these two properties right after `private var freehandPoints: [CGPoint] = []` / `private var isDragging: Bool = false` (in the `// MARK: - Drag State` block):

```swift

    // MARK: - Vanishing Pen

    /// Strokes drawn while `drawingState.isVanishingPenEnabled` is true.
    /// Never baked into `finishedLayer` — rendered live in `draw(_:)` with
    /// a per-stroke fade alpha, and dropped once fully faded.
    private var vanishingStrokes: [Stroke] = []

    /// Drives the fade animation while `vanishingStrokes` is non-empty.
    /// Started lazily on the first vanishing stroke, stopped once the
    /// array empties out (so an idle canvas burns no CPU).
    private var vanishingTimer: Timer?
```

- [ ] **Step 2: Add the `V` key toggle**

In `keyDown(with:)`, add this case right after the `case "T": enterTextMode()` case and before `// Tab key for ellipse (track as key, not modifier)`:

```swift

        // Toggle Vanishing Pen mode
        case "V":
            drawingState.isVanishingPenEnabled.toggle()
            setNeedsDisplay(bounds)
```

- [ ] **Step 3: Branch `mouseUp` on Vanishing Pen mode**

In `mouseUp(with:)`, replace this block:

```swift
        // Push current state for undo
        strokeManager.pushUndoSnapshot(
            finishedLayer,
            backgroundMode: drawingState.backgroundMode,
            spotlightRect: drawingState.spotlightRect
        )

        // Composite the completed stroke onto finishedLayer
        finishedLayer = compositeStrokeOntoFinished(
            shapeType: shapeType,
            endPoint: currentPoint
        )

        // Clear transient layers
        previewLayer = nil
        activeFreehand = nil
        freehandPoints.removeAll()

        setNeedsDisplay(bounds)
```

with:

```swift
        if drawingState.isVanishingPenEnabled {
            vanishingStrokes.append(makeVanishingStroke(shapeType: shapeType, endPoint: currentPoint))
            startVanishingTimerIfNeeded()
        } else {
            // Push current state for undo
            strokeManager.pushUndoSnapshot(
                finishedLayer,
                backgroundMode: drawingState.backgroundMode,
                spotlightRect: drawingState.spotlightRect
            )

            // Composite the completed stroke onto finishedLayer
            finishedLayer = compositeStrokeOntoFinished(
                shapeType: shapeType,
                endPoint: currentPoint
            )
        }

        // Clear transient layers
        previewLayer = nil
        activeFreehand = nil
        freehandPoints.removeAll()

        setNeedsDisplay(bounds)
```

- [ ] **Step 4: Add `makeVanishingStroke` and the timer lifecycle methods**

Add these new private methods right after `compositeStrokeOntoFinished(shapeType:endPoint:)` (still within the `// MARK: - Compositing` section):

```swift

    /// Captures the just-finished stroke's geometry/style into a `Stroke`
    /// for the Vanishing Pen live collection, instead of rasterizing it.
    private func makeVanishingStroke(shapeType: ShapeType, endPoint: CGPoint) -> Stroke {
        Stroke(
            points: freehandPoints,
            startPoint: dragOrigin,
            endPoint: endPoint,
            color: drawingState.currentNSColor,
            lineWidth: drawingState.penWidth,
            shapeType: shapeType,
            isHighlighter: drawingState.isHighlighterMode
        )
    }

    // MARK: - Vanishing Pen Timer

    private func startVanishingTimerIfNeeded() {
        guard vanishingTimer == nil else { return }
        let timer = Timer.scheduledTimer(withTimeInterval: 1.0 / 30.0, repeats: true) { [weak self] _ in
            self?.tickVanishingStrokes()
        }
        // .common lets the timer keep firing while a mouse drag is being
        // tracked (the default run loop mode pauses during tracking loops).
        RunLoop.main.add(timer, forMode: .common)
        vanishingTimer = timer
    }

    private func tickVanishingStrokes() {
        let now = CACurrentMediaTime()
        let lifetime = Settings.shared.vanishingPenLifetime
        vanishingStrokes.removeAll {
            VanishingPenFader.isExpired(createdAt: $0.createdAt, now: now, lifetime: lifetime)
        }
        setNeedsDisplay(bounds)
        if vanishingStrokes.isEmpty {
            vanishingTimer?.invalidate()
            vanishingTimer = nil
        }
    }
```

- [ ] **Step 5: Clear vanishing strokes on "Clear All" (`E` key)**

Replace this block in `keyDown(with:)`:

```swift
        // Clear all
        case "E":
            strokeManager.pushUndoSnapshot(
                finishedLayer,
                backgroundMode: drawingState.backgroundMode,
                spotlightRect: drawingState.spotlightRect
            )
            finishedLayer = nil
            setNeedsDisplay(bounds)
```

with:

```swift
        // Clear all
        case "E":
            strokeManager.pushUndoSnapshot(
                finishedLayer,
                backgroundMode: drawingState.backgroundMode,
                spotlightRect: drawingState.spotlightRect
            )
            finishedLayer = nil
            vanishingStrokes.removeAll()
            vanishingTimer?.invalidate()
            vanishingTimer = nil
            setNeedsDisplay(bounds)
```

- [ ] **Step 6: Build to verify it compiles**

Run: `make build`
Expected: build succeeds with no errors. (`vanishingStrokes` is written to but not yet read by `draw(_:)` — that's Task 7 — so expect an "unused" warning at most, not an error.)

- [ ] **Step 7: Commit**

```bash
git add src/ZoomacIt/Draw/DrawingCanvasView.swift
git commit -m "feat: wire Vanishing Pen toggle, live stroke storage, and fade timer"
```

---

### Task 7: `DrawingCanvasView` — render fading strokes + HUD indicator

**Files:**
- Modify: `src/ZoomacIt/Draw/DrawingCanvasView.swift`

**Interfaces:**
- Consumes: `vanishingStrokes`, `VanishingPenFader.alpha` (Task 2), `ShapeRenderer`/`FreehandRenderer`/`HighlighterRenderer` (pre-existing), `drawingState.isVanishingPenEnabled` (Task 3).
- Produces: `private func drawVanishingStroke(_:now:lifetime:in:)` — Task 8 reuses this exact method for export.

- [ ] **Step 1: Insert the vanishing-strokes render pass in `draw(_:)`**

In `draw(_:)`, insert this new step between step 2 (`finishedLayer`) and step 3 (`previewLayer`) — i.e. right after:

```swift
        // 2. Draw finishedLayer (all confirmed strokes)
        if let finished = finishedLayer {
            context.draw(finished, in: bounds)
        }
```

add:

```swift

        // 2.5. Draw vanishingStrokes (Vanishing Pen mode — fading confirmed strokes)
        if !vanishingStrokes.isEmpty {
            let now = CACurrentMediaTime()
            let lifetime = Settings.shared.vanishingPenLifetime
            for stroke in vanishingStrokes {
                drawVanishingStroke(stroke, now: now, lifetime: lifetime, in: context)
            }
        }
```

- [ ] **Step 2: Add the Vanishing Pen HUD indicator**

At the very end of `draw(_:)`, right after the closing brace of the "4. Draw activeFreehand" block (i.e. after `NSGraphicsContext.current?.cgContext.setBlendMode(.normal)` that follows the `activeFreehand` stroke), add:

```swift

        // 5. Vanishing Pen mode indicator (HUD) — only while the mode is on
        if drawingState.isVanishingPenEnabled {
            drawVanishingPenIndicator(in: context)
        }
```

- [ ] **Step 3: Implement `drawVanishingStroke` and `drawVanishingPenIndicator`**

Add these new private methods right after `drawSpotlightMask(rect:in:)`:

```swift

    /// Renders a single Vanishing Pen stroke at its current fade alpha,
    /// reusing the same path builders as the finished-layer compositing path.
    private func drawVanishingStroke(
        _ stroke: Stroke,
        now: CFTimeInterval,
        lifetime: TimeInterval,
        in context: CGContext
    ) {
        let fadeAlpha = VanishingPenFader.alpha(createdAt: stroke.createdAt, now: now, lifetime: lifetime)
        guard fadeAlpha > 0 else { return }

        let path: NSBezierPath
        switch stroke.shapeType {
        case .freehand:
            path = FreehandRenderer.smoothedPath(from: stroke.points)
        case .line:
            path = ShapeRenderer.linePath(from: stroke.startPoint, to: stroke.endPoint)
        case .rectangle:
            path = ShapeRenderer.rectanglePath(from: stroke.startPoint, to: stroke.endPoint)
        case .ellipse:
            path = ShapeRenderer.ellipsePath(from: stroke.startPoint, to: stroke.endPoint)
        case .arrow:
            path = ShapeRenderer.arrowPath(from: stroke.startPoint, to: stroke.endPoint, penWidth: stroke.lineWidth)
        }

        context.saveGState()
        if stroke.isHighlighter {
            HighlighterRenderer.applyHighlighterStyle(to: path, penWidth: stroke.lineWidth)
            let blendMode: CGBlendMode = (backgroundImage != nil) ? .multiply : .normal
            context.setBlendMode(blendMode)
        } else {
            path.lineWidth = stroke.lineWidth
            path.lineCapStyle = .round
            path.lineJoinStyle = .round
        }
        stroke.color.withAlphaComponent(stroke.color.alphaComponent * fadeAlpha).setStroke()
        path.stroke()
        context.restoreGState()
    }

    /// Small bottom-right badge shown while Vanishing Pen mode is armed, so
    /// it's obvious at a glance during a live lecture that strokes will fade.
    private func drawVanishingPenIndicator(in context: CGContext) {
        let text = "Vanishing Pen"
        let attrs: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13, weight: .semibold),
            .foregroundColor: NSColor.white
        ]
        let attributedString = NSAttributedString(string: text, attributes: attrs)
        let textSize = attributedString.size()

        let padding: CGFloat = 8
        let margin: CGFloat = 16
        let badgeRect = CGRect(
            x: bounds.maxX - textSize.width - padding * 2 - margin,
            y: margin,
            width: textSize.width + padding * 2,
            height: textSize.height + padding
        )

        context.saveGState()
        NSColor.black.withAlphaComponent(0.6).setFill()
        NSBezierPath(roundedRect: badgeRect, xRadius: 6, yRadius: 6).fill()
        context.restoreGState()

        let textOrigin = CGPoint(x: badgeRect.minX + padding, y: badgeRect.minY + padding / 2)
        attributedString.draw(at: textOrigin)
    }
```

- [ ] **Step 4: Build to verify it compiles**

Run: `make build`
Expected: build succeeds with no errors

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Draw/DrawingCanvasView.swift
git commit -m "feat: render fading Vanishing Pen strokes and HUD indicator"
```

---

### Task 8: `DrawingCanvasView` — include vanishing strokes in copy/save export

**Files:**
- Modify: `src/ZoomacIt/Draw/DrawingCanvasView.swift`

**Interfaces:**
- Consumes: `drawVanishingStroke(_:now:lifetime:in:)` (Task 7), `vanishingStrokes` (Task 6).

- [ ] **Step 1: Add the vanishing-strokes pass to `renderFinalImage()`**

In `renderFinalImage()`, insert this right after:

```swift
        // Finished strokes
        if let finished = finishedLayer {
            context.draw(finished, in: CGRect(origin: .zero, size: size))
        }
```

and before `return context.makeImage()`:

```swift

        // Vanishing Pen strokes — export at their current on-screen fade
        // state (WYSIWYG), same alpha math as the live draw(_:) pass.
        if !vanishingStrokes.isEmpty {
            let now = CACurrentMediaTime()
            let lifetime = Settings.shared.vanishingPenLifetime
            for stroke in vanishingStrokes {
                drawVanishingStroke(stroke, now: now, lifetime: lifetime, in: context)
            }
        }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `make build`
Expected: build succeeds with no errors

- [ ] **Step 3: Commit**

```bash
git add src/ZoomacIt/Draw/DrawingCanvasView.swift
git commit -m "feat: include Vanishing Pen strokes in copy/save export"
```

---

### Task 9: Manual end-to-end verification

**Files:** none (interactive verification only)

`DrawingCanvasView` drives real mouse/keyboard/screen-capture events, so it has no automated coverage in this codebase (confirmed: no `DrawingCanvasViewTests.swift` exists — the project's own convention is to keep pure logic like `VanishingPenFader` unit-tested and verify the view by running the app). This task is the check for everything wired in Tasks 6-8.

**Prerequisite (one-time, if not already done as part of Task 5's `make build`):** edit `src/project.yml`'s two `DEVELOPMENT_TEAM` values to your own team ID, then `make generate`.

- [ ] **Step 1: Launch the app**

Run: `make run`
Expected: ZoomacIt appears in the menu bar.

- [ ] **Step 2: Enter Draw mode and confirm the toggle + HUD**

- Press ⌃2 (default Draw hotkey) to enter Draw mode.
- Press `V`.
- Expected: a small "Vanishing Pen" badge appears in the bottom-right corner of the screen.
- Press `V` again.
- Expected: the badge disappears.
- Press `V` a third time to leave it enabled for the next steps.

- [ ] **Step 3: Confirm freehand strokes fade**

- Draw a freehand squiggle.
- Expected: over ~3 seconds (default `vanishingPenLifetime`), the stroke smoothly fades from fully opaque to fully invisible, then is gone.

- [ ] **Step 4: Confirm shape strokes fade**

- Hold Shift and drag to draw a line; release Shift.
- Hold Control and drag to draw a rectangle; release Control.
- Hold Tab and drag to draw an ellipse; release Tab.
- Hold Shift+Control and drag to draw an arrow.
- Expected: each shape fades out the same way as the freehand stroke.

- [ ] **Step 5: Confirm highlighter strokes fade**

- Hold Shift and press a color key (e.g. Shift+Y) to arm highlighter mode, then drag to draw.
- Expected: the semi-transparent highlighter stroke fades out over the same duration, ending fully invisible (not stuck at its highlighter opacity).

- [ ] **Step 6: Confirm the fade duration setting takes effect**

- Open the app's Settings window → Draw tab → "Vanishing Pen" section → drag "Fade Duration" to e.g. 1.0s.
- Return to Draw mode (still in Vanishing Pen mode), draw a new stroke.
- Expected: it fades out in about 1 second instead of 3.
- Reset the slider back to 3.0s when done (or leave as preferred).

- [ ] **Step 7: Confirm Vanishing Pen strokes are excluded from Undo**

- With Vanishing Pen mode on, draw a stroke, then immediately press ⌘Z.
- Expected: nothing visibly changes (there's no non-vanishing stroke to undo) — the vanishing stroke keeps fading on its own timeline, unaffected by ⌘Z.

- [ ] **Step 8: Confirm "Clear All" clears vanishing strokes too**

- With Vanishing Pen mode on, draw 2-3 strokes so they're still visible.
- Press `E`.
- Expected: all of them disappear immediately, along with anything on `finishedLayer`.

- [ ] **Step 9: Confirm normal (non-vanishing) Draw mode is untouched**

- Press `V` to turn Vanishing Pen off.
- Draw a freehand stroke.
- Expected: it persists on screen indefinitely (no fade), exactly like before this feature existed.

- [ ] **Step 10: Confirm export includes the current fade state**

- Turn Vanishing Pen back on, draw a stroke, and within ~1 second press ⌘C.
- Paste into Preview or Notes.
- Expected: the pasted image shows the stroke at roughly the transparency it had on screen at the moment of copying (not fully opaque, not missing).

- [ ] **Step 11: Run the full automated suite once more**

Run: `make test`
Expected: all tests pass (this re-confirms Tasks 1-4's tests still pass after all the `DrawingCanvasView` changes in Tasks 6-8, which don't touch any tested code path).

- [ ] **Step 12: Push the branch**

```bash
git push origin main
```

(No PR needed against `upstream` — this fork is for personal use. `origin` already points to `workcalmkite-hue/ZoomacIt`.)
