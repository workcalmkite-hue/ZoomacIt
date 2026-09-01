using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 화면 위 메모 한 장. 항상 최상단에 떠 있고, 텍스트가 창 안에 있으므로 노트를
/// 끌면 글자도 같이 움직인다. × 를 누를 때까지 사라지지 않는다.
/// (맥 원본 <c>StickyNotePanel</c> + <c>StickyNoteView</c>에 대응)
///
/// 한글 입력 관련: 원본은 Ctrl+A가 "Ctrl+ㅁ"으로 들어오는 문제 때문에 물리 키코드로
/// 단축키를 판정했는데, WPF의 TextBox는 애초에 물리 키(Key 열거형) 기준이라
/// Ctrl+A/C/V/X/Z가 한글 자판에서도 그대로 동작한다.
/// </summary>
internal sealed class StickyNoteWindow : Window
{
    public event Action<StickyNoteWindow>? CloseRequested;
    public event Action<Size>? SizeChanged2;
    public event Action<double>? FontSizeChanged;

    private static readonly Color PaperColor = Color.FromRgb(0xFF, 0xF2, 0xA1);
    private static readonly Color BarColor = Color.FromRgb(0xFA, 0xDE, 0x70);
    private const double DragBarHeight = 24;
    private const double CornerRadius = 10;

    private readonly TextBox _text;
    private readonly StickyNoteResizeGrip _grip;
    private double _fontSize;

    public StickyNoteWindow(Point originPx, Size size, double fontSize)
    {
        _fontSize = StickyNoteMetrics.ClampedFontSize(fontSize);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = size.Width;
        Height = size.Height;

        // --- 본문 텍스트 ---
        _text = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromArgb(217, 0, 0, 0)),
            CaretBrush = new SolidColorBrush(Color.FromArgb(217, 0, 0, 0)),
            SelectionBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
            FontFamily = new FontFamily("Malgun Gothic"),
            FontSize = _fontSize,
            Padding = new Thickness(8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        _text.PreviewMouseWheel += OnTextWheel;
        _text.PreviewKeyDown += OnTextKeyDown;

        // --- 드래그 바 ---
        var closeButton = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        // 닫기 버튼은 드래그 바 위에 얹혀 있다. 누름(Down)을 여기서 멈추지 않으면
        // 이벤트가 드래그 바로 올라가 DragMove()가 모달 이동 루프로 마우스를 가져가고,
        // 그러면 뗌(Up)이 버튼에 도달하지 못해 노트가 닫히지 않는다.
        closeButton.MouseLeftButtonDown += (_, e) => e.Handled = true;
        closeButton.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            CloseRequested?.Invoke(this);
        };
        closeButton.MouseEnter += (_, _) =>
            closeButton.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        closeButton.MouseLeave += (_, _) => closeButton.Background = Brushes.Transparent;

        var dragBar = new Border
        {
            Height = DragBarHeight,
            Background = new SolidColorBrush(BarColor),
            CornerRadius = new CornerRadius(CornerRadius, CornerRadius, 0, 0),
            Cursor = Cursors.SizeAll,
            Child = closeButton
        };
        dragBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        // --- 리사이즈 그립 ---
        _grip = new StickyNoteResizeGrip(this)
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _grip.ResizeCompleted += () => SizeChanged2?.Invoke(new Size(Width, Height));

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(dragBar, 0);
        Grid.SetRow(_text, 1);
        body.Children.Add(dragBar);
        body.Children.Add(_text);

        var overlay = new Grid();
        overlay.Children.Add(body);
        overlay.Children.Add(_grip);

        Content = new Border
        {
            CornerRadius = new CornerRadius(CornerRadius),
            Background = new SolidColorBrush(PaperColor),
            Child = overlay,
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.35,
                Color = Colors.Black
            }
        };

        SourceInitialized += (_, _) =>
        {
            WindowInterop.HideFromAltTab(this);
            WindowInterop.SetPositionPx(this, originPx);
        };

        Loaded += (_, _) =>
        {
            Activate();
            _text.Focus();
        };
    }

    // --- 글자 크기 ---

    private void OnTextWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        SetFontSize(StickyNoteMetrics.FontSizeAfterScroll(e.Delta / 120.0, _fontSize));
        e.Handled = true;
    }

    private void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            if (e.Key == Key.Escape)
            {
                // 메모는 Escape로 닫지 않는다 (실수로 판서 내용을 날리지 않도록).
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                SetFontSize(_fontSize + StickyNoteMetrics.FontSizeStep);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                SetFontSize(_fontSize - StickyNoteMetrics.FontSizeStep);
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0:
                SetFontSize(StickyNoteMetrics.DefaultFontSize);
                e.Handled = true;
                break;
        }
    }

    /// <summary>메모는 글자 크기가 하나뿐이므로 노트 전체에 적용한다(원본과 동일).</summary>
    private void SetFontSize(double newSize)
    {
        double clamped = StickyNoteMetrics.ClampedFontSize(newSize);
        if (Math.Abs(clamped - _fontSize) < 0.01) return;
        _fontSize = clamped;
        _text.FontSize = clamped;
        FontSizeChanged?.Invoke(clamped);
    }
}

/// <summary>
/// 오른쪽 아래 모서리 그립. 빗금 세 줄을 그리고 드래그로 노트 크기를 바꾼다.
/// </summary>
internal sealed class StickyNoteResizeGrip : FrameworkElement
{
    public event Action? ResizeCompleted;

    private readonly Window _window;
    private Rect _initialFrame;
    private Point _initialCursorPx;
    private bool _dragging;

    public StickyNoteResizeGrip(Window window)
    {
        _window = window;
        Cursor = Cursors.SizeNWSE;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // 히트 테스트를 받으려면 투명하게라도 칠해야 한다.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 1.5);
        foreach (double offset in new[] { 4.0, 8.0, 12.0 })
        {
            dc.DrawLine(pen,
                new Point(ActualWidth - offset, ActualHeight - 3),
                new Point(ActualWidth - 3, ActualHeight - offset));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        _initialFrame = new Rect(0, 0, _window.Width, _window.Height);
        _initialCursorPx = ScreenHelper.CursorPositionPx();
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var cursor = ScreenHelper.CursorPositionPx();
        double dx = (cursor.X - _initialCursorPx.X) / scale;
        double dy = (cursor.Y - _initialCursorPx.Y) / scale;

        var frame = StickyNoteMetrics.FrameForGripDrag(_initialFrame, dx, dy);
        _window.Width = frame.Width;
        _window.Height = frame.Height;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        ResizeCompleted?.Invoke();
    }
}
