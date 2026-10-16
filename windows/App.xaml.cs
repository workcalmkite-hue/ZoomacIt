using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using ZoomacItWin.Core;
using ZoomacItWin.Draw;
using ZoomacItWin.Overlay;
using Forms = System.Windows.Forms;

namespace ZoomacItWin;

/// <summary>
/// 트레이에 상주하면서 네 기능의 전역 단축키를 잡는 진입점.
/// (맥 원본의 <c>AppDelegate</c> + <c>StatusBarController</c>에 대응)
/// </summary>
public partial class App : Application
{
    private Forms.NotifyIcon? _tray;
    private HotkeyManager? _hotkeys;

    private DrawOverlayWindow? _drawOverlay;
    private BreakTimerWindow? _breakTimer;
    private StillZoomWindow? _stillZoom;
    private LiveZoomWindow? _liveZoom;
    private SnipWindow? _snip;

    /// <summary>화면 캡처 동안만 잠시 숨겨 둔 오버레이들 — 캡처가 끝나면 그대로 되돌린다.</summary>
    private readonly List<Window> _hiddenForSnip = new();
    private readonly MouseSpotlightController _spotlight = new();
    private readonly StickyNoteManager _stickyNotes = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Reset();
        Settings.Load();

        // 화면에 아무것도 띄우지 않고 그리기 결과만 PNG로 뽑는 진단 모드.
        if (e.Args.Length > 0 && e.Args[0] == "--render-test")
        {
            string outDir = e.Args.Length > 1 ? e.Args[1] : Environment.CurrentDirectory;
            RenderTest.Run(outDir);
            Shutdown();
            return;
        }

        // 드로잉 오버레이를 잠깐 띄워 창 자체를 캡처한 뒤 바로 닫는 진단 모드.
        if (e.Args.Length > 0 && e.Args[0] == "--capture-draw")
        {
            string outFile = e.Args.Length > 1
                ? e.Args[1]
                : Path.Combine(Environment.CurrentDirectory, "capture_draw.png");
            RenderTest.CaptureDrawOverlay(outFile, Shutdown);
            return;
        }

        // 라이브 줌 창을 잠깐 띄워 그 내용만 저장하는 진단 모드.
        if (e.Args.Length > 0 && e.Args[0] == "--capture-livezoom")
        {
            string outFile = e.Args.Length > 1
                ? e.Args[1]
                : Path.Combine(Environment.CurrentDirectory, "capture_livezoom.png");
            RenderTest.CaptureLiveZoom(outFile, Shutdown);
            return;
        }

        // 판서를 띄운 채 GDI 화면 캡처를 해서 판서가 캡처에 들어오는지 확인하는 진단 모드.
        if (e.Args.Length > 0 && e.Args[0] == "--snip-test")
        {
            string outFile = e.Args.Length > 1
                ? e.Args[1]
                : Path.Combine(Environment.CurrentDirectory, "snip_test.png");
            RenderTest.SnipTest(outFile, Shutdown);
            return;
        }

        SetUpTray();
        SetUpHotkeys();

        // 오버레이 하나에서 난 예외로 앱 전체가 죽지 않게 한다 — 수업 중이라면
        // 기능 하나가 안 되는 것보다 트레이 앱이 통째로 사라지는 게 훨씬 나쁘다.
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write($"처리되지 않은 예외: {args.Exception}");
            args.Handled = true;
        };
    }

    // --- 기능 토글 ---

    /// <summary>
    /// 화면을 덮는 모드(줌·라이브줌·드로잉)는 한 번에 하나만 떠 있어야 한다.
    /// 겹쳐 뜨면 어느 쪽이 입력을 받는지 알 수 없어진다.
    /// </summary>
    private void CloseFullScreenModes()
    {
        _drawOverlay?.Close();
        _stillZoom?.Close();
        _liveZoom?.Close();
        _snip?.Close();
    }

    private void ToggleDrawOverlay(bool? startVanishing = null,
        System.Windows.Media.ImageSource? background = null)
    {
        if (_drawOverlay is not null)
        {
            _drawOverlay.Close();
            return;
        }

        CloseFullScreenModes();
        _drawOverlay = new DrawOverlayWindow(startVanishing, background);
        _drawOverlay.Closed += (_, _) => _drawOverlay = null;
        _drawOverlay.Show();
    }

    private void ToggleStillZoom()
    {
        if (_stillZoom is not null)
        {
            _stillZoom.Close();
            return;
        }

        CloseFullScreenModes();
        var zoom = new StillZoomWindow();
        // 줌 화면에서 D를 누르면 그 확대 화면을 배경으로 깔고 바로 그리기로 넘어간다.
        zoom.DrawRequested += image => ToggleDrawOverlay(startVanishing: false, background: image);
        zoom.Closed += (_, _) => _stillZoom = null;
        _stillZoom = zoom;
        zoom.Show();
    }

    /// <summary>화면 캡처 — 드래그로 영역을 고르면 클립보드 + PNG 파일로 저장.</summary>
    private void StartSnip()
    {
        if (_snip is not null)
        {
            _snip.Close();
            return;
        }

        // 다른 전체화면 모드를 닫지 않는다. 그리기 오버레이를 닫아 버리면 애써 판서한
        // 것이 사라진 뒤에 찍혀서, 정작 찍고 싶었던 화면이 안 나온다.
        // 대신 지금 화면 그대로 한 장 찍은 다음(SnipWindow 생성자) 잠깐 숨겼다가,
        // 캡처가 끝나면 그리던 상태 그대로 되돌린다.
        var snip = new SnipWindow();
        snip.Finished += savedPath =>
        {
            if (savedPath is null || _tray is null) return;
            _tray.BalloonTipTitle = "캡처 완료";
            _tray.BalloonTipText = $"클립보드에 복사했고 파일로도 저장했습니다.\n{savedPath}";
            _tray.ShowBalloonTip(3000);
        };
        snip.Closed += (_, _) =>
        {
            _snip = null;
            RestoreAfterSnip();
        };
        _snip = snip;
        snip.Show();

        // 스닙 창을 띄운 "뒤에" 숨긴다 — 먼저 숨기면 그 찰나에 바탕화면이 번쩍인다.
        HideDuringSnip();
    }

    /// <summary>
    /// 캡처 화면과 겹치는 오버레이를 잠시 숨긴다. 닫는 것이 아니라 숨기는 것이므로
    /// 그려 둔 획과 확대 상태가 그대로 남는다.
    /// </summary>
    private void HideDuringSnip()
    {
        foreach (var window in new Window?[] { _drawOverlay, _stillZoom, _liveZoom })
        {
            if (window is null || !window.IsVisible) continue;

            window.Visibility = Visibility.Hidden;
            _hiddenForSnip.Add(window);

            // 캡처 도중 닫힌 창을 되살리려 하면 예외가 난다.
            window.Closed += (_, _) => _hiddenForSnip.Remove(window);
        }
    }

    /// <summary>숨겨 뒀던 오버레이를 되돌리고 키보드 포커스까지 돌려준다.</summary>
    private void RestoreAfterSnip()
    {
        // 되살리는 중에 닫히는 창이 있어도 열거가 깨지지 않도록 복사본을 돈다.
        foreach (var window in _hiddenForSnip.ToArray())
        {
            window.Visibility = Visibility.Visible;
            window.Topmost = true;
            window.Activate();
            System.Windows.Input.Keyboard.Focus(window);
        }

        _hiddenForSnip.Clear();
    }

    private void ToggleLiveZoom()
    {
        if (_liveZoom is not null)
        {
            _liveZoom.Close();
            return;
        }

        CloseFullScreenModes();
        _liveZoom = new LiveZoomWindow();
        _liveZoom.Closed += (_, _) => _liveZoom = null;
        _liveZoom.Show();
    }

    private void ToggleBreakTimer()
    {
        if (_breakTimer is not null)
        {
            _breakTimer.Close();
            return;
        }

        _breakTimer = new BreakTimerWindow();
        _breakTimer.Closed += (_, _) => _breakTimer = null;
        _breakTimer.Show();
    }

    // --- 트레이 ---

    private void SetUpTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add($"확대 (정지)  ({Settings.Shared.HotkeyStillZoom})", null,
            (_, _) => ToggleStillZoom());
        menu.Items.Add($"확대 (라이브)  ({Settings.Shared.HotkeyLiveZoom})", null,
            (_, _) => ToggleLiveZoom());
        menu.Items.Add($"화면 캡처  ({Settings.Shared.HotkeySnip})", null,
            (_, _) => StartSnip());
        menu.Items.Add($"그리기  ({Settings.Shared.HotkeyDraw})", null,
            (_, _) => ToggleDrawOverlay(startVanishing: false));
        menu.Items.Add($"지워지는 펜  ({Settings.Shared.HotkeyVanishingPen})", null,
            (_, _) => ToggleDrawOverlay(startVanishing: true));
        menu.Items.Add($"브레이크 타이머  ({Settings.Shared.HotkeyBreakTimer})", null,
            (_, _) => ToggleBreakTimer());
        menu.Items.Add($"마우스 스포트라이트  ({Settings.Shared.HotkeyMouseSpotlight})", null,
            (_, _) => _spotlight.Toggle());
        menu.Items.Add($"새 메모  ({Settings.Shared.HotkeyStickyNote})", null,
            (_, _) => _stickyNotes.SpawnNote());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("메모 전부 닫기", null, (_, _) => _stickyNotes.CloseAll());
        menu.Items.Add("단축키 도움말", null, (_, _) => ShowHelp());
        menu.Items.Add("설정 파일 열기", null, (_, _) => OpenSettingsFile());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Shutdown());

        _tray = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "ZoomacIt for Windows",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => _stickyNotes.SpawnNote();
    }

    /// <summary>
    /// 트레이 아이콘. exe에 박아 넣은 앱 아이콘(app.ico)을 그대로 쓰고,
    /// 어떤 이유로든 꺼내지 못하면 같은 모양을 코드로 그린다.
    /// </summary>
    private static Icon CreateTrayIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null)
            {
                var extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null) return extracted;
            }
        }
        catch
        {
            // 아래 폴백으로 계속
        }

        return DrawFallbackTrayIcon();
    }

    /// <summary>노란 메모지 위의 빨간 링 — app.ico와 같은 모양.</summary>
    private static Icon DrawFallbackTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var paper = new SolidBrush(Color.FromArgb(255, 255, 242, 161));
            g.FillRectangle(paper, 6, 6, 20, 20);
            using var ring = new Pen(Color.FromArgb(255, 255, 59, 48), 4);
            g.DrawEllipse(ring, 4, 4, 24, 24);
        }

        IntPtr hIcon = bitmap.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        return (Icon)temp.Clone();
    }

    private void ShowHelp()
    {
        var s = Settings.Shared;
        string message =
            $"""
             [전역 단축키]
             {s.HotkeyStillZoom} — 확대 (화면을 얼려서 확대)
             {s.HotkeyDraw} — 그리기 (일반 펜)
             {s.HotkeyBreakTimer} — 브레이크 타이머 위젯
             {s.HotkeySnip} — 화면 캡처 (드래그한 영역을 클립보드 + 파일로)
             {s.HotkeyLiveZoom} — 확대 (화면이 계속 움직이는 상태)
             {s.HotkeyVanishingPen} — 그리기 (지워지는 펜으로 바로 시작)
             {s.HotkeyMouseSpotlight} — 마우스 스포트라이트
             {s.HotkeyStickyNote} — 새 메모 띄우기

             [확대 화면 안에서]
             마우스 이동 — 확대 지점 이동    휠 / ↑ ↓ — 배율
             D — 확대한 화면 위에 바로 그리기 (정지 확대만)
             Esc 또는 오른쪽 클릭 — 닫기

             [그리기 오버레이 안에서]
             드래그 — 그리기          Shift — 직선
             Ctrl — 사각형            Ctrl+Shift — 화살표
             Tab(누른 채) — 타원      휠 — 굵기
             V — 지워짐/영구 전환     H — 형광펜
             R G B O Y P — 색상       E — 전부 지우기
             W / K / T — 흰 배경 / 검은 배경 / 투명
             Ctrl+Z — 실행취소        Esc — 닫기

             [브레이크 타이머 위젯]
             드래그 — 이동            휠 — 크기 조절
             −, 재생, ＋, × 버튼은 늘 보임 (도는 동안 옅게, 올리면 또렷)
             멈췄거나 시간이 다 된 뒤에는 올리지 않아도 또렷

             [메모]
             Ctrl+휠 — 글자 크기      Ctrl + / − / 0 — 글자 크기 조절/초기화
             오른쪽 아래 모서리 — 크기 조절

             페이드 시간 등 세부 설정은 트레이 → "설정 파일 열기".
             """;

        Forms.MessageBox.Show(message, "ZoomacIt for Windows — 단축키",
            Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
    }

    private static void OpenSettingsFile()
    {
        Settings.Save(); // 파일이 없으면 만들어 준다
        Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
    }

    // --- 단축키 ---

    private void SetUpHotkeys()
    {
        _hotkeys = new HotkeyManager();
        var s = Settings.Shared;
        var failed = new List<string>();

        void Bind(string gesture, string name, Action action)
        {
            bool ok = _hotkeys!.Register(gesture, action);
            Log.Write($"단축키 {gesture} ({name}) → {(ok ? "등록 성공" : "등록 실패 (다른 프로그램이 사용 중)")}");
            if (!ok) failed.Add($"{gesture} ({name})");
        }

        Bind(s.HotkeyStillZoom, "확대(정지)", ToggleStillZoom);
        Bind(s.HotkeyDraw, "그리기", () => ToggleDrawOverlay(startVanishing: false));
        Bind(s.HotkeyBreakTimer, "브레이크 타이머", ToggleBreakTimer);
        Bind(s.HotkeySnip, "화면 캡처", StartSnip);
        Bind(s.HotkeyLiveZoom, "확대(라이브)", ToggleLiveZoom);
        Bind(s.HotkeyVanishingPen, "지워지는 펜", () => ToggleDrawOverlay(startVanishing: true));
        Bind(s.HotkeyMouseSpotlight, "마우스 스포트라이트", () => _spotlight.Toggle());
        Bind(s.HotkeyStickyNote, "메모", () => _stickyNotes.SpawnNote());

        if (failed.Count > 0 && _tray is not null)
        {
            // 다른 앱이 이미 쓰는 단축키는 조용히 포기하고 알려만 준다 —
            // 수업 중에 앱 자체가 안 뜨는 것이 훨씬 나쁘다.
            _tray.BalloonTipTitle = "일부 단축키를 등록하지 못했습니다";
            _tray.BalloonTipText =
                $"{string.Join(", ", failed)} — 다른 프로그램이 쓰고 있습니다.\n" +
                "트레이 메뉴로는 그대로 쓸 수 있고, 설정 파일에서 단축키를 바꿀 수 있습니다.";
            _tray.ShowBalloonTip(6000);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _spotlight.Dispose();
        _stickyNotes.CloseAll();
        _drawOverlay?.Close();
        _breakTimer?.Close();
        _hotkeys?.Dispose();

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        Settings.Save();
        base.OnExit(e);
    }
}
