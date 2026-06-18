using ClipboardManager.Models;

namespace ClipboardManager.ViewModels;

/// <summary>
/// Sidebar'da listelenen grubun UI temsili. "Tumu" sistem grubu Id=-1 gibi ozel
/// degerler de tasiyabilir (UI filtrelemesi icin).
/// </summary>
public class GroupViewModel : Mvvm.ObservableObject
{
    public int Id { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string? _icon;
    public string? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool IsSystem { get; set; }

    /// <summary>Bu gruptaki oge sayisi (badge icin).</summary>
    public int Count { get; set; }

    /// <summary>UI'da secili mi? (tek secili).</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
