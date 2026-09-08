using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 브레이크 타이머의 상태와 로직. 위젯 모양과 무관한 부분이라
/// 맥 원본 <c>Models/BreakTimerState.swift</c>를 그대로 옮겼다.
/// </summary>
internal sealed class BreakTimerState
{
    /// <summary>남은 시간(초). 0에서 멈춘다.</summary>
    public int RemainingSeconds { get; set; } = Settings.Shared.BreakTimerDefaultDurationSeconds;

    /// <summary>0에 도달했는지.</summary>
    public bool IsExpired => RemainingSeconds <= 0;

    /// <summary>만료 후 경과한 초(0부터 증가).</summary>
    public int ElapsedSinceExpiration { get; set; }

    /// <summary>일시정지 여부. 타이머는 멈춘 상태로 시작하며 사용자가 재생을 눌러야 간다.</summary>
    public bool IsPaused { get; set; } = true;

    /// <summary>이번 세션에서 재생을 한 번이라도 눌렀는지.</summary>
    public bool HasStarted { get; set; }

    /// <summary>
    /// 진행 링의 기준이 되는 총 시간: 설정 기본값이 아니라 사용자가 이번에
    /// 실제로 고른 시간(재생 전 +/−로 조정한 값).
    /// </summary>
    public int SessionTotalSeconds { get; set; } = Settings.Shared.BreakTimerDefaultDurationSeconds;

    /// <summary>만료 후 경과 시간을 보여줄지.</summary>
    public bool ShowElapsed { get; set; } = Settings.Shared.BreakTimerShowElapsed;

    /// <summary>만료 시 소리를 낼지.</summary>
    public bool PlaySoundOnExpiration { get; set; } = Settings.Shared.BreakTimerPlaySound;

    /// <summary>남은 시간을 분 단위로 조정한다. 0초 아래로는 내려가지 않는다.</summary>
    public void AdjustTime(int minutes)
    {
        RemainingSeconds = Math.Max(0, RemainingSeconds + minutes * 60);

        // 만료 후 시간을 더했으면 경과 카운터를 초기화한다.
        if (RemainingSeconds > 0) ElapsedSinceExpiration = 0;

        if (!HasStarted)
        {
            // 첫 재생 전에는 아직 시간을 고르는 중이므로 링은 꽉 찬 원으로 둔다.
            SessionTotalSeconds = RemainingSeconds;
        }
        else if (RemainingSeconds > SessionTotalSeconds)
        {
            SessionTotalSeconds = RemainingSeconds;
        }
    }

    /// <summary>1초 진행. 방금 만료됐으면 true.</summary>
    public bool Tick()
    {
        if (RemainingSeconds > 0)
        {
            RemainingSeconds--;
            return RemainingSeconds == 0;
        }

        ElapsedSinceExpiration++;
        return false;
    }

    /// <summary>"10:00", "1:01", "0:00" 형식.</summary>
    public string FormattedTime => $"{RemainingSeconds / 60}:{RemainingSeconds % 60:00}";

    /// <summary>만료 후 경과 시간 — "(1:15)" 형식.</summary>
    public string FormattedElapsed => $"({ElapsedSinceExpiration / 60}:{ElapsedSinceExpiration % 60:00})";
}
