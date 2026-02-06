# REQ-025: HTML出力の検索・フィルタ機能

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-020 |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

HTML出力（REQ-020）はインタラクティブな折りたたみ/展開機能を持つが、大きなバイナリファイル（数百チャンクのPNG、多数エントリのZIP等）では特定のフィールドや値を目視で探すのが困難。

ユースケース:
- PNGで `color_type` が特定の値を持つチャンクを探す
- ZIPアーカイブ内の特定ファイル名を含むエントリを見つける
- 特定のオフセットやサイズを持つフィールドを絞り込む
- バリデーション失敗（✗）のフィールドだけを表示する

検索・フィルタ機能をHTML内のJavaScriptで実装することで、ブラウザ上で即座にナビゲーション可能になる。

## 機能要件

### 追加する機能

- [ ] 検索UI
  - HTMLページ上部に固定表示される検索バー
  - テキスト入力でリアルタイム絞り込み（インクリメンタルサーチ）
  - 検索対象: フィールド名、値（文字列・数値・hex）、enumラベル、構造体名
  - ヒット件数の表示
  - 前/次のマッチへのジャンプ（ショートカットキー対応）
- [ ] フィルタ機能
  - 特定の値に一致するフィールドを含む構造体ツリーのみを表示
  - フィルタ適用時、マッチしたフィールドの祖先ノードを自動展開
  - マッチしないブランチは折りたたみまたは非表示
- [ ] マッチのハイライト
  - マッチしたテキスト部分を視覚的にハイライト
  - 現在フォーカス中のマッチを他のマッチと区別して強調
- [ ] キーボードショートカット
  - `/` または `Ctrl+F`: 検索バーにフォーカス
  - `Enter` / `Shift+Enter`: 次/前のマッチへジャンプ
  - `Escape`: 検索をクリア・閉じる

### 変更する既存機能

- [ ] HtmlOutputFormatter: 検索UIとJavaScriptロジックの追加

### 変更しないもの（スコープ外）

- 正規表現による検索 — 将来課題
- 複合条件フィルタ（AND/OR組み合わせ） — 将来課題
- サーバーサイド検索（全てクライアントサイドJavaScriptで完結）
- 検索結果のエクスポート

## 受入条件

1. [ ] HTML出力に検索バーが表示されること
2. [ ] テキスト入力でフィールド名・値・enumラベルがリアルタイムに検索できること
3. [ ] マッチしたフィールドがハイライトされること
4. [ ] マッチしたフィールドを含む構造体の祖先が自動展開されること
5. [ ] 前/次のマッチへジャンプできること
6. [ ] ヒット件数が表示されること
7. [ ] マッチがない場合に適切なメッセージ（例: "0 件"）が表示されること
8. [ ] キーボードショートカット（`/`, `Enter`, `Escape`）が動作すること
9. [ ] ダークテーマでハイライトが視認できること
10. [ ] 検索クリア時に元の表示状態に戻ること
11. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | HtmlOutputFormatter に検索UI・JSを追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/cli-usage.md — html出力形式の説明に検索機能を追記
- [ ] docs/dsl-reference.md — 変更不要
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

変更対象は `HtmlOutputFormatter.cs` のみ。CSS・HTML・JavaScriptの追加で完結し、C#側のモデルやインタフェースは一切変更しない。

現在のHTMLの構造を活かす:
- 各ノードは `<div class="node {type}">` で型別CSSクラスを持つ
- `<span class="name">`, `<span class="value ...">`, `<span class="value enum">` 等が検索対象テキストを含む
- `collapsible` / `collapsed` クラスと `.children` の `display:none` で折りたたみ管理済み

検索はDOMベースで実装する。全ノードのテキストコンテンツに対して文字列マッチを行い、マッチしたノードのハイライトと祖先展開をJavaScriptで制御する。

### 構成

```
src/BinAnalyzer.Output/
└── HtmlOutputFormatter.cs   # WriteToolbar, WriteStyle, WriteScript を拡張
```

変更箇所:
1. `WriteToolbar` — 検索バーUIの追加
2. `WriteStyle` — 検索バー・ハイライト・フォーカス用CSSの追加
3. `WriteScript` — 検索ロジックJavaScriptの追加
4. `WriteNode` 系メソッド — 各ノードに `data-searchable` 属性を追加（検索対象テキストの明示化）

### HTML変更

ツールバーに検索バーを追加:

```html
<div class="toolbar">
  <button onclick="expandAll()">全展開</button>
  <button onclick="collapseAll()">全折りたたみ</button>
  <div class="search-bar">
    <input type="text" id="search-input" placeholder="検索..." />
    <span id="search-count"></span>
    <button onclick="jumpPrev()" id="btn-prev" disabled>▲</button>
    <button onclick="jumpNext()" id="btn-next" disabled>▼</button>
  </div>
</div>
```

各リーフノードに検索用テキストをdata属性で付与:

```html
<!-- 変更前 -->
<div class="node integer">
  <span class="name">width</span>: <span class="value int">1920</span>
</div>

<!-- 変更後 -->
<div class="node integer" data-searchable="width 1920">
  <span class="name">width</span>: <span class="value int">1920</span>
</div>
```

`data-searchable` にはフィールド名・値・enumラベル等、検索対象となるテキストを空白区切りで連結する。これにより `textContent` を走査するよりも高速かつ正確にマッチできる。

### CSS追加

```css
.search-bar { display: inline-flex; align-items: center; margin-left: 16px; gap: 4px; }
.search-bar input { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 4px 8px;
                     font-family: inherit; font-size: 13px; width: 200px; }
.search-bar input:focus { border-color: #007acc; outline: none; }
#search-count { color: #888; font-size: 12px; min-width: 48px; }
.search-bar button { background: #333; color: #d4d4d4; border: 1px solid #555; padding: 2px 8px;
                     cursor: pointer; font-size: 11px; }
.search-bar button:disabled { color: #555; cursor: default; }
.search-match { background: #5a4a00; border-radius: 2px; }
.search-focus { background: #007acc; color: #fff; border-radius: 2px; }
.search-hidden { display: none; }
```

### JavaScript検索ロジック

```
状態:
  matches = []    // マッチしたノードのDOM要素リスト
  currentIndex = -1  // 現在フォーカス中のインデックス
  savedState = Map<Element, boolean>  // 検索前の展開/折りたたみ状態

search(query):
  1. 前回のハイライトをクリア
  2. queryが空なら状態リセット（savedStateから復元）して終了
  3. 全 .node[data-searchable] を走査
  4. data-searchable にqueryを含むノードを matches に追加
  5. マッチノードに .search-match クラスを付与
  6. マッチノードの全祖先 .collapsible を展開（.children を display:'' に）
  7. マッチしないブランチはそのまま（非表示にはしない — 構造の文脈を維持）
  8. カウンター更新: "3 / 15 件"
  9. currentIndex = 0 にして最初のマッチにフォーカス

jumpNext() / jumpPrev():
  1. currentIndex を更新（ラップアラウンド）
  2. 前フォーカスから .search-focus を除去
  3. 新フォーカスに .search-focus を付与
  4. scrollIntoView({ block: 'center' })
  5. カウンター更新

clearSearch():
  1. 全 .search-match, .search-focus クラスを除去
  2. savedState から展開/折りたたみ状態を復元
  3. matches = [], currentIndex = -1
  4. カウンター・入力をクリア
```

### data-searchable の生成ルール

| ノード型 | data-searchable の内容 |
|---|---|
| DecodedInteger | `"{name} {value} 0x{hex}" + enumLabel` |
| DecodedString | `"{name} {value}"` |
| DecodedFloat | `"{name} {value}"` |
| DecodedBytes | `"{name}"` + hex先頭16バイト |
| DecodedFlags | `"{name} 0x{rawValue}" + flagStates` |
| DecodedBitfield | `"{name} 0x{rawValue}"` |
| DecodedCompressed | `"{name} {algorithm}"` |
| DecodedStruct | `"{name} {structType}"` |
| DecodedArray | `"{name}"` |

### キーボードショートカット

```javascript
document.addEventListener('keydown', (e) => {
  if (e.key === '/' && !isInputFocused()) {
    e.preventDefault();
    document.getElementById('search-input').focus();
  }
  if (e.key === 'Escape') clearSearch();
  if (e.key === 'Enter' && isSearchFocused()) {
    e.preventDefault();
    e.shiftKey ? jumpPrev() : jumpNext();
  }
});
```

### モデル変更

なし。

### インタフェース変更

なし。IOutputFormatter のシグネチャに変更なし。

### 代替案

**案A: textContent ベースの検索（不採用）**
- 各ノードの `textContent` を直接検索する方式。
- 不採用理由: metaテキスト（オフセット・サイズ等）もヒットしてしまいノイズが多い。`data-searchable` で検索対象を明示的に制御する方が精度が高い。

**案B: フィルタモード（マッチしないブランチを非表示）（不採用）**
- マッチしないノードに `.search-hidden` を付与して完全に隠す方式。
- 不採用理由: 構造の文脈が失われ、マッチしたフィールドがどの構造体に属するか分かりにくくなる。祖先展開+ハイライト+ジャンプで十分な検索体験を提供できる。将来的にオプションとしてフィルタモードを追加することは可能。

### 懸念事項

1. **大規模ファイルのパフォーマンス**: 数千ノードのHTMLでのDOM走査。`data-searchable` のマッチは `indexOf` で十分高速（数万ノードでもミリ秒オーダー）だが、祖先展開のDOM操作が重くなる可能性がある。初期実装では最適化しない。問題が出た場合は `requestAnimationFrame` でバッチ処理を検討。

2. **検索前の展開状態の保存と復元**: 検索クリア時にユーザーが手動で展開/折りたたみした状態に戻す必要がある。検索開始時に全 `.collapsible` の `collapsed` 状態をMapに保存し、クリア時に復元する。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
