//! Olay tabanlı pano dinleyici. Polling yok: gizli bir "message-only" pencere
//! AddClipboardFormatListener ile kaydolur ve yalnızca WM_CLIPBOARDUPDATE geldiğinde uyanır.

use std::borrow::Cow;
use std::fs::File;
use std::io::BufWriter;
use std::path::Path;
use std::sync::OnceLock;

use image::codecs::png::{CompressionType, FilterType, PngEncoder};
use image::{ExtendedColorType, ImageEncoder};
use tauri::{AppHandle, Emitter, Manager};
use windows::core::w;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows::Win32::System::DataExchange::AddClipboardFormatListener;
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DispatchMessageW, GetMessageW, RegisterClassW, HWND_MESSAGE, MSG,
    WINDOW_EX_STYLE, WINDOW_STYLE, WM_CLIPBOARDUPDATE, WNDCLASSW,
};

use crate::db::{self, NewItem};
use crate::{now_ms, remove_files, win32, AppState};

/// Bu boyuttan büyük metinler yakalanmaz (log dökümleri vb.).
const MAX_TEXT_BYTES: usize = 4 * 1024 * 1024;

static APP: OnceLock<AppHandle> = OnceLock::new();

pub fn start_listener(app: AppHandle) {
    if APP.set(app).is_err() {
        return;
    }
    std::thread::Builder::new()
        .name("clipboard-listener".into())
        .spawn(|| unsafe {
            if let Err(e) = run_message_loop() {
                eprintln!("pano dinleyici başlatılamadı: {e}");
            }
        })
        .expect("clipboard listener thread");
}

unsafe fn run_message_loop() -> windows::core::Result<()> {
    let instance = GetModuleHandleW(None)?;
    let class_name = w!("ClipboardManagerListener");
    let wc = WNDCLASSW {
        lpfnWndProc: Some(wnd_proc),
        hInstance: instance.into(),
        lpszClassName: class_name,
        ..Default::default()
    };
    RegisterClassW(&wc);
    let hwnd = CreateWindowExW(
        WINDOW_EX_STYLE::default(),
        class_name,
        w!("clipboard-listener"),
        WINDOW_STYLE::default(),
        0,
        0,
        0,
        0,
        Some(HWND_MESSAGE),
        None,
        Some(instance.into()),
        None,
    )?;
    AddClipboardFormatListener(hwnd)?;

    // Thread burada mesaj gelene kadar uyur; CPU tüketimi sıfırdır.
    let mut msg = MSG::default();
    while GetMessageW(&mut msg, None, 0, 0).as_bool() {
        DispatchMessageW(&msg);
    }
    Ok(())
}

unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    if msg == WM_CLIPBOARDUPDATE {
        if let Some(app) = APP.get() {
            // FFI sınırından panik geçmemeli.
            let _ = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| capture(app)));
        }
        return LRESULT(0);
    }
    DefWindowProcW(hwnd, msg, wparam, lparam)
}

struct Captured {
    kind: &'static str,
    content: String,
    hash: u64,
    char_count: i64,
    image_path: Option<String>,
}

fn capture(app: &AppHandle) {
    let state = app.state::<AppState>();
    let settings = state.settings().clone();

    let source = win32::clipboard_source();
    // Kendi yazdığımız içerik (Enter ile geri kopyalama) tekrar yakalanmasın.
    if source.as_ref().is_some_and(|s| s.pid == std::process::id()) {
        return;
    }
    let app_name = source.and_then(|s| s.app_name);

    if settings.ignore_passwords
        && (win32::clipboard_marked_sensitive()
            || app_name.as_deref().is_some_and(win32::is_password_manager))
    {
        return;
    }

    let Some(captured) = read_clipboard(&state.images_dir, settings.capture_images) else {
        return;
    };

    let now = now_ms();
    let removed = {
        let conn = state.conn();
        let item = NewItem {
            kind: captured.kind,
            content: &captured.content,
            hash: captured.hash,
            char_count: captured.char_count,
            source_app: app_name.as_deref(),
            image_path: captured.image_path.as_deref(),
        };
        if let Err(e) = db::upsert_item(&conn, &item, now) {
            eprintln!("öğe kaydedilemedi: {e}");
            return;
        }
        db::prune(&conn, settings.max_items, settings.retention_days, now).unwrap_or_default()
    };
    remove_files(&removed);
    let _ = app.emit("clipboard-changed", ());
}

fn read_clipboard(images_dir: &Path, capture_images: bool) -> Option<Captured> {
    let mut cb = arboard::Clipboard::new().ok()?;

    // Explorer'dan kopyalanan dosyalar (CF_HDROP)
    if let Ok(files) = cb.get().file_list() {
        if !files.is_empty() {
            let content = files
                .iter()
                .map(|p| p.display().to_string())
                .collect::<Vec<_>>()
                .join("\n");
            return Some(text_item("path", content));
        }
    }

    if let Ok(text) = cb.get_text() {
        if text.trim().is_empty() || text.len() > MAX_TEXT_BYTES {
            return None;
        }
        let kind = classify(&text);
        return Some(text_item(kind, text));
    }

    if capture_images {
        if let Ok(img) = cb.get_image() {
            return save_image(images_dir, img);
        }
    }
    None
}

fn text_item(kind: &'static str, content: String) -> Captured {
    Captured {
        kind,
        hash: fnv1a(kind.as_bytes(), content.as_bytes()),
        char_count: content.chars().count() as i64,
        content,
        image_path: None,
    }
}

fn classify(text: &str) -> &'static str {
    let t = text.trim();
    if t.contains('\n') {
        return "text";
    }
    if t.starts_with("http://") || t.starts_with("https://") {
        return "link";
    }
    let b = t.as_bytes();
    let drive_path = b.len() > 2 && b[0].is_ascii_alphabetic() && b[1] == b':' && (b[2] == b'\\' || b[2] == b'/');
    if drive_path || t.starts_with("\\\\") {
        return "path";
    }
    "text"
}

fn save_image(dir: &Path, img: arboard::ImageData) -> Option<Captured> {
    let hash = fnv1a(b"image", &img.bytes);
    let path = dir.join(format!("{hash:016x}.png"));
    if !path.exists() {
        let file = BufWriter::new(File::create(&path).ok()?);
        // Hızlı sıkıştırma: büyük ekran görüntülerinde bile birkaç on ms.
        let encoder = PngEncoder::new_with_quality(file, CompressionType::Fast, FilterType::Adaptive);
        if encoder
            .write_image(&img.bytes, img.width as u32, img.height as u32, ExtendedColorType::Rgba8)
            .is_err()
        {
            let _ = std::fs::remove_file(&path);
            return None;
        }
    }
    Some(Captured {
        kind: "image",
        content: format!("Görsel {}×{}", img.width, img.height),
        hash,
        char_count: 0,
        image_path: Some(path.to_string_lossy().into_owned()),
    })
}

/// Bir öğeyi tekrar Windows panosuna yazar ve listede en üste taşır.
pub fn write_item(app: &AppHandle, id: i64) -> Result<(), String> {
    let state = app.state::<AppState>();
    let payload = {
        let conn = state.conn();
        let payload = db::get_payload(&conn, id)
            .map_err(|e| e.to_string())?
            .ok_or("Öğe bulunamadı")?;
        db::touch_item(&conn, id, now_ms()).map_err(|e| e.to_string())?;
        payload
    };

    let mut cb = arboard::Clipboard::new().map_err(|e| e.to_string())?;
    match (payload.kind.as_str(), payload.image_path) {
        ("image", Some(path)) => {
            let img = image::open(&path).map_err(|e| e.to_string())?.to_rgba8();
            let (width, height) = img.dimensions();
            cb.set_image(arboard::ImageData {
                width: width as usize,
                height: height as usize,
                bytes: Cow::Owned(img.into_raw()),
            })
            .map_err(|e| e.to_string())?;
        }
        _ => cb.set_text(payload.content).map_err(|e| e.to_string())?,
    }
    let _ = app.emit("clipboard-changed", ());
    Ok(())
}

/// Kararlı (sürümden bağımsız) 64-bit FNV-1a.
fn fnv1a(prefix: &[u8], data: &[u8]) -> u64 {
    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    for &b in prefix.iter().chain(data) {
        hash ^= u64::from(b);
        hash = hash.wrapping_mul(0x0000_0100_0000_01b3);
    }
    hash
}
