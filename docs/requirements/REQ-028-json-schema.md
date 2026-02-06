# REQ-028: フォーマット定義のJSON Schema公開

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 最高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

`.bdef.yaml` フォーマット定義ファイルの作成・編集時に、エディタの補完やバリデーションが効かないため、プロパティ名のタイプミスや型の間違いに気づきにくい。

JSON SchemaをYAML Language Server（VS Code, JetBrains等）に連携することで、以下が可能になる:
- プロパティ名のオートコンプリート
- 型ミスマッチの即時警告
- 各プロパティのホバードキュメント表示
- 必須プロパティの欠落警告

今後のDSL拡張（virtual field, element_size等）のたびにスキーマも更新することで、常に最新の仕様がエディタに反映される。

## 機能要件

### 追加する機能

- [ ] JSON Schemaファイルの生成
  - `.bdef.yaml` の全プロパティ・型制約を定義するJSON Schemaファイル
  - トップレベル: name, endianness, root, imports, enums, flags, structs
  - フィールド定義: name, type, size, enum, flags, struct, repeat, repeat_count, repeat_until, switch_on, cases, default, fields(bitfield), if, checksum, expected, description, align, padding
  - type プロパティの列挙値（uint8, u8, uint16, ... 全エイリアス）
  - endianness の列挙値（big, little, be, le）
  - size の特殊値（remaining）
  - repeat の特殊値（eof）
- [ ] スキーマファイルの配置
  - `schemas/bdef.schema.json` として配置
- [ ] エディタ連携の設定例
  - VS Code用 `.vscode/settings.json` のYAML Schema連携設定例を文書化
  - ファイルパターン `*.bdef.yaml` にスキーマを自動適用

### 変更する既存機能

なし。

### 変更しないもの（スコープ外）

- スキーマの自動生成（C#コードからの自動生成）— 手書きで管理
- CLIコマンドによるスキーマ出力
- 式（`{...}`）内部の構文検証 — YAML Schemaの範囲外

## 受入条件

1. [ ] `schemas/bdef.schema.json` が有効なJSON Schema Draft 2020-12であること
2. [ ] 全フィールド型（24種+エイリアス）がtypeプロパティのenumに含まれること
3. [ ] 必須プロパティ（name, type, root, structs内のフィールド）が `required` で定義されていること
4. [ ] 各プロパティに `description` が付与されていること（ホバードキュメント用）
5. [ ] 既存の全フォーマット定義（png, bmp, wav, zip, elf）がスキーマに適合すること
6. [ ] VS Code + YAML拡張でオートコンプリートが動作すること（手動確認）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] README.md — スキーマの存在とエディタ連携について追記
- [ ] docs/dsl-reference.md — スキーマファイルへの参照を追記

---

## 設計メモ

- JSON Schema Draft 2020-12 を採用。`$defs` でサブスキーマを分離。
- `additionalProperties: false` をトップレベル・各定義に設定し、タイプミスを検出。
- `size` フィールドは `["string", "integer"]` で文字列・整数の両方を許容（YAMLでは `size: 4` と `size: "4"` の両方が書けるため）。
- `expected` のアイテムは `integer`（0-255）。
- `type` プロパティには正規型 + 全エイリアス（37値）を列挙。

---

## 実装メモ

### 実装中の設計変更

なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| JsonSchemaTests | Schema_IsValidJson | 1 |
| JsonSchemaTests | Schema_HasDraft202012 | 1 |
| JsonSchemaTests | Schema_HasAllFieldTypes | 2 |
| JsonSchemaTests | Schema_RequiredProperties_TopLevel | 3 |
| JsonSchemaTests | Schema_RequiredProperties_Field | 3 |
| JsonSchemaTests | Schema_AllPropertiesHaveDescriptions | 4 |
| JsonSchemaTests | Schema_ValidatesFormatFile (5ファイル) | 5 |
| JsonSchemaTests | Schema_RejectsInvalidFormat_MissingName | 3 |
| JsonSchemaTests | Schema_RejectsInvalidFormat_MissingRoot | 3 |
| JsonSchemaTests | Schema_RejectsInvalidFieldType | 2 |
| JsonSchemaTests | Schema_RejectsUnknownTopLevelProperty | 1 |

### 気づき・今後の課題

- YamlDotNetの `JsonCompatible()` シリアライザは全スカラーを文字列として出力するため、テストでの YAML→JSON 変換には型推論付きの独自コンバーターが必要だった。
- 式（`{...}`）内部の構文はスキーマ範囲外。将来的に `pattern` で簡易チェックを追加できる余地あり。
