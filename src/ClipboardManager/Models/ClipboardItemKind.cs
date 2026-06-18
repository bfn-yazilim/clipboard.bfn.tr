namespace ClipboardManager.Models;

/// <summary>
/// Pano ogresinin tipi. Goruntuler dosya olarak saklanip yol tutulur;
/// metinler dogrudan DB'de tutulur.
/// </summary>
public enum ClipboardItemKind
{
    PlainText = 0,
    RichText = 1,
    Image = 2
}
