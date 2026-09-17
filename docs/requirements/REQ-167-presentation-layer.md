# REQ-167: Presentation 層の抽出（表示ロジックの UI 非依存化）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-17 |
| 更新日 | 2026-09-17 |

## 背景・動機

TUI（`BinAnalyzer.Tui`）には、デコード結果ツリー（`DecodedNode`）を「表示用の文字列・行」に変換する純粋なロジックが `internal` として存在する。

- `DecodedNodeTreeBuilder.GetDisplayText` — ノード1行の表示文字列
- `NodeDetailFormatter.Format` — 詳細ペインの Key/Value 行
- `TuiState.CollectMatches` — 名前による検索
- 子ノード走査（struct → Children / array → Elements / compressed → DecodedContent）が各所に重複

REQ-168（GUIモード）で同じロジックを Blazor コンポーネントから使う必要があるが、`BinAnalyzer.Tui` は Terminal.Gui に依存しており、GUI から参照できない（Terminal.Gui を GUI に持ち込むことになる）。
Core は「ドメインモデル・依存ゼロ」、Output は「CLI 向け文字列フォーマッタ」という位置づけなので、どちらにも表示用ロジックを置くのは責務が合わない。

そこで、Core のみに依存する新プロジェクト `BinAnalyzer.Presentation` を作り、これらの純粋関数を移動する。TUI は Presentation に委譲する。挙動は変えない。

## 機能要件

### 追加する機能

- [x] 新プロジェクト `src/BinAnalyzer.Presentation`（依存: `BinAnalyzer.Core` のみ、外部 NuGet なし）
- [x] `NodeDisplayText.For(DecodedNode)` — `DecodedNodeTreeBuilder.GetDisplayText` を移動（public）
- [x] `NodeDisplayText.TypeLabel(DecodedNode)` — 型ラベル（`u32`, `ascii[4]`, `u32 (crc32)`, `bytes (zlib)` 等）。GUI インスペクター用
- [x] `NodeDetailFormatter.Format(DecodedNode)` — 移動して public 化。戻り値を `IReadOnlyList<DetailRow>`（`DetailRow(string Key, string Value, DetailRowKind Kind)`）に変更。`DetailRowKind` は `Text / Mono / Validation / Child`（TUI は Key/Value のみ使う）
- [x] `NodeSearch.ByName(DecodedNode root, string query)` — `TuiState.CollectMatches` を移動
- [x] `NodeChildren.Of(DecodedNode)` — 子ノード走査の単一情報源（struct は padding 除外、array は Elements、compressed は DecodedContent.Children、bitfield は子なし）
- [x] `NodeChildren.HasChildren(DecodedNode)` — `DecodedNodeTreeBuilder.CanExpand` 相当

### 変更する既存機能

- [x] `BinAnalyzer.Tui` が `BinAnalyzer.Presentation` を参照し、上記ヘルパへ委譲する（`DecodedNodeTreeBuilder` は `ITreeBuilder<DecodedNode>` の薄いアダプタになる）
- [x] `tests/BinAnalyzer.Tui.Tests` のうち純粋関数のテストを `tests/BinAnalyzer.Presentation.Tests` へ移動（`NodeDetailFormatterTests`、`DecodedNodeTreeBuilderTests` の表示文字列部分、`TuiStateTests` の `CollectMatches_*`）
- [x] `BinAnalyzer.slnx` に `BinAnalyzer.Presentation` / `BinAnalyzer.Presentation.Tests` を追加

### 変更しないもの（スコープ外）

- TUI の見た目・操作・出力（文字列は1文字も変えない）
- `HexPane.GenerateHexDump`（Terminal.Gui 向けの文字列描画なので TUI に残す。GUI は別モデルを持つ）
- Output プロジェクトのフォーマッタ群

## 受入条件

1. [x] `BinAnalyzer.Presentation.csproj` の依存が `BinAnalyzer.Core` のみであること（`dotnet list reference` / `dotnet list package` で確認）
2. [x] `NodeDisplayText.For` が移動前の `GetDisplayText` と全ノード種別で同一文字列を返すこと（既存テストを移動して通過）
3. [x] `NodeDetailFormatter.Format` が移動前と同一の Key/Value 列を返すこと（既存テストを移動して通過）
4. [x] `NodeSearch.ByName` が移動前の `CollectMatches` と同一結果を返すこと（既存テストを移動して通過）
5. [x] `NodeChildren.Of` が struct(padding除外)/array/compressed/その他 の4系統で正しい子を返すこと
6. [x] `NodeDisplayText.TypeLabel` が integer/string/bytes/checksum付き/compressed で期待ラベルを返すこと
7. [x] TUI（`-o tui`）の起動・ツリー表示・詳細表示が従来どおり動作すること（手動確認）
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Presentation | **新規** |
| BinAnalyzer.Tui | Presentation へ委譲 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — プロジェクト構成・依存関係に Presentation を追加
- [x] CLAUDE.md — プロジェクト構成に Presentation を追加
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **移動であって複製ではない**。TUI 側に同じロジックを残さない。
- Presentation は「`DecodedNode` → 表示用データ」の純粋関数のみを置く。UI フレームワーク型（Terminal.Gui / Blazor）は一切参照しない。
- 名前空間は `BinAnalyzer.Presentation`。

### モデル変更

- `DetailRow(string Key, string Value, DetailRowKind Kind)` レコードと `DetailRowKind` 列挙を追加（Presentation 内）。IR / Decoded モデルは変更なし。

### インタフェース変更

- `BinAnalyzer.Tui` の `internal` メンバを削除・委譲するのみ。public API の破壊的変更なし。

### 代替案

- **Core に置く**: Core は依存ゼロのドメイン層。表示文字列（`✓`, `[n items]`）はドメインではない → 不採用。
- **Output に置く**: Output は CLI 出力フォーマッタ。TUI/GUI が「フォーマッタ」目的以外で Output を参照することになり、責務が濁る → 不採用。
- **TUI と GUI で複製する**: 表示文字列の不一致が生じる → 不採用。

### 懸念事項

- `DecodedNodeTreeBuilder.CanExpand` は `DecodedBitfield` を展開可能としているが `GetChildren` は空を返す（既存の不整合）。`NodeChildren` では bitfield を子なしとして統一する。TUI の見た目はビットフィールド行の展開マークが消えるだけなので許容範囲だが、受入条件 7 で確認する。

---

## 実装メモ

### 実装中の設計変更

- `NodeDisplayText.ValueOnly(DecodedNode)` と `NodeChildren.Descendants(root)` を追加した（REQ-168 のゴースト注釈・索引構築で必要になることが明らかなため、同時に用意した）。
- `DetailRowKind` は `Text / Mono / Validation / Child` の 4 値。TUI は Key/Value のみを使い、Kind は無視する。
- `NodeChildren.HasChildren` は `DecodedBitfield` を「子なし」に統一した（旧 `CanExpand` は true を返していたが `GetChildren` は空だった）。TUI ではビットフィールド行の展開マークが消えるだけで、表示文字列・詳細行は不変。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| NodeDisplayTextTests | `For_*`（旧 `GetDisplayText_*` を移動、7 件） | 2 |
| NodeDisplayTextTests | `ValueOnly_*`（2 件）、`TypeLabel_*`（Integer 3 ケース、String、Bytes、Compressed） | 6 |
| NodeDetailFormatterTests | 旧 `Format_*` を移動（13 件）、`Format_Offset_IsMonoRow`、`Format_Flags_ChildRowsAreMarkedChild` | 3 |
| NodeSearchTests | `ByName_*`（旧 `CollectMatches_*` を移動、4 件）、`ByName_EmptyQuery_ReturnsEmpty` | 4 |
| NodeChildrenTests | `HasChildren_*` / `Of_*`（旧 `CanExpand_*` / `GetChildren_*` を移動、9 件）、`HasChildren_Bitfield_ReturnsFalse`、`Descendants_ReturnsPreOrder_WithoutPadding` | 5 |
| （確認） | `dotnet list reference` で Core のみ、`dotnet list package` でパッケージなし | 1 |

`dotnet test BinAnalyzer.slnx`: 全 1,277 件通過（Presentation.Tests 46 件）。

### 気づき・今後の課題

- `DecodedInteger` は符号・DSL 型名を保持しないため `TypeLabel` は `int32` のようなサイズ由来の近似ラベルになる。正確な型名（`u32` / `i16`）が欲しければ Decoded モデルに型名を持たせる要望が別途必要 → REQ-171 として起票。
