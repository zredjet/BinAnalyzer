# REQ-117: Protobuf ワイヤーフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 実装完了 |
| 優先度 | 低 |
| 依存 | なし |
| フェーズ | D（フォーマット） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-14 |

## 背景・動機

Protocol Buffers のワイヤーフォーマットは多くのシステムで使われるバイナリシリアライゼーション形式。`.proto` スキーマなしでもフィールド番号とワイヤータイプの構造は解析可能。BinAnalyzer でワイヤーレベルの構造を確認できれば、デバッグや解析に有用。

### ユースケース

- `.proto` スキーマなしでの Protobuf バイナリの構造確認
- フィールド番号、ワイヤータイプ、値の一覧表示
- gRPC 通信のペイロード解析
- 不明な Protobuf メッセージの構造探索

## 機能要件

### 追加する機能

- [x] `formats/protobuf.bdef.yaml` の作成
- [x] varint（LEB128）ベースのタグ解析（フィールド番号 + ワイヤータイプ）
- [x] ワイヤータイプ別のペイロード読み取り（varint, 64-bit, length-delimited, 32-bit）
- [ ] ネストされた length-delimited フィールドの再帰的パース試行

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- `.proto` スキーマとの照合
- フィールド名の自動解決
- Protobuf テキスト形式への変換
- packed repeated フィールドの詳細展開

## 受入条件

1. [x] `formats/protobuf.bdef.yaml` が作成されていること
2. [x] Protobuf バイナリのフィールドタグが正しくデコードされること
3. [x] 各ワイヤータイプの値が正しく表示されること
4. [ ] length-delimited フィールドのネスト解析が動作すること
5. [x] 実際の Protobuf バイナリでデコードが成功すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

YAML-DSL のみで実装。C# コード変更不要。既存の `uleb128`、`virtual`、`if:` 機能を組み合わせて Protobuf ワイヤーフォーマットをデコードする。

### `if:` 方式 vs `switch` 方式

`if:` 条件分岐方式を採用。varint ペイロード（ワイヤータイプ 0）は可変長の `uleb128` で読む必要があり、`switch` + `size:` パターンでは varint のサイズを事前に計算できないため。

### length-delimited の扱い

スキーマなしではバイト列/文字列/ネストメッセージの区別が不可能なため、raw bytes として読む。ネスト解析は今後の課題。

### 非推奨ワイヤータイプ

ワイヤータイプ 3（start group）、4（end group）は非推奨のため対応しない。

### モデル変更

なし

### インタフェース変更

なし

### 代替案

なし

### 懸念事項

なし

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更 | 内容 |
|---|---|---|
| `formats/protobuf.bdef.yaml` | 新規 | Protobuf ワイヤーフォーマット定義 |
| `tests/BinAnalyzer.Integration.Tests/ProtobufTestDataGenerator.cs` | 新規 | 最小 Protobuf テストデータ生成（3フィールド、15バイト） |
| `tests/BinAnalyzer.Integration.Tests/RealFileFixture.cs` | 変更 | `EnsureFile("test.protobuf", ...)` 追加 |
| `tests/BinAnalyzer.Integration.Tests/GoldenFileTests.cs` | 変更 | `[InlineData("protobuf", ...)]` 追加 |
| `tests/BinAnalyzer.Integration.Tests/RealFileValidationTests.cs` | 変更 | `[InlineData("protobuf.bdef.yaml", ...)]` 追加 |
| `testdata/golden/protobuf.json` | 新規 | ゴールデンファイル |

### 実装中の設計変更

- 計画では `raw_data` struct を定義していたが、未使用で VAL109 警告が出るため削除

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| GoldenFileTests | DecodeResult_MatchesGoldenFile(protobuf) | 受入条件 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(protobuf) | 受入条件 5, 6 |

### 気づき・今後の課題

- 受入条件 4（length-delimited のネスト解析）は未実装。スキーマなしでのネスト判定が困難なため、今後の拡張課題とする
- `wire_type` の `virtual` フィールドに `enum:` を付与すると VAL103 警告が出るが、デコード動作には影響なし
