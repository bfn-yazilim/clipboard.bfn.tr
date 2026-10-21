import { memo, useEffect, useRef } from "react";
import { convertFileSrc } from "@tauri-apps/api/core";
import type { ClipItem, ItemKind } from "../types";
import { formatDate } from "../lib/format";
import { StarIcon } from "./icons";

const KIND_BADGE: Record<ItemKind, { label: string; className: string }> = {
  text: { label: "T", className: "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300" },
  path: { label: "/", className: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300" },
  link: { label: "@", className: "bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300" },
  image: { label: "▣", className: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300" },
};

interface Props {
  item: ClipItem;
  index: number;
  selected: boolean;
  onHover: (index: number) => void;
  onCopy: (item: ClipItem) => void;
  onTogglePin: (item: ClipItem) => void;
  onContextMenu: (item: ClipItem, x: number, y: number) => void;
}

export const ItemRow = memo(function ItemRow({ item, index, selected, onHover, onCopy, onTogglePin, onContextMenu }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const badge = KIND_BADGE[item.kind] ?? KIND_BADGE.text;

  useEffect(() => {
    if (selected) ref.current?.scrollIntoView({ block: "nearest" });
  }, [selected]);

  const meta = [
    item.kind === "image" ? null : `${item.charCount.toLocaleString("tr-TR")} karakter`,
    formatDate(item.createdAt),
    item.sourceApp,
  ].filter(Boolean);

  return (
    <div
      ref={ref}
      onMouseMove={() => !selected && onHover(index)}
      onClick={() => onCopy(item)}
      onContextMenu={(e) => {
        e.preventDefault();
        onHover(index);
        onContextMenu(item, e.clientX, e.clientY);
      }}
      className={`group flex cursor-default items-center gap-3 rounded-lg px-2.5 py-2 ${
        selected ? "bg-indigo-50 dark:bg-indigo-500/10" : ""
      }`}
    >
      {item.kind === "image" && item.imagePath ? (
        <img
          src={convertFileSrc(item.imagePath)}
          loading="lazy"
          className="size-8 shrink-0 rounded-md border border-zinc-200 object-cover dark:border-zinc-700"
          alt=""
        />
      ) : (
        <div className={`grid size-8 shrink-0 place-items-center rounded-md font-mono text-sm font-semibold ${badge.className}`}>
          {badge.label}
        </div>
      )}

      <div className="min-w-0 flex-1">
        <div className="truncate text-[13px] leading-5">{item.content}</div>
        <div className="truncate text-[11px] leading-4 text-zinc-400 dark:text-zinc-500">{meta.join(" · ")}</div>
      </div>

      <button
        onClick={(e) => {
          e.stopPropagation();
          onTogglePin(item);
        }}
        title={item.pinned ? "Sabitlemeyi kaldır" : "Sabitle"}
        className={`shrink-0 rounded p-1 transition-opacity ${
          item.pinned
            ? "text-amber-500"
            : "text-zinc-300 opacity-0 group-hover:opacity-100 hover:text-amber-500 dark:text-zinc-600"
        } ${selected ? "opacity-100" : ""}`}
      >
        <StarIcon className="size-4" filled={item.pinned} />
      </button>
    </div>
  );
});
