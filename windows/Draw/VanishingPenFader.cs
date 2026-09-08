namespace ZoomacItWin.Draw;

/// <summary>
/// 지워지는 펜(Vanishing Pen)의 페이드 계산만 담당한다. 화면/타이머 상태가 전혀
/// 섞이지 않은 순수 함수라 단위 테스트가 가능하다.
/// (맥 원본 <c>Draw/VanishingPenFader.swift</c>의 직역 이식)
/// </summary>
internal static class VanishingPenFader
{
    /// <summary>
    /// 획의 현재 불투명도. 그린 순간 1.0에서 시작해 <paramref name="lifetime"/>초
    /// 뒤 0.0이 되도록 선형으로 감소한다. 항상 [0, 1]로 클램프되므로
    /// (예상치 못하게) now가 createdAt보다 앞서도 음수/1 초과가 나오지 않는다.
    /// </summary>
    public static double Alpha(double createdAt, double now, double lifetime)
    {
        if (lifetime <= 0) return 0;
        double age = now - createdAt;
        double remaining = 1.0 - (age / lifetime);
        return Math.Max(0.0, Math.Min(1.0, remaining));
    }

    /// <summary>완전히 사라져서 목록에서 제거해도 되는 획인지.</summary>
    public static bool IsExpired(double createdAt, double now, double lifetime)
        => Alpha(createdAt, now, lifetime) <= 0;
}
