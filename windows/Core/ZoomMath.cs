using System.Windows;

namespace ZoomacItWin.Core;

/// <summary>
/// 줌의 순수 계산. (맥 원본 <c>Overlay/ZoomMath.swift</c> 이식)
///
/// 원본은 CALayer의 <c>contentsRect</c>(원점이 좌하단)를 계산하느라 y를 뒤집는
/// 함수가 따로 있었지만, WPF의 <c>ImageBrush.Viewbox</c>는 원본 이미지와 같은
/// y-아래 방향 좌표를 쓰므로 뒤집기가 필요 없다.
/// </summary>
internal static class ZoomMath
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 8.0;

    /// <summary>휠 한 칸 / 방향키 한 번에 바뀌는 배율 (원본과 동일).</summary>
    public const double ZoomStep = 0.2;

    public static double ClampZoomLevel(double level)
        => Math.Min(Math.Max(level, MinZoom), MaxZoom);

    /// <summary>이미지 한가운데 (팬 위치를 모를 때의 기본값).</summary>
    public static Point DefaultPanCenter(Size imageSize)
        => new(imageSize.Width * 0.5, imageSize.Height * 0.5);

    /// <summary>
    /// 지금 배율·팬 위치에서 화면에 보여야 할 원본 이미지의 영역을
    /// 정규화된 [0,1] 사각형으로 돌려준다. 이미지 밖으로 나가지 않도록 클램프한다.
    /// </summary>
    public static Rect VisibleContentsRect(double zoomLevel, Point panCenter, Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || zoomLevel <= 0)
            return new Rect(0, 0, 1, 1);

        double visible = 1.0 / zoomLevel;

        double normCenterX = panCenter.X / imageSize.Width;
        double normCenterY = panCenter.Y / imageSize.Height;

        double originX = Clamp(normCenterX - visible * 0.5, 0, 1 - visible);
        double originY = Clamp(normCenterY - visible * 0.5, 0, 1 - visible);

        return new Rect(originX, originY, visible, visible);
    }

    /// <summary>
    /// 라이브 줌이 매 프레임 캡처해야 할 화면 영역(물리 픽셀). 커서를 중심으로
    /// 화면 크기를 배율로 나눈 만큼 잘라내되, 화면 밖으로 나가지 않게 민다.
    /// </summary>
    public static Rect LiveCaptureRect(double zoomLevel, Point cursorPx, Rect screenPx)
    {
        double width = screenPx.Width / zoomLevel;
        double height = screenPx.Height / zoomLevel;

        double x = Clamp(cursorPx.X - width / 2, screenPx.X, screenPx.Right - width);
        double y = Clamp(cursorPx.Y - height / 2, screenPx.Y, screenPx.Bottom - height);

        return new Rect(x, y, width, height);
    }

    public static double Clamp(double value, double lower, double upper)
        => Math.Min(Math.Max(value, lower), upper);
}
