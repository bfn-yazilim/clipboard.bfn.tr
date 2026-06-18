using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ClipboardManager.Data;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.EntityFrameworkCore;

namespace ClipboardManager.ViewModels;

public class MainViewModel : Mvvm.ObservableObject, IDisposable, GongSolutions.Wpf.DragDrop.IDropTarget
{
    private readonly ClipboardRepository _repo;
    private readonly IClipboardListener _listener;
    private readonly IPasteService _pasteService;
    private readonly IDialogService _dialogs;
    private readonly SettingsStore _settings;

    private const int HotkeyShowWindow = 9001;
    public const int AllTagsId = 0;

    public ObservableCollection<TagViewModel> Tags { get; } = new();
    public ObservableCollection<ClipboardItemViewModel> Items { get; } = new();

    private TagViewModel? _selectedTag;
    public TagViewModel? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
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

        SelectTagCommand = new Mvvm.RelayCommand<TagViewModel>(t =>
        {
            SelectedTag = t;
            foreach (var x in Tags) x.IsSelected = x == t;
        });

        PasteItemCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(PasteItemAsync);
        DeleteItemCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(DeleteItemAsync);
        ToggleFavoriteCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(ToggleFavoriteAsync);
        TogglePinCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(TogglePinAsync);
        CopyToClipboardCommand = new Mvvm.AsyncRelayCommand<ClipboardItemViewModel>(CopyOnlyAsync);

        AddTagCommand = new Mvvm.AsyncRelayCommand(AddTagAsync);
        BeginEditTagCommand = new Mvvm.RelayCommand<TagViewModel>(t => { if (t != null && !t.IsSystem) t.IsEditing = true; });
        EndEditTagCommand = new Mvvm.AsyncRelayCommand<TagViewModel>(SaveTagAsync);
        ToggleItemTagCommand = new Mvvm.AsyncRelayCommand<object>(ToggleItemTagAsync);

        ClearSearchCommand = new Mvvm.RelayCommand(() => SearchText = string.Empty);
        RefreshCommand = new Mvvm.AsyncRelayCommand(() => InitializeAsync());

        _listener.ClipboardChanged += OnClipboardChanged;
        _ = InitializeAsync();
    }

    public void AttachToWindow(IntPtr hwnd)
    {
        _listener.Start(hwnd);
        try
        {
            var s = _settings.Load();
            var mods = ClipboardManager.Interop.HotkeyParser.ParseModifiers(s.ShowWindowHotkey.Modifiers);
            var vk = ClipboardManager.Interop.HotkeyParser.ParseVirtualKey(s.ShowWindowHotkey.Key);
            if (App.HotkeyService.Register(hwnd, HotkeyShowWindow, mods, vk))
                App.HotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
        catch { }
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        if (id == HotkeyShowWindow) ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? HideRequested;

    public Mvvm.RelayCommand<TagViewModel> SelectTagCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> PasteItemCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> DeleteItemCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> ToggleFavoriteCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> TogglePinCommand { get; }
    public Mvvm.AsyncRelayCommand<ClipboardItemViewModel> CopyToClipboardCommand { get; }
    
    public Mvvm.AsyncRelayCommand AddTagCommand { get; }
    public Mvvm.RelayCommand<TagViewModel> BeginEditTagCommand { get; }
    public Mvvm.AsyncRelayCommand<TagViewModel> EndEditTagCommand { get; }
    public Mvvm.AsyncRelayCommand<object> ToggleItemTagCommand { get; }

    public Mvvm.RelayCommand ClearSearchCommand { get; }
    public Mvvm.AsyncRelayCommand RefreshCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadTagsAsync();
        await LoadItemsAsync();
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _repo.GetTagsAsync();
        Tags.Clear();
        Tags.Add(new TagViewModel { Id = AllTagsId, Name = "Tümü", IsSystem = true, IsSelected = SelectedTag == null || SelectedTag.Id == AllTagsId });
        foreach (var t in tags)
        {
            Tags.Add(new TagViewModel { Id = t.Id, Name = t.Name, IsSelected = SelectedTag?.Id == t.Id });
        }
        if (SelectedTag == null)
            SelectedTag = Tags[0];
        else
            SelectedTag = Tags.FirstOrDefault(t => t.Id == SelectedTag.Id) ?? Tags[0];
        
        OnPropertyChanged(nameof(Tags));
    }

    private async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var tagId = SelectedTag?.Id;
            int? filterId = tagId is null or AllTagsId ? null : tagId;
            var items = await _repo.GetItemsAsync(filterId, SearchText);

            Items.Clear();
            foreach (var i in items) Items.Add(MapToViewModel(i));
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
        OrderIndex = i.OrderIndex,
        UseCount = i.UseCount,
        Tags = i.ItemTags.Select(it => it.Tag.Name).ToList()
    };

    private async Task DebouncedSearchAsync(CancellationToken ct)
    {
        try { await Task.Delay(250, ct); await LoadItemsAsync(); }
        catch (TaskCanceledException) { }
    }

    private void OnClipboardChanged(object? sender, ClipboardCapturedEventArgs e)
    {
        var app = Application.Current;
        if (app == null) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => OnClipboardChanged(sender, e));
            return;
        }
        _ = Task.Run(() => SaveCapturedAsync(e));
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

        if (s.SkipDuplicates && await IsDuplicateAsync(item)) return;

        await _repo.AddItemAsync(item);
        await _repo.TrimToAsync(s.MaxItems);

        await Application.Current.Dispatcher.BeginInvoke(async () => await LoadItemsAsync());
    }

    private async Task<bool> IsDuplicateAsync(ClipboardItem item)
    {
        await using var db = await App.DbFactory.CreateDbContextAsync();
        var last = await db.Items.OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync();
        if (last == null) return false;
        if (item.Kind == ClipboardItemKind.Image) return last.Kind == ClipboardItemKind.Image && last.Size == item.Size;
        return last.Content == item.Content;
    }

    private static string MakeTitle(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "(boş)";
        var firstLine = text.Split('\n', '\r').FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
        if (firstLine.Length > 48) firstLine = firstLine[..48] + "…";
        return firstLine;
    }

    private async Task PasteItemAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var targetWindow = App.LastActiveWindow;
        HideRequested?.Invoke(this, EventArgs.Empty);
        await Task.Delay(80);

        var item = new ClipboardItem { Kind = vm.Kind, Content = vm.Content, ImageFilePath = vm.ImageFilePath };
        _pasteService.PasteItem(item, targetWindow);

        await using var db = await App.DbFactory.CreateDbContextAsync();
        var entity = await db.Items.FindAsync(vm.Id);
        if (entity != null)
        {
            entity.UseCount++;
            entity.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task CopyOnlyAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        var item = new ClipboardItem { Kind = vm.Kind, Content = vm.Content, ImageFilePath = vm.ImageFilePath };

        if (_listener is ClipboardListener cl) cl.SuppressNext();

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
    }

    private async Task TogglePinAsync(ClipboardItemViewModel? vm)
    {
        if (vm == null) return;
        await using var db = await App.DbFactory.CreateDbContextAsync();
        var e = await db.Items.FindAsync(vm.Id);
        if (e != null)
        {
            e.IsPinned = !e.IsPinned;
            await db.SaveChangesAsync();
            await LoadItemsAsync();
        }
    }

    private async Task AddTagAsync()
    {
        var tag = await _repo.AddTagAsync("YeniEtiket");
        await LoadTagsAsync();
        var newTagVm = Tags.FirstOrDefault(t => t.Id == tag.Id);
        if (newTagVm != null)
        {
            newTagVm.IsEditing = true;
        }
    }

    private async Task SaveTagAsync(TagViewModel? tag)
    {
        if (tag == null || tag.IsSystem) return;
        tag.IsEditing = false;
        
        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            await _repo.DeleteTagAsync(tag.Id);
        }
        else
        {
            await _repo.UpdateTagAsync(tag.Id, tag.Name.Replace("#", "").Trim());
        }
        await LoadTagsAsync();
        await LoadItemsAsync();
    }

    private async Task ToggleItemTagAsync(object? param)
    {
        if (param is object[] arr && arr.Length == 2 && arr[0] is ClipboardItemViewModel itemVm && arr[1] is TagViewModel tagVm)
        {
            await _repo.ToggleTagAsync(itemVm.Id, tagVm.Id);
            await LoadItemsAsync();
        }
    }

    public void DragOver(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
    {
        if (dropInfo.Data is ClipboardItemViewModel && dropInfo.TargetItem is ClipboardItemViewModel)
        {
            dropInfo.DropTargetAdorner = GongSolutions.Wpf.DragDrop.DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Move;
        }
    }

    public void Drop(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
    {
        if (dropInfo.Data is not ClipboardItemViewModel sourceItem || dropInfo.TargetItem is not ClipboardItemViewModel targetItem || sourceItem == targetItem)
            return;

        int oldIndex = Items.IndexOf(sourceItem);
        int newIndex = dropInfo.InsertIndex;
        if (oldIndex < newIndex) newIndex--;

        Items.Move(oldIndex, newIndex);

        var itemsToUpdate = new List<ClipboardItem>();
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].OrderIndex = i;
            itemsToUpdate.Add(new ClipboardItem { Id = Items[i].Id, OrderIndex = i });
        }
        _ = _repo.UpdateItemsOrderAsync(itemsToUpdate);
    }

    public void Dispose()
    {
        _listener.ClipboardChanged -= OnClipboardChanged;
    }
}
