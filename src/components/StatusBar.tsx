interface Props {
  left: string;
  right: string;
}

// Dekorasyonsuz pencere: durum çubuğu aynı zamanda sürükleme alanıdır.
export function StatusBar({ left, right }: Props) {
  return (
    <div
      data-tauri-drag-region
      className="flex h-7 shrink-0 items-center justify-between gap-3 border-t border-zinc-200 bg-zinc-50 px-3 text-[11px] text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-400"
    >
      <span data-tauri-drag-region className="truncate">{left}</span>
      <span data-tauri-drag-region className="shrink-0">{right}</span>
    </div>
  );
}
