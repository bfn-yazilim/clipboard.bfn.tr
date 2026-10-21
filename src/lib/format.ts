const pad = (n: number) => String(n).padStart(2, "0");

/** 03.09 10:21 */
export function formatDate(ms: number): string {
  const d = new Date(ms);
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** "Ctrl+Alt+V" -> "Ctrl + Alt + V" */
export function formatShortcut(s: string): string {
  return s ? s.split("+").join(" + ") : "Yok";
}

/** KeyboardEvent.code -> global-hotkey'in anladığı tuş adı. */
export function codeToKey(code: string): string | null {
  if (/^Key[A-Z]$/.test(code)) return code.slice(3);
  if (/^Digit\d$/.test(code)) return code.slice(5);
  if (/^F\d{1,2}$/.test(code)) return code;
  if (/^(Control|Alt|Shift|Meta)(Left|Right)$/.test(code)) return null;
  return code; // Space, Backquote, ArrowUp, Numpad1 ...
}
