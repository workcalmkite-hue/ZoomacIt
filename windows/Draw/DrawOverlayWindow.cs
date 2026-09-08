using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZoomacItWin.Core;

namespace ZoomacItWin.Draw;

/// <summary>
/// 지워지는 펜(Vanishing Pen) 드로잉 오버레이.
///
/// 맥에서는 이 기능이 Draw 모드 안의 토글(V)이었지만, 윈도우에서는 드로잉을
/// Sysinternals ZoomIt이 담당하고 그 안에 토글을 끼워 넣을 수 없다. 그래서
/// 여기서는 "지워지는 펜"을 자체 단축키로 뜨는 독립 오버레이로 만든다.
/// ZoomIt(Ctrl+2)과 충돌 없이 공존한다.
/// </summary>
internal sealed class DrawOverlayWindow : Window
{
    /// <summary>눈에 보이지 않지만 입력은 받는 배경 (알파 1/255).</summary>
    private static readonly Brush NearlyTransparent =
        new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double Now => Clock.Elapsed.TotalSeconds;

    private readonly StrokeLayer _permanentLayer = new();
    private readonly StrokeLayer _vanishingLayer = new();
    private readonly StrokeLayer _liveLayer = new();
    private readonly HudLayer _hud = new();

    private DispatcherTimer? _vanishingTimer;

    // --- 현재 도구 상태 ---
    private PenColor _color = PenColor.Red;
    private double _penWidth = Settings.Shared.PenWidth;
    private bool _isHighlighter;
    private bool _isVanishing;
    private bool _tabHeld;

    // --- 진행 중인 획 ---
    private bool _isDrawing;
    private Point _dragOrigin;
    private readonly List<Point> _freehandPoints = new();

    /// <summary>줌 화면에서 넘어온 경우, 그 아래 깔리는 정지 화면.</summary>
    private readonly System.Windows.Controls.Image _backgroundImage = new()
    {
        Stretch = Stretch.Fill,
        Visibility = Visibility.Collapsed
    };

    /// <param name="startVanishing">열자마자 지워지는 펜으로 시작할지.</param>
    /// <param name="background">줌 화면 위에 그리는 경우 그 정지 화면(없으면 투명).</param>
    public DrawOverlayWindow(bool? startVanishing = null, ImageSource? background = null)
    {
        _isVanishing = startVanishing ?? Settings.Shared.VanishingPenOnByDefault;
        if (background is not null)
        {
            _backgroundImage.Source = background;
            _backgroundImage.Visibility = Visibility.Visible;
        }

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        // 완전 투명(알파 0)한 레이어드 창은 윈도우가 마우스/키보드 입력을 그대로
        // 아래 앱으로 통과시켜 버린다. 알파를 1/255만 줘도 창이 입력을 받는다 —
        // 눈으로는 구별되지 않으면서 그리기가 가능해진다.
        // (맥 원본이 타이머 위젯 가운데에 1% 검정을 깔아 드래그를 살린 것과 같은 트릭)
        Background = NearlyTransparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = Cursors.Cross;

        // 한글 입력 상태에서는 V/R/G/E 같은 단축키가 IME에 먼저 먹혀(ㅍ/ㄱ/ㅎ/ㄷ)
        // 창까지 오지 않는다. 이 오버레이는 글자를 입력받지 않으므로 IME를 꺼둔다.
        // (맥 원본이 같은 문제를 물리 키코드 판정으로 푼 것과 같은 목적)
        InputMethod.SetIsInputMethodEnabled(this, false);

        _vanishingLayer.AlphaProvider = s =>
            VanishingPenFader.Alpha(s.CreatedAt, Now, Settings.Shared.VanishingPenLifetime);

        var grid = new Grid();
        grid.Children.Add(_backgroundImage);
        grid.Children.Add(_permanentLayer);
        grid.Children.Add(_vanishingLayer);
        grid.Children.Add(_liveLayer);
        grid.Children.Add(_hud);
        Content = grid;

        SourceInitialized += (_, _) =>
        {
            WindowInterop.FillScreenContainingMouse(this);
            WindowInterop.HideFromAltTab(this);
        };

        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
            RefreshHud();
            Log.Write($"드로잉 오버레이 열림 — {ActualWidth}x{ActualHeight}, 지워지는 펜={_isVanishing}");
        };

        MouseLeftButtonDown += OnDrawStart;
        MouseMove += OnDrawMove;
        MouseLeftButtonUp += OnDrawEnd;
        MouseWheel += OnWheel;
        KeyDown += OnKeyDownHandler;
        KeyUp += OnKeyUpHandler;
    }

    // --- 그리기 ---

    private ShapeType CurrentShapeType()
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // ZoomIt 관례: Shift=직선, Ctrl=사각형, Ctrl+Shift=화살표, Tab=타원
        if (_tabHeld) return ShapeType.Ellipse;
        if (ctrl && shift) return ShapeType.Arrow;
        if (ctrl) return ShapeType.Rectangle;
        if (shift) return ShapeType.Line;
        return ShapeType.Freehand;
    }

    private Stroke MakeStroke(Point end, double createdAt) => new()
    {
        Points = new List<Point>(_freehandPoints),
        Start = _dragOrigin,
        End = end,
        Color = _color.ToColor(),
        LineWidth = _penWidth,
        ShapeType = CurrentShapeType(),
        IsHighlighter = _isHighlighter,
        CreatedAt = createdAt
    };

    private void OnDrawStart(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = true;
        _dragOrigin = e.GetPosition(this);
        _freehandPoints.Clear();
        _freehandPoints.Add(_dragOrigin);
        CaptureMouse();
    }

    private void OnDrawMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        var p = e.GetPosition(this);
        _freehandPoints.Add(p);

        _liveLayer.Strokes.Clear();
        _liveLayer.Strokes.Add(MakeStroke(p, Now));
        _liveLayer.Refresh();
    }

    private void OnDrawEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        ReleaseMouseCapture();

        var stroke = MakeStroke(e.GetPosition(this), Now);
        _liveLayer.Strokes.Clear();
        _liveLayer.Refresh();

        if (_isVanishing)
        {
            _vanishingLayer.Strokes.Add(stroke);
            StartVanishingTimerIfNeeded();
            _vanishingLayer.Refresh();
        }
        else
        {
            _permanentLayer.Strokes.Add(stroke);
            _permanentLayer.Refresh();
        }
    }

    // --- 사라짐 타이머 ---

    /// <summary>
    /// 사라지는 획이 하나라도 있을 때만 초당 30회 다시 그린다. 다 사라지면
    /// 타이머를 꺼서, 오버레이를 열어둔 채 두어도 CPU를 먹지 않게 한다.
    /// (원본 DrawingCanvasView의 startVanishingTimerIfNeeded와 같은 구조)
    /// </summary>
    private void StartVanishingTimerIfNeeded()
    {
        if (_vanishingTimer is not null) return;
        _vanishingTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromSeconds(1.0 / 30.0)
        };
        _vanishingTimer.Tick += (_, _) => TickVanishingStrokes();
        _vanishingTimer.Start();
    }

    private void TickVanishingStrokes()
    {
        double now = Now;
        double lifetime = Settings.Shared.VanishingPenLifetime;
        _vanishingLayer.Strokes.RemoveAll(s => VanishingPenFader.IsExpired(s.CreatedAt, now, lifetime));
        _vanishingLayer.Refresh();

        if (_vanishingLayer.Strokes.Count == 0)
        {
            _vanishingTimer?.Stop();
            _vanishingTimer = null;
        }
    }

    // --- 입력 ---

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        _penWidth = Math.Clamp(_penWidth + e.Delta / 120.0, 1, 40);
        Settings.Shared.PenWidth = _penWidth;
        RefreshHud();
    }

    private void OnKeyUpHandler(object sender, KeyEventArgs e)
    {
        if (ResolveKey(e) == Key.Tab) _tabHeld = false;
    }

    /// <summary>
    /// 실제로 눌린 키. IME가 가로챈 경우(<c>Key.ImeProcessed</c>)와 Alt 조합
    /// (<c>Key.System</c>)에서는 원래 키가 다른 속성에 들어 있다. IME를 꺼두긴 했지만,
    /// 입력기가 바뀌는 환경에서도 단축키가 죽지 않도록 이중으로 대비한다.
    /// </summary>
    private static Key ResolveKey(KeyEventArgs e) => e.Key switch
    {
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.System => e.SystemKey,
        _ => e.Key
    };

    private void OnKeyDownHandler(object sender, KeyEventArgs e)
    {
        var key = ResolveKey(e);

        // Tab은 WPF가 포커스 이동에 쓰므로 반드시 가로챈다.
        if (key == Key.Tab)
        {
            _tabHeld = true;
            e.Handled = true;
            return;
        }

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if (ctrl && key == Key.Z)
        {
            Undo();
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Escape:
                Close();
                break;

            case Key.V: // 지워지는 펜 on/off — 원본과 같은 키
                _isVanishing = !_isVanishing;
                RefreshHud();
                break;

            case Key.H:
                _isHighlighter = !_isHighlighter;
                RefreshHud();
                break;

            case Key.E: // 전부 지우기
                _permanentLayer.Strokes.Clear();
                _vanishingLayer.Strokes.Clear();
                _permanentLayer.Refresh();
                _vanishingLayer.Refresh();
                break;

            case Key.W: // 화이트보드
                _backgroundImage.Visibility = Visibility.Collapsed;
                Background = Brushes.White;
                break;

            case Key.K: // 칠판(검정)
                _backgroundImage.Visibility = Visibility.Collapsed;
                Background = Brushes.Black;
                break;

            case Key.T: // 투명 (화면 위에 직접)
                _backgroundImage.Visibility = Visibility.Collapsed;
                Background = NearlyTransparent;
                break;

            case Key.OemOpenBrackets:
                _penWidth = Math.Max(1, _penWidth - 1);
                RefreshHud();
                break;

            case Key.OemCloseBrackets:
                _penWidth = Math.Min(40, _penWidth + 1);
                RefreshHud();
                break;

            default:
                var pressed = PenColors.FromKey(key.ToString());
                if (pressed is not null)
                {
                    _color = pressed.Value;
                    RefreshHud();
                }
                break;
        }
    }

    /// <summary>
    /// 진단 모드(<c>--snip-test</c>)에서 마우스 없이 확정 획을 하나 넣는다.
    /// 화면 캡처에 판서가 들어오는지 확인하는 용도 외에는 쓰지 않는다.
    /// </summary>
    internal void AddStrokeForDiagnostics(Stroke stroke)
    {
        _permanentLayer.Strokes.Add(stroke);
        _permanentLayer.Refresh();
    }

    private void Undo()
    {
        if (_permanentLayer.Strokes.Count == 0) return;
        _permanentLayer.Strokes.RemoveAt(_permanentLayer.Strokes.Count - 1);
        _permanentLayer.Refresh();
    }

    private void RefreshHud()
    {
        _hud.IsVanishing = _isVanishing;
        _hud.IsHighlighter = _isHighlighter;
        _hud.PenWidth = _penWidth;
        _hud.PenColorValue = _color.ToColor();
        _hud.Lifetime = Settings.Shared.VanishingPenLifetime;
        _hud.Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        Log.Write("드로잉 오버레이 닫힘");
        _vanishingTimer?.Stop();
        _vanishingTimer = null;
        Settings.Save();
        base.OnClosed(e);
    }
}
