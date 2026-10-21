//! Frontend'in `invoke()` ile çağırdığı IPC komutları.

use std::time::Duration;

use tauri::{AppHandle, Emitter, State};
use tauri_plugin_autostart::ManagerExt;

use crate::db::{self, Item, Tag, TagSummary};
use crate::settings::Settings;
use crate::{clipboard, now_ms, remove_files, shortcuts, win32, window, AppState};

type CmdResult<T> = Result<T, String>;

fn err(e: impl std::fmt::Display) -> String {
    e.to_string()
}

#[tauri::command]
pub fn get_items(state: State<'_, AppState>, query: String, tag_id: Option<i64>) -> CmdResult<Vec<Item>> {
    db::list_items(&state.conn(), &query, tag_id).map_err(err)
}

#[tauri::command]
pub fn get_tags(state: State<'_, AppState>) -> CmdResult<TagSummary> {
    db::tag_summary(&state.conn()).map_err(err)
}

#[tauri::command]
pub fn add_tag(state: State<'_, AppState>, name: String) -> CmdResult<Tag> {
    let name = name.trim();
    if name.is_empty() {
        return Err("Etiket adı boş olamaz".into());
    }
    db::add_tag(&state.conn(), name).map_err(|e| match e {
        rusqlite::Error::SqliteFailure(f, _) if f.code == rusqlite::ErrorCode::ConstraintViolation => {
            "Bu etiket zaten var".to_string()
        }
        e => e.to_string(),
    })
}

#[tauri::command]
pub fn delete_tag(state: State<'_, AppState>, id: i64) -> CmdResult<()> {
    db::delete_tag(&state.conn(), id).map_err(err)
}

#[tauri::command]
pub fn toggle_pin(state: State<'_, AppState>, id: i64) -> CmdResult<bool> {
    db::toggle_pin(&state.conn(), id).map_err(err)
}

#[tauri::command]
pub fn set_item_tag(state: State<'_, AppState>, item_id: i64, tag_id: i64, enabled: bool) -> CmdResult<()> {
    db::set_item_tag(&state.conn(), item_id, tag_id, enabled).map_err(err)
}

#[tauri::command]
pub fn delete_item(state: State<'_, AppState>, id: i64) -> CmdResult<()> {
    let removed = db::delete_item(&state.conn(), id).map_err(err)?;
    remove_files(&removed);
    Ok(())
}

/// Öğeyi panoya yazar ve pencereyi gizler. `paste = true` ise (Shift+Enter)
/// pencere kapandıktan sonra önceki uygulamaya Ctrl+V da gönderilir.
#[tauri::command]
pub async fn copy_item(app: AppHandle, id: i64, paste: bool) -> CmdResult<()> {
    if paste {
        // Hâlâ ön plan süreciyken odağı geri ver; gizlenince Windows izin vermeyebilir.
        win32::restore_foreground();
    }
    window::hide(&app);
    clipboard::write_item(&app, id)?;
    if paste {
        // Hedef uygulamanın odağı alması için kısa bekleme.
        std::thread::sleep(Duration::from_millis(80));
        win32::send_paste();
    }
    Ok(())
}

#[tauri::command]
pub fn clear_history(app: AppHandle, state: State<'_, AppState>) -> CmdResult<()> {
    let removed = db::clear_items(&state.conn()).map_err(err)?;
    remove_files(&removed);
    let _ = app.emit("clipboard-changed", ());
    Ok(())
}

/// Ana ekrandaki "Temizle": sabitlenmiş öğeler korunur.
#[tauri::command]
pub fn clear_unpinned(app: AppHandle, state: State<'_, AppState>) -> CmdResult<()> {
    let removed = db::clear_unpinned(&state.conn()).map_err(err)?;
    remove_files(&removed);
    let _ = app.emit("clipboard-changed", ());
    Ok(())
}

#[tauri::command]
pub fn get_settings(state: State<'_, AppState>) -> Settings {
    state.settings().clone()
}

#[tauri::command]
pub fn save_settings(app: AppHandle, state: State<'_, AppState>, settings: Settings) -> CmdResult<Settings> {
    let settings = settings.normalized();
    let previous = state.settings().clone();

    if let Err(e) = shortcuts::apply(&app, &settings) {
        let _ = shortcuts::apply(&app, &previous);
        return Err(e);
    }

    if settings.launch_at_startup != previous.launch_at_startup {
        let autolaunch = app.autolaunch();
        let res = if settings.launch_at_startup { autolaunch.enable() } else { autolaunch.disable() };
        res.map_err(|e| format!("Başlangıç ayarı değiştirilemedi: {e}"))?;
    }

    let removed = {
        let conn = state.conn();
        db::save_settings(&conn, &settings).map_err(err)?;
        db::prune(&conn, settings.max_items, settings.retention_days, now_ms()).unwrap_or_default()
    };
    remove_files(&removed);
    *state.settings() = settings.clone();
    let _ = app.emit("clipboard-changed", ());
    Ok(settings)
}

#[tauri::command]
pub fn suspend_shortcuts(app: AppHandle) {
    shortcuts::suspend(&app);
}

#[tauri::command]
pub fn resume_shortcuts(app: AppHandle, state: State<'_, AppState>) -> CmdResult<()> {
    let settings = state.settings().clone();
    shortcuts::apply(&app, &settings)
}

#[tauri::command]
pub fn hide_window(app: AppHandle) {
    window::hide(&app);
}
