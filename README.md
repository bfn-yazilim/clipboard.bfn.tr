# Clipboard Manager

Windows'a özel, hafif pano geçmişi yöneticisi — Rust + Tauri 2 + React + Tailwind CSS + SQLite.

## Geliştirme

```powershell
npm install
npm run tauri dev      # geliştirme (hot reload)
npm run tauri build    # NSIS kurulum dosyası: src-tauri/target/release/bundle/nsis/
```

## Klasör yapısı

```
├─ index.html, vite.config.ts, tsconfig.json, package.json
├─ src/                     React arayüzü
│  ├─ App.tsx               durum, klavye kısayolları, backend olayları
│  ├─ api.ts                tipli invoke() sarmalayıcıları (IPC)
│  ├─ types.ts              Rust struct'larının TS karşılıkları
│  ├─ hooks/useTheme.ts     koyu / açık / sistem tema
│  ├─ lib/format.ts         tarih + kısayol biçimlendirme
│  └─ components/           SearchBar, TagBar, ItemList, ItemRow, ContextMenu, StatusBar, Settings
└─ src-tauri/               Rust backend
   ├─ Cargo.toml, tauri.conf.json, capabilities/default.json
   └─ src/
      ├─ lib.rs             uygulama kurulumu, state, tray, pencere olayları
      ├─ clipboard.rs       AddClipboardFormatListener ile olay tabanlı dinleyici, okuma/yazma
      ├─ db.rs              SQLite şeması ve sorgular
      ├─ commands.rs        #[tauri::command] IPC uçları
      ├─ shortcuts.rs       global kısayollar (Alt+V, Ctrl+Alt+V)
      ├─ window.rs          imleç konumunda açma, gizleme
      ├─ win32.rs           kaynak uygulama, parola işaretleri, Ctrl+V simülasyonu
      └─ settings.rs        ayar modeli
```

## Kısayollar

| Tuş | İşlev |
| --- | --- |
| `Alt+V` (global) | Pencereyi imleç konumunda aç / gizle |
| `Ctrl+Alt+V` (global) | Son öğeyi düz metin olarak yapıştır |
| `↑` `↓` | Listede gez |
| `Enter` | Panoya kopyala ve gizle |
| `Shift+Enter` | Kopyala ve önceki uygulamaya yapıştır |
| `Ctrl+F` | Aramaya odaklan |
| `Ctrl+Shift+Del` | Geçmişi temizle (sabitlenmişler korunur) |
| `Ctrl+,` | Ayarlar |
| `Esc` | Aramayı temizle / geri / gizle |
| Sol tık | Kopyala ve imlecin olduğu yere yapıştır |
| Sağ tık | Etiket atama, sabitleme, silme |

Veriler `%APPDATA%\net.soneracar.clipboardmanager\` altında saklanır (`clipboard.db`, `images/`).
