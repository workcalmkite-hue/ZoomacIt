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
