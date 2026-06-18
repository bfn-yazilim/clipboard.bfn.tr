using System.Windows;

namespace ClipboardManager.Services;

public class DialogService : IDialogService
{
    public string? Prompt(string title, string message, string defaultValue = "")
    {
        // Basit input dialog: WPF'in yerlesik input dialog'u yoktur.
        // Minimal bir pencere ile sariyoruz. Pratikte ayri bir InputDialog view eklenebilir.
        var dlg = new Views.InputDialog(title, message, defaultValue)
        {
            Owner = Application.Current.MainWindow
        };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Info(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
