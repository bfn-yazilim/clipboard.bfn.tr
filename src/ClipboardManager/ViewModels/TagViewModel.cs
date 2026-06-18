using ClipboardManager.Models;

namespace ClipboardManager.ViewModels;

public class TagViewModel : Mvvm.ObservableObject
{
    public int Id { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName => $"#{Name}";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    // "Tumu" ogesi gibi sistem ogeleri icin
    public bool IsSystem { get; set; }
}
