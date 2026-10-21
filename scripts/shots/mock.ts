// Tarayıcıda gerçek arayüzü örnek verilerle çalıştırmak için sahte Tauri IPC katmanı.
// URL parametreleri: ?theme=dark|light  &view=list|settings  &tag=<id>
import type { ClipItem, Settings, Tag } from "../../src/types";

const params = new URLSearchParams(location.search);
const theme = (params.get("theme") ?? "light") as Settings["theme"];
const view = params.get("view") ?? "list";

const min = 60_000;
const now = new Date(2026, 8, 3, 10, 42).getTime();

const tags: Omit<Tag, "count">[] = [
  { id: 1, name: "İş" },
  { id: 2, name: "Şifre" },
  { id: 3, name: "Kod" },
  { id: 4, name: "Linkler" },
];

let nextId = 1;
const item = (
  kind: ClipItem["kind"],
  content: string,
  sourceApp: string,
  minutesAgo: number,
  extra: Partial<ClipItem> = {},
): ClipItem => ({
  id: nextId++,
  kind,
  content,
  charCount: [...content].length,
  sourceApp,
  imagePath: null,
  pinned: false,
  createdAt: now - minutesAgo * min,
  tagIds: [],
  ...extra,
});

const items: ClipItem[] = [
  item("text", "SELECT TOP 100 * FROM dbo.Orders WHERE Status = 'Open' ORDER BY CreatedAt DESC", "SSMS", 2900, { pinned: true, tagIds: [1, 3] }),
  item("link", "https://github.com/tauri-apps/tauri/releases", "opera", 4300, { pinned: true, tagIds: [4] }),
  item("text", "npm run tauri build", "WindowsTerminal", 3),
  item("path", "C:\\Projects\\clipboard-manager\\src-tauri\\src\\clipboard.rs", "Code", 21, { tagIds: [3] }),
  item("text", "Sprint planlaması perşembe 14:00'te, demo cuma 11:00'de. Gündem: pano geçmişi, etiketler, ayarlar ekranı.", "Notepad", 48, { tagIds: [1] }),
  item("text", "fn fnv1a(prefix: &[u8], data: &[u8]) -> u64 {", "Code", 95, { tagIds: [3] }),
  item("link", "https://learn.microsoft.com/windows/win32/dataxchg/clipboard", "msedge", 160, { tagIds: [4] }),
  item("text", "destek@ornek.com.tr", "OUTLOOK", 230),
  item("text", "#4F46E5", "Figma", 410),
  item("path", "\\\\fileserver\\paylasim\\raporlar\\2026-Q3", "explorer", 1300),
  item("text", "Merhaba, toplantı notlarını ekte paylaşıyorum. Sorunuz olursa yazabilirsiniz.", "OUTLOOK", 1500),
];

const settings: Settings = {
  openShortcut: "Alt+V",
  pasteLastShortcut: "Ctrl+Alt+V",
  maxItems: 250,
  retentionDays: 30,
  launchAtStartup: true,
  captureImages: true,
  ignorePasswords: true,
  theme,
};

const callbacks = new Map<number, (e: unknown) => void>();
let callbackId = 1;

function handle(cmd: string, args: Record<string, unknown>): unknown {
  switch (cmd) {
    case "get_items": {
      const q = String(args.query ?? "").toLowerCase();
      const tagId = args.tagId as number | null;
      return items.filter(
        (i) => (!q || i.content.toLowerCase().includes(q)) && (tagId == null || i.tagIds.includes(tagId)),
      );
    }
    case "get_tags":
      return {
        total: items.length,
        tags: tags.map((t) => ({ ...t, count: items.filter((i) => i.tagIds.includes(t.id)).length })),
      };
    case "get_settings":
      return settings;
    case "save_settings":
      return args.settings;
    case "plugin:event|listen": {
      const handler = callbacks.get(args.handler as number);
      if (args.event === "open-settings" && view === "settings" && handler) {
        setTimeout(() => handler({ event: "open-settings", id: 0, payload: null }), 30);
      }
      return args.handler;
    }
    default:
      return null;
  }
}

(window as any).__TAURI_INTERNALS__ = {
  invoke: async (cmd: string, args: Record<string, unknown> = {}) => handle(cmd, args),
  transformCallback: (cb: (e: unknown) => void) => {
    const id = callbackId++;
    callbacks.set(id, cb);
    return id;
  },
  convertFileSrc: (path: string) => path,
};
(window as any).__TAURI_EVENT_PLUGIN_INTERNALS__ = { unregisterListener: () => {} };

// Headless Edge'in sanal zamanında CSS geçişleri ilerlemiyor; tema değişimi yarım kalmasın.
const noTransitions = document.createElement("style");
noTransitions.textContent = "*,*::before,*::after{transition:none!important}";
document.head.append(noTransitions);
