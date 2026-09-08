using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZoomacItWin.Core;

/// <summary>
/// 맥 원본의 <c>Settings</c>(UserDefaults)에 대응. %APPDATA%\ZoomacItWin\settings.json에
/// 저장된다. 값을 바꾸고 싶으면 이 파일을 메모장으로 열어 고친 뒤 앱을 재시작하면 된다
/// (트레이 메뉴 → "설정 파일 열기").
/// </summary>
internal sealed class Settings
{
    /// <summary>
    /// 설정 파일 형식 버전. 단축키 기본값을 바꿀 때 올린다 — 예전 버전으로 저장된
    /// 파일을 열면 단축키만 새 기본값으로 갈아끼운다(나머지 값은 보존).
    /// </summary>
    public int SettingsVersion { get; set; }

    /// <summary>현재 코드가 기대하는 버전. 3 = 캡처를 Ctrl+4로, 라이브 확대를 Ctrl+8로.</summary>
    public const int CurrentVersion = 3;

    // --- 단축키 (ZoomIt과 같은 배치로 통일) ---
    public string HotkeyStillZoom { get; set; } = "Ctrl+1";
    public string HotkeyDraw { get; set; } = "Ctrl+2";
    public string HotkeyBreakTimer { get; set; } = "Ctrl+3";
    public string HotkeySnip { get; set; } = "Ctrl+4";
    public string HotkeyLiveZoom { get; set; } = "Ctrl+8";
    public string HotkeyVanishingPen { get; set; } = "Ctrl+Shift+D";
    public string HotkeyMouseSpotlight { get; set; } = "Ctrl+Shift+S";
    public string HotkeyStickyNote { get; set; } = "Ctrl+M";

    /// <summary>캡처 이미지를 저장할 폴더. 비워두면 "내 그림  ZoomacIt".</summary>
    public string SnipFolder { get; set; } = "";

    // --- 줌 ---
    /// <summary>줌을 켰을 때의 초기 배율 (1.0 ~ 8.0).</summary>
    public double DefaultZoomLevel { get; set; } = 2.0;

    // --- 지워지는 펜 ---
    /// <summary>획이 완전히 사라지기까지의 시간(초). 원본 기본값과 동일.</summary>
    public double VanishingPenLifetime { get; set; } = 3.0;
    public double PenWidth { get; set; } = 5.0;
    /// <summary>드로잉 오버레이를 열었을 때 "지워지는 펜"이 처음부터 켜져 있는지.</summary>
    public bool VanishingPenOnByDefault { get; set; } = true;

    // --- 브레이크 타이머 위젯 ---
    public int BreakTimerDefaultDurationSeconds { get; set; } = 600;
    public double BreakTimerDiameter { get; set; } = 110;
    public double? BreakTimerPositionXPx { get; set; }
    public double? BreakTimerPositionYPx { get; set; }
    public bool BreakTimerShowElapsed { get; set; } = true;
    public bool BreakTimerPlaySound { get; set; } = false;
    /// <summary>진행 링 색상 (#RRGGBB).</summary>
    public string BreakTimerColor { get; set; } = "#FF3B30";
    public double BreakTimerOpacity { get; set; } = 1.0;

    // --- 마우스 스포트라이트 ---
    public double MouseSpotlightRadius { get; set; } = 150;
    /// <summary>스포트라이트 바깥 어두움 (0.0 ~ 1.0).</summary>
    public double MouseSpotlightDim { get; set; } = 0.5;

    // --- 스티키 노트 ---
    public double StickyNoteWidth { get; set; } = 240;
    public double StickyNoteHeight { get; set; } = 180;
    public double StickyNoteFontSize { get; set; } = 15;

    // ---------------------------------------------------------------

    [JsonIgnore]
    public static Settings Shared { get; private set; } = new();

    [JsonIgnore]
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ZoomacItWin", "settings.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<Settings>(json);
                if (loaded is not null) Shared = loaded;
            }
        }
        catch
        {
            // 파일이 깨졌으면 기본값으로 계속 간다 — 수업 중에 앱이 안 뜨는 게 최악이다.
            Shared = new Settings();
        }

        MigrateIfNeeded();
        Save();
    }

    /// <summary>
    /// 예전 버전 설정 파일이면 단축키만 새 기본값으로 갈아끼운다. 지워지는 펜
    /// 시간이나 위젯 위치처럼 사용자가 맞춰둔 값은 건드리지 않는다.
    /// </summary>
    private static void MigrateIfNeeded()
    {
        if (Shared.SettingsVersion >= CurrentVersion) return;

        var defaults = new Settings();
        Shared.HotkeyStillZoom = defaults.HotkeyStillZoom;
        Shared.HotkeyDraw = defaults.HotkeyDraw;
        Shared.HotkeyBreakTimer = defaults.HotkeyBreakTimer;
        Shared.HotkeySnip = defaults.HotkeySnip;
        Shared.HotkeyLiveZoom = defaults.HotkeyLiveZoom;
        Shared.HotkeyVanishingPen = defaults.HotkeyVanishingPen;
        Shared.HotkeyMouseSpotlight = defaults.HotkeyMouseSpotlight;
        Shared.HotkeyStickyNote = defaults.HotkeyStickyNote;
        Shared.SettingsVersion = CurrentVersion;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Shared,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 저장 실패는 조용히 무시 (다음 실행 때 기본값으로 뜰 뿐)
        }
    }
}
