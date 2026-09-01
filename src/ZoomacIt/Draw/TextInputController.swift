import AppKit
import CoreGraphics

/// Manages the live text boxes placed during Draw mode.
///
/// Text is kept as real `NSTextView` objects for the whole Draw session rather
/// than being rasterized into `finishedLayer` on commit. That is what makes a
/// single box selectable (double-click to delete, drag to move) and lets the
/// user keep drawing on top of finished text — strokes go to the layer beneath
/// while the boxes stay editable. They are only flattened into a bitmap when
/// the canvas is exported.
@MainActor
final class TextInputController: NSObject, NSTextViewDelegate {

    private weak var canvasView: DrawingCanvasView?
    private let drawingState: DrawingState
    private var boxes: [MovableTextView] = []
    private weak var activeBox: MovableTextView?
    private var fontSize: CGFloat = Settings.shared.defaultFontSize

    /// Called when the user presses Escape inside a text box.
    /// The owner (DrawingCanvasView) should leave text mode in response.
    var onCommit: (() -> Void)?

    init(canvasView: DrawingCanvasView, drawingState: DrawingState) {
        self.canvasView = canvasView
        self.drawingState = drawingState
        super.init()
    }

    // MARK: - Public

    /// Whether any box holds text.
    var hasText: Bool {
        boxes.contains { !$0.string.isEmpty }
    }

    /// Place a new text box at the specified position within the canvas and
    /// give it focus. Existing boxes stay exactly as they are.
    func placeTextField(at point: CGPoint) {
        guard let canvas = canvasView else { return }

        discardEmptyBoxes()

        let textView = MovableTextView(frame: CGRect(x: point.x, y: point.y - 30, width: 400, height: 60))
        textView.controller = self
        textView.backgroundColor = .clear
        textView.isEditable = true
        textView.isSelectable = true
        textView.isRichText = false
        textView.drawsBackground = false
        textView.insertionPointColor = drawingState.activeColor.nsColor
        textView.textColor = drawingState.activeColor.nsColor
        textView.font = NSFont.systemFont(ofSize: fontSize, weight: Settings.shared.fontWeight.nsFontWeight)

        // Allow the text view to grow as the user types
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = true
        textView.maxSize = canvas.bounds.size
        textView.textContainer?.widthTracksTextView = false
        textView.textContainer?.containerSize = NSSize(
            width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)

        textView.delegate = self
        textView.wantsLayer = true

        canvas.addSubview(textView)
        boxes.append(textView)
        setInteractive(true)
        activate(textView)
    }

    /// Switch the boxes between editable (text mode) and inert (pen mode).
    /// Inert boxes keep showing their text but let clicks and strokes through,
    /// so the user can draw over finished text.
    func setInteractive(_ interactive: Bool) {
        if !interactive {
            discardEmptyBoxes()
            activeBox = nil
        }
        for box in boxes {
            box.isInteractive = interactive
            box.isEditable = interactive
            box.isSelectable = interactive
            box.layer?.borderWidth = interactive ? 1 : 0
            box.layer?.borderColor = NSColor.systemGray.withAlphaComponent(0.7).cgColor
            box.layer?.cornerRadius = 4
            box.window?.invalidateCursorRects(for: box)
        }
        if !interactive, let canvas = canvasView, canvas.window?.firstResponder !== canvas {
            canvas.window?.makeFirstResponder(canvas)
        }
    }

    /// Make a box the focused one — color and font-size changes apply to it.
    func activate(_ box: MovableTextView) {
        discardEmptyBoxes(except: box)
        activeBox = box
        box.window?.makeFirstResponder(box)
    }

    /// The topmost box holding text under a canvas point, if any.
    func box(at point: CGPoint) -> MovableTextView? {
        boxes.last { !$0.string.isEmpty && $0.frame.contains(point) }
    }

    /// Delete one box (double-click).
    func delete(_ box: MovableTextView) {
        NSLog("[TextInputController] Deleting text box (%d remaining)", boxes.count - 1)
        box.delegate = nil
        box.removeFromSuperview()
        boxes.removeAll { $0 === box }
        if activeBox === box {
            activeBox = nil
            if let canvas = canvasView {
                canvas.window?.makeFirstResponder(canvas)
            }
        }
    }

    /// Delete every box (right-click in text mode, or Clear-all).
    func deleteAll() {
        for box in boxes {
            box.delegate = nil
            box.removeFromSuperview()
        }
        boxes.removeAll()
        activeBox = nil
        if let canvas = canvasView {
            canvas.window?.makeFirstResponder(canvas)
        }
    }

    /// Empty the focused box without removing it.
    func clearText() {
        activeBox?.string = ""
    }

    /// Adjust the focused box's font size (scroll wheel).
    /// Returns false when there is no focused box, so the caller can fall back
    /// to its own scroll handling.
    @discardableResult
    func adjustFontSize(delta: CGFloat) -> Bool {
        guard let activeBox else { return false }
        fontSize = max(8, min(200, fontSize + delta))
        activeBox.font = NSFont.systemFont(ofSize: fontSize, weight: Settings.shared.fontWeight.nsFontWeight)
        return true
    }

    /// Update the focused box's color (color keys pressed in text mode).
    func updateColor(_ color: NSColor) {
        activeBox?.textColor = color
        activeBox?.insertionPointColor = color
    }

    /// Composite every box's text onto the given layer without touching the
    /// live views — used when exporting the canvas.
    func rasterize(onto finishedLayer: CGImage?, canvasSize: CGSize) -> CGImage? {
        let filled = boxes.filter { !$0.string.isEmpty }
        guard !filled.isEmpty else { return finishedLayer }

        guard let bitmapContext = CGContext.createBitmapContext(size: canvasSize) else {
            return finishedLayer
        }

        if let existing = finishedLayer {
            bitmapContext.draw(existing, in: CGRect(origin: .zero, size: canvasSize))
        }

        let nsGraphicsContext = NSGraphicsContext(cgContext: bitmapContext, flipped: false)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = nsGraphicsContext

        for box in filled {
            let attrs: [NSAttributedString.Key: Any] = [
                .font: box.font ?? NSFont.systemFont(ofSize: fontSize),
                .foregroundColor: box.textColor ?? NSColor.red
            ]
            let string = NSAttributedString(string: box.string, attributes: attrs)

            // Draw into the box the user actually sees. The text view lays out
            // from the top of its frame with a horizontal line-fragment padding,
            // and `draw(in:)` fills from the top of the rect too, so anchoring
            // maxY and padding minX keeps both in register. The extra height
            // below only prevents clipping; layout still starts at the top.
            let frame = box.frame
            let padding = box.textContainer?.lineFragmentPadding ?? 0
            string.draw(in: CGRect(
                x: frame.minX + padding,
                y: frame.minY - frame.height,
                width: max(frame.width - padding, 1),
                height: frame.height * 2
            ))
        }

        NSGraphicsContext.restoreGraphicsState()

        return bitmapContext.makeImage()
    }

    /// Remove every text view from the canvas (Draw mode ending).
    func cleanup() {
        deleteAll()
    }

    /// Drop boxes the user left empty so stray outlines don't pile up.
    private func discardEmptyBoxes(except keep: MovableTextView? = nil) {
        for box in boxes where box.string.isEmpty && box !== keep {
            box.delegate = nil
            box.removeFromSuperview()
        }
        boxes.removeAll { $0.superview == nil }
    }

    // MARK: - NSTextViewDelegate

    /// Intercept Escape key (cancelOperation:) before NSTextView consumes it.
    func textView(_ textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
        if commandSelector == #selector(NSResponder.cancelOperation(_:)) {
            // Escape pressed — notify owner to leave text mode (text stays)
            onCommit?()
            return true // We handled it
        }
        return false // Let NSTextView handle other commands
    }
}

/// Text view that moves itself when dragged, deletes itself on double-click,
/// and steps out of the way entirely when text mode is off.
///
/// On a screen-annotation canvas the text is a label being placed, so dragging
/// it to the right spot matters more than mouse text selection — the caret is
/// still movable with the arrow keys, and ⌘A / ⇧+arrows still select.
/// Not calling `super.mouseDown` keeps NSTextView out of its own tracking loop,
/// so the drag events arrive here.
final class MovableTextView: NSTextView {

    weak var controller: TextInputController?

    /// When false the box is inert: it still shows its text, but clicks and
    /// strokes pass through to the canvas so the user can draw over it.
    var isInteractive: Bool = true

    private var dragStartInWindow: CGPoint = .zero
    private var frameOriginAtDragStart: CGPoint = .zero

    override func hitTest(_ point: NSPoint) -> NSView? {
        isInteractive ? super.hitTest(point) : nil
    }

    override func mouseDown(with event: NSEvent) {
        if event.clickCount >= 2 {
            // Deleting the view that is currently handling the event would pull
            // the ground out from under AppKit's dispatch — do it next turn.
            let controller = self.controller
            DispatchQueue.main.async { [weak self] in
                guard let self else { return }
                controller?.delete(self)
            }
            return
        }
        controller?.activate(self)
        dragStartInWindow = event.locationInWindow
        frameOriginAtDragStart = frame.origin
    }

    override func mouseDragged(with event: NSEvent) {
        let current = event.locationInWindow
        setFrameOrigin(CGPoint(
            x: frameOriginAtDragStart.x + (current.x - dragStartInWindow.x),
            y: frameOriginAtDragStart.y + (current.y - dragStartInWindow.y)
        ))
    }

    /// Right-click belongs to the canvas (clear all text / exit draw mode).
    override func rightMouseDown(with event: NSEvent) {
        (superview as? DrawingCanvasView)?.rightMouseDown(with: event)
    }

    /// While typing, every keystroke belongs to the text — so canvas commands
    /// (color, clear, whiteboard, undo…) are reached by holding Control and
    /// handed straight to the canvas. Control-letter combos aren't needed for
    /// text entry, and the canvas resolves them by physical key, so they work
    /// on a Korean layout too.
    override func keyDown(with event: NSEvent) {
        if event.modifierFlags.contains(.control),
           ANSIKey.letter(forKeyCode: event.keyCode) != nil,
           let canvas = superview as? DrawingCanvasView {
            canvas.keyDown(with: event)
            return
        }
        super.keyDown(with: event)
    }

    /// Scrolling over the box should still resize the font — the canvas owns
    /// that gesture, and NSTextView would otherwise swallow it.
    override func scrollWheel(with event: NSEvent) {
        if let canvas = superview as? DrawingCanvasView {
            canvas.scrollWheel(with: event)
            return
        }
        super.scrollWheel(with: event)
    }

    /// Keep the move cursor over the box so the drag affordance is discoverable.
    override func resetCursorRects() {
        guard isInteractive else { return }
        addCursorRect(bounds, cursor: .openHand)
    }
}
