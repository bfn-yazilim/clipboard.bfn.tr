//! Doğrudan Win32 çağrıları: kaynak uygulama tespiti, hassas içerik işaretleri, Ctrl+V simülasyonu.

use std::ffi::c_void;
use std::path::Path;
use std::sync::atomic::{AtomicIsize, Ordering};
use std::time::Duration;

use windows::core::{w, PCWSTR, PWSTR};
use windows::Win32::Foundation::{CloseHandle, HGLOBAL, HWND};
use windows::Win32::System::DataExchange::{
    CloseClipboard, GetClipboardData, GetClipboardOwner, IsClipboardFormatAvailable, OpenClipboard,
    RegisterClipboardFormatW,
};
use windows::Win32::System::Memory::{GlobalLock, GlobalUnlock};
use windows::Win32::System::Threading::{
    OpenProcess, QueryFullProcessImageNameW, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    GetAsyncKeyState, SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT, KEYBD_EVENT_FLAGS,
    KEYEVENTF_KEYUP, VIRTUAL_KEY, VK_CONTROL, VK_LMENU, VK_LSHIFT, VK_LWIN, VK_RMENU, VK_RSHIFT,
    VK_RWIN, VK_V,
};
use windows::Win32::UI::WindowsAndMessaging::{
    GetForegroundWindow, GetWindowThreadProcessId, IsWindow, SetForegroundWindow,
};

/// Pencere açılmadan önce ön planda olan uygulama; yapıştırma oraya yapılır.
static PREV_FOREGROUND: AtomicIsize = AtomicIsize::new(0);

/// Pencere gösterilmeden hemen önce çağrılır.
pub fn remember_foreground() {
    unsafe {
        let hwnd = GetForegroundWindow();
        if hwnd.is_invalid() {
            return;
        }
        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        if pid != std::process::id() {
            PREV_FOREGROUND.store(hwnd.0 as isize, Ordering::Relaxed);
        }
    }
}

/// Odağı önceki uygulamaya geri verir. Biz hâlâ ön plan süreciyken çağrıldığı için
/// Windows'un odak çalma kısıtlamasına takılmaz.
pub fn restore_foreground() {
    let hwnd = HWND(PREV_FOREGROUND.load(Ordering::Relaxed) as *mut c_void);
    unsafe {
        if !hwnd.is_invalid() && IsWindow(Some(hwnd)).as_bool() {
            let _ = SetForegroundWindow(hwnd);
        }
    }
}

pub struct ClipboardSource {
    pub pid: u32,
    pub app_name: Option<String>,
}

/// Panoyu son yazan pencerenin süreç bilgisini döndürür.
/// Sahip pencere yoksa (bazı uygulamalar NULL ile yazar) ön plandaki pencereye düşer.
pub fn clipboard_source() -> Option<ClipboardSource> {
    unsafe {
        let mut hwnd = GetClipboardOwner().unwrap_or_default();
        if hwnd.is_invalid() {
            hwnd = GetForegroundWindow();
        }
        if hwnd.is_invalid() {
            return None;
        }
        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        (pid != 0).then(|| ClipboardSource { pid, app_name: process_name(pid) })
    }
}

unsafe fn process_name(pid: u32) -> Option<String> {
    let handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid).ok()?;
    let mut buf = [0u16; 1024];
    let mut len = buf.len() as u32;
    let res = QueryFullProcessImageNameW(handle, PROCESS_NAME_WIN32, PWSTR(buf.as_mut_ptr()), &mut len);
    let _ = CloseHandle(handle);
    res.ok()?;
    let path = String::from_utf16_lossy(&buf[..len as usize]);
    Path::new(&path).file_stem().map(|s| s.to_string_lossy().into_owned())
}

/// Parola yöneticilerinin ve Windows'un kullandığı "geçmişe alma" işaretlerini kontrol eder.
/// KeePassXC, 1Password, Bitwarden vb. bu formatlardan en az birini panoya ekler.
pub fn clipboard_marked_sensitive() -> bool {
    unsafe {
        let exclude = register(w!("ExcludeClipboardContentFromMonitorProcessing"));
        let viewer_ignore = register(w!("Clipboard Viewer Ignore"));
        if available(exclude) || available(viewer_ignore) {
            return true;
        }
        // DWORD 0 değeri "Win+V geçmişine ekleme" anlamına gelir.
        let history = register(w!("CanIncludeInClipboardHistory"));
        available(history) && read_u32_format(history) == Some(0)
    }
}

/// İşaret koymayan parola yöneticileri için süreç adı sezgisi.
pub fn is_password_manager(app_name: &str) -> bool {
    const KNOWN: &[&str] = &[
        "keepass", "1password", "bitwarden", "lastpass", "dashlane", "enpass", "roboform",
        "nordpass", "keeper", "protonpass", "passwordsafe", "kwalletmanager",
    ];
    let name = app_name.to_lowercase().replace([' ', '-', '_'], "");
    KNOWN.iter().any(|k| name.contains(k))
}

unsafe fn register(name: PCWSTR) -> u32 {
    RegisterClipboardFormatW(name)
}

unsafe fn available(format: u32) -> bool {
    format != 0 && IsClipboardFormatAvailable(format).is_ok()
}

unsafe fn read_u32_format(format: u32) -> Option<u32> {
    // Pano başka bir süreç tarafından kısa süreliğine kilitli olabilir.
    let mut opened = false;
    for _ in 0..5 {
        if OpenClipboard(None).is_ok() {
            opened = true;
            break;
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    if !opened {
        return None;
    }
    let value = (|| {
        let handle = GetClipboardData(format).ok()?;
        let mem = HGLOBAL(handle.0);
        let ptr = GlobalLock(mem) as *const u32;
        if ptr.is_null() {
            return None;
        }
        let v = ptr.read_unaligned();
        let _ = GlobalUnlock(mem);
        Some(v)
    })();
    let _ = CloseClipboard();
    value
}

/// Ön plandaki uygulamaya Ctrl+V gönderir. Kullanıcının hâlâ basılı tuttuğu
/// Alt/Shift/Win tuşları önce bırakılır; aksi halde Ctrl+Alt+V gibi bir kombinasyon oluşur.
pub fn send_paste() {
    unsafe {
        // 0xE8 atanmamış bir tuş: Alt bırakılınca menü çubuğunun aktifleşmesini engeller.
        let dummy = VIRTUAL_KEY(0xE8);
        let mut inputs = vec![key(dummy, false), key(dummy, true)];
        for vk in [VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LWIN, VK_RWIN] {
            if GetAsyncKeyState(vk.0 as i32) < 0 {
                inputs.push(key(vk, true));
            }
        }
        inputs.extend([
            key(VK_CONTROL, false),
            key(VK_V, false),
            key(VK_V, true),
            key(VK_CONTROL, true),
        ]);
        SendInput(&inputs, std::mem::size_of::<INPUT>() as i32);
    }
}

fn key(vk: VIRTUAL_KEY, up: bool) -> INPUT {
    INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                wScan: 0,
                dwFlags: if up { KEYEVENTF_KEYUP } else { KEYBD_EVENT_FLAGS(0) },
                time: 0,
                dwExtraInfo: 0,
            },
        },
    }
}
