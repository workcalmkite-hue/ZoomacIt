using System.Windows.Input;
using System.Windows.Interop;

namespace ZoomacItWin.Core;

/// <summary>
/// 전역 단축키 등록기. 맥의 Carbon <c>RegisterEventHotKey</c>(원본 HotkeyManager)에
/// 대응한다. 메시지 전용 창 하나를 만들어 WM_HOTKEY를 받는다.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int HWND_MESSAGE = -3;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("ZoomacItWinHotkeyWindow")
        {
            ParentWindow = new IntPtr(HWND_MESSAGE),
            Width = 0,
            Height = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// "Ctrl+Shift+D" 같은 문자열로 단축키를 등록한다.
    /// 이미 다른 앱이 쓰고 있으면 false를 돌려준다 (앱은 계속 동작).
    /// </summary>
    public bool Register(string gesture, Action handler)
    {
        if (!TryParse(gesture, out uint modifiers, out uint vk)) return false;

        int id = _nextId++;
        // MOD_NOREPEAT: 키를 누르고 있어도 한 번만 발생 (토글이 떨리는 것 방지)
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk))
            return false;

        _handlers[id] = handler;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    /// <summary>"Ctrl+Shift+D" → (MOD_CONTROL|MOD_SHIFT, VK_D)</summary>
    public static bool TryParse(string gesture, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? keyToken = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "win" or "windows": modifiers |= NativeMethods.MOD_WIN; break;
                default: keyToken = part; break;
            }
        }

        if (keyToken is null || modifiers == 0) return false;

        // "1" 같은 숫자는 Key.D1로 바꿔줘야 파싱된다.
        if (keyToken.Length == 1 && char.IsDigit(keyToken[0])) keyToken = "D" + keyToken;

        if (!Enum.TryParse<Key>(keyToken, ignoreCase: true, out var key)) return false;

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
