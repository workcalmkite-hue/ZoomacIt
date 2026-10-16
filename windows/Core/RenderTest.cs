using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZoomacItWin.Draw;
using ZoomacItWin.Overlay;

namespace ZoomacItWin.Core;

/// <summary>
/// <c>ZoomacItWin.exe --render-test [출력폴더]</c> 로 실행하면, 화면에 아무것도
/// 띄우지 않고 각 그리기 코드의 결과만 PNG로 뽑아 준다. 수업 중에 오버레이를
/// 띄워 보지 않고도 렌더링이 깨지지 않았는지 확인하는 용도.
/// </summary>
internal static class RenderTest
{
    public static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        RenderVanishingPen(Path.Combine(outputDirectory, "test_vanishing_pen.png"));
        RenderBreakTimer(Path.Combine(outputDirectory, "test_break_timer.png"));
    }

    /// <summary>
    /// 실제 드로잉 오버레이 창을 띄워 1.2초 뒤 창 내용을 그대로 캡처하고 닫는다.
    /// 창 생성·전체화면 배치·HUD 렌더까지 한 번에 확인하는 용도.
    /// </summary>
    public static void CaptureDrawOverlay(string outFile, Action onFinished)
    {
        var window = new DrawOverlayWindow();
        window.Show();

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                Log.Write($"오버레이 캡처: 크기 {window.ActualWidth}x{window.ActualHeight}, " +
                          $"표시됨={window.IsVisible}, 최상단={window.Topmost}");

                var bitmap = new RenderTargetBitmap(
                    (int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(outFile))
                    encoder.Save(stream);
            }
            catch (Exception ex)
            {
                Log.Write($"오버레이 캡처 실패: {ex}");
            }

            window.Close();
            onFinished();
        };
        timer.Start();
    }

    /// <summary>
    /// 라이브 줌 창을 잠깐 띄워 창 내용을 그대로 저장하고 닫는다. 이 창은
    /// 화면 캡처에서 제외돼 있어(무한 거울 방지) 일반 스크린샷으로는 확인할 수 없다.
    /// </summary>
    public static void CaptureLiveZoom(string outFile, Action onFinished)
    {
        var window = new LiveZoomWindow();
        window.Show();
        CaptureWindowAfterDelay(window, outFile, onFinished, TimeSpan.FromSeconds(1.5));
    }

    /// <summary>
    /// 드로잉 오버레이를 띄운 채로 GDI 화면 캡처를 해서, 판서가 캡처에 들어오는지
    /// 확인한다. Ctrl+4(화면 캡처)가 판서를 지워 버리던 문제를 고치면서,
    /// "오버레이를 닫지 않기만 하면 되는지"를 눈이 아니라 픽셀로 확인하려고 만들었다.
    /// 레이어드 창은 GDI 캡처에서 빠진다는 통념이 요즘 윈도우에서는 맞지 않기 때문.
    /// </summary>
    public static void SnipTest(string outFile, Action onFinished)
    {
        var window = new DrawOverlayWindow(startVanishing: false);
        window.Show();

        var marker = PenColor.Green.ToColor();

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.2)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                // 화면 가운데를 가로지르는 굵은 초록 선 하나.
                double y = window.ActualHeight / 2;
                var points = new List<Point>();
                for (double x = 100; x <= window.ActualWidth - 100; x += 10)
                    points.Add(new Point(x, y));

                window.AddStrokeForDiagnostics(new Stroke
                {
                    Points = points,
                    Start = points[0],
                    End = points[^1],
                    Color = marker,
                    LineWidth = 20,
                    ShapeType = ShapeType.Freehand,
                    CreatedAt = 0
                });

                // 획이 실제로 화면에 올라간 뒤에 찍어야 한다.
                var shoot = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(0.6)
                };
                shoot.Tick += (_, _) =>
                {
                    shoot.Stop();
                    try
                    {
                        var boundsPx = ScreenHelper.ScreenContainingMouse().BoundsPx();
                        var shot = ScreenCapture.CaptureRegion(boundsPx);
                        int hits = CountPixelsNear(shot, marker);

                        Log.Write($"스닙 진단: 캡처 {shot.PixelWidth}x{shot.PixelHeight}, " +
                                  $"판서 색 픽셀 {hits}개 → " +
                                  (hits > 500 ? "GDI 캡처에 판서가 들어온다" : "판서가 캡처에서 빠진다"));
                        Console.WriteLine($"SNIPTEST hits={hits}");

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(shot));
                        using (var stream = File.Create(outFile))
                            encoder.Save(stream);
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"스닙 진단 실패: {ex}");
                    }

                    window.Close();
                    onFinished();
                };
                shoot.Start();
            }
            catch (Exception ex)
            {
                Log.Write($"스닙 진단 실패: {ex}");
                window.Close();
                onFinished();
            }
        };
        timer.Start();
    }

    /// <summary>비트맵에서 지정한 색과 거의 같은 픽셀 수.</summary>
    private static int CountPixelsNear(BitmapSource source, System.Windows.Media.Color color)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        int hits = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (Math.Abs(pixels[i] - color.B) <= 12 &&
                Math.Abs(pixels[i + 1] - color.G) <= 12 &&
                Math.Abs(pixels[i + 2] - color.R) <= 12)
            {
                hits++;
            }
        }

        return hits;
    }

    private static void CaptureWindowAfterDelay(Window window, string outFile,
        Action onFinished, TimeSpan delay)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var bitmap = new RenderTargetBitmap(
                    (int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(outFile))
                    encoder.Save(stream);
                Log.Write($"창 캡처 저장: {outFile} ({window.ActualWidth}x{window.ActualHeight})");
            }
            catch (Exception ex)
            {
                Log.Write($"창 캡처 실패: {ex}");
            }

            window.Close();
            onFinished();
        };
        timer.Start();
    }

    /// <summary>같은 획을 페이드 진행도별로 나란히 그려 알파 계산을 눈으로 확인한다.</summary>
    private static void RenderVanishingPen(string path)
    {
        var canvas = new Canvas { Width = 960, Height = 280, Background = Brushes.White };

        // 0초 / 1.5초 / 2.4초 / 2.9초 경과 (수명 3초)
        double[] ages = { 0.0, 1.5, 2.4, 2.9 };
        for (int i = 0; i < ages.Length; i++)
        {
            // for 루프 변수는 반복마다 새로 만들어지지 않으므로, 렌더 시점에
            // 값이 바뀌지 않도록 지역 변수로 복사해서 캡처한다.
            double age = ages[i];
            var layer = new StrokeLayer
            {
                Width = 240,
                Height = 280,
                AlphaProvider = s => VanishingPenFader.Alpha(s.CreatedAt, age, 3.0)
            };
            layer.Strokes.Add(SineStroke(PenColor.Red, 6));
            layer.Strokes.Add(ArrowStroke(PenColor.Blue, 5));
            layer.Strokes.Add(HighlighterStroke(PenColor.Yellow, 6));
            Canvas.SetLeft(layer, i * 240);
            canvas.Children.Add(layer);

            var label = new TextBlock
            {
                Text = $"{age:0.0}s → alpha {VanishingPenFader.Alpha(0, age, 3.0):0.00}",
                FontSize = 13,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(label, i * 240 + 12);
            Canvas.SetTop(label, 8);
            canvas.Children.Add(label);
        }

        Save(canvas, path);
    }

    private static Stroke SineStroke(PenColor color, double width)
    {
        var points = new List<Point>();
        for (int i = 0; i <= 30; i++)
            points.Add(new Point(20 + i * 6.5, 120 + 40 * Math.Sin(i / 3.0)));

        return new Stroke
        {
            Points = points,
            Start = points[0],
            End = points[^1],
            Color = color.ToColor(),
            LineWidth = width,
            ShapeType = ShapeType.Freehand,
            CreatedAt = 0
        };
    }

    private static Stroke ArrowStroke(PenColor color, double width) => new()
    {
        Start = new Point(40, 200),
        End = new Point(200, 250),
        Color = color.ToColor(),
        LineWidth = width,
        ShapeType = ShapeType.Arrow,
        CreatedAt = 0
    };

    private static Stroke HighlighterStroke(PenColor color, double width) => new()
    {
        Points = new List<Point> { new(30, 70), new(210, 70) },
        Start = new Point(30, 70),
        End = new Point(210, 70),
        Color = color.ToColor(),
        LineWidth = width,
        ShapeType = ShapeType.Freehand,
        IsHighlighter = true,
        CreatedAt = 0
    };

    /// <summary>남은 시간별 링 상태를 나란히 그린다.</summary>
    private static void RenderBreakTimer(string path)
    {
        var canvas = new Canvas
        {
            Width = 720,
            Height = 220,
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3F))
        };

        (string label, int remaining, int total)[] cases =
        {
            ("시작 전 10:00", 600, 600),
            ("2/3 남음", 400, 600),
            ("1/6 남음", 100, 600),
            ("만료", 0, 600)
        };

        for (int i = 0; i < cases.Length; i++)
        {
            var (label, remaining, total) = cases[i];
            var state = new BreakTimerState
            {
                RemainingSeconds = remaining,
                SessionTotalSeconds = total,
                ElapsedSinceExpiration = remaining == 0 ? 75 : 0
            };

            var ring = new BreakTimerRingElement
            {
                State = state,
                Diameter = BreakTimerWidgetMetrics.BaseDiameter,
                Width = BreakTimerWidgetMetrics.BaseDiameter,
                Height = BreakTimerWidgetMetrics.BaseDiameter
            };
            Canvas.SetLeft(ring, 20 + i * 175);
            Canvas.SetTop(ring, 40);
            canvas.Children.Add(ring);

            var text = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontFamily = new FontFamily("Malgun Gothic"),
                Foreground = Brushes.White
            };
            Canvas.SetLeft(text, 20 + i * 175);
            Canvas.SetTop(text, 165);
            canvas.Children.Add(text);
        }

        Save(canvas, path);
    }

    private static void Save(FrameworkElement element, string path)
    {
        var size = new Size(element.Width, element.Height);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
