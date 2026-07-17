import AppKit
import QuartzCore

/// Draws the Break Timer circular widget: a translucent circle, a progress ring for
/// time remaining, and the countdown number centered inside it. Hover controls
/// live in the strip below the circle (`BreakTimerWidgetMetrics.controlBarHeight`).
@MainActor
final class BreakTimerView: NSView {

    let state: BreakTimerState

    /// Called when the user dismisses the timer via the hover close button.
    var onDismiss: (() -> Void)?

    /// Called when the user scrolls over the widget to resize it.
    /// Parameters are the raw `scrollingDeltaY` and `hasPreciseScrollingDeltas`.
    var onResizeRequest: ((_ scrollDeltaY: CGFloat, _ isPrecise: Bool) -> Void)?

    /// Current widget diameter; drawing and control layout scale with it.
    private(set) var diameter: CGFloat

    private var widgetScale: CGFloat { BreakTimerWidgetMetrics.scale(forDiameter: diameter) }

    private var ringLineWidth: CGFloat { 6 * widgetScale }

    private lazy var minusButton = makeControlButton(
        symbolName: "minus",
        accessibilityDescription: "Subtract one minute",
        action: #selector(minusTapped)
    )
    private lazy var closeButton = makeControlButton(
        symbolName: "xmark",
        accessibilityDescription: "Close timer",
        action: #selector(closeTapped)
    )
    private lazy var plusButton = makeControlButton(
        symbolName: "plus",
        accessibilityDescription: "Add one minute",
        action: #selector(plusTapped)
    )

    /// The three hover control buttons, grouped for show/hide and layout.
    private var controlButtons: [NSButton] { [minusButton, closeButton, plusButton] }

    private var trackingArea: NSTrackingArea?

    init(state: BreakTimerState, diameter: CGFloat) {
        self.state = state
        self.diameter = diameter
        super.init(frame: NSRect(
            origin: .zero,
            size: BreakTimerWidgetMetrics.windowSize(forDiameter: diameter)
        ))
        setUpControlButtons()
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
            y: BreakTimerWidgetMetrics.controlBarHeight(forDiameter: diameter),
            width: diameter,
            height: diameter
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

        if state.isExpired {
            let alpha = BreakTimerRingGeometry.expiredPulseAlpha(elapsedTime: CACurrentMediaTime())
            let full = NSBezierPath(ovalIn: ringRect)
            full.lineWidth = ringLineWidth
            // Deliberately ignores state.opacity so the expiration alert stays visible
            // even when the widget's overall opacity is set low.
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

    private func drawTime(in circle: NSRect) {
        let fontSize = diameter * 0.24
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

    // MARK: - Hover Controls

    private func makeControlButton(symbolName: String, accessibilityDescription: String, action: Selector) -> NSButton {
        let button = NSButton(
            image: NSImage(systemSymbolName: symbolName, accessibilityDescription: accessibilityDescription) ?? NSImage(),
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
            for button in controlButtons {
                button.animator().alphaValue = hidden ? 0 : 1
            }
        }
    }
}
