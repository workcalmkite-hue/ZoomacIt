using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 화면 캡처(스닙). 지금 화면을 얼린 뒤 드래그로 영역을 고르면
/// 클립보드에 복사하고 PNG 파일로도 저장한다.
///
/// 맥 원본에는 없는 기능이다 — 윈도우에서 ZoomIt을 끄면서 없어진 Ctrl+4(캡처)를
/// 대신하려고 새로 만들었다. 동작은 ZoomIt의 스닙과 같게 맞췄다.
/// </summary>
internal sealed class SnipWindow : Window
{
    /// <summary>캡처가 끝났을 때 (저장 경로, 취소면 null).</summary>
    public event Action<string?>? Finished;

    private readonly BitmapSource _shot;
    private readonly Rect _boundsPx;
    private readonly double _scale;

    private readonly Canvas _canvas = new();
    private readonly Shapes.Path _dimPath = new();
    private readonly Shapes.Rectangle _selectionOutline = new();
    private readonly Border _sizeLabel;
    private readonly TextBlock _sizeText;

    private bool _dragging;
    private Point _origin;
    private Rect _selection;

    public SnipWindow()
    {
        var screen = ScreenHelper.ScreenContainingMouse();
        _boundsPx = screen.BoundsPx();
        _scale = ScreenHelper.ScaleOf(screen);

        // 창을 띄우기 전에 찍는다 (자기 자신이 찍히지 않도록).
        _shot = ScreenCapture.CaptureRegion(_boundsPx);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Black;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;
        InputMethod.SetIsInputMethodEnabled(this, false);

        // 얼린 화면 위에 어두운 막을 덮고, 고른 영역만 구멍을 뚫는다
        // (스포트라이트와 같은 EvenOdd 방식).
        var frozen = new System.Windows.Controls.Image
        {
            Source = _shot,
            Stretch = Stretch.Fill
        };
        _canvas.Children.Add(frozen);

        _dimPath.Fill = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));
        _dimPath.IsHitTestVisible = false;
        _canvas.Children.Add(_dimPath);

        _selectionOutline.Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30));
        _selectionOutline.StrokeThickness = 2;
        _selectionOutline.Visibility = Visibility.Collapsed;
        _selectionOutline.IsHitTestVisible = false;
        _canvas.Children.Add(_selectionOutline);

        _sizeText = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Malgun Gothic"),
            FontSize = 12
        };
        _sizeLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            Child = _sizeText,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _canvas.Children.Add(_sizeLabel);

        var hint = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8),
            Child = new TextBlock
            {
                Text = "드래그해서 캡처할 곳을 고르세요 · 클립보드 복사 + 그림 폴더에 저장 · Esc 취소",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Malgun Gothic"),
                FontSize = 13
            },
            IsHitTestVisible = false
        };
        _canvas.Children.Add(hint);

        Content = _canvas;

        SourceInitialized += (_, _) =>
        {
            WindowInterop.SetBoundsPx(this, _boundsPx);
            WindowInterop.HideFromAltTab(this);
        };

        Loaded += (_, _) =>
        {
            frozen.Width = ActualWidth;
            frozen.Height = ActualHeight;
            UpdateDim();
            Canvas.SetLeft(hint, (ActualWidth - hint.ActualWidth) / 2);
            Canvas.SetTop(hint, 40);
            Activate();
            Keyboard.Focus(this);
        };

        MouseLeftButtonDown += OnDragStart;
        MouseMove += OnDragMove;
        MouseLeftButtonUp += OnDragEnd;
        MouseRightButtonDown += (_, _) => Cancel();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Cancel();
        };
    }

    private void Cancel()
    {
        Finished?.Invoke(null);
        Close();
    }

    /// <summary>고른 영역만 빼고 화면을 어둡게 (선택 전에는 전체를 어둡게).</summary>
    private void UpdateDim()
    {
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            _dimPath.Data = full;
            return;
        }

        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(full);
        group.Children.Add(new RectangleGeometry(_selection));
        _dimPath.Data = group;
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _origin = e.GetPosition(this);
        _selection = new Rect(_origin, new Size(0, 0));
        CaptureMouse();
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var p = e.GetPosition(this);
        _selection = new Rect(
            Math.Min(_origin.X, p.X), Math.Min(_origin.Y, p.Y),
            Math.Abs(p.X - _origin.X), Math.Abs(p.Y - _origin.Y));

        UpdateDim();

        _selectionOutline.Visibility = Visibility.Visible;
        _selectionOutline.Width = _selection.Width;
        _selectionOutline.Height = _selection.Height;
        Canvas.SetLeft(_selectionOutline, _selection.X);
        Canvas.SetTop(_selectionOutline, _selection.Y);

        _sizeText.Text = $"{(int)(_selection.Width * _scale)} × {(int)(_selection.Height * _scale)}";
        _sizeLabel.Visibility = Visibility.Visible;
        Canvas.SetLeft(_sizeLabel, _selection.X);
        Canvas.SetTop(_sizeLabel, Math.Max(0, _selection.Y - 26));
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        // 너무 작은 선택은 실수로 클릭한 것으로 보고 취소한다.
        if (_selection.Width < 5 || _selection.Height < 5)
        {
            Cancel();
            return;
        }

        string? saved = SaveSelection();
        Finished?.Invoke(saved);
        Close();
    }

    /// <summary>고른 영역을 잘라 클립보드에 넣고 PNG로도 저장한다.</summary>
    private string? SaveSelection()
    {
        try
        {
            var crop = new Int32Rect(
                (int)Math.Round(_selection.X * _scale),
                (int)Math.Round(_selection.Y * _scale),
                (int)Math.Round(_selection.Width * _scale),
                (int)Math.Round(_selection.Height * _scale));

            // 화면 밖으로 삐져나가지 않게 자른다.
            crop.X = Math.Max(0, Math.Min(crop.X, _shot.PixelWidth - 1));
            crop.Y = Math.Max(0, Math.Min(crop.Y, _shot.PixelHeight - 1));
            crop.Width = Math.Max(1, Math.Min(crop.Width, _shot.PixelWidth - crop.X));
            crop.Height = Math.Max(1, Math.Min(crop.Height, _shot.PixelHeight - crop.Y));

            var cropped = new CroppedBitmap(_shot, crop);
            cropped.Freeze();

            try
            {
                Clipboard.SetImage(cropped);
            }
            catch
            {
                // 다른 앱이 클립보드를 잡고 있으면 실패할 수 있다 — 파일 저장은 계속한다.
                Log.Write("캡처: 클립보드 복사 실패 (파일 저장은 정상)");
            }

            string folder = Settings.Shared.SnipFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ZoomacIt");
            }
            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, $"캡처_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(cropped));
            using (var stream = File.Create(file))
                encoder.Save(stream);

            Log.Write($"캡처 저장: {file}");
            return file;
        }
        catch (Exception ex)
        {
            Log.Write($"캡처 실패: {ex}");
            return null;
        }
    }
}
