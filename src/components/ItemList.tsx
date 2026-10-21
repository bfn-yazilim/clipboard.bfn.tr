import { Fragment } from "react";
import type { ClipItem } from "../types";
import { ItemRow } from "./ItemRow";

interface Props {
  items: ClipItem[];
  selected: number;
  hasQuery: boolean;
  onHover: (index: number) => void;
  onCopy: (item: ClipItem) => void;
  onTogglePin: (item: ClipItem) => void;
  onContextMenu: (item: ClipItem, x: number, y: number) => void;
}

export function ItemList({ items, selected, hasQuery, ...handlers }: Props) {
  if (items.length === 0) {
    return (
      <div className="grid flex-1 place-items-center px-6 text-center text-sm text-zinc-400">
        {hasQuery ? "Sonuç bulunamadı." : "Henüz bir şey kopyalanmadı. Kopyaladığınız her şey burada görünecek."}
      </div>
    );
  }

  // Backend sabitlenmişleri her zaman önce döndürür.
  const pinnedCount = items.findIndex((i) => !i.pinned) === -1 ? items.length : items.findIndex((i) => !i.pinned);

  return (
    <div className="flex-1 overflow-y-auto px-2 py-1">
      {items.map((item, index) => (
        <Fragment key={item.id}>
          {pinnedCount > 0 && index === 0 && <SectionHeader>Sabitlenmiş</SectionHeader>}
          {pinnedCount > 0 && index === pinnedCount && <SectionHeader>Geçmiş</SectionHeader>}
          <ItemRow item={item} index={index} selected={index === selected} {...handlers} />
        </Fragment>
      ))}
    </div>
  );
}

function SectionHeader({ children }: { children: string }) {
  return (
    <div className="px-2.5 pt-2 pb-1 text-[11px] font-semibold tracking-wide text-zinc-400 uppercase dark:text-zinc-500">
      {children}
    </div>
  );
}
