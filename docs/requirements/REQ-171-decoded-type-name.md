# REQ-171: デコード結果ノードに DSL 型名を保持する

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

`DecodedInteger` 等は値とサイズしか持たず、DSL の型名（`uint32` / `int16` / `uleb128` 等）や符号を保持していない。そのため `NodeDisplayText.TypeLabel`（REQ-167）は `int32` のようなサイズ由来の近似ラベルしか出せず、GUI インスペクター・ステータスバー・TUI 詳細の「型」表示が不正確になる（REQ-167 / REQ-168 の課題）。

## 機能要件

### 追加する機能

- [ ] `DecodedNode` に `TypeName: string?`（DSL の `type:` 文字列）を追加。エンジンが全ノード種別で設定
- [ ] `NodeDisplayText.TypeLabel` は `TypeName` があればそれを優先し、無ければ従来の近似
- [ ] JSON 出力（`JsonOutputFormatter`）に `type` を出す（既に出しているなら整合を確認）

### 変更する既存機能

- [ ] `DecodedInteger` 等の生成箇所（`BinaryDecoder`）で `TypeName` を設定

### 変更しないもの（スコープ外）

- Decoded モデルへの符号・ビット幅以外のメタデータ追加

## 受入条件

1. [ ] `uint32` / `int16` / `float32` / `ascii` / `bytes` の各フィールドで `TypeName` が DSL の型名と一致すること
2. [ ] `TypeLabel` が `u32` / `i16` 等の正確なラベルを返すこと（既存の `TypeLabel_*` テストを更新）
3. [ ] ゴールデンファイル（JSON 出力）が更新されること
4. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodedNode.TypeName` |
| BinAnalyzer.Engine | 設定箇所 |
| BinAnalyzer.Presentation | `TypeLabel` |
| BinAnalyzer.Output | JSON 出力（必要なら） |

### 変更が必要なドキュメント

- [ ] docs/architecture.md — デコード結果ツリー

---

## 設計メモ

### 設計方針
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
