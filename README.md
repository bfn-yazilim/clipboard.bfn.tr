# Clipboard & Asset Manager

WPF + .NET 8 + Entity Framework Core (SQLite) ile geliştirilmiş, etiketleme ve gruplandırma özelliklerine sahip modern bir **Pano ve Snippet Yöneticisi**. Arka planda çalışır, `Ctrl+C` ile kopyalanan düz metin, zengin metin (RTF) ve görselleri yakalar; `Ctrl+Shift+V` ile açılır ve seçilen öğeyi otomatik olarak aktif pencereye yapıştırır.

---

## Özellikler

| # | Özellik | Açıklama |
|---|---------|----------|
| 1 | **Pano Dinleme** | `AddClipboardFormatListener` (user32.dll) ile metin / RTF / görsel yakalama |
| 2 | **Kalıcı Depolama** | EF Core + SQLite; öğeler, gruplar ve etiketler kalıcıdır |
| 3 | **Global Kısayol** | `RegisterHotKey` ile `Ctrl+Shift+V` (çakışma yok, `MOD_NOREPEAT`) |
| 4 | **Modern WPF UI (MVVM)** | Sidebar grup menüsü, arama çubuğu, 3 sütunlu `UniformGrid`, `ContextMenu` |
| 5 | **Auto-Paste** | Öğeye tıklayınca pencere gizlenir, hedef pencereye odak verilip `SendInput` ile `Ctrl+V` gönderilir |

---

## Mimari

```
ClipboardManager/
├── Models/                 # Veri modelleri (EF Core entity'leri)
│   ├── ClipboardItem.cs    # Tek pano öğesi (içerik/tip/grup/etiketler)
│   ├── Group.cs            # Grup/Kategori (1:N)
│   ├── Tag.cs              # Etiket (N:N -> ItemTag)
│   ├── ItemTag.cs          # Çoka-çok join entity
│   ├── ClipboardItemKind.cs# Enum: PlainText / RichText / Image
│   └── AppSettings.cs      # JSON ayar modeli
│
├── Data/                   # Kalıcılık katmanı
│   ├── AppDbContext.cs     # EF Core DbContext (Fluent API)
│   ├── ClipboardRepository.cs # İşlem katmanı (sorgu/CRUD)
│   ├── DbSeeder.cs         # Varsayılan gruplar
│   └── SettingsStore.cs    # JSON ayar deposu
│
├── Interop/                # Win32 P/Invoke
│   ├── NativeMethods.cs    # Clipboard/Hotkey/SendInput/Focus API'leri
│   └── HotkeyParser.cs     # "Ctrl+Shift" metni -> Win32 mod bayrakları
│
├── Services/               # İş mantığı servisleri
│   ├── IClipboardListener.cs  + ClipboardListener.cs   # Pano dinleyici
│   ├── IGlobalHotkeyService.cs + GlobalHotkeyService.cs # Global kısayol
│   ├── IPasteService.cs       + PasteService.cs        # Auto-paste (SendInput)
│   ├── IDialogService.cs      + DialogService.cs       # UI diyaloğu
│
├── Mvvm/                   # MVVM altyapısı
│   ├── ObservableObject.cs # INotifyPropertyChanged base
│   └── RelayCommand.cs     # ICommand (sync/async + generic)
│
├── ViewModels/
│   ├── MainViewModel.cs             # Ana pencere mantığı
│   ├── ClipboardItemViewModel.cs    # Öğe DTO'su
│   └── GroupViewModel.cs            # Grup DTO'su
│
├── Views/
│   ├── MainWindow.xaml(.cs)  # Pencere + WndProc yönlendirme
│   └── InputDialog.xaml(.cs) # Metin girişi diyaloğu
│
├── Themes/
│   ├── Colors.xaml           # Koyu tema renkleri
│   └── Controls.xaml         # Modern buton/textbox/card stilleri
│
├── Converters/
│   └── BooleanToVisibilityConverter.cs
│
├── App.xaml(.cs)             # DI container + startup
└── app.manifest             # DPI farkındalığı
```

---

## Derleme ve Çalıştırma

### Önkoşullar
- **.NET 8 SDK** (Windows)
- Visual Studio 2022 (17.8+) *veya* `dotnet` CLI

### CLI ile

```bash
cd C:\Github\bfn-yazilim\clipboard.bfn.tr
dotnet restore
dotnet build -c Debug
dotnet run --project src\ClipboardManager\ClipboardManager.csproj
```

### Visual Studio ile
`ClipboardManager.sln` dosyasını açın → **F5** (Debug) veya **Ctrl+F5** (Run).

---

## Kullanım

1. **Uygulama açıldığında** arka planda pano dinlenmeye başlar.
2. Herhangi bir uygulamada **`Ctrl+C`** yapın — öğe otomatik yakalanır ve veritabanına kaydedilir.
3. **`Ctrl+Shift+V`** ile yönetici penceresini açın.
4. Sol **sidebar**'dan bir grup seçin veya **arama çubuğu**yla öğe/etiket arayın.
5. Bir **öğeye tıklayın**:
   - Pencere gizlenir,
   - İçerik panoya konur,
   - Aktif pencereye `Ctrl+V` gönderilir (otomatik yapıştırma).
6. Bir öğeye **sağ tıklayın**: grup atama, etiket ekleme, favori, silme.
7. **`ESC`** ile pencereyi gizleyebilirsiniz.

### Veri konumu
- Veritabanı: `%LOCALAPPDATA%\ClipboardManager\clipboard.db`
- Görseller: `%LOCALAPPDATA%\ClipboardManager\images\`
- Ayarlar:   `%LOCALAPPDATA%\ClipboardManager\settings.json`

---

## Teknik Notlar

### Pano Dinleme
`SetClipboardViewer` zinciri yerine modern **`AddClipboardFormatListener`** kullanılır. Bu yöntem cakışma ve zincir kopması sorunlarını ortadan kaldırır. WM_CLIPBOARDUPDATE mesajı `WndProc` üzerinden dinlenir.

### Global Hotkey
`RegisterHotKey` + `MOD_NOREPEAT` ile sistem geneli kısayol kaydedilir. `WM_HOTKEY` mesajı `WndProc`'tan `GlobalHotkeyService.OnHotkey`'e yönlendirilir.

### Auto-Paste (Focus yönetimi)
`SetForegroundWindow` tek başına güvenilmez olduğu için **`AttachThreadInput`** ile hedef ve UI thread'lerinin input kuyrukları geçici olarak birleştirilir, böylece odak güvenle aktarılır. Sonrasında `SendInput` ile `Ctrl+V` dizisi gönderilir.

Kendi yerleştirdiğimiz pano içeriğinin dinleyici tarafından **tekrar yakalanmaması** için `ClipboardListener.SuppressNext()` çağrılır.

### Kalıcılık
- **EF Core + SQLite** (ana veri)
- **JSON** (kullanıcı ayarları — hotkey, pencere boyutu vb.)
- Görseller dosya sisteminde, DB'de sadece yol saklanır (şişmeyi önler).

### MVVM
- Kendi `ObservableObject` ve `RelayCommand` altyapısı (CommunityToolkit.Mvvm bağımlılığı minimize edildi).
- Code-behind yalnızca Win32 `WndProc` yönlendirmesi ve show/hide işlemlerini içerir; tüm iş mantığı `MainViewModel`'dedir.
- Dependency Injection ile gevşek bağlı servisler.

---

## Genişletme Noktaları

- **Tray ikonu**: `NotifyIcon` ile sistem tepsisi entegrasyonu eklenebilir.
- **Ayarlar penceresi**: `AppSettings` düzenleme UI'ı.
- **Şifreli metinler**: Hassas öğeler için AES şifreleme.
- **Senkronizasyon**: Öğeleri bulut senkronizasyonu (OneDrive/Dropbox klasörü).
- **Snippet şablonları**: Değişken (`{date}`, `{name}`) içeren dinamik metinler.
- **Çoklu seçim**: Birden fazla öğeyi aynı anda yapıştırma.
