using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ClipboardManager.Data;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.EntityFrameworkCore;

namespace ClipboardManager.ViewModels;

/// <summary>
/// Ana pencere ViewModel'i. Pano dinleme, grup filtreleme, arama, etiket yonetimi
/// ve yapistirma (auto-paste) mantigini koordine eder.
/// </summary>
public class MainViewModel : Mvvm.ObservableObject, IDisposable
{
    private readonly ClipboardRepository _repo;
    private readonly IClipboardListener _listener;
    private readonly IPasteService _pasteService;
    private readonly IDialogService _dialogs;
    private readonly SettingsStore _settings;

    // Sinirli sayida hotkey id
    private const int HotkeyShowWindow = 9001;

    // Secili grup: null/0 = "Tumu"
    public const int AllGroupsId = 0;

    public ObservableCollection<GroupViewModel> Groups { get; } = new();
    public ObservableCollection<ClipboardItemViewModel> Items { get; } = new();
    public ObservableCollection<string> AllTags { get; } = new();

    private GroupViewModel? _selectedGroup;
    public GroupViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                _ = LoadItemsAsync();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                _ = DebouncedSearchAsync(_debounceCts.Token);
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private CancellationTokenSource? _debounceCts;

    public MainViewModel(
        ClipboardRepository repo,
        IClipboardListener listener,
        IPasteService pasteService,
        IDialogService dialogs,
        SettingsStore settings)
    {
        _repo = repo;
        _listener = listener;
        _pasteService = pasteService;
        _dialogs = dialogs;
        _settings = settings;

        SelectGroupCommand = new Mvvm.RelayCommand<GroupViewModel>(g =>
        {
            SelectedGroup = g;
            foreach (var x in Groups) x.IsSelected = x == g;
        });

        PasteItemCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(PasteItemAsync);
        AssignGroupCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(AssignGroupAsync);
        AddTagCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(AddTagAsync);
        DeleteItemCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(DeleteItemAsync);
        ToggleFavoriteCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(ToggleFavoriteAsync);
        CopyToClipboardCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(CopyOnlyAsync);

        AddGroupCommand = new Mvvm.AsyncRelayCommand(AddGroupAsync);
        AddTagGlobalCommand = new Mvvm.AsyncRelayCommand(AddTagGlobalAsync);
        ClearSearchCommand = new Mvvm.RelayCommand(() => SearchText = string.Empty);
        RefreshCommand = new Mvvm.AsyncRelayCommand(() => InitializeAsync());

        // Pano dinleyiciyi bagla
        _listener.ClipboardChanged += OnClipboardChanged;

        // Ilk yukleme
        _ = InitializeAsync();
    }

    /// <summary>Window HWND set edilince cagrilmali (Hotkey + Clipboard listener baslat).</summary>
    public void AttachToWindow(IntPtr hwnd)
    {
        _listener.Start(hwnd);
        try
        {
            var s = _settings.Load();
            var mods = ClipboardManager.Interop.HotkeyParser.ParseModifiers(s.ShowWindowHotkey.Modifiers);
            var vk = ClipboardManager.Interop.HotkeyParser.ParseVirtualKey(s.ShowWindowHotkey.Key);
            if (App.HotkeyService.Register(hwnd, HotkeyShowWindow, mods, vk))
            {
                App.HotkeyService.HotkeyPressed += OnHotkeyPressed;
            }
        }
        catch
        {
            // Cakisma olabilir; sessiz gec.
        }
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        if (id == HotkeyShowWindow)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Pencerenin gosterilmesi istendiginde firlatilir (Window tarafindan dinlenir).</summary>
    public event EventHandler? ShowRequested;

    public Mvvm.RelayCommand<GroupViewModel> SelectGroupCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> PasteItemCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> AssignGroupCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> AddTagCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> DeleteItemCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> ToggleFavoriteCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> CopyToClipboardCommand { get; }

    public Mvvm.AsyncRelayCommand AddGroupCommand { get; }
    public Mvvm.AsyncRelayCommand AddTagGlobalCommand { get; }
    public Mvvm.RelayCommand ClearSearchCommand { get; }
    public Mvvm.AsyncRelayCommand RefreshCommand { get; }

    public string SelectedGroupName => SelectedGroup?.Name ?? "Tümü";

    // ---- Yükleme ----
    public async Task InitializeAsync()
    {
        await LoadGroupsAsync();
        await LoadTagsAsync();
        await LoadItemsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        var groups = await _repo.GetGroupsAsync();
        Groups.Clear();
        Groups.Add(new GroupViewModel { Id = AllGroupsId, Name = "Tümü", Icon = "🗂️", IsSystem = true, IsSelected = true });
        foreach (var g in groups.Where(g => !g.IsSystem))
            Groups.Add(new GroupViewModel { Id = g.Id, Name = g.Name, Icon = g.Icon });
        SelectedGroup = Groups[0];
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _repo.GetTagsAsync();
        AllTags.Clear();
        foreach (var t in tags) AllTags.Add(t.Name);
    }

    private async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var groupId = SelectedGroup?.Id;
            int? filterId = groupId is null or AllGroupsId ? null : groupId;
            var items = await _repo.GetItemsAsync(filterId, SearchText);

            Items.Clear();
            foreach (var i in items)
                Items.Add(MapToViewModel(i));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static ClipboardItemViewModel MapToViewModel(ClipboardItem i) => new()
    {
        Id = i.Id,
        Content = i.Content,
        Title = i.Title,
        ImageFilePath = i.ImageFilePath,
        Kind = i.Kind,
        CreatedAt = i.CreatedAt,
        IsFavorite = i.IsFavorite,
        IsPinned = i.IsPinned,
        UseCount = i.UseCount,
        GroupId = i.GroupId,
        GroupName = i.Group?.Name,
        Tags = i.ItemTags.Select(it => it.Tag.Name).ToList()
    };

    // ---- Arama (debounce'lu) ----
    private async Task DebouncedSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);
            await LoadItemsAsync();
        }
        catch (TaskCanceledException) { }
    }

    // ---- Pano olayi ----
    private void OnClipboardChanged(object? sender, ClipboardCapturedEventArgs e)
    {
        var app = Application.Current;
        if (app == null) return;

        // UI thread'te calistirildigindan emin ol
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => OnClipboardChanged(sender, e));
            return;
        }

        _ = Task.Run(() => SaveCapturedAsync(e)).ContinueWith(t =>
        {
            if (t.IsFaulted) { /* hata loglanabilir */ }
        });
    }

    private async Task SaveCapturedAsync(ClipboardCapturedEventArgs e)
    {
        var s = _settings.Load();
        if (!s.ClipboardListeningEnabled) return;

        var item = new ClipboardItem { CreatedAt = DateTime.UtcNow };

        switch (e.Kind)
        {
            case ClipboardItemKind.Image:
                if (e.ImageBytes == null) return;
                var fileName = $"img_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png";
                var path = Path.Combine(App.AppDataDir, "images", fileName);
                await File.WriteAllBytesAsync(path, e.ImageBytes);
                item.ImageFilePath = path;
                item.Title = fileName;
                item.Size = e.ImageBytes.Length;
                item.Kind = ClipboardItemKind.Image;
                break;

            case ClipboardItemKind.RichText:
                item.Content = e.Text;
                item.RichContent = e.RichText;
                item.Kind = ClipboardItemKind.RichText;
                item.Title = MakeTitle(e.Text);
                item.Size = e.Text?.Length ?? 0;
                break;

            default:
                item.Content = e.Text;
                item.Kind = ClipboardItemKind.PlainText;
                item.Title = MakeTitle(e.Text);
                item.Size = e.Text?.Length ?? 0;
                break;
        }

        // Yinelenenleri atlama
        if (s.SkipDuplicates && await IsDuplicateAsync(item))
            return;

        await _repo.AddItemAsync(item);
        await _repo.TrimToAsync(s.MaxItems);

        // UI'i yenile (sadece aktif gorunum yuklenir)
        await Application.Current.Dispatcher.BeginInvoke(async () => await LoadItemsAsync());
    }

    private async Task<bool> IsDuplicateAsync(ClipboardItem item)
    {
        // Son kayitla karsilastir
        await using var db = await App.DbFactory.CreateDbContextAsync();
        var last = await db.Items.OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();
        if (last == null) return false;
        if (item.Kind == ClipboardItemKind.Image)
            return last.Kind == ClipboardItemKind.Image && last.Size == item.Size;
        return last.Content == item.Content;
    }

    private static string MakeTitle(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "(boş)";
        var firstLine = text.Split('\n', '\r').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
        if (firstLine.Length > 48) firstLine = firstLine[..48] + "…";
        return firstLine;
    }

    // ---- Komutlar ----
    private async Task PasteItemAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var targetWindow = App.LastActiveWindow; // kapatma oncesinde saklanan HWND
        HideRequested?.Invoke(this, EventArgs.Empty);

        await Task.Delay(80); // UI gizlenirken kisa bekleme

        var item = new ClipboardItem
        {
            Kind = vm.Kind,
            Content = vm.Content,
            ImageFilePath = vm.ImageFilePath
        };
        _pasteService.PasteItem(item, targetWindow);

        // Kullanim sayacini guncelle
        await using var db = await App.DbFactory.CreateDbContextAsync();
        var entity = await db.Items.FindAsync(vm.Id);
        if (entity != null)
        {
            entity.UseCount++;
            entity.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public event EventHandler? HideRequested;

    private async Task CopyOnlyAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var item = new ClipboardItem
        {
            Kind = vm.Kind,
            Content = vm.Content,
            ImageFilePath = vm.ImageFilePath
        };

        if (_listener is ClipboardListener cl)
            cl.SuppressNext();

        // Panoya koy (PasteService ile ayni mantik ama SendInput yok)
        if (item.Kind == ClipboardItemKind.Image && !string.IsNullOrEmpty(item.ImageFilePath))
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(item.ImageFilePath);
            bmp.EndInit();
            bmp.Freeze();
            System.Windows.Clipboard.SetImage(bmp);
        }
        else if (!string.IsNullOrEmpty(item.Content))
        {
            System.Windows.Clipboard.SetText(item.Content);
        }
        await Task.CompletedTask;
    }

    private async Task AssignGroupAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var groups = await _repo.GetGroupsAsync();
        var options = "Grup seç (id)\n" + string.Join("\n", groups.Select(g => $"{g.Id} - {g.Name}"));
        var input = _dialogs.Prompt("Gruba Ata", options, vm.GroupId?.ToString() ?? "");
        if (input == null) return;
        if (int.TryParse(input, out var gid))
        {
            await _repo.AssignGroupAsync(vm.Id, gid);
            await LoadItemsAsync();
        }
    }

    private async Task AddTagAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var tag = _dialogs.Prompt("Etiket Ekle", "Etiket adı (virgülle birden fazla):", "");
        if (string.IsNullOrWhiteSpace(tag)) return;
        foreach (var t in tag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            await _repo.AssignTagAsync(vm.Id, t);

        await LoadTagsAsync();
        await LoadItemsAsync();
    }

    private async Task DeleteItemAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        if (!_dialogs.Confirm("Sil", "Bu öğe silinsin mi?")) return;
        await _repo.DeleteItemAsync(vm.Id);
        await LoadItemsAsync();
    }

    private async Task ToggleFavoriteAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        await using var db = await App.DbFactory.CreateDbContextAsync();
        var e = await db.Items.FindAsync(vm.Id);
        if (e != null)
        {
            e.IsFavorite = !e.IsFavorite;
            await db.SaveChangesAsync();
            vm.IsFavorite = e.IsFavorite;
        }
        await Task.CompletedTask;
    }

    private async Task AddGroupAsync()
    {
        var name = _dialogs.Prompt("Grup Ekle", "Grup adı:", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        await _repo.AddGroupAsync(name.Trim());
        await LoadGroupsAsync();
    }

    private async Task AddTagGlobalAsync()
    {
        var name = _dialogs.Prompt("Etiket Ekle", "Etiket adı:", "");
        if (string.IsNullOrWhiteSpace(name)) return;
        await _repo.AddTagAsync(name.Trim());
        await LoadTagsAsync();
    }

    public void Dispose()
    {
        _listener.ClipboardChanged -= OnClipboardChanged;
    }
}
