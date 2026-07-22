# Mouse Spotlight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a native "Mouse Spotlight" feature to ZoomacIt — a ⌘1-toggled, click-through, cursor-following screen dimmer with a circular hole and a click-ripple effect — so the user can delete the third-party Mouseposé app (whose unregistered build shows an unwanted watermark).

**Architecture:** A new pure-math file (`MouseSpotlightGeometry`) drives radius clamping, scroll-to-resize math, and the even-odd circular "hole" path, mirroring this codebase's existing `BreakTimerWidgetMetrics` pattern. A new layer-backed `NSView` (`MouseSpotlightOverlayView`) renders the dim layer + hole mask + click-ripple animations. A new controller (`MouseSpotlightWindowController`) owns a *click-through* variant of the existing `OverlayWindow` (`ignoresMouseEvents = true`, never key/main — unlike Draw/Zoom, which grab input), a 60fps cursor-follow timer, and global mouse-down/scroll monitors (no extra permission needed — only global *keyboard* monitors require Accessibility trust). `HotkeyManager` gains a fixed ⌘1 registration, `AppDelegate` wires it to toggle the controller, and `StatusBarController` gets a matching menu item.

**Tech Stack:** Swift 6, AppKit (`NSView`, `CAShapeLayer`, `CAGradientLayer`-free radial mask via even-odd path), Carbon `RegisterEventHotKey` (via existing `HotkeyManager`), XCTest, classic-format Xcode project (no `xcodegen` on this machine).

## Global Constraints

- Never add a team ID / `PRODUCT_NAME` override to `src/ZoomacIt.xcodeproj/project.pbxproj` or `src/project.yml`, and never commit personal signing identity values. This machine's local signing identity (team `4S4L9F965Y`) is injected via CLI flag only — see build/test commands below.
- `xcodegen` is not installed on this machine — do not run `make generate`. Every new `.swift` file must be hand-registered in `project.pbxproj`: a `PBXBuildFile` entry, a `PBXFileReference` entry, a group-children entry (under the `Overlay` group for source files, the `ZoomacItTests` group for test files), and a `Sources` build-phase entry (under the `ZoomacIt` target for source files, `ZoomacItTests` for test files) — each with a fresh, unique 24-hex-char UUID. Follow the existing `BreakTimerWidgetMetrics.swift` / `BreakTimerWidgetMetricsTests.swift` entries as the exact template (four locations each). Generate fresh IDs with `uuidgen | tr -d '-' | cut -c1-24` (run once per ID needed). Commit the registration edits together with the new file in the same commit.
- Build: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacIt -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y build` (plain `make build` fails on this machine — no default team is configured in the project file).
- Test: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test` (plain `make test` fails the same way).
- Run: `open "$PWD/build/Build/Products/Debug/ZoomacIt.app"` after a successful build.
- The feature is named **Mouse Spotlight** (never bare "Spotlight") in every identifier, Settings key, log message, and menu item — Draw mode (⌃2) already has an unrelated `.spotlight` tool (`DrawingState.spotlightRect`, `Settings.spotlightDarkness`) and reusing the bare name would collide/confuse.
- All new identifiers, comments, and log messages are in English, matching the rest of the codebase.
- Run the full test suite (the Test command above) after every task and treat any *new* failure as a blocker — fix before moving on.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ZoomacIt/Overlay/MouseSpotlightGeometry.swift` | *Create.* Pure math: radius clamping, scroll-to-radius, even-odd hole `CGPath`. No AppKit. |
| `src/ZoomacItTests/MouseSpotlightGeometryTests.swift` | *Create.* Tests for the above. |
| `src/ZoomacIt/Models/Settings.swift` | *Modify.* Add persisted `mouseSpotlightRadius: CGFloat`. |
| `src/ZoomacItTests/SettingsTests.swift` | *Modify.* Round-trip/clamp/reset tests for the above. |
| `src/ZoomacIt/Overlay/MouseSpotlightOverlayView.swift` | *Create.* Layer-backed click-through view: dim layer + even-odd hole mask + click-ripple animation. |
| `src/ZoomacIt/Overlay/MouseSpotlightWindowController.swift` | *Create.* Click-through overlay window lifecycle, cursor-follow timer, global click/scroll monitors, multi-screen hand-off. |
| `src/ZoomacIt/Core/HotkeyManager.swift` | *Modify.* Register the fixed ⌘1 hotkey; dispatch `onMouseSpotlightHotkey`. |
| `src/ZoomacIt/App/AppDelegate.swift` | *Modify.* Wire the hotkey to toggle `MouseSpotlightWindowController`. |
| `src/ZoomacIt/App/StatusBarController.swift` | *Modify.* Add the "Mouse Spotlight" menu item. |

---

### Task 1: `MouseSpotlightGeometry` — pure math

**Files:**
- Create: `src/ZoomacIt/Overlay/MouseSpotlightGeometry.swift`
- Test: `src/ZoomacItTests/MouseSpotlightGeometryTests.swift`

**Interfaces:**
- Consumes: nothing.
- Produces: `MouseSpotlightGeometry.minRadius/maxRadius/defaultRadius: CGFloat`, `.clampedRadius(_: CGFloat) -> CGFloat`, `.radius(afterScrollDeltaY:isPrecise:from:) -> CGFloat`, `.holePath(center:radius:in:) -> CGPath`. Task 2 (Settings) reads `defaultRadius`/`clampedRadius`. Task 3 (overlay view) reads `holePath`. Task 4 (controller) reads `radius(afterScrollDeltaY:isPrecise:from:)`.

- [ ] **Step 1: Write the failing tests**

Create `src/ZoomacItTests/MouseSpotlightGeometryTests.swift`:

```swift
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
```

- [ ] **Step 2: Register the test file in `project.pbxproj`**

Generate two fresh IDs: `uuidgen | tr -d '-' | cut -c1-24` (run twice — one for the `PBXFileReference`, one for the `PBXBuildFile`). Using the Edit tool, add one line in each of these four spots, matching the exact style of the neighboring `BreakTimerWidgetMetricsTests.swift` entries:
1. `PBXBuildFile` section: `<BUILDFILE_ID> /* MouseSpotlightGeometryTests.swift in Sources */ = {isa = PBXBuildFile; fileRef = <FILEREF_ID> /* MouseSpotlightGeometryTests.swift */; };` (insert right after the `BreakTimerWidgetMetricsTests.swift in Sources` `PBXBuildFile` line).
2. `PBXFileReference` section: `<FILEREF_ID> /* MouseSpotlightGeometryTests.swift */ = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = MouseSpotlightGeometryTests.swift; sourceTree = "<group>"; };` (insert right after the `BreakTimerWidgetMetricsTests.swift` `PBXFileReference` line).
3. `ZoomacItTests` group's children list: `<FILEREF_ID> /* MouseSpotlightGeometryTests.swift */,` (insert right after the `BreakTimerWidgetMetricsTests.swift` entry in that list).
4. `ZoomacItTests` target's `Sources` build phase list: `<BUILDFILE_ID> /* MouseSpotlightGeometryTests.swift in Sources */,` (insert right after the `BreakTimerWidgetMetricsTests.swift in Sources` entry in that list).

- [ ] **Step 3: Run tests to verify they fail**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: FAIL — `cannot find 'MouseSpotlightGeometry' in scope`

- [ ] **Step 4: Implement**

Create `src/ZoomacIt/Overlay/MouseSpotlightGeometry.swift`:

```swift
import CoreGraphics

/// Pure geometry for Mouse Spotlight: the cursor-following, click-through
/// screen dimmer. No AppKit dependency, so it's testable without a real
/// screen or window — `MouseSpotlightOverlayView` reads `holePath(...)` on
/// every cursor tick, and `MouseSpotlightWindowController` uses the scroll
/// math to resize the hole live (same shape as `BreakTimerWidgetMetrics`).
enum MouseSpotlightGeometry {

    /// Radius the spotlight ships with.
    static let defaultRadius: CGFloat = 150

    /// Smallest radius the user can scroll down to.
    static let minRadius: CGFloat = 60

    /// Largest radius the user can scroll up to.
    static let maxRadius: CGFloat = 400

    static func clampedRadius(_ radius: CGFloat) -> CGFloat {
        min(max(radius, minRadius), maxRadius)
    }

    /// New radius after a scroll gesture. Precise deltas (trackpads) arrive in much
    /// larger quantities per gesture than mouse-wheel line deltas, so they use a finer
    /// points-per-unit ratio to make both input devices feel similar (same math as
    /// `BreakTimerWidgetMetrics.diameter(afterScrollDeltaY:...)`).
    static func radius(afterScrollDeltaY deltaY: CGFloat, isPrecise: Bool, from current: CGFloat) -> CGFloat {
        let pointsPerUnit: CGFloat = isPrecise ? 0.5 : 5
        return clampedRadius(current + deltaY * pointsPerUnit)
    }

    /// A path covering all of `rect` except a circle of `radius` around `center`.
    /// Must be filled with the `.evenOdd` rule (e.g. `CAShapeLayer.fillRule = .evenOdd`,
    /// or `CGPath.contains(_:using:.evenOdd)`) for the circle to read as a hole
    /// rather than an extra filled shape.
    static func holePath(center: CGPoint, radius: CGFloat, in rect: CGRect) -> CGPath {
        let path = CGMutablePath()
        path.addRect(rect)
        path.addEllipse(in: CGRect(
            x: center.x - radius, y: center.y - radius,
            width: radius * 2, height: radius * 2
        ))
        return path
    }
}
```

- [ ] **Step 5: Register the implementation file in `project.pbxproj`**

Generate two more fresh IDs the same way as Step 2. Add the four entries (`PBXBuildFile`, `PBXFileReference`, the `Overlay` group's children list, the `ZoomacIt` target's `Sources` build phase list — note: **not** the Tests target this time), following the `BreakTimerWidgetMetrics.swift` entries as the template.

- [ ] **Step 6: Run tests to verify they pass**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS (all 9 new tests)

- [ ] **Step 7: Commit**

```bash
git add src/ZoomacIt/Overlay/MouseSpotlightGeometry.swift src/ZoomacItTests/MouseSpotlightGeometryTests.swift src/ZoomacIt.xcodeproj/project.pbxproj
git commit -m "feat: add pure geometry for Mouse Spotlight"
```

---

### Task 2: `Settings.mouseSpotlightRadius`

**Files:**
- Modify: `src/ZoomacIt/Models/Settings.swift`
- Modify: `src/ZoomacItTests/SettingsTests.swift`

**Interfaces:**
- Consumes: `MouseSpotlightGeometry.defaultRadius`, `.clampedRadius(_:)` (Task 1).
- Produces: `Settings.shared.mouseSpotlightRadius: CGFloat` (read-clamp, write-clamp, included in `resetToDefaults()`). Task 4 (controller) reads/writes this.

- [ ] **Step 1: Write the failing tests**

In `src/ZoomacItTests/SettingsTests.swift`, add right after the existing `testResetClearsBreakTimerWidgetDiameter()` test (still before the `// MARK: - Reset` section):

```swift

    // MARK: - Mouse Spotlight

    func testMouseSpotlightRadiusDefaultsToDefaultRadius() {
        XCTAssertEqual(Settings.shared.mouseSpotlightRadius, MouseSpotlightGeometry.defaultRadius)
    }

    func testMouseSpotlightRadiusRoundTrip() {
        Settings.shared.mouseSpotlightRadius = 250
        XCTAssertEqual(Settings.shared.mouseSpotlightRadius, 250)
    }

    func testMouseSpotlightRadiusClampsStoredOutOfRangeValuesOnRead() {
        UserDefaults.standard.set(10_000.0, forKey: "mouseSpotlightRadius")
        XCTAssertEqual(Settings.shared.mouseSpotlightRadius, MouseSpotlightGeometry.maxRadius)
        UserDefaults.standard.set(1.0, forKey: "mouseSpotlightRadius")
        XCTAssertEqual(Settings.shared.mouseSpotlightRadius, MouseSpotlightGeometry.minRadius)
    }

    func testMouseSpotlightRadiusClampsOnWrite() {
        Settings.shared.mouseSpotlightRadius = 9999
        XCTAssertEqual(
            UserDefaults.standard.double(forKey: "mouseSpotlightRadius"),
            Double(MouseSpotlightGeometry.maxRadius))
    }

    func testResetClearsMouseSpotlightRadius() {
        Settings.shared.mouseSpotlightRadius = 300
        Settings.shared.resetToDefaults()
        XCTAssertEqual(Settings.shared.mouseSpotlightRadius, MouseSpotlightGeometry.defaultRadius)
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: FAIL — `value of type 'Settings' has no member 'mouseSpotlightRadius'`

- [ ] **Step 3: Implement**

In `src/ZoomacIt/Models/Settings.swift`, inside `enum Keys`, add right after `static let breakTimerWidgetDiameter = "breakTimerWidgetDiameter"`:

```swift

        // Mouse Spotlight
        static let mouseSpotlightRadius = "mouseSpotlightRadius"
```

Add the computed property right after the `breakTimerWidgetDiameter` property block (before `// MARK: - Reset`):

```swift

    // MARK: - Mouse Spotlight

    /// User-chosen spotlight hole radius (scroll-to-resize). Clamped on both read and
    /// write so a stale or hand-edited defaults value can't produce a broken spotlight.
    var mouseSpotlightRadius: CGFloat {
        get {
            guard let stored = defaults.object(forKey: Keys.mouseSpotlightRadius) as? Double else {
                return MouseSpotlightGeometry.defaultRadius
            }
            return MouseSpotlightGeometry.clampedRadius(CGFloat(stored))
        }
        set {
            defaults.set(
                Double(MouseSpotlightGeometry.clampedRadius(newValue)),
                forKey: Keys.mouseSpotlightRadius
            )
        }
    }
```

In `resetToDefaults()`, add `Keys.mouseSpotlightRadius` to the `allKeys` array, right after `Keys.breakTimerWidgetDiameter`:

```swift
            Keys.breakTimerWidgetDiameter,
            Keys.mouseSpotlightRadius
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS (all 5 new tests, plus no regressions)

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/Models/Settings.swift src/ZoomacItTests/SettingsTests.swift
git commit -m "feat: persist Mouse Spotlight radius in Settings"
```

---

### Task 3: `MouseSpotlightOverlayView`

**Files:**
- Create: `src/ZoomacIt/Overlay/MouseSpotlightOverlayView.swift`

**Interfaces:**
- Consumes: `MouseSpotlightGeometry.holePath(center:radius:in:)` (Task 1).
- Produces: `MouseSpotlightOverlayView(frame:)`, `.updateHole(center: CGPoint, radius: CGFloat)`, `.showClickRipple(at: CGPoint)`. Task 4 (controller) creates one instance per screen and calls both methods.

No unit tests for this file — it's a pure AppKit/Core Animation view with no meaningful logic to test in isolation, matching this codebase's existing convention (`BreakTimerView`, `DrawingCanvasView` have no dedicated test files either; only their pure-math siblings do). Verified by build success here and by the manual GUI checklist at the end of Task 7.

- [ ] **Step 1: Create the file**

Create `src/ZoomacIt/Overlay/MouseSpotlightOverlayView.swift`:

```swift
import AppKit

/// Full-screen, click-through view for Mouse Spotlight: dims everything
/// outside a circular hole around the cursor, and hosts transient
/// click-ripple animations on top (unmasked, so ripples stay visible
/// even where the hole has already made that area transparent).
///
/// This view never receives real mouse events — the hosting `OverlayWindow`
/// has `ignoresMouseEvents = true`. Positioning is driven entirely by
/// `MouseSpotlightWindowController` calling `updateHole`/`showClickRipple`.
final class MouseSpotlightOverlayView: NSView {

    /// Fixed dimming darkness for v1 — see design spec's Non-Goals
    /// (no Settings slider yet).
    private static let dimAlpha: CGFloat = 0.5

    private let dimLayer = CALayer()
    private let holeMask = CAShapeLayer()

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true

        dimLayer.backgroundColor = NSColor.black.withAlphaComponent(Self.dimAlpha).cgColor
        holeMask.fillRule = .evenOdd
        dimLayer.mask = holeMask
        layer?.addSublayer(dimLayer)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()
        dimLayer.frame = bounds
    }

    /// Re-cuts the hole at `center` (view coordinates) with the given radius.
    /// Cheap — only reassigns the mask layer's path, no view relayout.
    func updateHole(center: CGPoint, radius: CGFloat) {
        holeMask.frame = bounds
        holeMask.path = MouseSpotlightGeometry.holePath(center: center, radius: radius, in: bounds)
    }

    /// Spawns a ring at `point` (view coordinates) that expands and fades out,
    /// then removes itself. Added directly to the view's root layer — a sibling
    /// of `dimLayer`, not a descendant — so `dimLayer`'s hole mask never
    /// affects it.
    func showClickRipple(at point: CGPoint) {
        let startRadius: CGFloat = 8
        let endRadius: CGFloat = 40

        let ring = CAShapeLayer()
        ring.path = CGPath(
            ellipseIn: CGRect(x: point.x - startRadius, y: point.y - startRadius,
                               width: startRadius * 2, height: startRadius * 2),
            transform: nil
        )
        ring.fillColor = NSColor.clear.cgColor
        ring.strokeColor = NSColor.white.cgColor
        ring.lineWidth = 3
        layer?.addSublayer(ring)

        let pathAnimation = CABasicAnimation(keyPath: "path")
        pathAnimation.toValue = CGPath(
            ellipseIn: CGRect(x: point.x - endRadius, y: point.y - endRadius,
                               width: endRadius * 2, height: endRadius * 2),
            transform: nil
        )
        let opacityAnimation = CABasicAnimation(keyPath: "opacity")
        opacityAnimation.toValue = 0.0

        let group = CAAnimationGroup()
        group.animations = [pathAnimation, opacityAnimation]
        group.duration = 0.4
        group.timingFunction = CAMediaTimingFunction(name: .easeOut)
        group.fillMode = .forwards
        group.isRemovedOnCompletion = false

        CATransaction.begin()
        CATransaction.setCompletionBlock { [weak ring] in
            ring?.removeFromSuperlayer()
        }
        ring.add(group, forKey: "ripple")
        CATransaction.commit()
    }
}
```

- [ ] **Step 2: Register the file in `project.pbxproj`**

Generate two fresh IDs (`uuidgen | tr -d '-' | cut -c1-24`, run twice). Add the four entries (`PBXBuildFile`, `PBXFileReference`, `Overlay` group children, `ZoomacIt` target `Sources` build phase), following the same template as Task 1 Step 5.

- [ ] **Step 3: Run the full test suite (regression check)**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS — no new tests, this just confirms the new file compiles cleanly as part of the app target (the test target links against it via `@testable import ZoomacIt`).

- [ ] **Step 4: Commit**

```bash
git add src/ZoomacIt/Overlay/MouseSpotlightOverlayView.swift src/ZoomacIt.xcodeproj/project.pbxproj
git commit -m "feat: add Mouse Spotlight overlay view (dim + hole mask + click ripple)"
```

---

### Task 4: `MouseSpotlightWindowController`

**Files:**
- Create: `src/ZoomacIt/Overlay/MouseSpotlightWindowController.swift`

**Interfaces:**
- Consumes: `OverlayWindow(for: NSScreen)` + `.ignoresMouseEvents` (existing, `Overlay/OverlayWindow.swift`), `NSScreen.screenContainingMouse` (existing, `Utilities/NSScreen+Extensions.swift`), `MouseSpotlightOverlayView(frame:)` / `.updateHole` / `.showClickRipple` (Task 3), `MouseSpotlightGeometry.radius(afterScrollDeltaY:isPrecise:from:)` (Task 1), `Settings.shared.mouseSpotlightRadius` (Task 2).
- Produces: `MouseSpotlightWindowController()`, `.showSpotlight()`, `.dismiss()`, `.isActive: Bool`. Task 6 (`AppDelegate`) owns and toggles one instance.

No unit tests — window/timer/event-monitor lifecycle glue has no existing test-coverage precedent in this repo (same as `BreakTimerWindowController`, `LiveZoomWindowController`). Verified by build success + the manual GUI checklist at the end of Task 7.

- [ ] **Step 1: Create the file**

Create `src/ZoomacIt/Overlay/MouseSpotlightWindowController.swift`:

```swift
import AppKit

/// Manages the lifecycle of Mouse Spotlight: a click-through, cursor-following
/// screen dimmer with a click-ripple effect, toggled by ⌘1. Unlike Draw/Zoom,
/// it never grabs input or activates the app — see
/// docs/superpowers/specs/2026-07-22-mouse-spotlight-design.md.
@MainActor
final class MouseSpotlightWindowController {

    private var overlayWindow: OverlayWindow?
    private var overlayView: MouseSpotlightOverlayView?
    private var currentScreen: NSScreen?
    private var followTimer: Timer?
    private var mouseDownMonitor: Any?
    private var scrollMonitor: Any?
    private var radius: CGFloat

    /// True while the overlay is visible.
    var isActive: Bool { overlayWindow != nil }

    init() {
        self.radius = Settings.shared.mouseSpotlightRadius
    }

    // MARK: - Public

    func showSpotlight() {
        guard let screen = NSScreen.screenContainingMouse ?? NSScreen.main else {
            NSLog("[MouseSpotlightController] No screen available.")
            return
        }
        NSLog("[MouseSpotlightController] Showing spotlight, radius=%.0f", radius)
        presentOverlay(on: screen)
        startFollowing()
        startMonitors()
    }

    func dismiss() {
        NSLog("[MouseSpotlightController] Dismissing spotlight.")
        followTimer?.invalidate()
        followTimer = nil
        if let mouseDownMonitor {
            NSEvent.removeMonitor(mouseDownMonitor)
        }
        mouseDownMonitor = nil
        if let scrollMonitor {
            NSEvent.removeMonitor(scrollMonitor)
        }
        scrollMonitor = nil

        overlayWindow?.orderOut(nil)
        overlayWindow?.close()
        overlayWindow = nil
        overlayView = nil
        currentScreen = nil
    }

    // MARK: - Private — window presentation

    private func presentOverlay(on screen: NSScreen) {
        let window = OverlayWindow(for: screen)
        window.ignoresMouseEvents = true

        let view = MouseSpotlightOverlayView(frame: NSRect(origin: .zero, size: screen.frame.size))
        window.contentView = view
        window.orderFront(nil)

        overlayWindow = window
        overlayView = view
        currentScreen = screen
        updateHole(on: screen)
    }

    private func updateHole(on screen: NSScreen) {
        let mouseLocation = NSEvent.mouseLocation
        let localPoint = CGPoint(
            x: mouseLocation.x - screen.frame.minX,
            y: mouseLocation.y - screen.frame.minY
        )
        overlayView?.updateHole(center: localPoint, radius: radius)
    }

    // MARK: - Private — cursor following

    private func startFollowing() {
        followTimer?.invalidate()
        let timer = Timer(timeInterval: 1.0 / 60.0, repeats: true) { [weak self] _ in
            self?.tick()
        }
        RunLoop.main.add(timer, forMode: .common)
        followTimer = timer
    }

    /// Runs every tick: re-centers the hole, and hands the overlay off to a
    /// different screen if the cursor has crossed onto one.
    private func tick() {
        guard let targetScreen = NSScreen.screenContainingMouse else { return }
        if targetScreen !== currentScreen {
            overlayWindow?.orderOut(nil)
            overlayWindow?.close()
            presentOverlay(on: targetScreen)
            return
        }
        updateHole(on: targetScreen)
    }

    // MARK: - Private — global monitors (click ripple + scroll resize)

    /// Mouse-event global monitors (unlike keyboard ones) require no special
    /// permission — they work regardless of which app has focus.
    private func startMonitors() {
        mouseDownMonitor = NSEvent.addGlobalMonitorForEvents(
            matching: [.leftMouseDown, .rightMouseDown]
        ) { [weak self] event in
            self?.handleClick(event)
        }
        scrollMonitor = NSEvent.addGlobalMonitorForEvents(matching: .scrollWheel) { [weak self] event in
            self?.handleScroll(event)
        }
    }

    private func handleClick(_ event: NSEvent) {
        guard let screen = currentScreen, let view = overlayView else { return }
        let mouseLocation = NSEvent.mouseLocation
        guard screen.frame.contains(mouseLocation) else { return }
        let localPoint = CGPoint(
            x: mouseLocation.x - screen.frame.minX,
            y: mouseLocation.y - screen.frame.minY
        )
        view.showClickRipple(at: localPoint)
    }

    private func handleScroll(_ event: NSEvent) {
        let target = MouseSpotlightGeometry.radius(
            afterScrollDeltaY: event.scrollingDeltaY,
            isPrecise: event.hasPreciseScrollingDeltas,
            from: radius
        )
        guard target != radius else { return }
        radius = target
        Settings.shared.mouseSpotlightRadius = target
        if let screen = currentScreen {
            updateHole(on: screen)
        }
    }
}
```

- [ ] **Step 2: Register the file in `project.pbxproj`**

Generate two fresh IDs (`uuidgen | tr -d '-' | cut -c1-24`, run twice). Add the four entries, following the same template as Task 1 Step 5.

- [ ] **Step 3: Run the full test suite (regression check)**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS — confirms the new file compiles; nothing calls it yet (that's Task 6).

- [ ] **Step 4: Commit**

```bash
git add src/ZoomacIt/Overlay/MouseSpotlightWindowController.swift src/ZoomacIt.xcodeproj/project.pbxproj
git commit -m "feat: add Mouse Spotlight window controller (click-through overlay lifecycle)"
```

---

### Task 5: `HotkeyManager` — register ⌘1

**Files:**
- Modify: `src/ZoomacIt/Core/HotkeyManager.swift`

**Interfaces:**
- Consumes: nothing new (uses existing Carbon `RegisterEventHotKey`/`UnregisterEventHotKey` plumbing already in this file).
- Produces: `HotkeyManager.shared.onMouseSpotlightHotkey: (() -> Void)?`. Task 6 (`AppDelegate`) and Task 7 (`StatusBarController`) both read/invoke it.

No unit tests — this file has no existing test-coverage precedent (Carbon event-handler glue). Verified by build success + manual hotkey check in Task 7.

- [ ] **Step 1: Add the callback property**

Right after `var onMemoHotkey: (() -> Void)?`:

```swift

    /// Called when the Mouse Spotlight hotkey (⌘1) is triggered.
    var onMouseSpotlightHotkey: (() -> Void)?
```

- [ ] **Step 2: Add the hot-key-ref storage and ID**

Right after `private var memoHotKeyRef: EventHotKeyRef?`:

```swift
    private var mouseSpotlightHotKeyRef: EventHotKeyRef?
```

Right after `private let memoHotKeyID: UInt32 = 4`:

```swift
    private let mouseSpotlightHotKeyID: UInt32 = 5
```

- [ ] **Step 3: Register the hotkey in `start()`**

Right after the Memo hotkey registration block (the `NSLog("[HotkeyManager] Memo hotkey registered: %@", ...)` line), still inside `start()`, add:

```swift

        // Register Mouse Spotlight hotkey (⌘1) — fixed in code, not user-remappable in v1.
        let mouseSpotlightKeyID = EventHotKeyID(signature: hotKeySignature, id: mouseSpotlightHotKeyID)
        let mouseSpotlightStatus = RegisterEventHotKey(
            UInt32(kVK_ANSI_1),
            UInt32(cmdKey),
            mouseSpotlightKeyID,
            GetApplicationEventTarget(),
            0,
            &mouseSpotlightHotKeyRef
        )

        guard mouseSpotlightStatus == noErr else {
            NSLog("[HotkeyManager] Failed to register mouse spotlight hotkey: %d", mouseSpotlightStatus)
            return
        }

        NSLog("[HotkeyManager] Mouse Spotlight hotkey registered: %@",
              Settings.hotkeyDisplayString(keyCode: UInt32(kVK_ANSI_1), modifiers: UInt32(cmdKey)))
```

- [ ] **Step 4: Unregister in `stop()`**

Right after the memo unregister block:

```swift
        if let ref = mouseSpotlightHotKeyRef {
            UnregisterEventHotKey(ref)
            mouseSpotlightHotKeyRef = nil
        }
```

- [ ] **Step 5: Dispatch in `handleHotKeyEvent`**

Change the end of the `if`/`else if` chain from:

```swift
        } else if hotKeyID.id == memoHotKeyID {
            DispatchQueue.main.async { [weak self] in
                self?.onMemoHotkey?()
            }
        }
    }
```

to:

```swift
        } else if hotKeyID.id == memoHotKeyID {
            DispatchQueue.main.async { [weak self] in
                self?.onMemoHotkey?()
            }
        } else if hotKeyID.id == mouseSpotlightHotKeyID {
            DispatchQueue.main.async { [weak self] in
                self?.onMouseSpotlightHotkey?()
            }
        }
    }
```

- [ ] **Step 6: Run the full test suite (regression check)**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS — no new tests, confirms no regression.

- [ ] **Step 7: Commit**

```bash
git add src/ZoomacIt/Core/HotkeyManager.swift
git commit -m "feat: register fixed Cmd+1 hotkey for Mouse Spotlight"
```

---

### Task 6: `AppDelegate` — wire the toggle

**Files:**
- Modify: `src/ZoomacIt/App/AppDelegate.swift`

**Interfaces:**
- Consumes: `HotkeyManager.shared.onMouseSpotlightHotkey` (Task 5), `MouseSpotlightWindowController()` / `.showSpotlight()` / `.dismiss()` (Task 4).
- Produces: nothing new consumed elsewhere — this is the final wiring point for the hotkey path. Task 7's menu item calls `HotkeyManager.shared.onMouseSpotlightHotkey?()` directly (same pattern the existing menu items use), so it doesn't need a new `AppDelegate` entry point.

- [ ] **Step 1: Add the controller property**

Right after `private var breakTimerController: BreakTimerWindowController?`:

```swift
    private var mouseSpotlightController: MouseSpotlightWindowController?
```

- [ ] **Step 2: Wire the hotkey callback**

Right after `hotkeyManager.onMemoHotkey = { [weak self] in self?.spawnStickyNote() }`, still inside `applicationDidFinishLaunching`:

```swift
        hotkeyManager.onMouseSpotlightHotkey = { [weak self] in
            self?.toggleMouseSpotlight()
        }
```

- [ ] **Step 3: Add the toggle method**

Add a new section right after `// MARK: - Sticky Notes`'s `spawnStickyNote()` method (before `// MARK: - Preferences`):

```swift

    // MARK: - Mouse Spotlight

    private func toggleMouseSpotlight() {
        if let controller = mouseSpotlightController {
            controller.dismiss()
            mouseSpotlightController = nil
        } else {
            let controller = MouseSpotlightWindowController()
            controller.showSpotlight()
            mouseSpotlightController = controller
        }
    }
```

- [ ] **Step 4: Build and manually smoke-test the hotkey**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacIt -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y build`
Then: `open "$PWD/build/Build/Products/Debug/ZoomacIt.app"`

Press ⌘1. Confirm:
1. The screen dims gray with a soft hole around the cursor, without any permission prompt (Mouse Spotlight needs no Screen Recording or Accessibility access — unlike Zoom/Live Zoom/Draw).
2. You can still click and type in whatever app was frontmost — the overlay doesn't steal focus or block input.
3. Press ⌘1 again — the overlay disappears.

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/App/AppDelegate.swift
git commit -m "feat: wire Cmd+1 to toggle Mouse Spotlight"
```

---

### Task 7: `StatusBarController` — menu item + full verification

**Files:**
- Modify: `src/ZoomacIt/App/StatusBarController.swift`

**Interfaces:**
- Consumes: `HotkeyManager.shared.onMouseSpotlightHotkey` (Task 5).
- Produces: nothing new — this is the last task.

- [ ] **Step 1: Add the menu item**

In `buildMenu()`, right after the `liveZoomItem` block and before `menu.addItem(.separator())`:

```swift
        let mouseSpotlightItem = NSMenuItem(title: "Mouse Spotlight", action: #selector(mouseSpotlightAction), keyEquivalent: "1")
        mouseSpotlightItem.keyEquivalentModifierMask = [.command]
        mouseSpotlightItem.target = self
        menu.addItem(mouseSpotlightItem)
```

- [ ] **Step 2: Add the action**

Right after `liveZoomAction()`:

```swift

    @objc private func mouseSpotlightAction() {
        HotkeyManager.shared.onMouseSpotlightHotkey?()
    }
```

- [ ] **Step 3: Run the full test suite (regression check)**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacItTests -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y test`
Expected: PASS — no new tests, confirms no regression.

- [ ] **Step 4: Full manual GUI verification**

Run: `xcodebuild -project src/ZoomacIt.xcodeproj -scheme ZoomacIt -configuration Debug -derivedDataPath "$PWD/build" DEVELOPMENT_TEAM=4S4L9F965Y build`
Then: `open "$PWD/build/Build/Products/Debug/ZoomacIt.app"`

Confirm each of the following (this is the point where Mouseposé can actually be deleted):
1. The menu bar icon's menu shows a "Mouse Spotlight" item with a ⌘1 shortcut, next to Zoom/Draw/Break/Live Zoom; clicking it toggles the spotlight the same as pressing ⌘1.
2. With two monitors connected: turn Mouse Spotlight on, then move the cursor from one display to the other — the dimmed hole follows onto the new display (the old display stops being dimmed).
3. While Mouse Spotlight is on, scroll (trackpad or mouse wheel) over the hole — it grows/shrinks live.
4. Toggle Mouse Spotlight off, then back on (⌘1 twice) — the hole reappears at the size you left it. Quit and relaunch the app, turn it on again — the size is still remembered.
5. Click anywhere while Mouse Spotlight is on — a white ring appears at the click point and fades out within under a second.
6. Turn Mouse Spotlight off — both the dimming and any in-flight click ripple disappear immediately.

- [ ] **Step 5: Commit**

```bash
git add src/ZoomacIt/App/StatusBarController.swift
git commit -m "feat: add Mouse Spotlight menu bar item"
```

---

## Self-Review Notes

**Spec coverage:** every requirement in `docs/superpowers/specs/2026-07-22-mouse-spotlight-design.md` maps to a task — ⌘1 toggle fixed in code (Task 5/6), click-through/non-modal (Task 4's `ignoresMouseEvents = true` + `orderFront` instead of `makeKeyAndOrderFront`), dimming + soft-edged circular hole (Task 3), cursor-follow via `NSEvent.mouseLocation` polling (Task 4), multi-monitor hand-off (Task 4's `tick()`), scroll-to-resize with persistence (Task 1 + Task 2 + Task 4's `handleScroll`), click ripple bundled into the same toggle (Task 3's `showClickRipple` + Task 4's `handleClick`), menu bar item (Task 7). The naming-collision note and the added menu item (both agreed with the user after the spec was written) are reflected in the spec's "Naming Note" section and the "Menu bar" bullet.

**Placeholder scan:** no TBD/TODO markers; every step shows complete code or a concrete, runnable command (including the `uuidgen`-based pbxproj registration steps, which can't have fixed IDs baked in ahead of time since they must be unique against the live file).

**Type consistency:** `MouseSpotlightGeometry.{minRadius, maxRadius, defaultRadius, clampedRadius, radius(afterScrollDeltaY:isPrecise:from:), holePath(center:radius:in:)}`, `MouseSpotlightOverlayView.{init(frame:), updateHole(center:radius:), showClickRipple(at:)}`, `MouseSpotlightWindowController.{init(), showSpotlight(), dismiss(), isActive}`, and `Settings.mouseSpotlightRadius` are named identically everywhere they're defined (Tasks 1–4) and consumed (Tasks 2–7).
