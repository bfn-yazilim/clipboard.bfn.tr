mod clipboard;
mod commands;
mod db;
mod settings;
mod shortcuts;
mod win32;
mod window;

use std::path::PathBuf;
use std::sync::{Mutex, MutexGuard};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use rusqlite::Connection;
use tauri::menu::{Menu, MenuItem, PredefinedMenuItem};
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::{App, Emitter, Manager, WindowEvent};
use tauri_plugin_autostart::{MacosLauncher, ManagerExt};

use settings::Settings;
use shortcuts::ActiveShortcuts;

/// Otomatik başlatmada pencere gizli açılsın diye geçilen argüman.
const HIDDEN_ARG: &str = "--hidden";

pub struct AppState {
    db: Mutex<Connection>,
    settings: Mutex<Settings>,
    shortcuts: Mutex<ActiveShortcuts>,
    images_dir: PathBuf,
}

impl AppState {
    pub fn conn(&self) -> MutexGuard<'_, Connection> {
        self.db.lock().unwrap_or_else(|e| e.into_inner())
    }

    pub fn settings(&self) -> MutexGuard<'_, Settings> {
        self.settings.lock().unwrap_or_else(|e| e.into_inner())
    }
}

pub fn now_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or_default()
}

pub fn remove_files(paths: &[String]) {
    for path in paths {
        let _ = std::fs::remove_file(path);
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        // İkinci kez çalıştırılırsa yeni süreç açmak yerine mevcut pencereyi göster.
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            window::show_at_cursor(app);
        }))
        .plugin(tauri_plugin_autostart::init(
            MacosLauncher::LaunchAgent,
            Some(vec![HIDDEN_ARG]),
        ))
        .plugin(
            tauri_plugin_global_shortcut::Builder::new()
                .with_handler(shortcuts::handle)
                .build(),
        )
        .setup(setup)
        .on_window_event(|win, event| match event {
            // Dışarı tıklanınca (blur) kapatma yok, sadece gizle.
            WindowEvent::Focused(false) => {
                let _ = win.hide();
            }
            WindowEvent::CloseRequested { api, .. } => {
                api.prevent_close();
                let _ = win.hide();
            }
            _ => {}
        })
        .invoke_handler(tauri::generate_handler![
            commands::get_items,
            commands::get_tags,
            commands::add_tag,
            commands::delete_tag,
            commands::toggle_pin,
            commands::set_item_tag,
            commands::delete_item,
            commands::copy_item,
            commands::clear_history,
            commands::clear_unpinned,
            commands::get_settings,
            commands::save_settings,
            commands::suspend_shortcuts,
            commands::resume_shortcuts,
            commands::hide_window,
        ])
        .run(tauri::generate_context!())
        .expect("uygulama başlatılamadı");
}

fn setup(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    let data_dir = app.path().app_data_dir()?;
    let images_dir = data_dir.join("images");
    std::fs::create_dir_all(&images_dir)?;

    let conn = db::open(&data_dir.join("clipboard.db"))?;
    let settings = db::load_settings(&conn);
    let removed = db::prune(&conn, settings.max_items, settings.retention_days, now_ms())?;
    remove_files(&removed);

    app.manage(AppState {
        db: Mutex::new(conn),
        settings: Mutex::new(settings.clone()),
        shortcuts: Mutex::new(ActiveShortcuts::default()),
        images_dir,
    });

    let handle = app.handle().clone();
    if let Err(e) = shortcuts::apply(&handle, &settings) {
        eprintln!("{e}");
    }

    // Kayıt defteri (HKCU\...\Run) durumunu ayarla senkronize et.
    let autolaunch = app.autolaunch();
    if autolaunch.is_enabled().unwrap_or(false) != settings.launch_at_startup {
        let _ = if settings.launch_at_startup { autolaunch.enable() } else { autolaunch.disable() };
    }

    build_tray(app)?;
    clipboard::start_listener(handle.clone());
    spawn_periodic_prune(handle.clone());

    if !std::env::args().any(|a| a == HIDDEN_ARG) {
        window::show_at_cursor(&handle);
    }
    Ok(())
}

fn build_tray(app: &App) -> tauri::Result<()> {
    let show = MenuItem::with_id(app, "show", "Göster", true, None::<&str>)?;
    let settings = MenuItem::with_id(app, "settings", "Ayarlar", true, None::<&str>)?;
    let quit = MenuItem::with_id(app, "quit", "Çıkış", true, None::<&str>)?;
    let separator = PredefinedMenuItem::separator(app)?;
    let menu = Menu::with_items(app, &[&show, &settings, &separator, &quit])?;

    let mut tray = TrayIconBuilder::with_id("main")
        .tooltip("Clipboard Manager")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| match event.id.as_ref() {
            "show" => window::show_at_cursor(app),
            "settings" => {
                window::show_at_cursor(app);
                let _ = app.emit("open-settings", ());
            }
            "quit" => app.exit(0),
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click {
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                window::show_at_cursor(tray.app_handle());
            }
        });
    if let Some(icon) = app.default_window_icon() {
        tray = tray.icon(icon.clone());
    }
    tray.build(app)?;
    Ok(())
}

/// Uygulama günlerce açık kalabileceği için saklama süresi saatte bir uygulanır.
fn spawn_periodic_prune(app: tauri::AppHandle) {
    std::thread::spawn(move || loop {
        std::thread::sleep(Duration::from_secs(3600));
        let state = app.state::<AppState>();
        let settings = state.settings().clone();
        let removed = db::prune(&state.conn(), settings.max_items, settings.retention_days, now_ms())
            .unwrap_or_default();
        if !removed.is_empty() {
            remove_files(&removed);
        }
        let _ = app.emit("clipboard-changed", ());
    });
}
