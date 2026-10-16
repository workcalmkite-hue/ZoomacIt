using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ZoomacItWin.Draw;

/// <summary>
/// 판서 화면 위의 «살아 있는» 글상자 하나 (맥 원본 v1.2 텍스트 상자).
/// 글자를 확정해도 그림으로 굳지 않아서, 나중에 눌러 고치거나 옮길 수 있다.
///
/// 바깥 테두리(여백)를 잡고 끌면 옮겨지고, 테두리를 더블클릭하면 이 글상자만 지워진다.
/// 안쪽은 평범한 입력칸이라 클릭·드래그는 글자 선택으로 쓰인다.
/// </summary>
internal sealed class DrawTextBox : Border
{
    private static readonly Brush EditBorder = new SolidColorBrush(Color.FromArgb(150, 128, 128, 128));
    /// <summary>테두리도 마우스를 받아야 끌 수 있다 — 알파 1/255로 눈엔 안 보이게 채운다.</summary>
    private static readonly Brush GrabFill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

    public TextBox Box { get; }

    /// <summary>테두리를 더블클릭해서 지워 달라고 할 때.</summary>
    public event Action<DrawTextBox>? DeleteRequested;

    private bool _dragging;
    private Point _dragStartMouse;
    private Point _dragStartPos;

    public DrawTextBox(Color color, double fontSize)
    {
        Box = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(color),
            CaretBrush = new SolidColorBrush(color),
            FontFamily = new FontFamily("Malgun Gothic"),
            FontWeight = FontWeights.SemiBold,
            FontSize = fontSize,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinWidth = fontSize * 0.8,
            Padding = new Thickness(0)
        };
        // 오버레이 창은 단축키 때문에 IME를 꺼 두었다. 글상자 안에서는 한글을 쳐야 하므로 다시 켠다.
        InputMethod.SetIsInputMethodEnabled(Box, true);

        Child = Box;
        Padding = new Thickness(8);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Background = GrabFill;
        Cursor = Cursors.SizeAll;
        Box.Cursor = Cursors.IBeam;
        SetEditable(true);

        MouseLeftButtonDown += OnGrab;
        MouseMove += OnDrag;
        MouseLeftButtonUp += (_, _) => EndDrag();
        LostMouseCapture += (_, _) => _dragging = false;
    }

    public bool HasText => !string.IsNullOrWhiteSpace(Box.Text);

    /// <summary>글상자 모드일 때만 고치고 옮길 수 있다. 펜으로 돌아가면 그 위에 그대로 그려진다.</summary>
    public void SetEditable(bool editable)
    {
        IsHitTestVisible = editable;
        BorderBrush = editable ? EditBorder : Brushes.Transparent;
        Background = editable ? GrabFill : Brushes.Transparent;
    }

    public void SetColor(Color color)
    {
        Box.Foreground = new SolidColorBrush(color);
        Box.CaretBrush = new SolidColorBrush(color);
    }

    public void FocusText()
    {
        Box.Focus();
        Keyboard.Focus(Box);
        Box.CaretIndex = Box.Text.Length;
    }

    private void OnGrab(object sender, MouseButtonEventArgs e)
    {
        // 안쪽 입력칸에서 시작한 클릭은 입력칸이 처리한다(여기까지 오지 않는다).
        if (e.ClickCount == 2)
        {
            DeleteRequested?.Invoke(this);
            e.Handled = true;
            return;
        }

        if (Parent is not IInputElement parent) return;
        _dragging = true;
        _dragStartMouse = e.GetPosition(parent);
        _dragStartPos = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
        CaptureMouse();
        e.Handled = true;
    }

    private void OnDrag(object sender, MouseEventArgs e)
    {
        if (!_dragging || Parent is not IInputElement parent) return;
        var p = e.GetPosition(parent);
        Canvas.SetLeft(this, _dragStartPos.X + (p.X - _dragStartMouse.X));
        Canvas.SetTop(this, _dragStartPos.Y + (p.Y - _dragStartMouse.Y));
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        FocusText();
    }
}
