import AppKit

/// Pure geometry/clamping for sticky notes — kept AppKit-free so it's unit-testable.
enum StickyNoteMetrics {

    static let defaultSize = CGSize(width: 240, height: 180)
    static let minSize = CGSize(width: 140, height: 100)
    static let maxSize = CGSize(width: 800, height: 620)

    static let defaultFontSize: CGFloat = 15
    static let minFontSize: CGFloat = 10
    static let maxFontSize: CGFloat = 60
    static let fontSizeStep: CGFloat = 2

    static func clampedSize(_ size: CGSize) -> CGSize {
        CGSize(
            width: min(max(size.width, minSize.width), maxSize.width),
            height: min(max(size.height, minSize.height), maxSize.height)
        )
    }

    static func clampedFontSize(_ size: CGFloat) -> CGFloat {
        min(max(size, minFontSize), maxFontSize)
    }

    /// Window frame after dragging the bottom-right grip by (dx, dy) in screen
    /// coordinates from the initial frame. The top-left corner stays fixed, so
    /// the note grows toward the drag direction (right/down).
    static func frameForGripDrag(initial: CGRect, dx: CGFloat, dy: CGFloat) -> CGRect {
        let size = clampedSize(CGSize(width: initial.width + dx, height: initial.height - dy))
        return CGRect(
            x: initial.minX,
            y: initial.maxY - size.height,
            width: size.width,
            height: size.height
        )
    }
}

/// Spawns and tracks sticky notes (Draw mode, press M). Each note is an
/// independent always-on-top panel: the text lives inside the window, so
/// dragging the note moves the text with it, and the note survives leaving
/// Draw mode until the user closes it with its × button.
@MainActor
final class StickyNoteManager {

    private var notes: [StickyNotePanel] = []

    /// Cascade counter so consecutive notes don't stack exactly on top of each other.
    private var spawnCount = 0

    /// New notes reuse the size/font the user last chose (this app run).
    private var preferredSize = StickyNoteMetrics.defaultSize
    private var preferredFontSize = StickyNoteMetrics.defaultFontSize

    func spawnNote() {
        guard let screen = NSScreen.screenContainingMouse ?? NSScreen.main else { return }

        let size = preferredSize
        let cascade = CGFloat(spawnCount % 8) * 28
        spawnCount += 1
        let origin = CGPoint(
            x: screen.frame.midX - size.width / 2 + cascade,
            y: screen.frame.midY - size.height / 2 - cascade
        )

        let panel = StickyNotePanel(at: origin, size: size, fontSize: preferredFontSize)
        panel.onClose = { [weak self, weak panel] in
            guard let self, let panel else { return }
            self.close(panel)
        }
        panel.onSizeChanged = { [weak self] newSize in
            self?.preferredSize = newSize
        }
        panel.onFontSizeChanged = { [weak self] newFontSize in
            self?.preferredFontSize = newFontSize
        }
        panel.makeKeyAndOrderFront(nil)
        notes.append(panel)
        NSLog("[StickyNoteManager] Spawned note #%d.", notes.count)
    }

    private func close(_ panel: StickyNotePanel) {
        panel.orderOut(nil)
        panel.close()
        notes.removeAll { $0 === panel }
    }
}

/// A borderless, non-activating floating panel hosting one sticky note.
/// `level = .screenSaver` keeps it above the Draw overlay and every app window.
final class StickyNotePanel: NSPanel {

    var onClose: (() -> Void)?
    var onSizeChanged: ((CGSize) -> Void)?
    var onFontSizeChanged: ((CGFloat) -> Void)?

    /// Borderless panels refuse key status by default; the note needs it for typing.
    override var canBecomeKey: Bool { true }

    init(at origin: CGPoint, size: CGSize, fontSize: CGFloat) {
        super.init(
            contentRect: NSRect(origin: origin, size: size),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        level = .screenSaver
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        isReleasedWhenClosed = false
        isMovableByWindowBackground = true
        hidesOnDeactivate = false

        let view = StickyNoteView(frame: NSRect(origin: .zero, size: size), fontSize: fontSize)
        view.onClose = { [weak self] in
            self?.onClose?()
        }
        view.onFontSizeChanged = { [weak self] newFontSize in
            self?.onFontSizeChanged?(newFontSize)
        }
        contentView = view
        makeFirstResponder(view.textView)
    }

    /// ⌘+ / ⌘− / ⌘0 adjust the note's text size while the note is key.
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        guard event.modifierFlags.contains(.command),
              let noteView = contentView as? StickyNoteView,
              let characters = event.charactersIgnoringModifiers else {
            return super.performKeyEquivalent(with: event)
        }
        switch characters {
        case "+", "=":
            noteView.adjustFontSize(by: StickyNoteMetrics.fontSizeStep)
            return true
        case "-":
            noteView.adjustFontSize(by: -StickyNoteMetrics.fontSizeStep)
            return true
        case "0":
            noteView.setFontSize(StickyNoteMetrics.defaultFontSize)
            return true
        default:
            return super.performKeyEquivalent(with: event)
        }
    }
}

/// Draws the sticky-note body (yellow rounded rect with a darker drag bar on top)
/// and hosts the editable text view. The drag bar has no subviews except the close
/// button, so `isMovableByWindowBackground` lets the user drag the note by it.
@MainActor
final class StickyNoteView: NSView {

    var onClose: (() -> Void)?
    var onFontSizeChanged: ((CGFloat) -> Void)?

    private static let dragBarHeight: CGFloat = 24
    private static let cornerRadius: CGFloat = 10

    private static let paperColor = NSColor(calibratedRed: 1.0, green: 0.949, blue: 0.63, alpha: 0.98)
    private static let barColor = NSColor(calibratedRed: 0.98, green: 0.87, blue: 0.44, alpha: 0.98)

    let textView: NSTextView

    private(set) var fontSize: CGFloat

    private lazy var closeButton: NSButton = {
        let button = NSButton(
            image: NSImage(systemSymbolName: "xmark", accessibilityDescription: "Close note") ?? NSImage(),
            target: self,
            action: #selector(closeTapped)
        )
        button.bezelStyle = .circular
        button.isBordered = false
        button.imageScaling = .scaleProportionallyDown
        button.contentTintColor = NSColor.black.withAlphaComponent(0.55)
        return button
    }()

    init(frame frameRect: NSRect, fontSize: CGFloat) {
        self.fontSize = StickyNoteMetrics.clampedFontSize(fontSize)

        let textFrame = NSRect(
            x: 0,
            y: 0,
            width: frameRect.width,
            height: frameRect.height - Self.dragBarHeight
        )
        let text = NSTextView(frame: textFrame)
        text.drawsBackground = false
        text.isRichText = false
        text.allowsUndo = true
        text.font = .systemFont(ofSize: self.fontSize)
        text.textColor = NSColor.black.withAlphaComponent(0.85)
        text.insertionPointColor = NSColor.black.withAlphaComponent(0.85)
        text.textContainerInset = NSSize(width: 8, height: 8)
        self.textView = text

        super.init(frame: frameRect)

        let scroll = NSScrollView(frame: textFrame)
        scroll.drawsBackground = false
        scroll.borderType = .noBorder
        scroll.hasVerticalScroller = false
        scroll.autoresizingMask = [.width, .height]
        text.autoresizingMask = [.width]
        scroll.documentView = text
        addSubview(scroll)

        let buttonSize: CGFloat = 16
        closeButton.frame = NSRect(
            x: frameRect.width - buttonSize - 6,
            y: frameRect.height - Self.dragBarHeight + (Self.dragBarHeight - buttonSize) / 2,
            width: buttonSize,
            height: buttonSize
        )
        closeButton.autoresizingMask = [.minXMargin, .minYMargin]
        addSubview(closeButton)

        let gripSize: CGFloat = 16
        let grip = StickyNoteResizeGrip(frame: NSRect(
            x: frameRect.width - gripSize,
            y: 0,
            width: gripSize,
            height: gripSize
        ))
        grip.autoresizingMask = [.minXMargin, .maxYMargin]
        addSubview(grip)
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    override func draw(_ dirtyRect: NSRect) {
        let paper = NSBezierPath(roundedRect: bounds, xRadius: Self.cornerRadius, yRadius: Self.cornerRadius)
        Self.paperColor.setFill()
        paper.fill()

        // Drag bar strip across the top, clipped to the rounded outline.
        NSGraphicsContext.saveGraphicsState()
        paper.addClip()
        Self.barColor.setFill()
        NSRect(
            x: 0,
            y: bounds.height - Self.dragBarHeight,
            width: bounds.width,
            height: Self.dragBarHeight
        ).fill()
        NSGraphicsContext.restoreGraphicsState()
    }

    // MARK: - Font Size

    func adjustFontSize(by delta: CGFloat) {
        setFontSize(fontSize + delta)
    }

    func setFontSize(_ newSize: CGFloat) {
        let clamped = StickyNoteMetrics.clampedFontSize(newSize)
        guard clamped != fontSize else { return }
        fontSize = clamped
        // Setting `font` restyles the whole note — a memo has one text size.
        textView.font = .systemFont(ofSize: clamped)
        onFontSizeChanged?(clamped)
    }

    @objc private func closeTapped() {
        onClose?()
    }
}

/// Bottom-right corner grip that resizes the note by dragging. A separate view so
/// `mouseDownCanMoveWindow == false` exempts just this corner from the window-drag
/// behavior the rest of the note keeps.
@MainActor
private final class StickyNoteResizeGrip: NSView {

    override var mouseDownCanMoveWindow: Bool { false }

    private var initialFrame: CGRect = .zero
    private var initialMouse: NSPoint = .zero

    override func draw(_ dirtyRect: NSRect) {
        NSColor.black.withAlphaComponent(0.35).setStroke()
        // Three short diagonal hatch lines pointing into the corner.
        for offset: CGFloat in [4, 8, 12] {
            let path = NSBezierPath()
            path.move(to: NSPoint(x: bounds.maxX - offset, y: bounds.minY + 3))
            path.line(to: NSPoint(x: bounds.maxX - 3, y: bounds.minY + offset))
            path.lineWidth = 1.5
            path.stroke()
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard let window else { return }
        initialFrame = window.frame
        initialMouse = NSEvent.mouseLocation
    }

    override func mouseDragged(with event: NSEvent) {
        guard let window else { return }
        let mouse = NSEvent.mouseLocation
        let frame = StickyNoteMetrics.frameForGripDrag(
            initial: initialFrame,
            dx: mouse.x - initialMouse.x,
            dy: mouse.y - initialMouse.y
        )
        window.setFrame(frame, display: true)
    }

    override func mouseUp(with event: NSEvent) {
        guard let panel = window as? StickyNotePanel else { return }
        panel.onSizeChanged?(panel.frame.size)
    }
}
