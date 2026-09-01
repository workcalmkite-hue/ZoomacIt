using System.Windows;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 스티키 노트의 순수 크기/클램프 계산.
/// (맥 원본 <c>Overlay/StickyNote.swift</c> 안의 <c>StickyNoteMetrics</c> 이식)
/// </summary>
internal static class StickyNoteMetrics
{
    public static readonly Size DefaultSize = new(240, 180);
    public static readonly Size MinSize = new(140, 100);
    public static readonly Size MaxSize = new(800, 620);

    public const double DefaultFontSize = 15;
    public const double MinFontSize = 10;
    public const double MaxFontSize = 60;
    public const double FontSizeStep = 2;

    public static Size ClampedSize(Size size) => new(
        Math.Min(Math.Max(size.Width, MinSize.Width), MaxSize.Width),
        Math.Min(Math.Max(size.Height, MinSize.Height), MaxSize.Height));

    public static double ClampedFontSize(double size)
        => Math.Min(Math.Max(size, MinFontSize), MaxFontSize);

    /// <summary>
    /// 노트 위에서 Ctrl+스크롤했을 때의 글자 크기. 원본의 휠 비율(칸당 1.5pt)을
    /// 윈도우 휠 한 칸(=120) 기준으로 적용한다.
    /// </summary>
    public static double FontSizeAfterScroll(double wheelNotches, double current)
    {
        const double pointsPerNotch = 1.5;
        return ClampedFontSize(current + wheelNotches * pointsPerNotch);
    }

    /// <summary>
    /// 오른쪽 아래 그립을 (dx, dy)만큼 끌었을 때의 새 창 영역. 왼쪽 위 모서리는
    /// 고정이므로 끄는 방향(오른쪽/아래)으로 커진다.
    ///
    /// 원본은 맥의 y축이 위로 증가해 <c>height - dy</c>였지만, 윈도우는 아래로
    /// 증가하므로 <c>height + dy</c>가 같은 동작이 된다.
    /// </summary>
    public static Rect FrameForGripDrag(Rect initial, double dx, double dy)
    {
        var size = ClampedSize(new Size(initial.Width + dx, initial.Height + dy));
        return new Rect(initial.X, initial.Y, size.Width, size.Height);
    }
}
