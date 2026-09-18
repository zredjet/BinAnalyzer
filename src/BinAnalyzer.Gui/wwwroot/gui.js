// BinAnalyzer GUI: 小さな補助スクリプト（スクロール・ショートカット・イベント委譲）
window.binGui = {
  scrollIntoView(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: "nearest" });
  },
  // 行仮想化しているコンテナ（ヘックス・ツリー）で、指定 top の行が見えていなければ中央に寄せる
  scrollRowTo(containerId, top, height) {
    const el = document.getElementById(containerId);
    if (!el) return;
    const h = el.clientHeight;
    if (top < el.scrollTop || top + height > el.scrollTop + h)
      el.scrollTop = Math.max(0, top - h / 2);
  },
  // GuiShell が DotNetObjectReference を登録する。Ctrl+Z / Ctrl+Y / Ctrl+S を OnShortcut(name) に渡す
  _shortcuts: null,
  registerShortcuts(dotnet) { this._shortcuts = dotnet; },
  unregisterShortcuts() { this._shortcuts = null; },
};

// ヘックスのセル・構造マップのバンドは数千個並ぶので、要素ごとに Blazor のハンドラを付けず、
// コンテナ 1 つで mouseover / click を受けて data-n（ノード ID）だけを .NET に渡す（REQ-177）。
// gui.js は blazor.*.js より先に読まれるため、DOMContentLoaded（同期スクリプト実行後）で登録する。
(function registerNodeEvents() {
  const register = () => {
    const blazor = window.Blazor;
    if (!blazor || typeof blazor.registerCustomEventType !== "function") return;
    const createEventArgs = e => {
      const t = e.target && e.target.closest ? e.target.closest("[data-n]") : null;
      const n = t ? parseInt(t.getAttribute("data-n"), 10) : NaN;
      return { nodeId: Number.isNaN(n) ? -1 : n };
    };
    blazor.registerCustomEventType("nodehover", { browserEventName: "mouseover", createEventArgs });
    blazor.registerCustomEventType("nodeclick", { browserEventName: "click", createEventArgs });
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", register);
  else register();
})();

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
