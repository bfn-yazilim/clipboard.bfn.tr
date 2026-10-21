import { invoke } from "@tauri-apps/api/core";
import type { ClipItem, Settings, Tag, TagSummary } from "./types";

// Rust tarafındaki #[tauri::command] fonksiyonlarının tipli sarmalayıcıları.
// Tauri argüman adlarını otomatik olarak camelCase -> snake_case dönüştürür.
export const api = {
  getItems: (query: string, tagId: number | null) =>
    invoke<ClipItem[]>("get_items", { query, tagId }),
  getTags: () => invoke<TagSummary>("get_tags"),
  addTag: (name: string) => invoke<Tag>("add_tag", { name }),
  deleteTag: (id: number) => invoke<void>("delete_tag", { id }),
  togglePin: (id: number) => invoke<boolean>("toggle_pin", { id }),
  setItemTag: (itemId: number, tagId: number, enabled: boolean) =>
    invoke<void>("set_item_tag", { itemId, tagId, enabled }),
  deleteItem: (id: number) => invoke<void>("delete_item", { id }),
  copyItem: (id: number, paste = false) => invoke<void>("copy_item", { id, paste }),
  clearHistory: () => invoke<void>("clear_history"),
  clearUnpinned: () => invoke<void>("clear_unpinned"),
  getSettings: () => invoke<Settings>("get_settings"),
  saveSettings: (settings: Settings) => invoke<Settings>("save_settings", { settings }),
  suspendShortcuts: () => invoke<void>("suspend_shortcuts"),
  resumeShortcuts: () => invoke<void>("resume_shortcuts"),
  hideWindow: () => invoke<void>("hide_window"),
};
