# Break Timer Widget — Scroll-to-Resize Design

**Date:** 2026-07-17
**Status:** Approved
**Builds on:** `2026-07-16-break-timer-widget-design.md` (circular widget, shipped in `5c239e8`)

## Goal

The circular Break Timer widget is a fixed 110 pt diameter. The user wants it both
bigger and smaller. Add live resizing: scrolling (mouse wheel or trackpad swipe) while
the pointer is over the widget grows/shrinks it, continuously, between **70 pt and
220 pt**. The chosen size persists across timer sessions, like the widget's position.

## Non-Goals

- No size slider in Settings (scroll-only, per user decision).
- No preset sizes (S/M/L).
- No change to timer behavior, sounds, hover controls' function, or the Settings tab.

## Interaction

- Pointer over the widget + scroll → diameter changes smoothly.
  - Precise deltas (trackpad): `diameter += scrollingDeltaY * 0.5`.
  - Line-based deltas (mouse wheel): `diameter += scrollingDeltaY * 5`.
  - Scroll up = larger (natural-scrolling deltas already arrive sign-flipped from
    AppKit, so using `scrollingDeltaY` directly is correct on both settings).
- Diameter clamps to **[70, 220]**; 110 stays the default for fresh installs.
- The widget resizes around its **center** — the circle's center point stays fixed
  while the window grows/shrinks — then the origin is clamped so the whole window
  stays on its current screen (reuses the existing screen-clamp logic).
- Everything scales proportionally with `scale = diameter / 110`:
  ring line width (6 pt base), countdown font (`0.24 × diameter`, already
  proportional), control bar height (34 pt base), hover button size (22 pt base)
  and spacing (6 pt base).
- Resizing works in every state: counting down, expired (red pulse + count-up
  keep animating), and while hover controls are visible (buttons re-layout).
- Dragging is unaffected: drag uses mouse-down/-drag events, scroll is a separate
  event stream — no conflict.

## Persistence

- New `Settings` computed property `breakTimerWidgetDiameter: CGFloat`
  (UserDefaults key `breakTimerWidgetDiameter`, Double).
  - Read: missing → 110; stored value clamped to [70, 220] on read so a stale or
    hand-edited value can't produce a broken widget.
  - Write: saved **immediately on every resize** (cheap; avoids the
    position-save-on-quit gap that position saving has).
- `showTimer()` reads the saved diameter and uses the matching window size for
  position clamping and default centering.

## Architecture (follows existing boundaries)

1. **`BreakTimerWidgetMetrics`** (pure geometry, no AppKit) — the statics become
   diameter-parameterized:
   - `baseDiameter = 110`, `minDiameter = 70`, `maxDiameter = 220`
   - `clampedDiameter(_:)` → clamp to [min, max]
   - `controlBarHeight(forDiameter:)`, `windowSize(forDiameter:)`
   - `defaultOrigin(in:forDiameter:)`, `clamped(origin:in:forDiameter:)`
   - `diameter(afterScrollDeltaY:isPrecise:from:)` → new clamped diameter
   - `resizedOrigin(currentOrigin:fromDiameter:toDiameter:)` → center-anchored
     new origin (before screen clamping)
2. **`BreakTimerView`** — gains a `diameter` property (set by the controller).
   `circleFrame`, ring width, font size, and button frames derive from it.
   Overrides `scrollWheel(with:)` and forwards the raw delta via a new
   `onResizeRequest: ((CGFloat, Bool) -> Void)?` closure (deltaY, isPrecise) —
   same pattern as the existing `onDismiss`. Button frames re-layout on resize;
   the tracking area already rebuilds via `updateTrackingAreas()` when bounds
   change.
3. **`BreakTimerWindowController`** — owns the resize transaction: compute new
   diameter via metrics → compute center-anchored origin → clamp to the window's
   current screen → `window.setFrame(_:display:)` → update `view.diameter` →
   persist to Settings. `showTimer()` initializes from the saved diameter.
4. **`BreakTimerWindow`** — unchanged except its `init` takes the initial size
   (origin + size instead of origin only).

## Testing

Pure-geometry tests in the existing style (no GUI):

- Metrics scale proportionally: `windowSize`/`controlBarHeight` at 70/110/220.
- `clampedDiameter` clamps below 70 and above 220; identity in range.
- Scroll math: precise vs line deltas, accumulation, clamping at both ends.
- `resizedOrigin` keeps the window center fixed for grow and shrink.
- Settings round-trip: default 110 when unset; out-of-range stored values
  clamped on read; write/read consistency.

Human GUI verification (end of implementation): scroll to grow/shrink on both
trackpad and mouse wheel, proportional text/ring/buttons, center-anchored feel,
screen-edge clamping, size retained after ⌃3 off/on and app relaunch, resize
while expired (pulse keeps running).
