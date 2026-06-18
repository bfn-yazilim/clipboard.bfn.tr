using ClipboardManager.Models;

namespace ClipboardManager.Services;

/// <summary>
/// Pano degisikliklerini dinleyen servis arayuzu.
/// Pano degistiginde <see cref="ClipboardChanged"/> olayi tetiklenir.
/// </summary>
public interface IClipboardListener : IDisposable
{
    /// <summary>Dinlemeyi baslatir (pencere HWND vererek).</summary>
    void Start(IntPtr hwnd);

    /// <summary>WM_CLIPBOARDUPDATE mesaji islenirken cagrilmali (WndProc'tan).</summary>
    void OnClipboardUpdate();

    /// <summary>Pano degisti; yeni icerik yakalandi.</summary>
    event EventHandler<ClipboardCapturedEventArgs>? ClipboardChanged;

    bool IsListening { get; }
}

public class ClipboardCapturedEventArgs : EventArgs
{
    public required ClipboardItemKind Kind { get; init; }
    public string? Text { get; init; }
    public string? RichText { get; init; }
    /// <summary>Yakalanan goruntu (BitmapSource). Servis bunu dosyaya kaydeder.</summary>
    public System.Windows.Media.ImageSource? Image { get; init; }
    /// <summary>Goruntu ham baytlari (PNG).</summary>
    public byte[]? ImageBytes { get; init; }
}
