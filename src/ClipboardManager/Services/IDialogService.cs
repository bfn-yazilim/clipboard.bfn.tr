using ClipboardManager.Models;

namespace ClipboardManager.Services;

/// <summary>
/// UI ile ilgili dialog islemleri icin soyutlama (grup/etiket ekleme promptu vb.).
/// Bu katman ViewModel'in dogrudan MessageBox cagirmasini engeller, test edilebilirlik kazandirir.
/// </summary>
public interface IDialogService
{
    string? Prompt(string title, string message, string defaultValue = "");
    bool Confirm(string title, string message);
    void Info(string title, string message);
}
