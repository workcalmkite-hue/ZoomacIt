using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace ZoomacItWin.Core;

/// <summary>
/// 화면 캡처. 맥 원본은 ScreenCaptureKit을 썼지만, 윈도우에서는 GDI의
/// <c>BitBlt</c>(<see cref="Drawing.Graphics.CopyFromScreen"/>)로 충분하다.
///
/// 참고: 기본 BitBlt는 레이어드 창(우리 오버레이들이 전부 그렇다)을 캡처에서
/// 제외한다 — 라이브 줌이 자기 자신을 되찍는 무한 거울이 생기지 않는 이유다.
/// </summary>
internal static class ScreenCapture
{
    /// <summary>한 번만 찍는다 (정지 줌 진입 시).</summary>
    public static BitmapSource CaptureRegion(Rect px)
    {
        int w = Math.Max(1, (int)Math.Round(px.Width));
        int h = Math.Max(1, (int)Math.Round(px.Height));

        using var bitmap = new Drawing.Bitmap(w, h, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen((int)Math.Round(px.X), (int)Math.Round(px.Y), 0, 0,
                new Drawing.Size(w, h), Drawing.CopyPixelOperation.SourceCopy);
        }

        return ToBitmapSource(bitmap, freeze: true);
    }

    /// <summary>
    /// 반복 캡처용. 비트맵과 그래픽스를 재사용해 초당 30회 캡처에도 GDI 핸들이
    /// 쌓이지 않게 한다 (라이브 줌).
    /// </summary>
    public sealed class Repeating : IDisposable
    {
        private Drawing.Bitmap? _bitmap;
        private Drawing.Graphics? _graphics;
        private WriteableBitmap? _target;

        /// <summary>지정한 화면 영역을 찍어 항상 같은 <see cref="WriteableBitmap"/>에 채워 넣는다.</summary>
        public BitmapSource? Capture(Rect px)
        {
            int w = Math.Max(1, (int)Math.Round(px.Width));
            int h = Math.Max(1, (int)Math.Round(px.Height));

            if (_bitmap is null || _bitmap.Width != w || _bitmap.Height != h)
            {
                _graphics?.Dispose();
                _bitmap?.Dispose();
                _bitmap = new Drawing.Bitmap(w, h, Drawing.Imaging.PixelFormat.Format32bppArgb);
                _graphics = Drawing.Graphics.FromImage(_bitmap);
                _target = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            }

            try
            {
                _graphics!.CopyFromScreen((int)Math.Round(px.X), (int)Math.Round(px.Y), 0, 0,
                    new Drawing.Size(w, h), Drawing.CopyPixelOperation.SourceCopy);
            }
            catch
            {
                // 화면 잠금/전환 순간에는 캡처가 실패할 수 있다. 이전 프레임을 유지한다.
                return _target;
            }

            var data = _bitmap.LockBits(
                new Drawing.Rectangle(0, 0, w, h),
                Drawing.Imaging.ImageLockMode.ReadOnly,
                Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                _target!.WritePixels(new Int32Rect(0, 0, w, h), data.Scan0,
                    data.Stride * h, data.Stride);
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }

            return _target;
        }

        public void Dispose()
        {
            _graphics?.Dispose();
            _bitmap?.Dispose();
            _graphics = null;
            _bitmap = null;
            _target = null;
        }
    }

    private static BitmapSource ToBitmapSource(Drawing.Bitmap bitmap, bool freeze)
    {
        var data = bitmap.LockBits(
            new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            Drawing.Imaging.ImageLockMode.ReadOnly,
            Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var source = BitmapSource.Create(
                bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgra32, null,
                data.Scan0, data.Stride * bitmap.Height, data.Stride);
            if (freeze) source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
