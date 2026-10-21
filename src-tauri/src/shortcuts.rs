use std::time::Duration;

use tauri::{AppHandle, Manager};
use tauri_plugin_global_shortcut::{GlobalShortcutExt, Shortcut, ShortcutEvent, ShortcutState};

use crate::settings::Settings;
use crate::{clipboard, db, win32, window, AppState};

#[derive(Default, Clone, Copy)]
pub struct ActiveShortcuts {
    pub open: Option<Shortcut>,
    pub paste_last: Option<Shortcut>,
}

/// Ayarlardaki kısayolları (yeniden) kaydeder.
pub fn apply(app: &AppHandle, settings: &Settings) -> Result<(), String> {
    let open = parse(&settings.open_shortcut)?;
    let paste_last = parse(&settings.paste_last_shortcut)?;
    if open.is_some() && open == paste_last {
        return Err("İki kısayol aynı olamaz".into());
    }

    let gs = app.global_shortcut();
    let _ = gs.unregister_all();
    let mut errors = Vec::new();
    for shortcut in [open, paste_last].into_iter().flatten() {
        if let Err(e) = gs.register(shortcut) {
            errors.push(format!("{shortcut} kaydedilemedi (başka bir uygulama kullanıyor olabilir): {e}"));
        }
    }
    *app.state::<AppState>().shortcuts.lock().unwrap_or_else(|e| e.into_inner()) =
        ActiveShortcuts { open, paste_last };

    if errors.is_empty() { Ok(()) } else { Err(errors.join("\n")) }
}

/// Ayarlar ekranında yeni kısayol kaydedilirken mevcut kısayollar devre dışı kalır.
pub fn suspend(app: &AppHandle) {
    let _ = app.global_shortcut().unregister_all();
}

fn parse(s: &str) -> Result<Option<Shortcut>, String> {
    if s.trim().is_empty() {
        return Ok(None);
    }
    s.parse::<Shortcut>()
        .map(Some)
        .map_err(|e| format!("Geçersiz kısayol '{s}': {e}"))
}

/// Global kısayol plugin'inin ortak handler'ı (ana thread'de çalışır).
pub fn handle(app: &AppHandle, shortcut: &Shortcut, event: ShortcutEvent) {
    let active = *app.state::<AppState>().shortcuts.lock().unwrap_or_else(|e| e.into_inner());

    if active.open == Some(*shortcut) && event.state == ShortcutState::Pressed {
        window::toggle(app);
    } else if active.paste_last == Some(*shortcut) && event.state == ShortcutState::Released {
        let app = app.clone();
        // Ana thread'i bloklamamak için ayrı thread.
        std::thread::spawn(move || paste_last(&app));
    }
}

fn paste_last(app: &AppHandle) {
    let id = {
        let state = app.state::<AppState>();
        let conn = state.conn();
        db::latest_item_id(&conn).ok().flatten()
    };
    let Some(id) = id else { return };
    if clipboard::write_item(app, id).is_ok() {
        std::thread::sleep(Duration::from_millis(30));
        win32::send_paste();
    }
}
