using System.Windows;
using System.Windows.Interop;
using ClipboardManager.Data;
using ClipboardManager.Interop;
using ClipboardManager.Services;
using ClipboardManager.ViewModels;

namespace ClipboardManager.Views;

/// <summary>
/// Ana pencere. Code-behind sadece Win32 mesaj dongusu (WndProc) ile ilgili isi yapar;
/// is mantiginin tamami MainViewModel'dedir.
///
/// Sorumluluklar:
///   - WM_CLIPBOARDUPDATE ve WM_HOTKEY mesajlarini ViewModel'e yonlendirir.
///   - ShowRequested / HideRequested olaylarini UI'a (goster/gizle) uygular.
///   - Aktif pencere (hedef) HWND'sini gosterimden once App.LastActiveWindow'a saklar.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IClipboardListener _clipboardListener;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly SettingsStore _settings;

    public MainWindow(MainViewModel viewModel,
                      IClipboardListener clipboardListener,
                      IGlobalHotkeyService hotkeyService,
                      SettingsStore settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _clipboardListener = clipboardListener;
        _hotkeyService = hotkeyService;
        _settings = settings;

        DataContext = _viewModel;

        // ViewModel olaylarini UI ile bagla
        _viewModel.ShowRequested += (_, _) => ShowFromTray();
        _viewModel.HideRequested += (_, _) => HideToTray();

        // Kaydedilmis pencere boyutunu geri yukle
        var s = _settings.Load();
        if (!double.IsNaN(s.WindowWidth)) Width = s.WindowWidth;
        if (!double.IsNaN(s.WindowHeight)) Height = s.WindowHeight;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Win32 mesaj dongusune baglan (WndProc hook)
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();

        var hwndSource = HwndSource.FromHwnd(helper.Handle);
        hwndSource?.AddHook(WndProc);

        // Dinleyici + hotkey'i baslat
        _viewModel.AttachToWindow(helper.Handle);
    }

    /// <summary>
    /// Win32 mesajlarini isler. Sadece bizi ilgilendiren iki mesaj vardir:
    ///   - WM_CLIPBOARDUPDATE: pano degisti (kullanici CTRL+C yapti)
    ///   - WM_HOTKEY: global kisa yol tetiklendi
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_CLIPBOARDUPDATE:
                _clipboardListener.OnClipboardUpdate();
                break;

            case NativeMethods.WM_HOTKEY:
                _hotkeyService.OnHotkey(wParam.ToInt32());
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Pencere gosterilmeden once kullanıcının o anki aktif penceresini kaydeder;
    /// auto-paste bu pencereye CTRL+V gonderecek.
    /// </summary>
    private void ShowFromTray()
    {
        // Hedef pencereyi kaydet (kendi penceremiz degilse)
        var fore = NativeMethods.GetForegroundWindow();
        if (fore != IntPtr.Zero && fore != new WindowInteropHelper(this).Handle)
            App.LastActiveWindow = fore;

        Show();
        Activate();
        Topmost = true;
        // Focus'u search'a ver
        SearchBox.Focus();
    }

    private void HideToTray()
    {
        // Pozisyon/boyutu kalici olarak sakla
        var s = _settings.Load();
        s.WindowWidth = ActualWidth;
        s.WindowHeight = ActualHeight;
        _settings.Save(s);

        Hide();
    }

    // ---- Title bar butonlari ----
    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var info =
            "Kısayol: Ctrl+Shift+V\n" +
            "Veri konumu: " + App.AppDataDir + "\n\n" +
            "Yakalanan içerikler otomatik kaydedilir.\n" +
            "Bir öğeye tıklayınca hedef pencereye yapıştırılır.";
        MessageBox.Show(info, "Hakkında", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Pencere X ile kapatilirsa gizle; uygulama arka planda calismaya devam etsin.
        // Tam cikis icin tray ikonu veya Alt+F4 kullanilabilir (basitlestirilmis hali: tamamen kapat).
        _viewModel.Dispose();
        base.OnClosing(e);
    }

    /// <summary>ESC ile pencereyi gizle.</summary>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            HideToTray();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
