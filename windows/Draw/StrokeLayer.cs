using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

/// <summary>
/// 획 목록을 그리는 레이어. 맥 원본이 확정 획을 하나의 비트맵(finishedLayer)에
/// 합성하고 사라지는 획만 벡터로 따로 들고 있던 구조를, WPF에서는 "다시 그리는
/// 주기가 다른 레이어 두 개"로 나눠 같은 효과를 낸다:
/// 확정 레이어는 획이 추가될 때만, 사라지는 레이어는 초당 30회 다시 그린다.
/// </summary>
internal sealed class StrokeLayer : FrameworkElement
{
    /// <summary>그릴 획 목록. 칠판 페이지를 넘기면 그 페이지의 목록으로 갈아 끼운다.</summary>
    public List<Stroke> Strokes { get; set; } = new();

    /// <summary>획별 불투명도(사라지는 레이어는 경과 시간 기반, 확정 레이어는 항상 1).</summary>
    public Func<Stroke, double>? AlphaProvider { get; set; }

    public StrokeLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var stroke in Strokes)
        {
            if (stroke.VisibleClip is not null) dc.PushClip(stroke.VisibleClip);
            StrokeRenderer.DrawStroke(dc, stroke, AlphaProvider?.Invoke(stroke) ?? 1.0);
            if (stroke.VisibleClip is not null) dc.Pop();
        }
    }

    public void Refresh() => InvalidateVisual();
}
