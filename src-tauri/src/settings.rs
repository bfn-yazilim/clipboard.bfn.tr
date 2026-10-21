use serde::{Deserialize, Serialize};

/// Kullanıcı ayarları. SQLite `settings` tablosunda tek bir JSON satırı olarak saklanır.
/// `default` sayesinde yeni eklenen alanlar eski kayıtlarda otomatik doldurulur.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", default)]
pub struct Settings {
    pub open_shortcut: String,
    pub paste_last_shortcut: String,
    /// Sabitlenmemiş öğe limiti. 0 = sınırsız.
    pub max_items: u32,
    /// Gün cinsinden saklama süresi. 0 = sınırsız.
    pub retention_days: u32,
    pub launch_at_startup: bool,
    pub capture_images: bool,
    pub ignore_passwords: bool,
    /// "dark" | "light" | "system"
    pub theme: String,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            open_shortcut: "Alt+V".into(),
            paste_last_shortcut: "Ctrl+Alt+V".into(),
            max_items: 250,
            retention_days: 30,
            launch_at_startup: false,
            capture_images: true,
            ignore_passwords: true,
            theme: "system".into(),
        }
    }
}

impl Settings {
    pub fn normalized(mut self) -> Self {
        self.open_shortcut = self.open_shortcut.trim().to_string();
        self.paste_last_shortcut = self.paste_last_shortcut.trim().to_string();
        if !matches!(self.theme.as_str(), "dark" | "light" | "system") {
            self.theme = "system".into();
        }
        self
    }
}
