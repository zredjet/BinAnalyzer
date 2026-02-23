# REQ-152: 文字列テーブルのUTF-8対応

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在の文字列テーブル機能（`string_table`）はASCIIエンコーディングに固定されている。null終端バイトまでを1エントリとして読み取り、ASCIIとしてデコードする。

ELFの `.strtab` セクション等、ASCIIで十分なフォーマットが大半だが、以下のケースではUTF-8やその他のエンコーディングが必要になる。

| ケース | エンコーディング | 例 |
|---|---|---|
| Windows PEのリソース文字列 | UTF-16LE | リソース名、バージョン情報 |
| Java Class定数プール | Modified UTF-8 | クラス名、メソッド名（日本語含む） |
| SQLite文字列 | UTF-8 / UTF-16LE/BE | テーブル名、カラム名 |
| macOS plistバイナリ | UTF-8 / UTF-16BE | プロパティ名、値 |

## 機能要件

### 追加する機能

- [x] `string_table` にエンコーディング指定を追加（`string_table: { name: "table_name", encoding: "utf8" }`）
- [x] UTF-8エンコーディングのサポート（null終端）
- [x] UTF-16LE/BEエンコーディングのサポート（null(0x0000)終端）
- [x] エンコーディング省略時のデフォルトはASCII（後方互換性維持）

### 変更する既存機能

- [x] `DecodeContext` — 文字列テーブルのエンコーディング情報保持
- [x] `BinaryDecoder` — 文字列テーブルルックアップ時のエンコーディング対応

### 変更しないもの（スコープ外）

- `string_table: true` の既存構文 — 後方互換として引き続きASCIIとして動作
- Shift_JIS、EUC-JP等の文字エンコーディング — 需要が出てから追加

## 受入条件

1. [x] `encoding: "utf8"` 指定時にUTF-8文字列が正しくルックアップされること
2. [x] `encoding: "utf16le"` 指定時にUTF-16LE文字列がnull(0x0000)終端で正しくルックアップされること
3. [x] エンコーディング省略時にASCIIとして動作すること（後方互換性）
4. [x] `string_table: true` の既存構文が引き続き動作すること
5. [x] 不正なエンコーディング名の指定時にバリデーションエラーが発生すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition` — 文字列テーブルエンコーディング情報追加 |
| BinAnalyzer.Dsl | `YamlStructModel` — `string_table` のオブジェクト形式パース |
| BinAnalyzer.Engine | `DecodeContext` — エンコーディング付き文字列テーブル登録/ルックアップ |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 文字列テーブルのエンコーディング指定の説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- `StructDefinition.IsStringTable: bool` を `StringTableEncoding?: StringTableEncoding?` に変更。null = 文字列テーブルではない
- `StringTableEncoding` enum を新規追加: `Ascii`, `Utf8`, `Utf16Le`, `Utf16Be`
- YamlDotNet の `object?` デシリアライズを利用し、`true` → `bool`, `{ encoding: utf8 }` → `Dictionary` として自動変換
- `DecodeContext._stringTables` の値型にエンコーディング情報を追加

### モデル変更

- `StringTableEncoding.cs` (新規): enum定義
- `StructDefinition.cs`: `IsStringTable: bool` → `StringTableEncoding?: StringTableEncoding?`
- `YamlStructModel.cs`: `StringTable: bool?` → `StringTable: object?`
- `DecodeContext._stringTables`: `Dictionary<string, ReadOnlyMemory<byte>>` → `Dictionary<string, (ReadOnlyMemory<byte> Data, StringTableEncoding Encoding)>`

### インタフェース変更

- `DecodeContext.RegisterStringTable(name, offset, size)` → `RegisterStringTable(name, offset, size, encoding)`
- `DecodeContext.LookupString` — エンコーディングに応じたnull終端検索とデコード

### 代替案

- `IsStringTable` を残して `StringTableEncoding` を追加する案 → プロパティ重複、IsStringTable の意味が曖昧になるため却下
- struct レベルでなく field レベルで encoding を指定する案 → 文字列テーブル全体のエンコーディングは統一されるため struct レベルが自然

### 懸念事項

- UTF-16 の null 終端は 2 バイト単位で検索する必要がある（1 バイトの 0x00 は UTF-16 文字の一部として出現しうる）→ 実装済み

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

特になし。計画通り実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| StringTableParsingTests | ParsesStringTableWithUtf8Encoding | 1 |
| StringTableParsingTests | ParsesStringTableWithUtf16LeEncoding | 2 |
| StringTableParsingTests | ParsesStringTableWithUtf16BeEncoding | 2 |
| StringTableParsingTests | ParsesStringTableWithEncodingOmittedDefaultsToAscii | 3 |
| StringTableParsingTests | ParsesStructWithStringTable (既存更新) | 4 |
| StringTableParsingTests | StringTableFalseIsNull | 4 |
| StringTableParsingTests | ThrowsForInvalidEncoding | 5 |
| StringTableTests | LooksUpUtf8StringFromTable | 1 |
| StringTableTests | LooksUpUtf16LeStringFromTable | 2 |
| StringTableTests | LooksUpUtf16BeStringFromTable | 2 |
| StringTableTests | Utf16LeHandlesSingleNullByteInString | 2 |
| StringTableTests | LooksUpStringFromTable (既存、後方互換) | 3, 4 |

### 気づき・今後の課題

- Modified UTF-8 (Java Class) は標準 UTF-8 と微妙に異なるが、BinAnalyzer では標準 UTF-8 で十分な場合が多い
- Shift_JIS 等のマルチバイトエンコーディングは null 終端が不明確なため、将来的に長さプレフィックス方式の文字列テーブルが必要かもしれない
