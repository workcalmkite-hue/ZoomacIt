# Spotlight — Design

**Date:** 2026-07-22
**Status:** Approved

## Goal

Replace Mouseposé (third-party app with an unregistered-version watermark) with a
native ZoomacIt feature: a screen-dimming spotlight that follows the cursor, plus
a click ripple effect, both toggled by a single hotkey.

## Behavior

- **Hotkey:** ⌘1, toggles on/off. Fixed in code for v1 — not exposed in the
  Settings hotkey-customization UI (per user decision; can be added later if
  needed).
- **Click-through, not modal.** Unlike Draw (⌃2) and Zoom (⌃1/⌃4), which grab
  keyboard/mouse input via a key window, Spotlight must let the user keep
  working normally in whatever app is underneath. The overlay window sets
  `ignoresMouseEvents = true` and never becomes key/main.
- **Visual:** the screen (outside the spotlight) is dimmed with a translucent
  gray (~50% opacity), with a soft-edged circular hole around the cursor
  showing the desktop at full brightness. Darkness level is a fixed constant
  for v1 (per user decision) — no Settings slider now, revisit only if it
  turns out to need tuning.
- **Follow:** the hole re-centers on the cursor continuously. Tracked by
  polling `NSEvent.mouseLocation` on a display-synced timer (~60 fps) — this
  requires no extra permission (unlike keyboard global monitors) and works
  regardless of which app has focus.
- **Multi-monitor:** the overlay is presented on the screen currently
  containing the cursor (same `NSScreen.screenContainingMouse` pattern already
  used by Draw/Zoom). If the cursor crosses to a different screen while
  Spotlight is on, the controller tears down the overlay on the old screen and
  presents it on the new one.
- **Resize:** while Spotlight is on, scrolling (trackpad or wheel) grows/
  shrinks the hole radius live, same interaction pattern as the Break Timer
  widget's scroll-to-resize. The chosen radius persists across sessions.
- **Click ripple:** a global `NSEvent` monitor on `.leftMouseDown` /
  `.rightMouseDown` (mouse-event global monitors need no extra permission)
  spawns a ring at the click point that expands and fades out, then removes
  itself. Bundled into the same ⌘1 toggle — no independent on/off (per user
  decision); it is active exactly when Spotlight is active.
- Pressing ⌘1 again tears down the overlay window, the mouse-location timer,
  and the click monitor.

## Non-Goals

- No darkness slider, no Settings tab / hotkey remapping UI for Spotlight (all
  per explicit user decision — revisit only if requested later).
- No independent toggle for the click ripple.
- No screen-pixel capture (ScreenCaptureKit) — considered and rejected because
  Spotlight only needs to overlay a mask, not read or reproduce actual screen
  content the way Live Zoom does; capture would add cost with no benefit here.

## Architecture (follows existing boundaries)

1. **`SpotlightGeometry`** (`Overlay/SpotlightGeometry.swift`, pure struct, no
   AppKit) — mirrors the `BreakTimerRingGeometry` / `BreakTimerWidgetMetrics`
   pattern used elsewhere in this codebase:
   - `minRadius`, `maxRadius`, `defaultRadius` constants.
   - `clampedRadius(_:)`.
   - `radius(afterScrollDeltaY:isPrecise:from:)` — same precise-vs-line-delta
     scroll math as `BreakTimerWidgetMetrics.diameter(afterScrollDeltaY:...)`.
   - `holePath(center:radius:in:)` — returns the even-odd `CGPath` (full-screen
     rect minus circle) used as the mask layer's path.
2. **`SpotlightOverlayView`** (`Overlay/SpotlightOverlayView.swift`) —
   layer-backed `NSView` covering the full screen frame:
   - `layer?.backgroundColor` = dimming gray at fixed opacity.
   - `layer?.mask` = a `CAShapeLayer` whose `path` is
     `SpotlightGeometry.holePath(...)`, updated on every mouse-location tick
     (cheap — just reassigns the mask's `path`, no relayout).
   - Hosts click-ripple `CAShapeLayer`s as unmasked sublayers on top, so ripples
     are visible even where the dimming mask has cut a hole.
   - The view never overrides `scrollWheel(with:)` — since the window is
     click-through, no local event ever reaches it. Scroll-to-resize is instead
     read via a global `.scrollWheel` monitor at the controller level, the same
     mechanism as the mouse-moved/mouseDown monitors below.
3. **`SpotlightWindowController`** (`Overlay/SpotlightWindowController.swift`)
   — owns the toggle lifecycle:
   - `show()` / `dismiss()`, mirroring `OverlayWindowController`/
     `LiveZoomWindowController`'s shape.
   - Creates a borderless `OverlayWindow`-style window (click-through variant:
     `ignoresMouseEvents = true`, does not activate the app, does not call
     `makeKeyAndOrderFront`) sized to the screen containing the mouse.
   - Owns the mouse-location polling timer, the global mouse-moved/mouseDown/
     scroll monitors, and screen-crossing re-presentation.
   - Reads/writes the persisted radius via `Settings.spotlightRadius`.
4. **`HotkeyManager`** — add `onSpotlightHotkey: (() -> Void)?` and register
   ⌘1 (`kVK_ANSI_1` + `cmdKey`) via the same Carbon `RegisterEventHotKey` path
   used for the four existing hotkeys, but with the keyCode/modifier constants
   fixed (not read from `Settings`, since this hotkey isn't user-remappable in
   v1).
5. **`Settings`** — add `spotlightRadius: CGFloat` (UserDefaults key
   `spotlightRadius`), same read-clamp/write-immediately pattern as
   `breakTimerWidgetDiameter`.
6. **`AppDelegate`** — wire `HotkeyManager.onSpotlightHotkey` to toggle
   `SpotlightWindowController`, following the existing hotkey-to-controller
   wiring for Draw/Zoom/Break/Memo.

## Testing

Pure-geometry tests in the existing style (no GUI), mirroring
`BreakTimerWidgetMetricsTests`:

- `clampedRadius` clamps below min and above max; identity in range.
- Scroll math: precise vs. line deltas, clamping at both ends.
- `holePath` produces a path whose bounds match the screen rect and whose
  even-odd rule excludes the circle at the given center/radius.
- Settings round-trip: default radius when unset; out-of-range stored values
  clamped on read.

Human GUI verification (end of implementation, same caveat as prior ZoomacIt
features — agents can't drive this):

- ⌘1 toggles Spotlight on/off from anywhere, without stealing focus from the
  foreground app.
- Clicking/typing in the app underneath works normally while Spotlight is on
  (click-through confirmed).
- Hole follows the cursor smoothly, including across multiple monitors.
- Scroll resizes the hole live; size persists across ⌘1 off/on and app
  relaunch.
- Click ripple appears at the click point and fades out; disappears along
  with Spotlight when toggled off.
