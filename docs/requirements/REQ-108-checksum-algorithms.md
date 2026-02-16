# REQ-108: チェックサムアルゴリズム拡張

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 実装完了 |
| 優先度 | 高 |
| 依存 | なし |
| フェーズ | D（応用） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-17 |

## 背景・動機

現在のチェックサム検証は CRC-32（ISO 3309, PNG/ZIP互換）のみ対応。実際のバイナリフォーマットでは MD5, SHA-1, SHA-256, Adler-32, CRC-16 など多様なアルゴリズムが使われている。

### ユースケース

- ZIP ファイルのエントリごとの CRC-32（対応済み）に加え、central directory の整合性検証
- PE ファイルのヘッダチェックサム（独自アルゴリズム）
- 汎用的な MD5/SHA ダイジェスト検証
- zlib ストリームの Adler-32 検証

## 機能要件

### 追加する機能

- `checksum.algorithm` で指定可能なアルゴリズムの追加: `crc16-ccitt`, `crc16-ibm`, `adler32`, `md5`, `sha1`, `sha256`
- .NET BCL の `System.Security.Cryptography` を活用（MD5, SHA-1, SHA-256）
- Adler-32, CRC-16 はカスタム実装
- 各アルゴリズムの期待値フォーマット（hex文字列 or 整数）の統一

### 変更しないもの（スコープ外）

- PE ヘッダチェックサム（独自アルゴリズムのため個別対応が必要）
- HMAC 系のキー付きハッシュ
- チェックサムの自動修復

## 受入条件

1. `crc16-ccitt`, `crc16-ibm`, `adler32`, `md5`, `sha1`, `sha256` が `checksum.algorithm` に指定可能であること
2. 各アルゴリズムで正しいチェックサム値が計算されること
3. 検証結果が DecodedInteger / DecodedBytes の該当プロパティに反映されること
4. 既存の CRC-32 チェックサムが従来通り動作すること（後方互換性）
5. 単体テストが各アルゴリズムに対して追加されていること
6. JSON Schema に新アルゴリズムが記載されていること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumAlgorithms` 定数クラス新規、`DecodedInteger` に `ChecksumAlgorithm` プロパティ追加、`DecodedBytes` にチェックサム3プロパティ追加、`FormatValidator` にバリデーション追加（VAL113-115） |
| BinAnalyzer.Engine | `Crc16Calculator`, `Adler32Calculator`, `HashCalculator` 新規、`BinaryDecoder.VerifyChecksum` 拡張・`VerifyHashChecksum` 追加・`DecodeBytesField` 改修 |
| BinAnalyzer.Output | `TreeOutputFormatter`, `JsonOutputFormatter`, `HtmlOutputFormatter` でアルゴリズム名動的表示、`DecodedBytes` チェックサム表示追加 |
| BinAnalyzer.Dsl | 変更なし（既存の `YamlChecksumModel` / `YamlToIrMapper` はアルゴリズム文字列をそのまま通すため） |
| BinAnalyzer.Cli | 変更なし |
| schemas | `bdef.schema.json` の algorithm enum 拡張 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — チェックサムセクション（§チェックサム）を拡張: 「現在はCRC-32のみ対応」を削除し、アルゴリズム一覧テーブル（名前・カテゴリ・対応フィールド型）、整数系（uint16/uint32 + crc16/adler32）とハッシュ系（bytes + md5/sha1/sha256）の使い分け説明・構文例を追記
- [ ] docs/parser-design.md — §2.4 FormatValidator のバリデーション一覧に VAL113（未知アルゴリズム警告）, VAL114（整数アルゴリズム×非整数フィールド）, VAL115（ハッシュアルゴリズム×非bytesフィールド）を追記
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### アルゴリズム分類

アルゴリズムを「整数系」と「ハッシュ系」の2カテゴリに分類する。出力サイズがフィールド型と対応する。

| アルゴリズム | カテゴリ | 出力サイズ | 対応フィールド型 | 実装方式 |
|---|---|---|---|---|
| crc32 | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 | 既存 `Crc32Calculator` |
| crc16-ccitt | 整数系 | 2 bytes (16-bit) | uint16 等の整数型 | 新規 `Crc16Calculator` |
| crc16-ibm | 整数系 | 2 bytes (16-bit) | uint16 等の整数型 | 新規 `Crc16Calculator` |
| adler32 | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 | 新規 `Adler32Calculator` |
| md5 | ハッシュ系 | 16 bytes | bytes | `System.Security.Cryptography.MD5` |
| sha1 | ハッシュ系 | 20 bytes | bytes | `System.Security.Cryptography.SHA1` |
| sha256 | ハッシュ系 | 32 bytes | bytes | `System.Security.Cryptography.SHA256` |

整数系は既存の `DecodedInteger` のチェックサムプロパティで処理。ハッシュ系は出力が `long` に収まらないため `DecodedBytes` を拡張して対応。

### CRC-16 バリアント仕様

| バリアント | 多項式 | 初期値 | 入力反転 | 出力反転 | 最終XOR |
|---|---|---|---|---|---|
| crc16-ccitt | 0x1021 | 0xFFFF | false | false | 0x0000 |
| crc16-ibm | 0x8005 | 0x0000 | true | true | 0x0000 |

テストベクター（"123456789" = 0x31..0x39）:
- crc16-ccitt: 0x29B1
- crc16-ibm: 0xBB3D

### モデル変更

#### DecodedInteger への追加

```csharp
public sealed class DecodedInteger : DecodedNode
{
    // 既存プロパティ...
    public bool? ChecksumValid { get; init; }
    public long? ChecksumExpected { get; init; }
    public string? ChecksumAlgorithm { get; init; }   // ← 追加
}
```

`ChecksumAlgorithm` を保持することで、出力フォーマッターがアルゴリズム名を動的に表示可能になる。

#### DecodedBytes への追加

```csharp
public sealed class DecodedBytes : DecodedNode
{
    public required ReadOnlyMemory<byte> RawBytes { get; init; }
    public bool? ChecksumValid { get; init; }          // ← 追加
    public string? ChecksumExpectedHex { get; init; }  // ← 追加 (hex文字列)
    public string? ChecksumAlgorithm { get; init; }    // ← 追加
}
```

ハッシュ系の期待値は `long` に収まらないため、16進文字列（例: `"d41d8cd98f00b204e9800998ecf8427e"`）で保持する。

### ChecksumAlgorithms 静的クラス（Core 層）

Engine（アルゴリズム判定）と Output（表示名）の両方から参照するため、Core 層に配置。

```csharp
namespace BinAnalyzer.Core.Models;

public static class ChecksumAlgorithms
{
    public const string Crc32 = "crc32";
    public const string Crc16Ccitt = "crc16-ccitt";
    public const string Crc16Ibm = "crc16-ibm";
    public const string Adler32 = "adler32";
    public const string Md5 = "md5";
    public const string Sha1 = "sha1";
    public const string Sha256 = "sha256";

    private static readonly HashSet<string> IntegerAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Crc32, Crc16Ccitt, Crc16Ibm, Adler32
    };

    private static readonly HashSet<string> HashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Md5, Sha1, Sha256
    };

    public static bool IsIntegerAlgorithm(string algorithm) => IntegerAlgorithms.Contains(algorithm);
    public static bool IsHashAlgorithm(string algorithm) => HashAlgorithms.Contains(algorithm);
    public static bool IsKnown(string algorithm) => IntegerAlgorithms.Contains(algorithm) || HashAlgorithms.Contains(algorithm);

    public static string DisplayName(string algorithm) => algorithm.ToUpperInvariant() switch
    {
        "CRC32" => "CRC-32",
        "CRC16-CCITT" => "CRC-16/CCITT",
        "CRC16-IBM" => "CRC-16/IBM",
        "ADLER32" => "Adler-32",
        "MD5" => "MD5",
        "SHA1" => "SHA-1",
        "SHA256" => "SHA-256",
        _ => algorithm,
    };
}
```

### VerifyChecksum 拡張（整数系）

既存の `VerifyChecksum` メソッドを拡張し、バイト範囲収集を `CollectChecksumData` ヘルパーに抽出。

```csharp
// ヘルパー: 対象フィールドのバイト範囲を収集
private static byte[] CollectChecksumData(
    ChecksumSpec spec, IReadOnlyList<DecodedNode> siblings, DecodeContext context)
{
    using var ms = new MemoryStream();
    foreach (var fieldName in spec.FieldNames)
    {
        var sibling = siblings.FirstOrDefault(n => n.Name == fieldName);
        if (sibling is null) continue;
        var slice = context.SliceOriginal((int)sibling.Offset, (int)sibling.Size);
        ms.Write(slice.Span);
    }
    return ms.ToArray();
}

// 整数系チェックサム検証（既存メソッドの拡張）
private static (bool valid, long? expected) VerifyChecksum(
    ChecksumSpec spec, long actualValue,
    IReadOnlyList<DecodedNode> siblings, DecodeContext context)
{
    var data = CollectChecksumData(spec, siblings, context);

    long computed = spec.Algorithm.ToLowerInvariant() switch
    {
        "crc32" => Crc32Calculator.Compute(data),
        "crc16-ccitt" => Crc16Calculator.ComputeCcitt(data),
        "crc16-ibm" => Crc16Calculator.ComputeIbm(data),
        "adler32" => Adler32Calculator.Compute(data),
        _ => actualValue,  // 未知: 常にvalid
    };

    var valid = actualValue == computed;
    return (valid, valid ? null : computed);
}
```

### VerifyHashChecksum 新規メソッド（ハッシュ系）

ハッシュ系はフィールド型が `bytes` で、結果を `ReadOnlyMemory<byte>` で比較する。

```csharp
// ハッシュ系チェックサム検証
private static (bool valid, string? expectedHex) VerifyHashChecksum(
    ChecksumSpec spec, ReadOnlyMemory<byte> actualBytes,
    IReadOnlyList<DecodedNode> siblings, DecodeContext context)
{
    var data = CollectChecksumData(spec, siblings, context);

    byte[] computed = spec.Algorithm.ToLowerInvariant() switch
    {
        "md5" => HashCalculator.ComputeMd5(data),
        "sha1" => HashCalculator.ComputeSha1(data),
        "sha256" => HashCalculator.ComputeSha256(data),
        _ => actualBytes.ToArray(),  // 未知: 常にvalid
    };

    var valid = actualBytes.Span.SequenceEqual(computed);
    return (valid, valid ? null : Convert.ToHexString(computed).ToLowerInvariant());
}
```

### DecodeBytesField 改修

`DecodeBytesField` に `siblings` パラメータを追加し、チェックサム指定がある場合にハッシュ検証を実施。

```csharp
// DecodeBytesField 内、バイト列読み取り後
if (field.Checksum is not null && siblings is not null
    && ChecksumAlgorithms.IsHashAlgorithm(field.Checksum.Algorithm))
{
    var (valid, expectedHex) = VerifyHashChecksum(
        field.Checksum, rawBytes, siblings, context);
    checksumValid = valid;
    checksumExpectedHex = expectedHex;
    checksumAlgorithm = field.Checksum.Algorithm;
}
```

### 出力フォーマッター変更

#### TreeOutputFormatter

ハードコード "CRC-32" を動的表示に変更:

```csharp
// 変更前:
sb.Append(C("✓ (CRC-32)", AnsiColors.Green));
// 変更後:
var algName = ChecksumAlgorithms.DisplayName(node.ChecksumAlgorithm ?? "crc32");
sb.Append(C($"✓ ({algName})", AnsiColors.Green));
```

DecodedBytes のチェックサム表示も追加:

```csharp
if (node is DecodedBytes bytesNode && bytesNode.ChecksumValid.HasValue)
{
    var algName = ChecksumAlgorithms.DisplayName(bytesNode.ChecksumAlgorithm ?? "");
    if (bytesNode.ChecksumValid.Value)
        sb.Append(C($"✓ ({algName})", AnsiColors.Green));
    else
        sb.Append(C($"✗ ({algName}, 期待値: {bytesNode.ChecksumExpectedHex})", AnsiColors.Red));
}
```

#### JsonOutputFormatter

`checksum_algorithm` フィールドを追加:

```csharp
if (node.ChecksumValid.HasValue)
{
    writer.WriteBoolean("checksum_valid", node.ChecksumValid.Value);
    if (node.ChecksumExpected.HasValue)
        writer.WriteString("checksum_expected", $"0x{node.ChecksumExpected:X}");
    if (node.ChecksumAlgorithm is not null)
        writer.WriteString("checksum_algorithm", node.ChecksumAlgorithm);
}
```

DecodedBytes のチェックサム情報も同様に出力。

#### HtmlOutputFormatter

DecodedInteger / DecodedBytes 両方でアルゴリズム名を表示。

### バリデーション追加

`FormatValidator` に以下のルールを追加:

| コード | 内容 | レベル |
|---|---|---|
| VAL113 | 未知のチェックサムアルゴリズム | Warning |
| VAL114 | 整数系アルゴリズムが非整数フィールドに指定されている | Error |
| VAL115 | ハッシュ系アルゴリズムが非bytesフィールドに指定されている | Error |

```csharp
// VAL113: 未知アルゴリズム
if (field.Checksum is not null && !ChecksumAlgorithms.IsKnown(field.Checksum.Algorithm))
    warnings.Add($"VAL113: フィールド '{field.Name}' のチェックサムアルゴリズム '{field.Checksum.Algorithm}' は未知です");

// VAL114: 整数アルゴリズム × 非整数フィールド
if (field.Checksum is not null && ChecksumAlgorithms.IsIntegerAlgorithm(field.Checksum.Algorithm)
    && field.Type == "bytes")
    errors.Add($"VAL114: ...");

// VAL115: ハッシュアルゴリズム × 非bytesフィールド
if (field.Checksum is not null && ChecksumAlgorithms.IsHashAlgorithm(field.Checksum.Algorithm)
    && field.Type != "bytes")
    errors.Add($"VAL115: ...");
```

### DSL 構文例

#### 整数系（CRC-16/Adler-32）
```yaml
- name: header_crc
  type: uint16
  checksum:
    algorithm: crc16-ccitt
    fields: [magic, version, flags]

- name: adler
  type: uint32
  checksum:
    algorithm: adler32
    fields: [uncompressed_data]
```

#### ハッシュ系（MD5/SHA）
```yaml
- name: content_hash
  type: bytes
  size: "16"
  checksum:
    algorithm: md5
    fields: [content]

- name: digest
  type: bytes
  size: "32"
  checksum:
    algorithm: sha256
    fields: [header, payload]
```

---

## 変更・追加ファイル一覧

### 新規ファイル

| ファイル | 内容 |
|---|---|
| `src/BinAnalyzer.Core/Models/ChecksumAlgorithms.cs` | アルゴリズム定数・分類判定・表示名 |
| `src/BinAnalyzer.Engine/Crc16Calculator.cs` | CRC-16 計算（CCITT / IBM バリアント） |
| `src/BinAnalyzer.Engine/Adler32Calculator.cs` | Adler-32 計算 |
| `src/BinAnalyzer.Engine/HashCalculator.cs` | MD5 / SHA-1 / SHA-256 計算（BCL ラッパー） |
| `tests/BinAnalyzer.Engine.Tests/Crc16Tests.cs` | CRC-16 のテストベクター検証 |
| `tests/BinAnalyzer.Engine.Tests/Adler32Tests.cs` | Adler-32 のテストベクター検証 |
| `tests/BinAnalyzer.Engine.Tests/HashCalculatorTests.cs` | MD5 / SHA-1 / SHA-256 のテストベクター検証 |

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Decoded/DecodedInteger.cs` | `ChecksumAlgorithm` プロパティ追加 |
| `src/BinAnalyzer.Core/Decoded/DecodedBytes.cs` | `ChecksumValid`, `ChecksumExpectedHex`, `ChecksumAlgorithm` プロパティ追加 |
| `src/BinAnalyzer.Core/Validation/FormatValidator.cs` | VAL113, VAL114, VAL115 バリデーション追加 |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `CollectChecksumData` 抽出、`VerifyChecksum` 拡張（整数系ディスパッチ）、`VerifyHashChecksum` 追加、`DecodeBytesField` に siblings パラメータ追加 |
| `src/BinAnalyzer.Output/TreeOutputFormatter.cs` | "CRC-32" ハードコード → `ChecksumAlgorithms.DisplayName()` 動的表示、DecodedBytes チェックサム表示追加 |
| `src/BinAnalyzer.Output/JsonOutputFormatter.cs` | `checksum_algorithm` フィールド追加、DecodedBytes チェックサム情報出力 |
| `src/BinAnalyzer.Output/HtmlOutputFormatter.cs` | アルゴリズム名動的表示、DecodedBytes チェックサム表示追加 |
| `schemas/bdef.schema.json` | algorithm enum に `crc16-ccitt`, `crc16-ibm`, `adler32`, `md5`, `sha1`, `sha256` 追加 |
| `tests/BinAnalyzer.Engine.Tests/ChecksumDecoderTests.cs` | 各アルゴリズムのデコード検証テスト追加 |
| `tests/BinAnalyzer.Dsl.Tests/ChecksumParsingTests.cs` | 新アルゴリズム名の DSL パーステスト追加 |
| `tests/BinAnalyzer.Integration.Tests/ChecksumIntegrationTests.cs` | 各アルゴリズムのツリー/JSON出力検証追加 |
| `docs/dsl-reference.md` | チェックサムセクション拡張（アルゴリズム一覧、整数系/ハッシュ系の構文例） |
| `docs/parser-design.md` | §2.4 FormatValidator バリデーション一覧に VAL113-115 追記 |

---

## テスト計画

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Crc16Tests | ComputeCcitt_KnownVector (0x29B1) | AC-2 |
| Crc16Tests | ComputeIbm_KnownVector (0xBB3D) | AC-2 |
| Crc16Tests | ComputeCcitt_EmptyData_ReturnsInitialValue | AC-2 |
| Crc16Tests | ComputeIbm_EmptyData_ReturnsZero | AC-2 |
| Adler32Tests | Compute_KnownVector ("Wikipedia" = 0x11E60398) | AC-2 |
| Adler32Tests | Compute_EmptyData_ReturnsOne | AC-2 |
| HashCalculatorTests | ComputeMd5_KnownVector | AC-2 |
| HashCalculatorTests | ComputeSha1_KnownVector | AC-2 |
| HashCalculatorTests | ComputeSha256_KnownVector | AC-2 |
| HashCalculatorTests | ComputeMd5_EmptyData | AC-2 |
| ChecksumDecoderTests | Decode_Crc16Ccitt_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Crc16Ibm_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Adler32_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Md5_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Sha1_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Sha256_ValidChecksum | AC-1, AC-3 |
| ChecksumDecoderTests | Decode_Crc32_StillWorks (後方互換性) | AC-4 |
| ChecksumParsingTests | Load_NewAlgorithmNames_ParsesCorrectly (Theory) | AC-1 |
| ChecksumIntegrationTests | Crc16Ccitt_TreeOutput_ShowsAlgorithmName | AC-3, AC-5 |
| ChecksumIntegrationTests | Md5_TreeOutput_ShowsAlgorithmName | AC-3, AC-5 |
| ChecksumIntegrationTests | Sha256_JsonOutput_IncludesAlgorithm | AC-3, AC-5 |

---

## 受入条件の対応付け

| 受入条件 | 実装箇所 | テスト |
|---|---|---|
| AC-1: 新アルゴリズムがDSLで指定可能 | `bdef.schema.json` enum 拡張、既存パーサーはそのまま通過 | ChecksumParsingTests |
| AC-2: 各アルゴリズムの計算結果が正しい | `Crc16Calculator`, `Adler32Calculator`, `HashCalculator` | Crc16Tests, Adler32Tests, HashCalculatorTests |
| AC-3: 検証結果がデコード結果に反映 | `VerifyChecksum` 拡張 + `VerifyHashChecksum` 新規、`DecodedInteger.ChecksumAlgorithm` + `DecodedBytes` 3プロパティ | ChecksumDecoderTests |
| AC-4: 既存CRC-32の後方互換性 | `VerifyChecksum` の crc32 分岐は既存ロジック維持 | ChecksumDecoderTests (Crc32_StillWorks) |
| AC-5: 単体テスト追加 | 上記テスト計画参照 | 全テストクラス |
| AC-6: JSON Schema に新アルゴリズム記載 | `bdef.schema.json` の enum 拡張 | スキーマバリデーション |

---

## 懸念事項

1. **CRC-16 のバリアント差異**: CCITT と IBM は多項式・初期値・反転設定がすべて異なる。実装時に混同しないよう、パラメータをテーブルで管理し、既知のテストベクター（"123456789"）で必ず検証する。

2. **`System.Security.Cryptography` の .NET 10 互換性**: MD5 / SHA1 / SHA256 クラスは .NET 10 でも利用可能であることを確認済み。`HashAlgorithm.HashData(ReadOnlySpan<byte>)` 静的メソッド（.NET 7+）の使用を推奨。

3. **`SetPaddingFlag` / `SetValidation` での新プロパティ伝播**: `DecodedInteger` に `ChecksumAlgorithm` を追加するため、`with` 式でコピーされるプロパティが漏れないよう注意。`DecodedBytes` も同様。既存の `SetPaddingFlag` や `SetValidation` ヘルパーで新プロパティが正しく伝播されるか確認が必要。

4. **ゴールデンファイルの再生成**: PNG の JSON 出力ゴールデンファイルに `checksum_algorithm: "crc32"` が追加されるため、既存のゴールデンファイルを再生成する必要がある。

5. **DecodeBytesField の siblings パラメータ追加**: `DecodeIntegerField` と同様に `siblings` を受け取る必要がある。呼び出し元の `DecodeStruct` ループで構築中の `children` を渡す形に変更。既存の `DecodeBytesField` 呼び出しに影響があるため、シグネチャ変更を慎重に行う。
