// BinAnalyzer GUI: 小さな補助スクリプト（スクロール・ショートカット）
window.binGui = {
  scrollIntoView(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: "nearest" });
  },
  scrollHexTo(containerId, top) {
    const el = document.getElementById(containerId);
    if (!el) return;
    const h = el.clientHeight;
    if (top < el.scrollTop || top + 26 > el.scrollTop + h)
      el.scrollTop = Math.max(0, top - h / 2);
  },
  // GuiShell が DotNetObjectReference を登録する。Ctrl+Z / Ctrl+Y / Ctrl+S を OnShortcut(name) に渡す
  _shortcuts: null,
  registerShortcuts(dotnet) { this._shortcuts = dotnet; },
  unregisterShortcuts() { this._shortcuts = null; },
};
document.addEventListener("keydown", e => {
  if (!(e.ctrlKey || e.metaKey)) return;
  const k = e.key.toLowerCase();
  if (k === "k") {
    const s = document.getElementById("gui-search");
    if (s) { e.preventDefault(); s.focus(); s.select(); }
    return;
  }
  const target = window.binGui._shortcuts;
  if (!target) return;
  const t = e.target;
  // 入力欄の中ではブラウザ標準の undo / redo を邪魔しない
  const typing = t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable);
  let name = null;
  if (k === "s") name = "save";
  else if (!typing && k === "z") name = e.shiftKey ? "redo" : "undo";
  else if (!typing && k === "y") name = "redo";
  if (!name) return;
  e.preventDefault();
  target.invokeMethodAsync("OnShortcut", name);
});
