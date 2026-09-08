namespace ZoomacItWin.Overlay;

/// <summary>
/// 진행 링과 만료 깜빡임의 순수 계산.
/// (맥 원본 <c>Overlay/BreakTimerRingGeometry.swift</c>의 직역 이식)
/// </summary>
internal static class BreakTimerRingGeometry
{
    /// <summary>"남은 시간"으로 그릴 링의 비율 (1.0 = 원 전체, 0.0 = 없음).</summary>
    public static double RemainingFraction(int remainingSeconds, int totalSeconds)
    {
        if (totalSeconds <= 0) return 0;
        double fraction = (double)remainingSeconds / totalSeconds;
        return Math.Min(Math.Max(fraction, 0), 1);
    }

    /// <summary>
    /// 만료 후 빨간 링이 깜빡일 때의 불투명도. 0.9초 주기로 0.35 ~ 1.0 사이를 오간다.
    /// <paramref name="elapsedTime"/>은 단조 증가하는 아무 시계나 된다.
    /// </summary>
    public static double ExpiredPulseAlpha(double elapsedTime)
    {
        const double period = 0.9;
        double phase = (elapsedTime % period) / period;
        double wave = (Math.Sin(phase * 2 * Math.PI) + 1) / 2; // 0...1
        return 0.35 + wave * 0.65;                             // 0.35...1.0
    }
}
