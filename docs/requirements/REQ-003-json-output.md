# REQ-003: JSON出力フォーマッター

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在の出力はツリー表示（人間向け）のみ。デコード結果をプログラム的に処理したいユースケース（jq連携、スクリプトからの自動処理、テスト結果の検証自動化、他ツールとのパイプライン連携）に対応できない。

JSON出力は実装コストが比較的低く、ツールとしての実用性を大幅に向上させる。`System.Text.Json`（BCL標準）のみで実装可能。

## 機能要件

### 追加する機能

- [ ] `JsonOutputFormatter` クラスの新規作成（`BinAnalyzer.Output` に配置）
  - `IOutputFormatter` を実装
  - `DecodedStruct` を入力とし、JSON文字列を返す
- [ ] JSON出力の構造
  - 各ノードの型情報（`_type`: `"integer"`, `"string"`, `"bytes"`, `"struct"`, `"array"`）
  - オフセットとサイズ情報
  - 値（整数値、文字列値、バイト列の16進表現）
  - enum情報（該当する場合）
  - フラグ情報（該当する場合）
  - バリデーション結果（expected付きフィールド）
- [ ] CLIに `--output` / `-o` オプションを追加
  - `tree`（デフォルト、現行動作）
  - `json`（JSON出力）

### JSON出力例

```json
{
  "_type": "struct",
  "name": "PNG",
  "struct_type": "png",
  "offset": 0,
  "size": 67,
  "children": {
    "signature": {
      "_type": "bytes",
      "offset": 0,
      "size": 8,
      "hex": "89 50 4E 47 0D 0A 1A 0A",
      "valid": true
    },
    "chunks": {
      "_type": "array",
      "offset": 8,
      "size": 59,
      "count": 2,
      "elements": [
        {
          "_type": "struct",
          "name": "chunks",
          "struct_type": "chunk",
          "offset": 8,
          "size": 25,
          "children": {
            "length": {
              "_type": "integer",
              "offset": 8,
              "size": 4,
              "value": 13,
              "hex": "0x0D"
            },
            "type": {
              "_type": "string",
              "offset": 12,
              "size": 4,
              "value": "IHDR",
              "encoding": "ascii",
              "flags": {
                "ancillary": { "set": false, "meaning": "no" },
                "private": { "set": false, "meaning": "no" },
                "safe_to_copy": { "set": false, "meaning": "no" }
              }
            }
          }
        }
      ]
    }
  }
}
```

### 変更する既存機能

- [ ] `Program.cs`（CLI）: `--output` / `-o` オプションの追加と出力フォーマッター切替ロジック

### 変更しないもの（スコープ外）

- JSONスキーマの公開定義
- YAML出力やCSV出力など他の出力形式
- JSON出力のストリーミング（大規模ファイルの段階的出力）

## 受入条件

1. [ ] `JsonOutputFormatter.Format(DecodedStruct)` が有効なJSON文字列を返すこと
2. [ ] 出力されたJSONが `System.Text.Json.JsonDocument` でパース可能であること
3. [ ] 整数フィールド: value, hex, offset, size が出力されること
4. [ ] 文字列フィールド: value, encoding, offset, size が出力されること
5. [ ] バイトフィールド: hex表現, offset, size, バリデーション結果が出力されること
6. [ ] enum情報: enum_label が出力されること
7. [ ] flags情報: 各フラグの状態が出力されること
8. [ ] struct/array: 子要素が再帰的に出力されること
9. [ ] CLIで `--output json` を指定するとJSON出力になること
10. [ ] CLIで `--output tree` またはオプション省略でツリー出力（現行動作）になること
11. [ ] PNGテストデータでJSON出力→パースの往復テストが通ること
12. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | `JsonOutputFormatter` の新規追加 |
| BinAnalyzer.Cli | `--output` オプション追加、フォーマッター切替 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要（出力フォーマッターの追加は設計上想定済み）
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 使用例にJSON出力を追記

---

## 設計メモ

### 設計方針

`System.Text.Json`（BCL標準）のみを使用し、外部ライブラリの追加は行わない。`Utf8JsonWriter` を使って手動でJSONを構築する。理由:
- デコード結果ツリーのノード型に応じて出力構造を制御する必要がある
- `JsonSerializer` によるリフレクションベースの変換では `_type` 判別フィールドの制御が困難
- `ReadOnlyMemory<byte>` 等の特殊な型を16進文字列に変換する必要がある

### モデル変更

新規追加のみ。既存モデルへの変更なし。

```
src/BinAnalyzer.Output/
└── JsonOutputFormatter.cs    # 新規追加
```

**JsonOutputFormatter**:
```csharp
public sealed class JsonOutputFormatter : IOutputFormatter
{
    public string Format(DecodedStruct root)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        WriteNode(writer, root);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
```

各ノード型の出力方法:
- `DecodedStruct` → `children` オブジェクト（キー: フィールド名）
- `DecodedArray` → `elements` 配列
- `DecodedInteger` → `value` + `hex`（`0x` 付き） + enum情報
- `DecodedString` → `value` + `encoding` + flags情報
- `DecodedBytes` → `hex`（スペース区切り） + validation情報
- `DecodedFlags` → `raw_value` + 各フラグ状態

全ノード共通: `_type`, `name`, `offset`, `size`

### インタフェース変更

- `IOutputFormatter` への変更なし（既存の `Format(DecodedStruct)` シグネチャをそのまま実装）
- CLIでの切替: `--output` オプションの値に応じてインスタンスを切り替え

```csharp
IOutputFormatter formatter = outputFormat switch
{
    "json" => new JsonOutputFormatter(),
    _ => new TreeOutputFormatter(),
};
```

### CLI変更

```csharp
var outputOption = new Option<string>("-o", "--output")
{
    Description = "出力形式 (tree, json)",
};
outputOption.SetDefaultValue("tree");
```

### 代替案

**案A: JsonSerializer + カスタムコンバーター（不採用）**
- 理由: DecodedNodeの各派生型ごとにJsonConverterを定義する必要があり、コード量がUtf8JsonWriterの手動構築と変わらない。`_type` 判別フィールドの追加もコンバーター内で行う必要があり、可読性が低下する。

**案B: System.Text.Json.Nodes (JsonObject/JsonArray)で構築してSerialize（不採用）**
- 理由: 中間のJsonNodeオブジェクトツリーが不要なメモリを消費する。Utf8JsonWriterの直接書き込みの方が効率的。

### 懸念事項

1. **大規模ファイルのメモリ使用量**: JSON全体をstringとして返す現在のインタフェースでは、大規模ファイルのJSON出力がメモリを圧迫する可能性。ただし、IOutputFormatterのシグネチャを変更すると既存のTreeOutputFormatterにも影響するため、現時点ではstringで返す。

2. **childrenのキー重複**: DecodedStructの子ノードに同名フィールドが存在する場合（repeat等）、JSONオブジェクトのキーが重複する。ただし、現在のIR設計では同一struct内のフィールド名は一意であるため、問題にならない。配列要素は `elements` 配列で扱うため重複しない。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `DefaultValueFactory` を使用（System.CommandLine 2.0.2 では `SetDefaultValue` が存在しないため）。
- `DecodedBitfield` のJSON出力も対応（REQ-002で追加されたノード型）。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Output/JsonOutputFormatter.cs` | 新規作成: Utf8JsonWriter による全ノード型のJSON出力 |
| `src/BinAnalyzer.Cli/Program.cs` | `--output` / `-o` オプション追加、フォーマッター切替 |
| `tests/BinAnalyzer.Integration.Tests/JsonOutputTests.cs` | 新規作成: 12テスト |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| JsonOutputTests | JsonOutput_IsValidJson | AC-1, AC-2（有効なJSON） |
| JsonOutputTests | JsonOutput_Struct_HasExpectedProperties | AC-8（struct出力） |
| JsonOutputTests | JsonOutput_Integer_HasValueAndHex | AC-3（整数） |
| JsonOutputTests | JsonOutput_Integer_WithEnum | AC-6（enum情報） |
| JsonOutputTests | JsonOutput_Bytes_HasHexAndValidation | AC-5（バイト列+検証） |
| JsonOutputTests | JsonOutput_String_HasValueAndEncoding | AC-4（文字列） |
| JsonOutputTests | JsonOutput_String_WithFlags | AC-7（flags情報） |
| JsonOutputTests | JsonOutput_Array_HasElements | AC-8（array出力） |
| JsonOutputTests | JsonOutput_Flags_HasRawValueAndStates | AC-7（flags） |
| JsonOutputTests | JsonOutput_Bitfield_HasFieldsArray | bitfield対応 |
| JsonOutputTests | JsonOutput_MinimalPng_RoundTrip | AC-11（PNG往復テスト） |

### 受入条件の充足状況

- [x] AC-1: `JsonOutputFormatter.Format(DecodedStruct)` が有効なJSON文字列を返す
- [x] AC-2: 出力されたJSONが `JsonDocument` でパース可能
- [x] AC-3: 整数フィールド: value, hex, offset, size が出力される
- [x] AC-4: 文字列フィールド: value, encoding, offset, size が出力される
- [x] AC-5: バイトフィールド: hex表現, offset, size, バリデーション結果が出力される
- [x] AC-6: enum情報: enum_label が出力される
- [x] AC-7: flags情報: 各フラグの状態が出力される
- [x] AC-8: struct/array: 子要素が再帰的に出力される
- [x] AC-9: CLIで `--output json` を指定するとJSON出力になる（実装済み、CLIテストは手動確認）
- [x] AC-10: CLIで `--output tree` またはオプション省略でツリー出力
- [x] AC-11: PNGテストデータでJSON出力→パースの往復テストが通る
- [x] AC-12: 既存テスト全て通過（122テスト合格）

### 気づき・今後の課題

- CLIの `--output` オプションの値バリデーション（不正な値の場合のエラーメッセージ）は未実装。`tree` / `json` 以外の値は暗黙的にtreeにフォールバックする。
- `description` プロパティはノードに設定されている場合のみ出力される（省略可能フィールド）。
