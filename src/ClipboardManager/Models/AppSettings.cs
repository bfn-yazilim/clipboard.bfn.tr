namespace ClipboardManager.Models;

/// <summary>
/// Uygulama genelinde kullanilan ayarlar (JSON ile saklanir).
/// Kullanici tercihleri VT'de degil ayar dosyasinda tutulur.
/// </summary>
public class AppSettings
{
    public HotkeyBinding ShowWindowHotkey { get; set; } = new()
    {
        Key = "V",
        Modifiers = "Ctrl+Shift"
    };

    /// <summary>Pano dinleme aktif mi?</summary>
    public bool ClipboardListeningEnabled { get; set; } = true;

    /// <summary>Yinelenen metinleri kaydetme.</summary>
    public bool SkipDuplicates { get; set; } = true;

    /// <summary>Pencere konumu / boyutu (hatirlanmasi icin).</summary>
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 760;
    public double WindowHeight { get; set; } = 560;

    /// <summary>Maksimum kayit sayisi (eski kayitlar silinir). 0 = sinirsiz.</summary>
    public int MaxItems { get; set; } = 1000;

    /// <summary>Otomatik yapistir (tiklayinca hedef pencereye CTRL+V gonder).</summary>
    public bool AutoPasteOnSelect { get; set; } = true;
}

public class HotkeyBinding
{
    public string Key { get; set; } = "V";
    /// <summary>"Ctrl+Shift", "Alt", "Ctrl+Alt" gibi. Bos olabilir.</summary>
    public string Modifiers { get; set; } = "Ctrl+Shift";
}
