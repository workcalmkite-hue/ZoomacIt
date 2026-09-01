using System.Windows;
using System.Windows.Threading;

namespace ZoomacItWin.Core;

/// <summary>
/// 전역 저수준 마우스 훅. 맥의 <c>NSEvent.addGlobalMonitorForEvents</c>에 대응한다.
/// 이벤트를 소비하지 않고(항상 CallNextHookEx) 넘기므로, 훅이 켜져 있어도
/// 아래 앱의 클릭/스크롤은 정상 동작한다.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    /// <summary>클릭 지점 (물리 픽셀).</summary>
    public event Action<Point>? MouseDown;

    /// <summary>휠 델타 (한 칸 = ±120).</summary>
    public event Action<int>? MouseWheel;

    private IntPtr _hook = IntPtr.Zero;

    // GC가 델리게이트를 수거하면 훅이 죽으므로 반드시 필드로 붙잡아 둔다.
    private readonly NativeMethods.HookProc _proc;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public MouseHook()
    {
        _proc = HookCallback;
    }

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled) return;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var point = new Point(data.pt.X, data.pt.Y);
                // 훅 콜백 안에서는 아무것도 하지 않고 UI 스레드로 넘긴다
                // (훅이 느리면 시스템 전체 마우스가 끊긴다).
                _dispatcher.InvokeAsync(() => MouseDown?.Invoke(point));
            }
            else if (msg == NativeMethods.WM_MOUSEWHEEL)
            {
                var data = System.Runtime.InteropServices.Marshal
                    .PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int delta = (short)(data.mouseData >> 16);
                _dispatcher.InvokeAsync(() => MouseWheel?.Invoke(delta));
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
