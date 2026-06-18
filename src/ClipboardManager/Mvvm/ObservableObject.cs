using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipboardManager.Mvvm;

/// <summary>
/// INotifyPropertyChanged altyapisi. Modern MVVM (CommunityToolkit.Mvvm) kullanilsa da,
/// temel siniflari elle yazmak mimariyi seffaf kilar ve bagimliliklari azaltir.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Property degerini set eder; degisirse PropertyChanged tetikler.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
