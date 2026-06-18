namespace ClipboardManager.Services;

/// <summary>
/// Sistem geneli (global) kisa yol (hotkey) servisi. RegisterHotKey API'sini kullanir.
/// Arka planda calisirken bile kullanicinin tanimli kisa yolla uygulamayi acmasini saglar.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    bool Register(IntPtr hwnd, int id, uint modifiers, uint vk);
    void Unregister(int id);
    /// <summary>WM_HOTKEY mesaji WndProc'tan geliyorsa cagrilmali.</summary>
    void OnHotkey(int id);
    event EventHandler<int>? HotkeyPressed;
}
