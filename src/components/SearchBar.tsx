import { forwardRef } from "react";
import { GearIcon, SearchIcon, TrashIcon, XIcon } from "./icons";

interface Props {
  value: string;
  onChange: (v: string) => void;
  onClear: () => void;
  onOpenSettings: () => void;
}

export const SearchBar = forwardRef<HTMLInputElement, Props>(({ value, onChange, onClear, onOpenSettings }, ref) => (
  <div className="flex items-center gap-2 px-3 pt-3 pb-2">
    <div className="relative flex-1">
      <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
      <input
        ref={ref}
        autoFocus
        spellCheck={false}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Ara…  (Ctrl + F)"
        className="h-9 w-full rounded-lg border border-zinc-200 bg-zinc-50 pr-8 pl-9 outline-none placeholder:text-zinc-400 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 dark:border-zinc-700 dark:bg-zinc-800"
      />
      {value && (
        <button
          onClick={() => onChange("")}
          className="absolute top-1/2 right-2 -translate-y-1/2 rounded p-0.5 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200"
          title="Temizle (Esc)"
        >
          <XIcon className="size-4" />
        </button>
      )}
    </div>
    <button
      onClick={onClear}
      title="Temizle – sabitlenmişler korunur (Ctrl + Shift + Del)"
      className="flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-xs font-medium text-zinc-500 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
    >
      <TrashIcon className="size-4" />
      Temizle
    </button>
    <button
      onClick={onOpenSettings}
      title="Ayarlar (Ctrl + ,)"
      className="grid size-9 place-items-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-800 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
    >
      <GearIcon className="size-5" />
    </button>
  </div>
));
