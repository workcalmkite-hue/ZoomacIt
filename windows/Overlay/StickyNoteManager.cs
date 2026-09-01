using System.Windows;
using ZoomacItWin.Core;

namespace ZoomacItWin.Overlay;

/// <summary>
/// 메모를 만들고 추적한다. 노트마다 독립된 최상단 창이라, 드로잉 오버레이를 닫아도
/// 살아남고 × 를 눌러야 사라진다. (맥 원본 <c>StickyNoteManager</c>에 대응)
/// </summary>
internal sealed class StickyNoteManager
{
    private readonly List<StickyNoteWindow> _notes = new();

    /// <summary>연속으로 만든 노트가 정확히 겹치지 않도록 하는 계단 카운터.</summary>
    private int _spawnCount;

    /// <summary>
    /// 새 노트는 사용자가 마지막으로 고른 크기/글자 크기를 물려받는다.
    /// 원본은 앱 실행 중에만 기억했지만, 여기서는 설정 파일에 저장해
    /// 다음 수업에도 같은 크기로 뜨게 한다.
    /// </summary>
    private Size _preferredSize = StickyNoteMetrics.ClampedSize(
        new Size(Settings.Shared.StickyNoteWidth, Settings.Shared.StickyNoteHeight));

    private double _preferredFontSize =
        StickyNoteMetrics.ClampedFontSize(Settings.Shared.StickyNoteFontSize);

    public void SpawnNote()
    {
        var screen = ScreenHelper.ScreenContainingMouse();
        double scale = ScreenHelper.ScaleOf(screen);
        var work = screen.WorkingAreaPx();

        var size = _preferredSize;
        double cascade = (_spawnCount % 8) * 28;
        _spawnCount++;

        // 화면 정중앙에서 시작해 노트가 늘어날수록 오른쪽 아래로 조금씩 밀린다.
        double centerXPx = work.X + work.Width / 2;
        double centerYPx = work.Y + work.Height / 2;
        var originPx = new Point(
            centerXPx - size.Width * scale / 2 + cascade * scale,
            centerYPx - size.Height * scale / 2 + cascade * scale);

        var note = new StickyNoteWindow(originPx, size, _preferredFontSize);
        note.CloseRequested += Close;
        note.SizeChanged2 += newSize =>
        {
            _preferredSize = StickyNoteMetrics.ClampedSize(newSize);
            Settings.Shared.StickyNoteWidth = _preferredSize.Width;
            Settings.Shared.StickyNoteHeight = _preferredSize.Height;
            Settings.Save();
        };
        note.FontSizeChanged += newFont =>
        {
            _preferredFontSize = newFont;
            Settings.Shared.StickyNoteFontSize = newFont;
            Settings.Save();
        };

        note.Show();
        _notes.Add(note);
    }

    private void Close(StickyNoteWindow note)
    {
        _notes.Remove(note);
        note.Close();
    }

    public void CloseAll()
    {
        foreach (var note in _notes.ToList()) note.Close();
        _notes.Clear();
    }

    public int Count => _notes.Count;
}
