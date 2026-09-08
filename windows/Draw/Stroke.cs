using System.Windows;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

internal enum ShapeType
{
    Freehand,
    Line,
    Rectangle,
    Ellipse,
    Arrow
}

/// <summary>맥 원본의 PenColor. 색상 키(R/G/B/O/Y/P)와 1:1 대응.</summary>
internal enum PenColor
{
    Red,
    Green,
    Blue,
    Orange,
    Yellow,
    Pink
}

internal static class PenColors
{
    // macOS system colors의 근사값 — 원본과 같은 인상을 유지한다.
    public static Color ToColor(this PenColor c) => c switch
    {
        PenColor.Red => Color.FromRgb(0xFF, 0x3B, 0x30),
        PenColor.Green => Color.FromRgb(0x34, 0xC7, 0x59),
        PenColor.Blue => Color.FromRgb(0x00, 0x7A, 0xFF),
        PenColor.Orange => Color.FromRgb(0xFF, 0x95, 0x00),
        PenColor.Yellow => Color.FromRgb(0xFF, 0xCC, 0x00),
        PenColor.Pink => Color.FromRgb(0xFF, 0x2D, 0x55),
        _ => Colors.Red
    };

    public static PenColor? FromKey(string key) => key.ToUpperInvariant() switch
    {
        "R" => PenColor.Red,
        "G" => PenColor.Green,
        "B" => PenColor.Blue,
        "O" => PenColor.Orange,
        "Y" => PenColor.Yellow,
        "P" => PenColor.Pink,
        _ => null
    };
}

/// <summary>
/// 확정된 획 하나. 맥 원본은 일반 획을 곧바로 비트맵에 합성해버리지만,
/// 지워지는 펜은 투명도를 계속 바꿔야 하므로 벡터 상태로 들고 있어야 한다.
/// (윈도우 판은 실행취소도 쉬워지도록 일반 획까지 같은 형태로 보관한다.)
/// </summary>
internal sealed class Stroke
{
    public List<Point> Points { get; init; } = new();
    public Point Start { get; init; }
    public Point End { get; init; }
    public Color Color { get; init; }
    public double LineWidth { get; init; }
    public ShapeType ShapeType { get; init; }
    public bool IsHighlighter { get; init; }

    /// <summary>생성 시각(초). Stopwatch 기준의 단조 증가 시계.</summary>
    public double CreatedAt { get; init; }
}
