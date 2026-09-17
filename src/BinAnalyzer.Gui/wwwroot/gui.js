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
};
document.addEventListener("keydown", e => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
    const s = document.getElementById("gui-search");
    if (s) { e.preventDefault(); s.focus(); s.select(); }
  }
});
