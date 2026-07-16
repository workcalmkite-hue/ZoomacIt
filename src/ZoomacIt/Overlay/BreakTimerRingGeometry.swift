import CoreGraphics
import Foundation

/// Pure math for the Break Timer widget's progress ring and expiration pulse. No
/// AppKit dependency — `BreakTimerView` is the only consumer, and keeping this here
/// (rather than inline in `draw(_:)`) makes both formulas unit-testable.
enum BreakTimerRingGeometry {

    /// Fraction of the ring to draw as "time remaining" (1.0 = full circle, 0.0 = empty).
    static func remainingFraction(remainingSeconds: Int, totalSeconds: Int) -> CGFloat {
        guard totalSeconds > 0 else { return 0 }
        let fraction = CGFloat(remainingSeconds) / CGFloat(totalSeconds)
        return min(max(fraction, 0), 1)
    }

    /// Alpha for the pulsing red ring while expired, oscillating between 0.35 and 1.0
    /// on a 0.9s cycle. `elapsedTime` is any monotonically increasing clock (the
    /// caller uses `CACurrentMediaTime()`).
    static func expiredPulseAlpha(elapsedTime: TimeInterval) -> CGFloat {
        let period: TimeInterval = 0.9
        let phase = elapsedTime.truncatingRemainder(dividingBy: period) / period
        let wave = (sin(phase * 2 * .pi) + 1) / 2 // 0...1
        return 0.35 + wave * 0.65 // 0.35...1.0
    }
}
