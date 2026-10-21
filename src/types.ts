export type ItemKind = "text" | "path" | "link" | "image";

export interface ClipItem {
  id: number;
  kind: ItemKind;
  /** İlk 300 karakterlik önizleme; tam metin yalnızca backend'de. */
  content: string;
  charCount: number;
  sourceApp: string | null;
  imagePath: string | null;
  pinned: boolean;
  createdAt: number;
  tagIds: number[];
}

export interface Tag {
  id: number;
  name: string;
  count: number;
}

export interface TagSummary {
  total: number;
  tags: Tag[];
}

export type Theme = "dark" | "light" | "system";

export interface Settings {
  openShortcut: string;
  pasteLastShortcut: string;
  maxItems: number;
  retentionDays: number;
  launchAtStartup: boolean;
  captureImages: boolean;
  ignorePasswords: boolean;
  theme: Theme;
}
