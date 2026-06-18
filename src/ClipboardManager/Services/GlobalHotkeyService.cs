using ClipboardManager.Interop;

namespace ClipboardManager.Services;

public class GlobalHotkeyService : IGlobalHotkeyService
{
    private IntPtr _hwnd;

    public event EventHandler<int>? HotkeyPressed;

    public bool Register(IntPtr hwnd, int id, uint modifiers, uint vk)
    {
        _hwnd = hwnd;
        // MOD_NOREPEAT: tus basilikeniyken tekrar tetiklenmesini engeller
        return NativeMethods.RegisterHotKey(hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
    }

    public void Unregister(int id)
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.UnregisterHotKey(_hwnd, id);
    }

    public void OnHotkey(int id) => HotkeyPressed?.Invoke(this, id);

    public void Dispose()
    {
        // Unregister cagrisi cagiran tarafindan yapilmali (id listesi tutmadigimiz icin)
    }
}
