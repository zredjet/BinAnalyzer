# REQ-020: HTML出力フォーマッター

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

解析結果を他者と共有する場合、ターミナル出力のコピー&ペーストでは構造の可読性が失われる。HTMLファイルとして出力すれば、ブラウザで開いてインタラクティブに構造を探索できる。

ユースケース:
- 解析結果のドキュメント化・レポート作成
- チーム内でのバイナリ構造の共有
- フォーマット定義のデバッグ（大きなファイルの階層を折りたたんで確認）

## 機能要件

### 追加する機能

- [ ] `HtmlOutputFormatter` クラスの新規作成
  - 自己完結型HTML（CSS/JS埋め込み、外部依存なし）
  - デコード結果ツリーをHTML要素として出力
- [ ] インタラクティブ機能
  - ノードの折りたたみ/展開（クリックで開閉）
  - デフォルトは第1階層のみ展開
  - 全展開/全折りたたみボタン
- [ ] フィールド情報の表示
  - 型に応じた色分け（ツリー出力と同様のカラースキーム）
  - オフセット/サイズのツールチップ表示
  - enum値のラベル・説明表示
  - バリデーション結果の視覚的表示（✓/✗）
- [ ] CLIオプション
  - `--output html` または `-o html`

### 変更する既存機能

- [ ] Program.cs（CLI）: `--output html` の分岐追加

### 変更しないもの（スコープ外）

- バイト列のインラインhexdump表示
- 検索機能
- テーマ切り替え（ダーク/ライト）
- 外部CSSフレームワークの使用

## 受入条件

1. [ ] `--output html` で自己完結型HTMLが出力されること
2. [ ] 出力HTMLをブラウザで開き、ツリーが表示されること
3. [ ] ノードをクリックして折りたたみ/展開ができること
4. [ ] 整数値、文字列値、バイト列が色分けされること
5. [ ] オフセットとサイズが表示されること
6. [ ] enum値のラベルが表示されること
7. [ ] バリデーション結果（✓/✗）が表示されること
8. [ ] 外部リソースへの依存がないこと（オフライン閲覧可能）
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | HtmlOutputFormatter の新規追加 |
| BinAnalyzer.Cli | `--output html` の追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

CSS/JSを埋め込んだ自己完結型HTMLを1つの `StringBuilder` で生成する。テンプレートエンジンは使用しない。`IOutputFormatter` を実装する。

### 構成

```
src/BinAnalyzer.Output/
└── HtmlOutputFormatter.cs   # 新規
```

### HTML構造

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>BinAnalyzer - {format_name}</title>
  <style>/* 埋め込みCSS */</style>
</head>
<body>
  <div class="toolbar">
    <button onclick="expandAll()">全展開</button>
    <button onclick="collapseAll()">全折りたたみ</button>
  </div>
  <div class="tree" id="root">
    <!-- デコード結果ツリー -->
  </div>
  <script>/* 埋め込みJS */</script>
</body>
</html>
```

### ノードのHTML表現

```html
<div class="node struct collapsible">
  <div class="header" onclick="toggle(this)">
    <span class="toggle">▼</span>
    <span class="name">chunks</span>
    <span class="meta">[0x00000008] (59 bytes) [2 items]</span>
  </div>
  <div class="children">
    <!-- 子ノード -->
  </div>
</div>

<div class="node integer">
  <span class="name">width</span>:
  <span class="value int">100</span>
  <span class="meta">(0x64)</span>
  <span class="value enum">"truecolor"</span>
</div>
```

### CSSカラースキーム

ツリー出力と同様:
- 整数値: `color: #0ff` (cyan)
- 文字列値: `color: #0f0` (green)
- バイト列: `color: #ff0` (yellow)
- enum値: `color: #f0f` (magenta)
- オフセット/サイズ: `color: #888` (dim)
- バリデーション ✓: `color: #0f0`, ✗: `color: #f00`
- 背景: `#1e1e1e` (ダーク)

### JavaScript

```javascript
function toggle(el) {
  const node = el.parentElement;
  const children = node.querySelector('.children');
  const icon = el.querySelector('.toggle');
  if (children.style.display === 'none') {
    children.style.display = '';
    icon.textContent = '▼';
  } else {
    children.style.display = 'none';
    icon.textContent = '▶';
  }
}
function expandAll() { /* ... */ }
function collapseAll() { /* ... */ }
```

### インタフェース変更

`IOutputFormatter` を実装。`HexDumpOutputFormatter` と異なりバイトデータ不要。

### 代替案

**案A: React/Vue等のSPAとして出力（不採用）**
- 理由: 外部CDN依存が発生し、オフライン閲覧できない。自己完結型HTMLが要件。

### 懸念事項

1. **HTMLエスケープ**: フィールド名や文字列値に `<`, `>`, `&` が含まれる場合、HTMLエスケープが必要。`System.Net.WebUtility.HtmlEncode` を使用。

2. **大きなファイル**: デコード結果が大きい場合、HTMLファイルも大きくなる。初期実装ではサイズ制限なし。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
