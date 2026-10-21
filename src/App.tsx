import { useCallback, useEffect, useRef, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { api } from "./api";
import type { ClipItem, Settings as SettingsT, TagSummary } from "./types";
import { useTheme } from "./hooks/useTheme";
import { SearchBar } from "./components/SearchBar";
import { TagBar } from "./components/TagBar";
import { ItemList } from "./components/ItemList";
import { StatusBar } from "./components/StatusBar";
import { ContextMenu } from "./components/ContextMenu";
import { Settings } from "./components/Settings";

type View = "list" | "settings";
type Menu = { item: ClipItem; x: number; y: number } | null;

export default function App() {
  const [view, setView] = useState<View>("list");
  const [query, setQuery] = useState("");
  const [activeTag, setActiveTag] = useState<number | null>(null);
  const [items, setItems] = useState<ClipItem[]>([]);
  const [tagSummary, setTagSummary] = useState<TagSummary>({ total: 0, tags: [] });
  const [selected, setSelected] = useState(0);
  const [settings, setSettings] = useState<SettingsT | null>(null);
  const [menu, setMenu] = useState<Menu>(null);
  const [error, setError] = useState<string | null>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  useTheme(settings?.theme ?? "system");

  const refresh = useCallback(async () => {
    const [list, tags] = await Promise.all([api.getItems(query, activeTag), api.getTags()]);
    setItems(list);
    setTagSummary(tags);
  }, [query, activeTag]);

  // Arama yazarken kısa debounce.
  useEffect(() => {
    const t = setTimeout(refresh, query ? 80 : 0);
    return () => clearTimeout(t);
  }, [refresh, query]);

  useEffect(() => setSelected(0), [query, activeTag]);
  useEffect(() => setSelected((s) => Math.min(s, Math.max(items.length - 1, 0))), [items.length]);

  // Backend olayları
  const refreshRef = useRef(refresh);
  refreshRef.current = refresh;
  useEffect(() => {
    api.getSettings().then(setSettings);
    const unlisten = [
      listen("clipboard-changed", () => refreshRef.current()),
      listen("window-shown", () => {
        setView("list");
        setMenu(null);
        setQuery("");
        setSelected(0);
        refreshRef.current();
        requestAnimationFrame(() => searchRef.current?.focus());
      }),
      listen("open-settings", () => setView("settings")),
    ];
    return () => unlisten.forEach((p) => p.then((off) => off()));
  }, []);

  const copy = useCallback((item: ClipItem, paste = false) => {
    api.copyItem(item.id, paste).catch((e) => setError(String(e)));
  }, []);

  // Fareyle tıklama: kopyala ve önceki uygulamada imlecin olduğu yere yapıştır.
  const copyAndPaste = useCallback((item: ClipItem) => copy(item, true), [copy]);

  const togglePin =useCallback(async (item: ClipItem) => {
    await api.togglePin(item.id);
    refreshRef.current();
  }, []);

  const clearUnpinned = useCallback(async () => {
    await api.clearUnpinned();
    refreshRef.current();
  }, []);

  const openMenu =useCallback((item: ClipItem, x: number, y: number) => setMenu({ item, x, y }), []);
  const closeMenu = useCallback(() => setMenu(null), []);

  const addTag = async (name: string) => {
    try {
      await api.addTag(name);
      await refresh();
    } catch (e) {
      setError(String(e));
    }
  };

  const updateSettings = async (patch: Partial<SettingsT>) => {
    if (!settings) return;
    try {
      setSettings(await api.saveSettings({ ...settings, ...patch }));
      setError(null);
    } catch (e) {
      setError(String(e));
    }
  };

  // Klavye kısayolları
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === ",") {
        e.preventDefault();
        setMenu(null);
        setView((v) => (v === "list" ? "settings" : "list"));
        return;
      }
      if (e.key === "Escape") {
        e.preventDefault();
        if (menu) setMenu(null);
        else if (view === "settings") setView("list");
        else if (query) setQuery("");
        else api.hideWindow();
        return;
      }
      if (view !== "list") return;

      if (e.ctrlKey && e.shiftKey && e.key === "Delete") {
        e.preventDefault();
        clearUnpinned();
      } else if (e.ctrlKey && e.key.toLowerCase() === "f") {
        e.preventDefault();
        searchRef.current?.focus();
        searchRef.current?.select();
      } else if (e.key === "ArrowDown") {
        e.preventDefault();
        setSelected((s) => Math.min(s + 1, items.length - 1));
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        setSelected((s) => Math.max(s - 1, 0));
      } else if (e.key === "Enter" && items[selected]) {
        e.preventDefault();
        copy(items[selected], e.shiftKey);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [view, query, items, selected, menu, copy, clearUnpinned]);

  const activeTagName =
    activeTag === null ? "Tümü" : (tagSummary.tags.find((t) => t.id === activeTag)?.name ?? "Tümü");

  return (
    <div className="flex h-full flex-col border border-zinc-200 dark:border-zinc-800">
      {view === "list" ? (
        <>
          <SearchBar ref={searchRef} value={query} onChange={setQuery} onClear={clearUnpinned} onOpenSettings={() => setView("settings")} />
          <TagBar summary={tagSummary} active={activeTag} onSelect={setActiveTag} onAdd={addTag} />
          <ItemList
            items={items}
            selected={selected}
            hasQuery={!!query || activeTag !== null}
            onHover={setSelected}
            onCopy={copyAndPaste}
            onTogglePin={togglePin}
            onContextMenu={openMenu}
          />
        </>
      ) : (
        settings && (
          <Settings
            settings={settings}
            tags={tagSummary.tags}
            onChange={updateSettings}
            onAddTag={addTag}
            onDeleteTag={async (id) => {
              await api.deleteTag(id);
              if (activeTag === id) setActiveTag(null);
              await refresh();
            }}
            onClearHistory={async () => {
              await api.clearHistory();
              await refresh();
            }}
            onBack={() => setView("list")}
          />
        )
      )}

      {error && (
        <div className="flex items-start justify-between gap-3 border-t border-red-200 bg-red-50 px-3 py-2 text-xs whitespace-pre-line text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
          {error}
          <button onClick={() => setError(null)} className="font-semibold">Kapat</button>
        </div>
      )}

      <StatusBar
        left={`${items.length} öğe · ${activeTagName}`}
        right="↑↓ gez · Enter kopyala · Ctrl , ayarlar"
      />

      {menu && (
        <ContextMenu
          {...menu}
          tags={tagSummary.tags}
          onClose={closeMenu}
          onCopy={(paste) => copy(menu.item, paste)}
          onTogglePin={() => togglePin(menu.item)}
          onToggleTag={async (tagId, enabled) => {
            await api.setItemTag(menu.item.id, tagId, enabled);
            refresh();
          }}
          onDelete={async () => {
            await api.deleteItem(menu.item.id);
            refresh();
          }}
        />
      )}
    </div>
  );
}
