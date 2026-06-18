using System.ComponentModel.DataAnnotations;

namespace ClipboardManager.Models;

/// <summary>
/// Yakalanan tek bir pano ogesi. Metin icerigi dogrudan "Content" alaninda saklanir.
/// Goruntuler dosya sisteminde (AppData/images) tutulur ve dosya yolu "ImageFilePath" alaninda saklanir;
/// boylece DB sismez ve geri yukleme hizli olur.
/// </summary>
public class ClipboardItem
{
    public int Id { get; set; }

    /// <summary>Metin icerigi (PlainText ve RichText icin). Goruntulerde null.</summary>
    public string? Content { get; set; }

    /// <summary>RTF / HTML gibi zengin metin (RichText tipinde).</summary>
    public string? RichContent { get; set; }

    /// <summary>Goruntu dosyasinin yolu (Image tipinde).</summary>
    public string? ImageFilePath { get; set; }

    /// <summary>Ogrenin tipi.</summary>
    public ClipboardItemKind Kind { get; set; } = ClipboardItemKind.PlainText;

    /// <summary>UI'da onizlemede gosterilecek kisa baslik (ilk satir / dosya adi).</summary>
    [MaxLength(128)]
    public string? Title { get; set; }

    /// <summary>Oge boyutu (karakter veya bayt). Bilgi amaclidir.</summary>
    public long Size { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsPinned { get; set; }

    public int OrderIndex { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    /// <summary>Kullanim sayaci - sik kullanilanlar one cikarilabilir.</summary>
    public int UseCount { get; set; }

    // --- FK / Navigation ---
    public int? GroupId { get; set; }
    public Group? Group { get; set; }

    public List<ItemTag> ItemTags { get; set; } = new();

    // Yardimci: etiket isimlerini virgulle birlestirilmis halde doner (UI binding)
    [System.Text.Json.Serialization.JsonIgnore]
    public string TagsDisplay =>
        ItemTags.Count == 0 ? string.Empty : string.Join(", ", ItemTags.Select(it => it.Tag.Name));
}
