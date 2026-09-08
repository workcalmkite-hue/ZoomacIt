using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 위젯 호버 컨트롤용 동그란 아이콘 버튼. 기본 <see cref="Button"/> 대신 쓰는 이유는
/// 포커스를 뺏지 않는 창(WS_EX_NOACTIVATE)에서 포커스 기반 동작 없이
/// 마우스만으로 확실히 눌리게 하기 위해서다.
/// </summary>
internal sealed class RoundIconButton : Border
{
    public event Action? Click;

    private readonly TextBlock _label;
    private static readonly Brush Idle = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
    private static readonly Brush Hover = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40));

    public RoundIconButton(string glyph, string tooltip)
    {
        _label = new TextBlock
        {
            Text = glyph,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI Symbol")
        };

        Child = _label;
        Background = Idle;
        BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        BorderThickness = new Thickness(1);
        Cursor = Cursors.Hand;
        ToolTip = tooltip;

        MouseEnter += (_, _) => Background = Hover;
        MouseLeave += (_, _) => Background = Idle;
        MouseLeftButtonUp += (_, _) => Click?.Invoke();
        // 버튼 위에서 시작한 클릭이 창 드래그로 넘어가지 않게 막는다.
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    public string Glyph
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public void SetSize(double size)
    {
        Width = size;
        Height = size;
        CornerRadius = new CornerRadius(size / 2);
        _label.FontSize = Math.Max(8, size * 0.5);
    }
}
