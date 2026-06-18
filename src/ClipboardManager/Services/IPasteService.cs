using ClipboardManager.Models;

namespace ClipboardManager.Services;

/// <summary>
/// Yapistirma (Auto-Paste) servisi. Secilen ogeyi panoya koyar ve hedef
/// pencereye programatik CTRL+V gonderir.
/// </summary>
public interface IPasteService
{
    /// <summary>
    /// Ogeyi panoya yerlestirir ve hedef pencereye CTRL+V gonderir.
    /// </summary>
    /// <param name="item">Yapistirilacak oge.</param>
    /// <param name="targetWindow">Hedef pencere HWND. Sifir ise o anki foreground kullanilir.</param>
    void PasteItem(ClipboardItem item, IntPtr targetWindow);
}
