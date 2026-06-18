namespace ClipboardManager.Models;

/// <summary>
/// ClipboardItem <-> Tag arasindaki coktan-coga bag (join entity).
/// </summary>
public class ItemTag
{
    public int ItemId { get; set; }
    public ClipboardItem Item { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
