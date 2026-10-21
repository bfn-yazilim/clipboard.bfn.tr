import { useState } from "react";
import type { TagSummary } from "../types";

interface Props {
  summary: TagSummary;
  active: number | null;
  onSelect: (id: number | null) => void;
  onAdd: (name: string) => Promise<void>;
}

export function TagBar({ summary, active, onSelect, onAdd }: Props) {
  const [adding, setAdding] = useState(false);
  const [name, setName] = useState("");

  const submit = async () => {
    const trimmed = name.trim();
    if (trimmed) await onAdd(trimmed);
    setName("");
    setAdding(false);
  };

  return (
    <div className="flex items-center gap-2 border-b border-zinc-200 px-3 pb-2 dark:border-zinc-800">
      <div
        className="no-scrollbar flex flex-1 gap-1.5 overflow-x-auto"
        // Dikey tekerleği yatay kaydırmaya çevir.
        onWheel={(e) => (e.currentTarget.scrollLeft += e.deltaY)}
      >
        <TagChip label="Tümü" count={summary.total} active={active === null} onClick={() => onSelect(null)} />
        {summary.tags.map((t) => (
          <TagChip key={t.id} label={t.name} count={t.count} active={active === t.id} onClick={() => onSelect(t.id)} />
        ))}
      </div>

      {adding ? (
        <input
          autoFocus
          value={name}
          onChange={(e) => setName(e.target.value)}
          onBlur={submit}
          onKeyDown={(e) => {
            e.stopPropagation();
            if (e.key === "Enter") submit();
            if (e.key === "Escape") {
              setName("");
              setAdding(false);
            }
          }}
          placeholder="Etiket adı"
          className="h-7 w-28 shrink-0 rounded-md border border-indigo-500 bg-transparent px-2 text-xs outline-none"
        />
      ) : (
        <button
          onClick={() => setAdding(true)}
          className="h-7 shrink-0 rounded-md border border-dashed border-zinc-300 px-2.5 text-xs text-zinc-500 hover:border-indigo-500 hover:text-indigo-600 dark:border-zinc-600 dark:hover:text-indigo-400"
        >
          + Etiket ekle
        </button>
      )}
    </div>
  );
}

function TagChip({ label, count, active, onClick }: { label: string; count: number; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={`flex h-7 shrink-0 items-center gap-1.5 rounded-md px-2.5 text-xs font-medium transition-colors ${
        active
          ? "bg-indigo-600 text-white"
          : "bg-zinc-100 text-zinc-700 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700"
      }`}
    >
      {label}
      <span
        className={`min-w-5 rounded-full px-1.5 text-[10px] leading-4 ${
          active ? "bg-white/25" : "bg-zinc-200 text-zinc-500 dark:bg-zinc-700 dark:text-zinc-400"
        }`}
      >
        {count}
      </span>
    </button>
  );
}
