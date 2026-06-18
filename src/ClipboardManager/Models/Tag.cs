using System.ComponentModel.DataAnnotations;

namespace ClipboardManager.Models;

/// <summary>
/// Etiket (Tag). Coktan-coga iliskiyi "ItemTag" ara tablosuyla yonetiyoruz,
/// boylece bir ogenin birden cok etiketi, bir etiketin de birden cok ogesi olabilir.
/// </summary>
public class Tag
{
    public int Id { get; set; }

    [Required, MaxLength(48)]
    public string Name { get; set; } = string.Empty;

    /// <summary>UI'da rengi. Hex (#RRGGBB).</summary>
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<ItemTag> ItemTags { get; set; } = new();
}
