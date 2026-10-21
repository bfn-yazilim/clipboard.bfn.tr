import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { ClipItem, Tag } from "../types";
import { CheckIcon } from "./icons";

interface Props {
  item: ClipItem;
  x: number;
  y: number;
  tags: Tag[];
  onClose: () => void;
  onCopy: (paste: boolean) => void;
  onTogglePin: () => void;
  onToggleTag: (tagId: number, enabled: boolean) => void;
  onDelete: () => void;
}

export function ContextMenu({ item, x, y, tags, onClose, onCopy, onTogglePin, onToggleTag, onDelete }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const [pos, setPos] = useState({ x, y });

  // Menüyü pencere sınırları içinde tut.
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const { width, height } = el.getBoundingClientRect();
    setPos({
      x: Math.min(x, window.innerWidth - width - 8),
      y: Math.min(y, window.innerHeight - height - 8),
    });
  }, [x, y]);

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) onClose();
    };
    window.addEventListener("mousedown", close);
    window.addEventListener("blur", onClose);
    return () => {
      window.removeEventListener("mousedown", close);
      window.removeEventListener("blur", onClose);
    };
  }, [onClose]);

  const run = (fn: () => void) => () => {
    fn();
    onClose();
  };

  return (
    <div
      ref={ref}
      style={{ left: pos.x, top: pos.y }}
      className="fixed z-50 min-w-48 rounded-lg border border-zinc-200 bg-white p-1 text-[13px] shadow-xl dark:border-zinc-700 dark:bg-zinc-800"
    >
      <MenuButton onClick={run(() => onCopy(false))} hint="Enter">Kopyala</MenuButton>
      <MenuButton onClick={run(() => onCopy(true))} hint="Shift+Enter">Kopyala ve yapıştır</MenuButton>
      <MenuButton onClick={run(onTogglePin)}>{item.pinned ? "Sabitlemeyi kaldır" : "Sabitle"}</MenuButton>

      {tags.length > 0 && (
        <>
          <Divider />
          <div className="px-2.5 py-1 text-[11px] font-semibold text-zinc-400 uppercase">Etiketler</div>
          {tags.map((t) => {
            const on = item.tagIds.includes(t.id);
            return (
              <MenuButton key={t.id} onClick={run(() => onToggleTag(t.id, !on))}>
                <span className="flex items-center gap-2">
                  <span className="grid size-4 place-items-center text-indigo-500">{on && <CheckIcon className="size-4" />}</span>
                  {t.name}
                </span>
              </MenuButton>
            );
          })}
        </>
      )}

      <Divider />
      <MenuButton onClick={run(onDelete)} danger>Sil</MenuButton>
    </div>
  );
}

function MenuButton({ children, onClick, hint, danger }: { children: React.ReactNode; onClick: () => void; hint?: string; danger?: boolean }) {
  return (
    <button
      onClick={onClick}
      className={`flex w-full items-center justify-between gap-6 rounded-md px-2.5 py-1.5 text-left hover:bg-zinc-100 dark:hover:bg-zinc-700 ${
        danger ? "text-red-600 dark:text-red-400" : ""
      }`}
    >
      {children}
      {hint && <span className="text-[11px] text-zinc-400">{hint}</span>}
    </button>
  );
}

const Divider = () => <div className="my-1 h-px bg-zinc-200 dark:bg-zinc-700" />;
