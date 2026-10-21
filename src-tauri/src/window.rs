use tauri::{AppHandle, Emitter, Manager, PhysicalPosition, PhysicalSize, WebviewWindow};

pub const MAIN: &str = "main";

fn main_window(app: &AppHandle) -> Option<WebviewWindow> {
    app.get_webview_window(MAIN)
}

/// Pencereyi fare imlecinin bulunduğu noktada açar; ekran dışına taşmaması için
/// imlecin bulunduğu monitörün çalışma alanına (görev çubuğu hariç) sığdırır.
pub fn show_at_cursor(app: &AppHandle) {
    let Some(win) = main_window(app) else { return };
    crate::win32::remember_foreground();

    if let Ok(cursor) = app.cursor_position() {
        let mut size = win.outer_size().unwrap_or(PhysicalSize::new(600, 800));
        let (mut x, mut y) = (cursor.x as i32, cursor.y as i32);

        if let Ok(Some(monitor)) = app.monitor_from_point(cursor.x, cursor.y) {
            let area = monitor.work_area();
            let (ax, ay) = (area.position.x, area.position.y);
            let (aw, ah) = (area.size.width as i32, area.size.height as i32);
            if size.height as i32 > ah {
                size.height = ah as u32;
                let _ = win.set_size(size);
            }
            x = x.clamp(ax, (ax + aw - size.width as i32).max(ax));
            y = y.clamp(ay, (ay + ah - size.height as i32).max(ay));
        }
        let _ = win.set_position(PhysicalPosition::new(x, y));
    }

    let _ = win.unminimize();
    let _ = win.show();
    let _ = win.set_focus();
    let _ = win.emit("window-shown", ());
}

pub fn toggle(app: &AppHandle) {
    let Some(win) = main_window(app) else { return };
    if win.is_visible().unwrap_or(false) && win.is_focused().unwrap_or(false) {
        let _ = win.hide();
    } else {
        show_at_cursor(app);
    }
}

pub fn hide(app: &AppHandle) {
    if let Some(win) = main_window(app) {
        let _ = win.hide();
    }
}
