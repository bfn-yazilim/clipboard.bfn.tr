using System.Windows;
using System.Windows.Input;

namespace ClipboardManager.Views;

public partial class InputDialog : Window
{
    public string Value { get; private set; } = string.Empty;

    public InputDialog(string title, string message, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = message;
        InputBox.Text = defaultValue;
        Value = defaultValue;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Confirm();
    }

    private void Confirm()
    {
        Value = InputBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
