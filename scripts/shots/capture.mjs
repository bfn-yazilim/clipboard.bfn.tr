// Gerçek React arayüzünü örnek verilerle açıp headless Edge ile site/screenshots/ altına çeker.
// Kullanım: npm run screenshots
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { existsSync, mkdirSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { createServer } from "vite";

const PORT = 5199;
const OUT = resolve("site/screenshots");
const SHOTS = [
  { name: "liste-acik", query: "theme=light&view=list" },
  { name: "liste-koyu", query: "theme=dark&view=list" },
  { name: "ayarlar-koyu", query: "theme=dark&view=settings" },
  { name: "ayarlar-acik", query: "theme=light&view=settings" },
];

const edge = [
  "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
].find(existsSync);
if (!edge) throw new Error("Microsoft Edge bulunamadı");

mkdirSync(OUT, { recursive: true });
const profile = mkdtempSync(join(tmpdir(), "cm-shots-"));
const server = await createServer({ server: { host: "127.0.0.1", port: PORT, strictPort: true }, logLevel: "error" });
await server.listen();

try {
  for (const shot of SHOTS) {
    const file = join(OUT, `${shot.name}.png`);
    await promisify(execFile)(edge, [
      "--headless=new",
      "--disable-gpu",
      "--hide-scrollbars",
      "--no-first-run",
      `--user-data-dir=${join(profile, shot.name)}`,
      "--force-device-scale-factor=2",
      "--window-size=600,800",
      "--virtual-time-budget=5000",
      `--screenshot=${file}`,
      `http://127.0.0.1:${PORT}/scripts/shots/index.html?${shot.query}`,
    ]);
    console.log("✓", file);
  }
} finally {
  await server.close();
  rmSync(profile, { recursive: true, force: true });
}
