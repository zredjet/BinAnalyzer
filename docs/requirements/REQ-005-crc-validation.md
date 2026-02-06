# REQ-005: CRC検証機能

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

PNGフォーマットでは各チャンクにCRC-32値が含まれ、チャンクタイプ＋データから計算したCRC値と照合することでデータの整合性を検証できる。現在のBinAnalyzerはCRC値を整数として表示するのみで、計算・検証は行わない。

CRC検証はバイナリ解析ツールとしての信頼性に直結する機能であり、破損データの検出に有用。

DSLレベルでCRC検証を定義できるようにすることで、PNG以外のフォーマット（ZIP、GIF等）にも汎用的に適用可能になる。

## 機能要件

### 追加する機能

- [ ] DSLに `checksum` フィールド属性を追加
  ```yaml
  - name: crc
    type: uint32
    checksum:
      algorithm: crc32
      range: "{type_offset}:{type_offset + 4 + length}"
  ```
  または、より簡潔な記法:
  ```yaml
  - name: crc
    type: uint32
    checksum:
      algorithm: crc32
      fields: [type, data]
  ```
- [ ] CRC-32アルゴリズムの実装（`BinAnalyzer.Engine` に配置）
- [ ] デコード結果にチェックサム検証結果を含める
  - `DecodedInteger.ChecksumValid`: `true` / `false` / `null`（チェックサムフィールドでない場合）
- [ ] ツリー出力でチェックサム検証結果を表示
  ```
  └── crc: 0x1A2B3C4D ✓ (CRC-32)
  └── crc: 0x1A2B3C4D ✗ (CRC-32, expected: 0xDEADBEEF)
  ```

### 変更する既存機能

- [ ] `FieldDefinition` にチェックサム関連プロパティの追加
- [ ] `YamlFieldModel` にチェックサム定義の追加
- [ ] `YamlToIrMapper` にチェックサムのマッピング
- [ ] `BinaryDecoder` にチェックサム計算・検証ロジック
- [ ] `TreeOutputFormatter` にチェックサム結果の表示

### 変更しないもの（スコープ外）

- CRC-32以外のアルゴリズム（CRC-16、Adler-32、MD5、SHA等は将来の拡張として `algorithm` フィールドで対応予定）
- チェックサムエラー時の自動修復
- チェックサム計算の並列化

## 受入条件

1. [ ] DSLで `checksum` 属性付きフィールドが定義できること
2. [ ] CRC-32の計算結果が正しいこと（既知のテストベクターで検証）
3. [ ] 正しいCRCを持つPNGチャンクで `ChecksumValid = true` となること
4. [ ] 不正なCRCを持つデータで `ChecksumValid = false` となること
5. [ ] ツリー出力にチェックサム検証結果（✓/✗）が表示されること
6. [ ] 不正時に期待値が表示されること
7. [ ] `png.bdef.yaml` のcrcフィールドにchecksum属性を追加し、実際のPNGで検証が動作すること
8. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` にチェックサム属性追加、`DecodedInteger` に検証結果追加 |
| BinAnalyzer.Dsl | `YamlFieldModel` 拡張、`YamlToIrMapper` にマッピング追加 |
| BinAnalyzer.Engine | CRC-32実装、`BinaryDecoder` にチェックサム検証ロジック追加 |
| BinAnalyzer.Output | チェックサム結果の表示 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — checksum属性の構文ドキュメント
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

チェックサム検証を「名前付きフィールド群を対象とする」DSL構文で定義する。オフセットベースの範囲指定は式の複雑さと可読性の低下を招くため不採用。

DSL構文（確定版）:
```yaml
- name: crc
  type: uint32
  checksum:
    algorithm: crc32
    fields: [type, data]    # 同一struct内の先行フィールド名を列挙
```

`fields` で指定された名前のフィールドが占めるバイト範囲を元データから抽出し、CRC-32を計算。フィールド値（読み取られたCRC値）と比較する。

### モデル変更

#### Core/Models/ への追加

**ChecksumSpec**（チェックサム仕様）:
```csharp
public sealed class ChecksumSpec
{
    public required string Algorithm { get; init; }             // "crc32"
    public required IReadOnlyList<string> FieldNames { get; init; }  // 対象フィールド名
}
```

**FieldDefinition への追加**:
```csharp
public ChecksumSpec? Checksum { get; init; }
```

#### Core/Decoded/ への追加

**DecodedInteger への追加**:
```csharp
public bool? ChecksumValid { get; init; }          // チェックサム検証結果
public long? ChecksumExpected { get; init; }        // 不一致時の期待値
```

#### Engine/ への追加

**Crc32Calculator**（静的クラス）:
```csharp
public static class Crc32Calculator
{
    public static uint Compute(ReadOnlySpan<byte> data);
}
```

CRC-32アルゴリズム: 多項式 0xEDB88320（PNG/ZIPで使用されるISO 3309準拠）。ルックアップテーブルを使用。

#### Dsl/YamlModels/ への追加

**YamlChecksumModel**:
```csharp
public sealed class YamlChecksumModel
{
    public string Algorithm { get; set; }
    public List<string> Fields { get; set; }
}
```

**YamlFieldModel への追加**:
```csharp
public YamlChecksumModel? Checksum { get; set; }
```

### デコードロジック

チェックサム検証は `DecodeIntegerField` の後処理として実装:

```csharp
// DecodeIntegerField 内、値読み取り後
if (field.Checksum is not null)
{
    // 同一structの先行フィールドのデコード結果からオフセット+サイズを取得
    // 元バイナリからその範囲を抽出
    // CRC-32を計算
    // 読み取り値と比較
}
```

先行フィールドの位置情報の取得方法: `DecodeStruct` で構築中の `children` リスト（`List<DecodedNode>`）から、名前で検索してOffset/Sizeを取得。これを `DecodeIntegerField` に渡す必要があるため、シグネチャに `IReadOnlyList<DecodedNode> siblings` パラメータを追加する。

### ツリー出力変更

```
└── crc: 0x1A2B3C4D ✓ (CRC-32)
└── crc: 0x1A2B3C4D ✗ (CRC-32, 期待値: 0xDEADBEEF)
```

`TreeOutputFormatter.FormatInteger` で `ChecksumValid` を確認し、既存のバリデーション表示（✓/✗）と同じ箇所に表示。

### png.bdef.yaml の変更

```yaml
chunk:
  - name: length
    type: uint32
  - name: type
    type: ascii
    size: "4"
    flags: chunk_type_flags
  - name: data
    type: switch
    size: "{length}"
    switch_on: "{type}"
    cases: ...
  - name: crc
    type: uint32
    checksum:
      algorithm: crc32
      fields: [type, data]    # ← 追加
```

### 代替案

**案A: オフセット範囲で指定する（不採用）**
```yaml
checksum:
  algorithm: crc32
  range: "{type_offset}:{type_offset + 4 + length}"
```
- 理由: オフセット計算式が複雑で可読性が低い。フィールド名指定の方がDSLとして自然。

**案B: チェックサムをデコーダーのビルトイン機能にする（不採用）**
- 理由: DSLで宣言的に定義する方が汎用性が高い。PNGだけでなくZIP等にも適用可能。

### 懸念事項

1. **siblingsパラメータの追加**: `DecodeIntegerField` に `siblings` を渡す必要があり、メソッドシグネチャが変わる。`DecodeStruct` のループ内で構築中の `children` を渡す形に変更。他のフィールドデコーダーは影響を受けない。

2. **switchフィールドのdata範囲**: PNGの `data` フィールドはswitch型。そのOffset/Sizeは `DecodedStruct` として記録されている。`SliceOriginal` で元バイナリからバイト列を取得する際、switch内のデコード結果のOffset/Sizeを使用する。

3. **CRC計算の対象範囲が非連続な場合**: `fields: [type, data]` で指定されたフィールドがバイナリ上で連続であることを前提とする（PNGのチャンク構造では常に連続）。非連続の場合は各フィールドのバイト列を連結して計算。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `MemoryStream` を使用して対象フィールドのバイト列を連結し、CRC-32を計算するアプローチを採用。
- CRC-32のルックアップテーブルは静的フィールドとして一度だけ生成。
- `DecodeField` / `DecodeSingleField` に `siblings` パラメータ（オプション）を追加。既存の呼び出し元（`DecodeRepeatedField` 内）は影響なし。
- `JsonOutputFormatter` にもチェックサム情報（`checksum_valid`, `checksum_expected`）を出力するよう対応。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Models/ChecksumSpec.cs` | 新規: チェックサム仕様モデル |
| `src/BinAnalyzer.Core/Models/FieldDefinition.cs` | `Checksum` プロパティ追加 |
| `src/BinAnalyzer.Core/Decoded/DecodedInteger.cs` | `ChecksumValid`, `ChecksumExpected` プロパティ追加 |
| `src/BinAnalyzer.Engine/Crc32Calculator.cs` | 新規: CRC-32計算（ISO 3309準拠） |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `VerifyChecksum` メソッド追加、siblings パラメータ追加 |
| `src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs` | `YamlChecksumModel` 追加 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | `ParseChecksum` メソッド追加 |
| `src/BinAnalyzer.Output/TreeOutputFormatter.cs` | チェックサム結果表示（✓/✗）追加 |
| `src/BinAnalyzer.Output/JsonOutputFormatter.cs` | チェックサム情報のJSON出力追加 |
| `formats/png.bdef.yaml` | crcフィールドに `checksum` 属性追加 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Crc32Tests | Compute_KnownVector_ReturnsCorrect | AC-2（既知テストベクター） |
| Crc32Tests | Compute_EmptyData_ReturnsZero | AC-2 |
| Crc32Tests | Compute_SingleByte_ReturnsCorrect | AC-2 |
| Crc32Tests | Compute_ConsecutiveBytes_IsConsistent | AC-2 |
| Crc32Tests | Compute_DifferentData_ReturnsDifferentCrc | AC-2 |
| ChecksumDecoderTests | Decode_ValidChecksum_ReturnsValid | AC-3 |
| ChecksumDecoderTests | Decode_InvalidChecksum_ReturnsInvalidWithExpected | AC-4, AC-6 |
| ChecksumDecoderTests | Decode_MultipleFieldsChecksum_ComputesOverAll | AC-3 |
| ChecksumDecoderTests | Decode_FieldWithoutChecksum_HasNullProperties | AC-8 |
| ChecksumParsingTests | Load_ChecksumField_ParsesCorrectly | AC-1（DSL定義） |
| ChecksumParsingTests | Load_FieldWithoutChecksum_HasNullChecksum | AC-1 |
| ChecksumIntegrationTests | Png_ValidCrc_ShowsCheckmark | AC-5, AC-7（PNG統合テスト） |
| ChecksumIntegrationTests | Png_ValidCrc_JsonOutput_ShowsValid | AC-7（JSON出力） |

### 受入条件の充足状況

- [x] AC-1: DSLで `checksum` 属性付きフィールドが定義できる
- [x] AC-2: CRC-32の計算結果が正しい（ISO 3309テストベクター "123456789" = 0xCBF43926）
- [x] AC-3: 正しいCRCを持つPNGチャンクで `ChecksumValid = true`
- [x] AC-4: 不正なCRCで `ChecksumValid = false`
- [x] AC-5: ツリー出力にチェックサム検証結果（✓/✗）が表示される
- [x] AC-6: 不正時に期待値が表示される
- [x] AC-7: `png.bdef.yaml` のcrcフィールドにchecksum属性を追加し、実際のPNGで検証が動作する
- [x] AC-8: 既存テスト全て通過（150テスト合格）

### 気づき・今後の課題

- PngTestDataGenerator と Crc32Calculator の CRC 実装は同じアルゴリズムだが独立した実装。将来的にはテストヘルパーも Crc32Calculator を使うよう統一可能。
- CRC-32以外のアルゴリズム（CRC-16、Adler-32等）は `algorithm` フィールドで拡張可能な設計。現時点では `crc32` のみ対応。
