using System.ComponentModel.DataAnnotations;

namespace ClipboardManager.Models;

/// <summary>
/// Grup / Kategori. Ornek: "Is", "Kisisel", "Kod", "Resimler".
/// Bir grubun cok sayida panolama ogesi olabilir (1:N).
/// </summary>
public class Group
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>UI'da gosterilecek emoji/ikon karakteri. Orn: "💼", "📁".</summary>
    public string? Icon { get; set; }

    /// <summary>Siralama icin kullanilir.</summary>
    public int SortOrder { get; set; }

    /// <summary>Sistem tarafindan olusturulan (silinemeyen) grup. Orn: "Tumu".</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: bu gruba ait oge listesi
    public List<ClipboardItem> Items { get; set; } = new();
}
