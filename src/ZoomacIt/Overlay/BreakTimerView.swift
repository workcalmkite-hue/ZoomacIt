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

    private lazy var minusButton = makeControlButton(symbolName: "minus", action: #selector(minusTapped))
    private lazy var closeButton = makeControlButton(symbolName: "xmark", action: #selector(closeTapped))
    private lazy var plusButton = makeControlButton(symbolName: "plus", action: #selector(plusTapped))

    private var trackingArea: NSTrackingArea?

    init(state: BreakTimerState) {
        self.state = state
        super.init(frame: NSRect(origin: .zero, size: BreakTimerWidgetMetrics.windowSize))
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
}
