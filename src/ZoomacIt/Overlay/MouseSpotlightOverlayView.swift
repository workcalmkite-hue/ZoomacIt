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

    /// Click-ripple stroke color. Translucent red reads clearly against both
    /// dark and light backgrounds (a solid white ring nearly disappeared on
    /// the light-background pages most browsing happens on).
    private static let rippleColor = NSColor.systemRed.withAlphaComponent(0.75)

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
        ring.strokeColor = Self.rippleColor.cgColor
        ring.lineWidth = 6
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
        group.duration = 0.8
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
