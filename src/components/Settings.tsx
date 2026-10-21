import { useState } from "react";
import type { Settings as SettingsT, Tag, Theme } from "../types";
import { api } from "../api";
import { codeToKey, formatShortcut } from "../lib/format";
import { BackIcon, XIcon } from "./icons";

interface Props {
  settings: SettingsT;
  tags: Tag[];
  onChange: (patch: Partial<SettingsT>) => Promise<void>;
  onAddTag: (name: string) => Promise<void>;
  onDeleteTag: (id: number) => Promise<void>;
  onClearHistory: () => Promise<void>;
  onBack: () => void;
}

const MAX_ITEMS = [50, 100, 250, 500, 1000, 0];
const RETENTION = [1, 7, 30, 90, 365, 0];

export function Settings({ settings, tags, onChange, onAddTag, onDeleteTag, onClearHistory, onBack }: Props) {
  const [confirmClear, setConfirmClear] = useState(false);
  const [newTag, setNewTag] = useState("");

  const addTag = async () => {
    if (!newTag.trim()) return;
    await onAddTag(newTag.trim());
    setNewTag("");
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div data-tauri-drag-region className="flex items-center gap-2 border-b border-zinc-200 px-3 py-3 dark:border-zinc-800">
        <button onClick={onBack} title="Geri (Esc)" className="rounded-md p-1 text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800">
          <BackIcon className="size-5" />
        </button>
        <h1 data-tauri-drag-region className="text-base font-semibold">Ayarlar</h1>
      </div>

      <div className="flex-1 space-y-6 overflow-y-auto px-5 py-4">
        <Section title="Genel">
          <Row label="Panoyu aç">
            <ShortcutInput value={settings.openShortcut} onChange={(v) => onChange({ openShortcut: v })} />
          </Row>
          <Row label="Son öğeyi yapıştır">
            <ShortcutInput value={settings.pasteLastShortcut} onChange={(v) => onChange({ pasteLastShortcut: v })} />
          </Row>
          <Row label="Maksimum öğe sayısı">
            <Select
              value={settings.maxItems}
              options={MAX_ITEMS.map((n) => [n, n === 0 ? "Sınırsız" : `${n} öğe`])}
              onChange={(v) => onChange({ maxItems: v })}
            />
          </Row>
          <Row label="Saklama süresi" hint="Sabitlenmiş öğeler silinmez">
            <Select
              value={settings.retentionDays}
              options={RETENTION.map((n) => [n, n === 0 ? "Sınırsız" : `${n} gün`])}
              onChange={(v) => onChange({ retentionDays: v })}
            />
          </Row>
          <Row label="Geçmişi temizle" hint="Sabitlenmişler dahil tüm öğeler silinir">
            {confirmClear ? (
              <div className="flex gap-2">
                <button onClick={() => setConfirmClear(false)} className="h-8 rounded-md px-3 text-xs hover:bg-zinc-100 dark:hover:bg-zinc-800">
                  Vazgeç
                </button>
                <button
                  onClick={async () => {
                    await onClearHistory();
                    setConfirmClear(false);
                  }}
                  className="h-8 rounded-md bg-red-600 px-3 text-xs font-medium text-white hover:bg-red-700"
                >
                  Evet, temizle
                </button>
              </div>
            ) : (
              <button
                onClick={() => setConfirmClear(true)}
                className="h-8 rounded-md border border-red-300 px-3 text-xs font-medium text-red-600 hover:bg-red-50 dark:border-red-500/40 dark:text-red-400 dark:hover:bg-red-500/10"
              >
                Temizle
              </button>
            )}
          </Row>
        </Section>

        <Section title="Sistem & Davranış">
          <Row label="Windows ile başlat">
            <Toggle checked={settings.launchAtStartup} onChange={(v) => onChange({ launchAtStartup: v })} />
          </Row>
          <Row label="Görselleri yakala" hint="Görseller PNG olarak yerel diske kaydedilir">
            <Toggle checked={settings.captureImages} onChange={(v) => onChange({ captureImages: v })} />
          </Row>
          <Row label="Şifre yakalamayı engelle" hint="Parola yöneticilerinden gelen kopyalar yoksayılır">
            <Toggle checked={settings.ignorePasswords} onChange={(v) => onChange({ ignorePasswords: v })} />
          </Row>
        </Section>

        <Section title="Etiket Yönetimi">
          <div className="flex flex-wrap gap-1.5">
            {tags.length === 0 && <span className="text-xs text-zinc-400">Henüz etiket yok.</span>}
            {tags.map((t) => (
              <span key={t.id} className="flex h-7 items-center gap-1 rounded-md bg-zinc-100 pr-1 pl-2.5 text-xs dark:bg-zinc-800">
                {t.name}
                <span className="text-zinc-400">({t.count})</span>
                <button onClick={() => onDeleteTag(t.id)} title="Sil" className="rounded p-0.5 text-zinc-400 hover:bg-zinc-200 hover:text-red-500 dark:hover:bg-zinc-700">
                  <XIcon className="size-3.5" />
                </button>
              </span>
            ))}
          </div>
          <div className="flex gap-2">
            <input
              value={newTag}
              onChange={(e) => setNewTag(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && addTag()}
              placeholder="Yeni etiket adı"
              className="h-8 flex-1 rounded-md border border-zinc-200 bg-transparent px-2.5 text-sm outline-none focus:border-indigo-500 dark:border-zinc-700"
            />
            <button onClick={addTag} className="h-8 rounded-md bg-indigo-600 px-3 text-xs font-medium text-white hover:bg-indigo-700">
              Ekle
            </button>
          </div>
        </Section>

        <Section title="Görünüm">
          <Row label="Tema">
            <div className="flex rounded-lg bg-zinc-100 p-0.5 dark:bg-zinc-800">
              {(
                [
                  ["dark", "Koyu"],
                  ["light", "Açık"],
                  ["system", "Sistem"],
                ] as [Theme, string][]
              ).map(([value, label]) => (
                <button
                  key={value}
                  onClick={() => onChange({ theme: value })}
                  className={`h-7 rounded-md px-3 text-xs font-medium ${
                    settings.theme === value ? "bg-white shadow-sm dark:bg-zinc-700" : "text-zinc-500"
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </Row>
        </Section>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="text-[11px] font-semibold tracking-wide text-zinc-400 uppercase">{title}</h2>
      {children}
    </section>
  );
}

function Row({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div className="min-w-0">
        <div className="text-sm">{label}</div>
        {hint && <div className="text-[11px] text-zinc-400">{hint}</div>}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={`relative h-5 w-9 rounded-full transition-colors ${checked ? "bg-indigo-600" : "bg-zinc-300 dark:bg-zinc-600"}`}
    >
      <span className={`absolute top-0.5 left-0.5 size-4 rounded-full bg-white shadow transition-transform ${checked ? "translate-x-4" : ""}`} />
    </button>
  );
}

function Select({ value, options, onChange }: { value: number; options: [number, string][]; onChange: (v: number) => void }) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(Number(e.target.value))}
      className="h-8 rounded-md border border-zinc-200 bg-white px-2 text-sm outline-none focus:border-indigo-500 dark:border-zinc-700 dark:bg-zinc-800"
    >
      {options.map(([v, label]) => (
        <option key={v} value={v}>
          {label}
        </option>
      ))}
    </select>
  );
}

/** Tıklanınca tuş kombinasyonunu kaydeder. Kayıt sırasında global kısayollar askıya alınır. */
function ShortcutInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const [recording, setRecording] = useState(false);

  const start = () => {
    setRecording(true);
    api.suspendShortcuts();
  };
  const cancel = () => {
    setRecording(false);
    api.resumeShortcuts();
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (!recording) return;
    e.preventDefault();
    e.stopPropagation();
    if (e.key === "Escape") return cancel();
    const key = codeToKey(e.code);
    if (!key) return; // yalnızca modifier'a basıldı
    const mods = [e.ctrlKey && "Ctrl", e.altKey && "Alt", e.shiftKey && "Shift", e.metaKey && "Super"].filter(Boolean);
    if (mods.length === 0) return; // en az bir modifier zorunlu
    setRecording(false);
    // save_settings kısayolları yeniden kaydeder; resume gerekmez.
    onChange([...mods, key].join("+"));
  };

  return (
    <button
      onClick={recording ? undefined : start}
      onKeyDown={onKeyDown}
      onBlur={() => recording && cancel()}
      className={`h-8 min-w-36 rounded-md border px-3 font-mono text-xs ${
        recording
          ? "border-indigo-500 text-indigo-600 ring-2 ring-indigo-500/20 dark:text-indigo-400"
          : "border-zinc-200 hover:border-zinc-300 dark:border-zinc-700"
      }`}
    >
      {recording ? "Tuşlara basın…" : formatShortcut(value)}
    </button>
  );
}
