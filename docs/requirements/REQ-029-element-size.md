# REQ-029: 配列の要素サイズ指定（element_size）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットでは、固定サイズのレコードがテーブル状に並ぶ構造を持つ（例: ELFのセクションヘッダテーブル、PEのセクションテーブル、BMPのカラーテーブル）。

現在の `repeat_count` は要素数のみを指定し、各要素のサイズは構造体のフィールド定義から自動的に決まる。しかし以下のケースで問題が起きる:
- ヘッダで宣言された要素サイズと構造体定義が一致しない場合（バージョンによりフィールドが追加される）
- 要素内の未知フィールドを安全にスキップしたい場合
- 固定サイズレコードの末尾パディングを自動処理したい場合

`element_size` を導入することで、各要素をバウンダリスコープで囲み、要素サイズを保証できる。

## 機能要件

### 追加する機能

- [ ] `element_size` フィールド属性
  - 繰り返しフィールド（`repeat`, `repeat_count`, `repeat_until`）で使用可能
  - 各要素のデコード時にバウンダリスコープを設定
  - 固定値（`element_size: "40"`）と式（`element_size: "{entry_size}"`）の両方に対応
  - 要素のデコード後、スコープ末尾まで未読バイトを自動スキップ
- [ ] YAMLモデルへの追加
  - `YamlFieldModel` に `element_size` プロパティを追加
- [ ] IRモデルへの追加
  - `FieldDefinition` に `ElementSize` / `ElementSizeExpression` を追加
- [ ] バリデーション
  - `element_size` が繰り返しフィールド以外で指定された場合に警告

### 変更する既存機能

- [ ] BinaryDecoder: `DecodeRepeatedField` で各要素のデコード時にスコープを設定
- [ ] YamlToIrMapper: `element_size` のパース
- [ ] FormatValidator: element_size の妥当性検証

### 変更しないもの（スコープ外）

- 要素サイズの動的変更（全要素が同一サイズ）
- ネストされた繰り返しでの element_size の連鎖

## 受入条件

1. [ ] `element_size` に固定値を指定して繰り返しデコードできること
2. [ ] `element_size` に式を指定して繰り返しデコードできること
3. [ ] 要素のフィールドが element_size より小さい場合、残りバイトがスキップされること
4. [ ] 要素のフィールドが element_size を超えた場合、デコードエラーになること
5. [ ] 繰り返し以外のフィールドで element_size を指定した場合に警告が出ること
6. [ ] YAML定義から element_size が正しくパースされること
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldDefinition に ElementSize/ElementSizeExpression を追加 |
| BinAnalyzer.Dsl | YamlFieldModel, YamlToIrMapper に element_size を追加 |
| BinAnalyzer.Engine | BinaryDecoder の DecodeRepeatedField にスコープ制御を追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 繰り返しモードセクションに element_size を追記
- [ ] schemas/bdef.schema.json — element_size プロパティを追加（REQ-028後）

---

## 設計メモ

- 既存の `PushScope()` / `PopScope()` をそのまま利用。`PopScope()` がスコープ末尾まで位置を進める既存機能で未読バイトの自動スキップを実現。
- `ParseElementSize()` は `ParseSize()` と類似だが `remaining` キーワードは不要（要素サイズに「残り全部」は意味をなさない）。
- `DecodeElementWithScope()` ヘルパーを追加し、各繰り返しモードから統一的に呼び出す。

---

## 実装メモ

### 実装中の設計変更

なし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ElementSizeTests | FixedElementSize_SkipsRemainingBytes | AC1, AC3 |
| ElementSizeTests | ExpressionElementSize_UsesEvaluatedValue | AC2 |
| ElementSizeTests | ElementSize_Overflow_ThrowsError | AC4 |
| ElementSizeTests | ElementSize_WithRepeatUntilEof | AC1 |
| ElementSizeTests | ElementSize_WithRepeatUntil | AC1 |
| ElementSizeParsingTests | ParseElementSize_FixedValue | AC6 |
| ElementSizeParsingTests | ParseElementSize_Expression | AC6 |
| ElementSizeParsingTests | ParseElementSize_NotSpecified | AC6 |
| ElementSizeValidationTests | VAL110_NonRepeatField_ReportsWarning | AC5 |
| ElementSizeValidationTests | VAL110_RepeatField_NoWarning | AC5 |

### 気づき・今後の課題

- 要素サイズが動的に変わるケース（各要素のヘッダにサイズが含まれる等）は現在のスコープ外。将来的に検討の余地あり。
