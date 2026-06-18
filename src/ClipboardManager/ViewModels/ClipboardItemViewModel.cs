using System.Windows;
using System.Windows.Media;
using ClipboardManager.Models;

namespace ClipboardManager.ViewModels;

/// <summary>
/// Bir panolama ogesinin UI'daki temsili. Modelin (DB entity) birebir aynisi degil;
/// UI baglama ihtiyaclarina gore sekillenmis bir DTO'dur. Ornegin etiketler list olarak
/// burada tutulur; arama/siralama binding'leri buralardan beslenir.
/// </summary>
public class ClipboardItemViewModel : Mvvm.ObservableObject
{
    public int Id { get; set; }

    private string? _content;
    public string? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public ClipboardItemKind Kind { get; set; }

    private string? _title;
    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string? _imageFilePath;
    public string? ImageFilePath
    {
        get => _imageFilePath;
        set => SetProperty(ref _imageFilePath, value);
    }

    public DateTime CreatedAt { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsPinned { get; set; }
    public int UseCount { get; set; }

    private string? _groupName;
    public string? GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    public int? GroupId { get; set; }

    private List<string> _tags = new();
    public List<string> Tags
    {
        get => _tags;
        set
        {
            if (SetProperty(ref _tags, value))
                OnPropertyChanged(nameof(TagsDisplay));
        }
    }

    public string TagsDisplay => Tags.Count == 0 ? "—" : string.Join(" · ", Tags);

    /// <summary>UI yardimcilari:</summary>
    public string TypeIcon => Kind switch
    {
        ClipboardItemKind.Image => "🖼️",
        ClipboardItemKind.RichText => "📝",
        _ => "📄"
    };

    public Visibility ImageSectionVisibility => Kind == ClipboardItemKind.Image ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TextSectionVisibility => Kind != ClipboardItemKind.Image ? Visibility.Visible : Visibility.Collapsed;

    public string CreatedDisplay => CreatedAt.ToLocalTime().ToString("dd.MM HH:mm");

    public string Preview
    {
        get
        {
            if (Kind == ClipboardItemKind.Image)
                return System.IO.Path.GetFileName(ImageFilePath ?? "resim.png");
            var c = Content ?? "";
            var oneLine = c.Replace("\r", " ").Replace("\n", " ").Trim();
            return oneLine.Length > 90 ? oneLine[..90] + "…" : oneLine;
        }
    }
}
