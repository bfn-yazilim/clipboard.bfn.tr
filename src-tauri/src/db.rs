use std::path::Path;

use rusqlite::{params, Connection, OptionalExtension};
use serde::Serialize;

use crate::settings::Settings;

const SCHEMA: &str = r#"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS items (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    kind        TEXT    NOT NULL,              -- text | path | link | image
    content     TEXT    NOT NULL,
    hash        INTEGER NOT NULL UNIQUE,       -- tekrar eden kopyaları birleştirmek için
    char_count  INTEGER NOT NULL,
    source_app  TEXT,
    image_path  TEXT,
    pinned      INTEGER NOT NULL DEFAULT 0,
    created_at  INTEGER NOT NULL               -- unix ms
);
CREATE INDEX IF NOT EXISTS idx_items_order ON items(pinned DESC, created_at DESC);

CREATE TABLE IF NOT EXISTS tags (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE IF NOT EXISTS item_tags (
    item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    tag_id  INTEGER NOT NULL REFERENCES tags(id)  ON DELETE CASCADE,
    PRIMARY KEY (item_id, tag_id)
);
CREATE INDEX IF NOT EXISTS idx_item_tags_tag ON item_tags(tag_id);

CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
"#;

/// Listede gösterilecek önizleme uzunluğu; tam metin yalnızca kopyalarken okunur.
const PREVIEW_CHARS: i64 = 300;

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Item {
    pub id: i64,
    pub kind: String,
    pub content: String,
    pub char_count: i64,
    pub source_app: Option<String>,
    pub image_path: Option<String>,
    pub pinned: bool,
    pub created_at: i64,
    pub tag_ids: Vec<i64>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Tag {
    pub id: i64,
    pub name: String,
    pub count: i64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TagSummary {
    pub total: i64,
    pub tags: Vec<Tag>,
}

pub struct NewItem<'a> {
    pub kind: &'a str,
    pub content: &'a str,
    pub hash: u64,
    pub char_count: i64,
    pub source_app: Option<&'a str>,
    pub image_path: Option<&'a str>,
}

pub struct ItemPayload {
    pub kind: String,
    pub content: String,
    pub image_path: Option<String>,
}

pub fn open(path: &Path) -> rusqlite::Result<Connection> {
    let conn = Connection::open(path)?;
    conn.execute_batch(SCHEMA)?;
    let seeded: bool = conn.query_row(
        "SELECT EXISTS(SELECT 1 FROM settings WHERE key = 'seeded')",
        [],
        |r| r.get(0),
    )?;
    if !seeded {
        conn.execute_batch(
            "INSERT OR IGNORE INTO tags(name) VALUES ('İş'), ('Şifre');
             INSERT INTO settings(key, value) VALUES ('seeded', '1');",
        )?;
    }
    Ok(conn)
}

// ---------- Öğeler ----------

/// Aynı içerik tekrar kopyalanırsa yeni satır açmak yerine mevcut satırı en üste taşır.
pub fn upsert_item(conn: &Connection, item: &NewItem, now: i64) -> rusqlite::Result<()> {
    conn.execute(
        "INSERT INTO items (kind, content, hash, char_count, source_app, image_path, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)
         ON CONFLICT(hash) DO UPDATE SET created_at = excluded.created_at",
        params![
            item.kind,
            item.content,
            item.hash as i64,
            item.char_count,
            item.source_app,
            item.image_path,
            now
        ],
    )?;
    Ok(())
}

pub fn list_items(conn: &Connection, query: &str, tag_id: Option<i64>) -> rusqlite::Result<Vec<Item>> {
    let query = query.trim();
    let pattern = format!("%{}%", escape_like(query));
    let mut stmt = conn.prepare_cached(
        "SELECT i.id, i.kind, substr(i.content, 1, ?4), i.char_count, i.source_app,
                i.image_path, i.pinned, i.created_at,
                (SELECT group_concat(tag_id) FROM item_tags t WHERE t.item_id = i.id)
         FROM items i
         WHERE (?1 OR i.content LIKE ?2 ESCAPE '\' OR i.source_app LIKE ?2 ESCAPE '\')
           AND (?3 IS NULL OR EXISTS (SELECT 1 FROM item_tags t WHERE t.item_id = i.id AND t.tag_id = ?3))
         ORDER BY i.pinned DESC, i.created_at DESC",
    )?;
    let rows = stmt.query_map(params![query.is_empty(), pattern, tag_id, PREVIEW_CHARS], |r| {
        let tags: Option<String> = r.get(8)?;
        Ok(Item {
            id: r.get(0)?,
            kind: r.get(1)?,
            content: r.get(2)?,
            char_count: r.get(3)?,
            source_app: r.get(4)?,
            image_path: r.get(5)?,
            pinned: r.get(6)?,
            created_at: r.get(7)?,
            tag_ids: tags
                .map(|s| s.split(',').filter_map(|t| t.parse().ok()).collect())
                .unwrap_or_default(),
        })
    })?;
    rows.collect()
}

pub fn get_payload(conn: &Connection, id: i64) -> rusqlite::Result<Option<ItemPayload>> {
    conn.query_row(
        "SELECT kind, content, image_path FROM items WHERE id = ?1",
        [id],
        |r| {
            Ok(ItemPayload {
                kind: r.get(0)?,
                content: r.get(1)?,
                image_path: r.get(2)?,
            })
        },
    )
    .optional()
}

pub fn latest_item_id(conn: &Connection) -> rusqlite::Result<Option<i64>> {
    conn.query_row("SELECT id FROM items ORDER BY created_at DESC LIMIT 1", [], |r| r.get(0))
        .optional()
}

pub fn touch_item(conn: &Connection, id: i64, now: i64) -> rusqlite::Result<()> {
    conn.execute("UPDATE items SET created_at = ?2 WHERE id = ?1", params![id, now])?;
    Ok(())
}

pub fn toggle_pin(conn: &Connection, id: i64) -> rusqlite::Result<bool> {
    conn.query_row(
        "UPDATE items SET pinned = 1 - pinned WHERE id = ?1 RETURNING pinned",
        [id],
        |r| r.get(0),
    )
}

pub fn set_item_tag(conn: &Connection, item_id: i64, tag_id: i64, enabled: bool) -> rusqlite::Result<()> {
    if enabled {
        conn.execute(
            "INSERT OR IGNORE INTO item_tags(item_id, tag_id) VALUES (?1, ?2)",
            params![item_id, tag_id],
        )?;
    } else {
        conn.execute(
            "DELETE FROM item_tags WHERE item_id = ?1 AND tag_id = ?2",
            params![item_id, tag_id],
        )?;
    }
    Ok(())
}

/// Silinen öğelerin görsel dosya yollarını döndürür (diskten de silinsinler diye).
pub fn delete_item(conn: &Connection, id: i64) -> rusqlite::Result<Vec<String>> {
    collect_paths(conn, "DELETE FROM items WHERE id = ?1 RETURNING image_path", params![id])
}

pub fn clear_items(conn: &Connection) -> rusqlite::Result<Vec<String>> {
    collect_paths(conn, "DELETE FROM items RETURNING image_path", [])
}

pub fn clear_unpinned(conn: &Connection) -> rusqlite::Result<Vec<String>> {
    collect_paths(conn, "DELETE FROM items WHERE pinned = 0 RETURNING image_path", [])
}

/// Otomatik temizlik: süresi dolan ve limit dışında kalan öğeleri siler.
/// Sabitlenmiş öğeler hiçbir zaman silinmez ve limite dahil edilmez.
pub fn prune(conn: &Connection, max_items: u32, retention_days: u32, now: i64) -> rusqlite::Result<Vec<String>> {
    let cutoff = now - i64::from(retention_days) * 86_400_000;
    let limit: i64 = if max_items == 0 { -1 } else { i64::from(max_items) };
    collect_paths(
        conn,
        "DELETE FROM items WHERE pinned = 0 AND (
             (?1 > 0 AND created_at < ?2)
             OR id NOT IN (SELECT id FROM items WHERE pinned = 0 ORDER BY created_at DESC LIMIT ?3)
         ) RETURNING image_path",
        params![retention_days, cutoff, limit],
    )
}

fn collect_paths(conn: &Connection, sql: &str, p: impl rusqlite::Params) -> rusqlite::Result<Vec<String>> {
    let mut stmt = conn.prepare(sql)?;
    let rows = stmt.query_map(p, |r| r.get::<_, Option<String>>(0))?;
    let mut paths = Vec::new();
    for row in rows {
        if let Some(path) = row? {
            paths.push(path);
        }
    }
    Ok(paths)
}

// ---------- Etiketler ----------

pub fn tag_summary(conn: &Connection) -> rusqlite::Result<TagSummary> {
    let total = conn.query_row("SELECT COUNT(*) FROM items", [], |r| r.get(0))?;
    let mut stmt = conn.prepare_cached(
        "SELECT t.id, t.name, COUNT(it.item_id)
         FROM tags t LEFT JOIN item_tags it ON it.tag_id = t.id
         GROUP BY t.id ORDER BY t.id",
    )?;
    let tags = stmt
        .query_map([], |r| Ok(Tag { id: r.get(0)?, name: r.get(1)?, count: r.get(2)? }))?
        .collect::<rusqlite::Result<_>>()?;
    Ok(TagSummary { total, tags })
}

pub fn add_tag(conn: &Connection, name: &str) -> rusqlite::Result<Tag> {
    let id = conn.query_row("INSERT INTO tags(name) VALUES (?1) RETURNING id", [name], |r| r.get(0))?;
    Ok(Tag { id, name: name.to_string(), count: 0 })
}

pub fn delete_tag(conn: &Connection, id: i64) -> rusqlite::Result<()> {
    conn.execute("DELETE FROM tags WHERE id = ?1", [id])?;
    Ok(())
}

// ---------- Ayarlar ----------

pub fn load_settings(conn: &Connection) -> Settings {
    conn.query_row("SELECT value FROM settings WHERE key = 'app'", [], |r| r.get::<_, String>(0))
        .optional()
        .ok()
        .flatten()
        .and_then(|json| serde_json::from_str(&json).ok())
        .unwrap_or_default()
}

pub fn save_settings(conn: &Connection, settings: &Settings) -> rusqlite::Result<()> {
    let json = serde_json::to_string(settings).expect("settings serialize");
    conn.execute(
        "INSERT INTO settings(key, value) VALUES ('app', ?1)
         ON CONFLICT(key) DO UPDATE SET value = excluded.value",
        [json],
    )?;
    Ok(())
}

fn escape_like(s: &str) -> String {
    let mut out = String::with_capacity(s.len());
    for c in s.chars() {
        if matches!(c, '%' | '_' | '\\') {
            out.push('\\');
        }
        out.push(c);
    }
    out
}
