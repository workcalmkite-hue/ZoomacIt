import AppKit

/// Spawns and tracks sticky notes (Draw mode, press M). Each note is an
/// independent always-on-top panel: the text lives inside the window, so
/// dragging the note moves the text with it, and the note survives leaving
/// Draw mode until the user closes it with its × button.
@MainActor
final class StickyNoteManager {

    private var notes: [StickyNotePanel] = []

    /// Cascade counter so consecutive notes don't stack exactly on top of each other.
    private var spawnCount = 0

    func spawnNote() {
        guard let screen = NSScreen.screenContainingMouse ?? NSScreen.main else { return }

        let size = StickyNotePanel.defaultSize
        let cascade = CGFloat(spawnCount % 8) * 28
        spawnCount += 1
        let origin = CGPoint(
            x: screen.frame.midX - size.width / 2 + cascade,
            y: screen.frame.midY - size.height / 2 - cascade
        )

        let panel = StickyNotePanel(at: origin)
        panel.onClose = { [weak self, weak panel] in
            guard let self, let panel else { return }
            self.close(panel)
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

    static let defaultSize = CGSize(width: 240, height: 180)

    var onClose: (() -> Void)?

    /// Borderless panels refuse key status by default; the note needs it for typing.
    override var canBecomeKey: Bool { true }

    init(at origin: CGPoint) {
        super.init(
            contentRect: NSRect(origin: origin, size: Self.defaultSize),
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

        let view = StickyNoteView(frame: NSRect(origin: .zero, size: Self.defaultSize))
        view.onClose = { [weak self] in
            self?.onClose?()
        }
        contentView = view
        makeFirstResponder(view.textView)
    }
}

/// Draws the sticky-note body (yellow rounded rect with a darker drag bar on top)
/// and hosts the editable text view. The drag bar has no subviews except the close
/// button, so `isMovableByWindowBackground` lets the user drag the note by it.
@MainActor
final class StickyNoteView: NSView {

    var onClose: (() -> Void)?

    private static let dragBarHeight: CGFloat = 24
    private static let cornerRadius: CGFloat = 10

    private static let paperColor = NSColor(calibratedRed: 1.0, green: 0.949, blue: 0.63, alpha: 0.98)
    private static let barColor = NSColor(calibratedRed: 0.98, green: 0.87, blue: 0.44, alpha: 0.98)

    let textView: NSTextView

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

    override init(frame frameRect: NSRect) {
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
        text.font = .systemFont(ofSize: 15)
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

    @objc private func closeTapped() {
        onClose?()
    }
}
