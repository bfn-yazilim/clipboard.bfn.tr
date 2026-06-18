using System.IO;
using System.Windows;
using ClipboardManager.Data;
using ClipboardManager.Services;
using ClipboardManager.ViewModels;
using ClipboardManager.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClipboardManager;

/// <summary>
/// Uygulama giris noktasi. DI (Dependency Injection) container'i burada kurulur,
/// boylece servisler, ViewModel'ler ve DB baglamlari test edilebilir ve gevsek bagli olur.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IDbContextFactory<AppDbContext> DbFactory { get; private set; } = null!;
    public static IGlobalHotkeyService HotkeyService { get; private set; } = null!;

    /// <summary>
    /// Kullanicinin uygulamayi acmadan once aktif olan (hedef) pencere HWND'si.
    /// Auto-paste sirasinda CTRL+V bu pencereye gonderilir.
    /// </summary>
    public static IntPtr LastActiveWindow { get; set; }

    private const string DbFileName = "clipboard.db";
    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Uygulama veri klasoru + resim klasoru olustur
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(Path.Combine(AppDataDir, "images"));

        var services = new ServiceCollection();

        // --- Veritabani ---
        // Repository DbContextFactory bekledigi icin factory olarak kaydediyoruz;
        // boylece her sorgu kisa omurlu bir context kullanir (UI thread'i bloklamaz, thread-safe).
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(AppDataDir, DbFileName)}"));

        // Ayar deposu (JSON)
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<ClipboardRepository>();

        // --- Servisler ---
        services.AddSingleton<IClipboardListener, ClipboardListener>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IPasteService, PasteService>();
        services.AddSingleton<IDialogService, DialogService>();

        // --- ViewModels ---
        services.AddSingleton<MainViewModel>();

        // --- Pencereler ---
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
        DbFactory = Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        HotkeyService = Services.GetRequiredService<IGlobalHotkeyService>();

        // DB mig.
        using (var scope = Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();
            db.Database.EnsureCreated();
            DbSeeder.SeedDefaults(db);
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        // Ilk acilista gorev cubugunda gozukmesi istenmez; kullanicinin hotkey ile acmasi beklenir.
        // (Program ilk kurulumda bir kez gosterilir ki kullanici gorebilsin.)
        mainWindow.Show();
    }
}
