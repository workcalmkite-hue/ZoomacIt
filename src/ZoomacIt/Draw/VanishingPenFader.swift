import CoreGraphics
import Foundation

/// Pure fade-progress math for Vanishing Pen mode, kept free of AppKit/view
/// state so it's unit-testable without a running NSView or Timer.
enum VanishingPenFader {

    /// The stroke's current opacity multiplier: 1.0 the instant it's drawn,
    /// linearly decreasing to 0.0 by the time `lifetime` seconds have passed.
    /// Clamped to [0, 1] — callers never see negative or >1 values, including
    /// when `now` is (unexpectedly) before `createdAt`.
    static func alpha(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> CGFloat {
        guard lifetime > 0 else { return 0 }
        let age = now - createdAt
        let remaining = 1.0 - (age / lifetime)
        return CGFloat(max(0.0, min(1.0, remaining)))
    }

    /// Whether the stroke has fully faded and should be dropped from the
    /// live collection.
    static func isExpired(createdAt: CFTimeInterval, now: CFTimeInterval, lifetime: TimeInterval) -> Bool {
        alpha(createdAt: createdAt, now: now, lifetime: lifetime) <= 0
    }
}
