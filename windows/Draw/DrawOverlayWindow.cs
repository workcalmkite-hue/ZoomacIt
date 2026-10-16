using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZoomacItWin.Core;

namespace ZoomacItWin.Draw;

/// <summary>
/// 판서 오버레이 (맥 원본 DrawingCanvasView).
///
/// 일반 펜·지워지는 펜·도형·화살표·지우개·글상자·칠판 페이지를 한 창에서 다룬다.
/// 단축키 배치는 맥 v1.6과 같고, 윈도우에서 이미 손에 익은 키(T 투명·H 형광펜·[ ] 굵기)는 그대로 둔다.
/// </summary>
internal sealed class DrawOverlayWindow : Window
{
    /// <summary>눈에 보이지 않지만 입력은 받는 배경 (알파 1/255).</summary>
    private static readonly Brush NearlyTransparent =
        new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double Now => Clock.Elapsed.TotalSeconds;

    /// <summary>지우개는 펜보다 이만큼 굵다 (원본 eraserWidthMultiplier).</summary>
    private const double EraserWidthMultiplier = 4.0;

    private readonly StrokeLayer _permanentLayer = new();
    private readonly StrokeLayer _vanishingLayer = new();
    private readonly StrokeLayer _liveLayer = new();
    private readonly Canvas _textLayer = new();
    private readonly HudLayer _hud = new();

    private DispatcherTimer? _vanishingTimer;

    /// <summary>판서 중 M — 화면 위 메모를 하나 띄워 달라고 할 때.</summary>
    public event Action? StickyNoteRequested;

    // --- 현재 도구 상태 ---
    private PenColor _color = PenColor.Red;
    private double _penWidth = Settings.Shared.PenWidth;
    private bool _isHighlighter;
    private bool _isVanishing;
    private bool _isArrowMode;
    private bool _isEraser;
    private bool _isTextMode;
    private bool _tabHeld;

    private double EraserWidth => _penWidth * EraserWidthMultiplier;

    // --- 진행 중인 획 ---
    private bool _isDrawing;
    private Point _dragOrigin;
    private readonly List<Point> _freehandPoints = new();

    // --- 진행 중인 지우개 ---
    private bool _isErasing;
    private Point _lastErasePoint;
    /// <summary>이번 지우개 드래그 전의 상태. 드래그 한 번 = 실행취소 한 번.</summary>
    private Dictionary<Stroke, Geometry?>? _eraseBefore;

    // --- 페이지 ---
    private enum Board { None, White, Black }
    private Board _board = Board.None;

    /// <summary>화면(투명·정지 화면) 위에 그린 것. 칠판에 다녀와도 그대로 남는다.</summary>
    private readonly DrawPage _screenPage = new();

    /// <summary>칠판(흰/검정) 페이지들. → 로 다음 장, ← 로 앞 장.</summary>
    private readonly List<DrawPage> _boardPages = new();
    private int _boardPageIndex;

    private DrawPage CurrentPage => _board == Board.None ? _screenPage : _boardPages[_boardPageIndex];

    /// <summary>열 때 받은 정지 화면(⌃2로 얼린 화면 또는 확대 화면). 없으면 투명.</summary>
    private readonly System.Windows.Controls.Image _backgroundImage = new()
    {
        Stretch = Stretch.Fill,
        Visibility = Visibility.Collapsed
    };

    /// <param name="startVanishing">열자마자 지워지는 펜으로 시작할지.</param>
    /// <param name="background">얼린 화면 또는 줌 화면(없으면 투명 — 아래 앱이 그대로 보인다).</param>
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
        // 창까지 오지 않는다. 판서 단축키용으로 IME를 꺼두고, 글상자에서만 다시 켠다.
        // (맥 원본이 같은 문제를 물리 키코드 판정으로 푼 것과 같은 목적)
        InputMethod.SetIsInputMethodEnabled(this, false);

        _vanishingLayer.AlphaProvider = s =>
            VanishingPenFader.Alpha(s.CreatedAt, Now, Settings.Shared.VanishingPenLifetime);
        _permanentLayer.Strokes = _screenPage.Strokes;

        var grid = new Grid();
        grid.Children.Add(_backgroundImage);
        grid.Children.Add(_permanentLayer);
        grid.Children.Add(_vanishingLayer);
        grid.Children.Add(_liveLayer);
        grid.Children.Add(_textLayer);
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
            Log.Write($"드로잉 오버레이 열림 — {ActualWidth}x{ActualHeight}, 지워지는 펜={_isVanishing}, 정지화면={background is not null}");
        };

        MouseLeftButtonDown += OnDrawStart;
        MouseMove += OnDrawMove;
        MouseLeftButtonUp += OnDrawEnd;
        MouseRightButtonUp += OnRightClick;
        MouseLeave += (_, _) =>
        {
            if (_hud.EraserCenter is null) return;
            _hud.EraserCenter = null;
            _hud.Refresh();
        };
        MouseWheel += OnWheel;
        PreviewKeyDown += OnKeyDownHandler;
        KeyUp += OnKeyUpHandler;
    }

    // --- 그리기 ---

    private ShapeType CurrentShapeType()
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // ZoomIt 관례: Shift=직선, Ctrl=사각형, Ctrl+Shift=화살표(촉이 시작점), Tab=타원
        // A 화살표 모드: 그냥 드래그 = 파워포인트식 화살표(촉이 끝점)
        if (_tabHeld) return ShapeType.Ellipse;
        if (ctrl && shift) return ShapeType.Arrow;
        if (ctrl) return ShapeType.Rectangle;
        if (shift) return ShapeType.Line;
        if (_isArrowMode) return ShapeType.ArrowForward;
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
        var p = e.GetPosition(this);

        if (_isTextMode)
        {
            // 빈 곳을 누르면 글상자가 하나 더 생긴다 (글상자 위 클릭은 글상자가 처리).
            AddTextBox(p);
            e.Handled = true;
            return;
        }

        if (_isEraser)
        {
            _isErasing = true;
            _eraseBefore = new Dictionary<Stroke, Geometry?>();
            _lastErasePoint = p;
            EraseSegment(p, p);
            CaptureMouse();
            return;
        }

        _isDrawing = true;
        _dragOrigin = p;
        _freehandPoints.Clear();
        _freehandPoints.Add(_dragOrigin);
        CaptureMouse();
    }

    private void OnDrawMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);

        if (_isEraser)
        {
            _hud.EraserCenter = p;
            _hud.EraserDiameter = EraserWidth;
            _hud.Refresh();
            if (_isErasing && (p - _lastErasePoint).Length >= 1)
            {
                EraseSegment(_lastErasePoint, p);
                _lastErasePoint = p;
            }
            return;
        }

        if (!_isDrawing) return;
        _freehandPoints.Add(p);

        _liveLayer.Strokes.Clear();
        _liveLayer.Strokes.Add(MakeStroke(p, Now));
        _liveLayer.Refresh();
    }

    private void OnDrawEnd(object sender, MouseButtonEventArgs e)
    {
        if (_isErasing)
        {
            _isErasing = false;
            ReleaseMouseCapture();
            if (_eraseBefore is { Count: > 0 })
                CurrentPage.Undo.Add(new UndoEntry(UndoKind.Erase, null, _eraseBefore, null));
            _eraseBefore = null;
            return;
        }

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
            CurrentPage.Strokes.Add(stroke);
            CurrentPage.Undo.Add(new UndoEntry(UndoKind.Add, stroke, null, null));
            _permanentLayer.Refresh();
        }
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        // 글상자 모드에서 오른쪽 클릭 = 글상자 전부 지우기 (맥 원본과 같음)
        if (!_isTextMode) return;
        DeleteAllTextBoxes();
        e.Handled = true;
    }

    // --- 지우개 ---

    /// <summary>
    /// 지우개가 p0→p1 로 지나간 자국을, 닿은 획마다 «보이는 영역»에서 빼낸다.
    /// 지운 자리로는 그 아래(흰 칠판·검은 칠판·얼린 화면)가 그대로 드러난다.
    /// </summary>
    private void EraseSegment(Point p0, Point p1)
    {
        double r = EraserWidth / 2;
        Geometry seg = p0 == p1
            ? new EllipseGeometry(p1, r, r)
            : new LineGeometry(p0, p1).GetWidenedPathGeometry(new Pen(Brushes.Black, EraserWidth)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            });
        var segBounds = seg.Bounds;

        bool changed = false;
        foreach (var stroke in CurrentPage.Strokes)
        {
            if (!stroke.Bounds.IntersectsWith(segBounds)) continue;
            if (stroke.VisibleClip is not null && stroke.VisibleClip.IsEmpty()) continue;

            if (_eraseBefore is not null && !_eraseBefore.ContainsKey(stroke))
                _eraseBefore[stroke] = stroke.VisibleClip;

            Geometry visible = stroke.VisibleClip ?? new RectangleGeometry(stroke.Bounds);
            var clipped = Geometry.Combine(visible, seg, GeometryCombineMode.Exclude, null,
                0.25, ToleranceType.Absolute);
            clipped.Freeze();
            stroke.VisibleClip = clipped;
            changed = true;
        }

        if (changed) _permanentLayer.Refresh();
    }

    private void SetEraser(bool on)
    {
        _isEraser = on;
        if (on && _isTextMode) SetTextMode(false, createAtCursor: false);
        Cursor = on ? Cursors.None : (_isTextMode ? Cursors.IBeam : Cursors.Cross);
        if (on)
        {
            var p = Mouse.GetPosition(this);
            _hud.EraserCenter = new Rect(RenderSize).Contains(p) ? p : null;
        }
        else
        {
            _hud.EraserCenter = null;
        }
        RefreshHud();
    }

    // --- 글상자 ---

    private double TextFontSize => Settings.Shared.TextFontSize;

    private void AddTextBox(Point at)
    {
        // 빈 글상자를 여러 개 남기지 않는다.
        RemoveEmptyTextBoxes();

        var box = new DrawTextBox(_color.ToColor(), TextFontSize);
        box.DeleteRequested += b =>
        {
            _textLayer.Children.Remove(b);
            CurrentPage.Texts.Remove(b);
            Keyboard.Focus(this);
        };
        // 클릭한 자리가 글자의 첫 줄 가운데쯤 오게 살짝 올린다.
        Canvas.SetLeft(box, at.X - 9);
        Canvas.SetTop(box, at.Y - TextFontSize * 0.75);
        _textLayer.Children.Add(box);
        CurrentPage.Texts.Add(box);

        // 화면에 붙은 뒤에야 포커스를 받을 수 있다.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, box.FocusText);
    }

    private void RemoveEmptyTextBoxes()
    {
        foreach (var box in CurrentPage.Texts.Where(b => !b.HasText).ToList())
        {
            _textLayer.Children.Remove(box);
            CurrentPage.Texts.Remove(box);
        }
    }

    private void DeleteAllTextBoxes()
    {
        foreach (var box in CurrentPage.Texts) _textLayer.Children.Remove(box);
        CurrentPage.Texts.Clear();
        Keyboard.Focus(this);
    }

    /// <summary>Ctrl+T (또는 전역 단축키) — 글상자 모드를 켜고 끈다.</summary>
    public void ToggleTextMode() => SetTextMode(!_isTextMode, createAtCursor: true);

    /// <summary>오버레이를 막 열면서 글상자부터 쓰고 싶을 때 (전역 글상자 단축키).</summary>
    public void StartInTextMode()
    {
        if (IsLoaded) SetTextMode(true, createAtCursor: true);
        else Loaded += (_, _) => SetTextMode(true, createAtCursor: true);
    }

    private void SetTextMode(bool on, bool createAtCursor)
    {
        if (on && _isEraser) SetEraser(false);
        _isTextMode = on;

        foreach (var box in CurrentPage.Texts) box.SetEditable(on);
        Cursor = on ? Cursors.IBeam : Cursors.Cross;

        if (on)
        {
            var p = Mouse.GetPosition(this);
            if (createAtCursor)
                AddTextBox(new Rect(RenderSize).Contains(p) ? p : new Point(ActualWidth / 2, ActualHeight / 2));
        }
        else
        {
            RemoveEmptyTextBoxes();
            Keyboard.Focus(this);
        }
        RefreshHud();
    }

    private DrawTextBox? FocusedTextBox =>
        CurrentPage.Texts.FirstOrDefault(b => b.Box.IsKeyboardFocusWithin);

    // --- 칠판 페이지 ---

    private void ShowPage(DrawPage page)
    {
        _textLayer.Children.Clear();
        foreach (var box in page.Texts)
        {
            box.SetEditable(_isTextMode);
            _textLayer.Children.Add(box);
        }
        _permanentLayer.Strokes = page.Strokes;
        _permanentLayer.Refresh();

        // 사라지는 획은 페이지에 붙지 않는다 — 넘기면 같이 없어진다 (원본과 같음).
        ClearVanishing();
        RefreshHud();
    }

    /// <summary>W/K — 칠판으로 간다. 화면에서 오면 새 칠판 한 장으로 시작한다.</summary>
    private void SetBoard(Board board)
    {
        if (_isTextMode) SetTextMode(false, createAtCursor: false);

        bool fromScreen = _board == Board.None;
        _board = board;
        Background = board == Board.White ? Brushes.White : Brushes.Black;
        _backgroundImage.Visibility = Visibility.Collapsed;

        if (fromScreen)
        {
            _boardPages.Clear();
            _boardPages.Add(new DrawPage());
            _boardPageIndex = 0;
        }
        ShowPage(CurrentPage);
    }

    /// <summary>T — 칠판에서 화면(얼린 화면 또는 투명)으로 돌아온다. 화면에 그린 것은 그대로다.</summary>
    private void BackToScreen()
    {
        if (_isTextMode) SetTextMode(false, createAtCursor: false);
        _board = Board.None;
        Background = NearlyTransparent;
        if (_backgroundImage.Source is not null) _backgroundImage.Visibility = Visibility.Visible;
        ShowPage(_screenPage);
    }

    /// <summary>→ 다음 장(끝에서 누르면 새 장), ← 앞 장. 처음·끝을 넘어 돌지는 않는다.</summary>
    private void GoToBoardPage(int target)
    {
        if (_board == Board.None || target < 0 || target > _boardPages.Count) return;
        if (_isTextMode) SetTextMode(false, createAtCursor: false);
        if (target == _boardPages.Count) _boardPages.Add(new DrawPage());
        _boardPageIndex = target;
        ShowPage(CurrentPage);
    }

    // --- 지우기 · 실행취소 ---

    /// <summary>Esc가 먼저 지울 것이 남아 있는지. 다 지워진 획은 치지 않는다.</summary>
    private bool HasCanvasContent =>
        CurrentPage.Strokes.Any(s => s.VisibleClip is null || !s.VisibleClip.IsEmpty())
        || _vanishingLayer.Strokes.Count > 0
        || CurrentPage.Texts.Any(b => b.HasText);

    /// <summary>
    /// 지금 화면을 깨끗이 지운다 (E, 그리고 첫 번째 Esc). 배경·칠판 페이지·펜 설정은 그대로다.
    /// 획은 Ctrl+Z로 되살릴 수 있다.
    /// </summary>
    private void ClearCanvas()
    {
        var page = CurrentPage;
        if (page.Strokes.Count > 0)
        {
            page.Undo.Add(new UndoEntry(UndoKind.Clear, null, null, new List<Stroke>(page.Strokes)));
            page.Strokes.Clear();
        }
        ClearVanishing();
        DeleteAllTextBoxes();
        _permanentLayer.Refresh();
    }

    private void ClearVanishing()
    {
        _vanishingLayer.Strokes.Clear();
        _vanishingLayer.Refresh();
        _vanishingTimer?.Stop();
        _vanishingTimer = null;
    }

    private void Undo()
    {
        var page = CurrentPage;
        if (page.Undo.Count == 0) return;
        var entry = page.Undo[^1];
        page.Undo.RemoveAt(page.Undo.Count - 1);

        switch (entry.Kind)
        {
            case UndoKind.Add:
                page.Strokes.Remove(entry.Stroke!);
                break;
            case UndoKind.Erase:
                foreach (var (stroke, clip) in entry.ClipsBefore!) stroke.VisibleClip = clip;
                break;
            case UndoKind.Clear:
                page.Strokes.InsertRange(0, entry.Cleared!);
                break;
        }
        _permanentLayer.Refresh();
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

    private void ChangePenWidth(double delta)
    {
        _penWidth = Math.Clamp(_penWidth + delta, 1, 40);
        Settings.Shared.PenWidth = _penWidth;
        if (_isEraser) _hud.EraserDiameter = EraserWidth;
        RefreshHud();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // 글상자 안에서 굴리는 휠은 굵기를 바꾸지 않는다.
        if (FocusedTextBox is not null) return;
        ChangePenWidth(e.Delta / 120.0);
    }

    private void OnKeyUpHandler(object sender, KeyEventArgs e)
    {
        if (ResolveKey(e) == Key.Tab) _tabHeld = false;
    }

    /// <summary>
    /// 실제로 눌린 키. IME가 가로챈 경우(<c>Key.ImeProcessed</c>)와 Alt 조합
    /// (<c>Key.System</c>)에서는 원래 키가 다른 속성에 들어 있다. 글상자에서는 IME가 켜져 있으므로
    /// 한글 자판 상태의 Ctrl+R 같은 조합도 이 경로로 들어온다.
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
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        // 글상자에 입력 중이면 글자 키는 글상자 몫이다. Esc와 Ctrl 조합만 가로챈다.
        // (Ctrl+A/C/V/X는 글상자의 복사·붙여넣기로 그대로 둔다)
        var typing = FocusedTextBox;
        if (typing is not null)
        {
            if (key == Key.Escape)
            {
                SetTextMode(false, createAtCursor: false);
                e.Handled = true;
            }
            else if (ctrl && HandleCtrlShortcut(key, typing))
            {
                e.Handled = true;
            }
            return;
        }

        // Tab은 WPF가 포커스 이동에 쓰므로 반드시 가로챈다.
        if (key == Key.Tab)
        {
            _tabHeld = true;
            e.Handled = true;
            return;
        }

        if (ctrl)
        {
            if (HandleCtrlShortcut(key, null)) e.Handled = true;
            return;
        }

        e.Handled = true;
        switch (key)
        {
            case Key.Escape:
                // 첫 Esc는 화면을 지우고, 지울 것이 없을 때 Esc를 누르면 닫는다 —
                // 설명하다가 실수로 판서를 통째로 닫는 일을 막는다 (맥 v1.4).
                if (_isTextMode) SetTextMode(false, createAtCursor: false);
                else if (HasCanvasContent) ClearCanvas();
                else Close();
                break;

            case Key.V: // 지워지는 펜 on/off — 원본과 같은 키
                _isVanishing = !_isVanishing;
                RefreshHud();
                break;

            case Key.A: // 화살표 모드 — 그냥 드래그하면 끝점에 촉이 달린 화살표
                _isArrowMode = !_isArrowMode;
                RefreshHud();
                break;

            case Key.X: // 지우개 — 드래그한 자리만 지운다
                SetEraser(!_isEraser);
                break;

            case Key.H:
                _isHighlighter = !_isHighlighter;
                RefreshHud();
                break;

            case Key.E: // 전부 지우기
                ClearCanvas();
                break;

            case Key.W: // 화이트보드
                SetBoard(Board.White);
                break;

            case Key.K: // 칠판(검정)
                SetBoard(Board.Black);
                break;

            case Key.T: // 화면으로 돌아오기 (투명 또는 얼린 화면)
                BackToScreen();
                break;

            case Key.M: // 화면 위 메모
                StickyNoteRequested?.Invoke();
                break;

            case Key.Right when _board != Board.None:
                GoToBoardPage(_boardPageIndex + 1);
                break;

            case Key.Left when _board != Board.None:
                GoToBoardPage(_boardPageIndex - 1);
                break;

            case Key.OemOpenBrackets:
            case Key.Down:
            case Key.Left:
                ChangePenWidth(-1);
                break;

            case Key.OemCloseBrackets:
            case Key.Up:
            case Key.Right:
                ChangePenWidth(+1);
                break;

            default:
                var pressed = PenColors.FromKey(key.ToString());
                if (pressed is not null)
                {
                    SetColor(pressed.Value);
                    // Shift+색 = 그 색 형광펜 (맥 원본과 같음)
                    if (shift) _isHighlighter = true;
                    RefreshHud();
                }
                else
                {
                    e.Handled = false;
                }
                break;
        }
    }

    /// <summary>Ctrl 조합. 글상자에 입력 중일 때도 같은 키로 동작한다 (맥 v1.2의 ⌃ 단축키).</summary>
    private bool HandleCtrlShortcut(Key key, DrawTextBox? typing)
    {
        switch (key)
        {
            case Key.Z:
                Undo();
                return true;
            case Key.T:
                ToggleTextMode();
                return true;
            case Key.E:
                ClearCanvas();
                return true;
            case Key.W:
                SetBoard(Board.White);
                return true;
            case Key.K:
                SetBoard(Board.Black);
                return true;
        }

        var pressed = PenColors.FromKey(key.ToString());
        if (pressed is not null)
        {
            SetColor(pressed.Value);
            typing?.SetColor(pressed.Value.ToColor());
            RefreshHud();
            return true;
        }
        return false;
    }

    private void SetColor(PenColor color)
    {
        _color = color;
        // 색을 고른다 = 다시 그리겠다는 뜻 — 지우개를 끈다 (원본과 같음).
        if (_isEraser) SetEraser(false);
    }

    /// <summary>
    /// 진단 모드(<c>--snip-test</c>)에서 마우스 없이 확정 획을 하나 넣는다.
    /// 화면 캡처에 판서가 들어오는지 확인하는 용도 외에는 쓰지 않는다.
    /// </summary>
    internal void AddStrokeForDiagnostics(Stroke stroke)
    {
        CurrentPage.Strokes.Add(stroke);
        _permanentLayer.Refresh();
    }

    /// <summary>
    /// 진단 모드(<c>--draw-test</c>)에서 키·마우스 없이 기능을 차례로 눌러 본다.
    /// 반환값은 로그에 남길 지금 상태 한 줄.
    /// </summary>
    internal string Diag(string command, Point a = default, Point b = default, string text = "")
    {
        switch (command)
        {
            case "erase":
                _eraseBefore = new Dictionary<Stroke, Geometry?>();
                EraseSegment(a, b);
                if (_eraseBefore.Count > 0)
                    CurrentPage.Undo.Add(new UndoEntry(UndoKind.Erase, null, _eraseBefore, null));
                _eraseBefore = null;
                break;
            case "white": SetBoard(Board.White); break;
            case "black": SetBoard(Board.Black); break;
            case "screen": BackToScreen(); break;
            case "next": GoToBoardPage(_boardPageIndex + 1); break;
            case "prev": GoToBoardPage(_boardPageIndex - 1); break;
            case "undo": Undo(); break;
            case "escape":
                if (HasCanvasContent) ClearCanvas(); else Close();
                break;
            case "eraser": SetEraser(!_isEraser); _hud.EraserCenter = a; _hud.Refresh(); break;
            case "arrowmode": _isArrowMode = !_isArrowMode; RefreshHud(); break;
            case "text":
                SetTextMode(true, createAtCursor: false);
                AddTextBox(a);
                CurrentPage.Texts[^1].Box.Text = text;
                break;
            case "pen": SetTextMode(false, createAtCursor: false); break;
        }
        return $"{command}: board={_board} page={_boardPageIndex + 1}/{(_board == Board.None ? 1 : _boardPages.Count)} " +
               $"strokes={CurrentPage.Strokes.Count} erased={CurrentPage.Strokes.Count(s => s.VisibleClip is not null)} " +
               $"texts={CurrentPage.Texts.Count} undo={CurrentPage.Undo.Count} hasContent={HasCanvasContent} open={IsVisible}";
    }

    private void RefreshHud()
    {
        _hud.IsVanishing = _isVanishing;
        _hud.IsHighlighter = _isHighlighter;
        _hud.IsArrowMode = _isArrowMode;
        _hud.IsEraser = _isEraser;
        _hud.IsTextMode = _isTextMode;
        _hud.EraserDiameter = EraserWidth;
        _hud.PenWidth = _penWidth;
        _hud.PenColorValue = _color.ToColor();
        _hud.Lifetime = Settings.Shared.VanishingPenLifetime;
        _hud.PageIndex = _boardPageIndex;
        _hud.PageCount = _board == Board.None ? 1 : _boardPages.Count;
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

    // --- 페이지 · 실행취소 기록 ---

    private sealed class DrawPage
    {
        public List<Stroke> Strokes { get; } = new();
        public List<UndoEntry> Undo { get; } = new();
        public List<DrawTextBox> Texts { get; } = new();
    }

    private enum UndoKind { Add, Erase, Clear }

    private sealed record UndoEntry(
        UndoKind Kind,
        Stroke? Stroke,
        Dictionary<Stroke, Geometry?>? ClipsBefore,
        List<Stroke>? Cleared);
}
