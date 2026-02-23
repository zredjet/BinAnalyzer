# REQ-109: 追加圧縮形式サポート

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| フェーズ | D（応用） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-17 |

## 背景・動機

現在の圧縮データ対応は `zlib` と `deflate` の2形式のみ。実際のバイナリフォーマットでは gzip, bzip2, lzma/xz, zstd など多様な圧縮形式が使われている。

### ユースケース

- gzip メンバの展開（HTTP レスポンスボディ、.gz ファイル）
- bzip2 圧縮データの展開（tar.bz2 内部）
- LZMA/XZ 圧縮セクション（ELF の debug_info 等）
- Zstandard 圧縮データ（モダンなフォーマットで増加中）
- LZ4 圧縮データ（高速展開が求められるフォーマット、LZ4 Frame Format）

## 機能要件

### 追加する機能

- [ ] `type` に `gzip`, `bzip2`, `lzma`, `zstd`（エイリアス: `zstandard`）, `lz4` を追加（計5形式）
- [ ] 展開後のデータに対してネスト解析（`struct` 指定時）を既存の仕組みで対応
- [ ] DecodedCompressed の Algorithm フィールドに新形式名を格納
- [ ] `BinAnalyzer.Compression` プロジェクトを新設し、外部 NuGet パッケージ（SharpCompress, ZstdSharp.Port）による展開処理をラップ
- [ ] gzip は BCL の `GZipStream` を使用し Engine 内で直接処理（zlib/deflate と対称）

### 変更する既存機能

- [ ] `BinaryDecoder.DecodeCompressedField` の圧縮データデコード処理にアルゴリズムディスパッチを追加
- [ ] `BinaryDecoder.DecodeSingleField` のディスパッチに5型を追加
- [ ] `FormatValidator.ValidateSizeSpec` (VAL007) の型リストに5型を追加
- [ ] `FormatValidator.ValidateTypeRefCombination` (VAL106) で圧縮型を除外（既存 zlib/deflate + 新5型）
- [ ] 出力フォーマッター（Tree/Json/Html）は変更不要（既存の `node.Algorithm` 動的表示がそのまま対応）

### 変更しないもの（スコープ外）

- 圧縮（エンコード）処理
- ストリーミング展開
- パスワード付き圧縮（暗号化）

## 受入条件

1. [ ] `gzip`, `bzip2`, `lzma`, `zstd`, `lz4` の各型でデータが正しく展開されること
2. [ ] 展開後のネスト解析が動作すること
3. [ ] DecodedCompressed に正しいアルゴリズム名と展開サイズが記録されること
4. [ ] 既存の zlib/deflate に影響がないこと（後方互換性）
5. [ ] 各形式の単体テストが追加されていること
6. [ ] JSON Schema に新型が記載されていること
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldType` enum に Gzip/Bzip2/Lzma/Zstd/Lz4 の5値追加、`FormatValidator` の VAL007 対象型リスト拡張・VAL106 圧縮型除外 |
| BinAnalyzer.Compression | **新設**: `Decompressor` 静的クラス（bzip2/lzma/zstd/lz4 展開）、NuGet: SharpCompress + ZstdSharp.Port |
| BinAnalyzer.Engine | `BinaryDecoder.DecodeCompressedField` のディスパッチ拡張、`BinAnalyzer.Compression` への ProjectReference 追加 |
| BinAnalyzer.Dsl | `YamlToIrMapper.ParseFieldType` に5分岐追加 |
| BinAnalyzer.Output | 変更なし（既存の DecodedCompressed 表示が汎用的: `node.Algorithm` を動的使用済み） |
| BinAnalyzer.Cli | 変更なし |
| schemas | `bdef.schema.json` の type enum 拡張 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型一覧・サイズ指定必要型リスト・圧縮データセクション拡張
- [ ] docs/parser-design.md — フィールド型解決テーブルに5型追記
- [x] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — プロジェクト構成に BinAnalyzer.Compression の説明追加
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

#### プロジェクト分離

bzip2, lzma, zstd, lz4 の展開には外部 NuGet パッケージが必要。Engine は BCL-only 原則を維持するため、外部依存を含む展開処理を `BinAnalyzer.Compression` プロジェクトに分離する。Engine は Compression への ProjectReference を追加し、展開処理を委譲する。

gzip は BCL の `System.IO.Compression.GZipStream` が利用可能なため、zlib/deflate と同様に Engine 内で直接処理する。

#### NuGet パッケージ選定

| アルゴリズム | 依存先 | 理由 |
|---|---|---|
| gzip | BCL (`GZipStream`) | .NET 標準ライブラリで対応。zlib/deflate と対称 |
| bzip2 | SharpCompress | BZip2Stream を提供。.NET で広く使われている |
| lzma | SharpCompress | LzmaStream を提供。raw LZMA 形式に対応 |
| lz4 | K4os.Compression.LZ4.Streams | LZ4 Frame Format 対応。SharpCompress は LZ4 非対応 |
| zstd | ZstdSharp.Port | マネージド実装。ネイティブライブラリ不要 |

### アルゴリズム一覧

既存2形式 + 新規5形式の全7アルゴリズム:

| DSL型名 | FieldType enum | ストリームクラス | 依存先 | フォーマット仕様 |
|---|---|---|---|---|
| `zlib` | Zlib | `ZLibStream` | BCL | RFC 1950 |
| `deflate` | Deflate | `DeflateStream` | BCL | RFC 1951 |
| `gzip` | Gzip | `GZipStream` | BCL | RFC 1952 |
| `bzip2` | Bzip2 | `BZip2Stream` | SharpCompress | bzip2 spec |
| `lzma` | Lzma | `LzmaStream` | SharpCompress | LZMA SDK (raw) |
| `zstd` / `zstandard` | Zstd | `DecompressionStream` | ZstdSharp.Port | RFC 8878 |
| `lz4` | Lz4 | `LZ4Stream` | K4os.Compression.LZ4.Streams | LZ4 Frame Format |

### プロジェクト依存グラフ変更

変更前:

```
Core ← Dsl
Core ← Engine
Core ← Output
```

変更後:

```
Core ← Dsl
Core ← Compression ← Engine
Core ← Output
```

Compression は Core のみに依存し、Engine が Compression に依存する。Output は Compression に依存しない。

### モデル変更

#### FieldType enum

`Deflate` の直後に5値を追加:

```csharp
public enum FieldType
{
    // ... 既存 ...
    Zlib,
    Deflate,
    Gzip,      // 追加
    Bzip2,     // 追加
    Lzma,      // 追加
    Zstd,      // 追加
    Lz4,       // 追加
    Virtual,
    ULeb128,
    SLeb128,
    Vlq,
}
```

#### DecodedCompressed

変更不要。既存の `Algorithm` プロパティ（`string` 型）が汎用的で、新形式にそのまま対応する。

```csharp
public sealed class DecodedCompressed : DecodedNode
{
    public required int CompressedSize { get; init; }
    public required int DecompressedSize { get; init; }
    public required string Algorithm { get; init; }  // "gzip", "bzip2" 等をそのまま格納
    public DecodedStruct? DecodedContent { get; init; }
    public ReadOnlyMemory<byte>? RawDecompressed { get; init; }
}
```

### BinAnalyzer.Compression 設計

#### csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\BinAnalyzer.Core\BinAnalyzer.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="SharpCompress" Version="0.38.*" />
    <PackageReference Include="ZstdSharp.Port" Version="0.8.*" />
    <PackageReference Include="K4os.Compression.LZ4.Streams" Version="1.3.*" />
  </ItemGroup>
</Project>
```

#### Decompressor 静的クラス

```csharp
namespace BinAnalyzer.Compression;

public static class Decompressor
{
    /// <summary>
    /// 指定されたアルゴリズムで圧縮データを展開する。
    /// </summary>
    /// <param name="compressedData">圧縮データ</param>
    /// <param name="algorithm">アルゴリズム名 ("bzip2", "lzma", "zstd", "lz4")</param>
    /// <returns>展開後のバイト配列</returns>
    public static byte[] Decompress(ReadOnlyMemory<byte> compressedData, string algorithm)
    {
        return algorithm.ToLowerInvariant() switch
        {
            "bzip2" => DecompressBzip2(compressedData),
            "lzma" => DecompressLzma(compressedData),
            "zstd" or "zstandard" => DecompressZstd(compressedData),
            "lz4" => DecompressLz4(compressedData),
            _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm)),
        };
    }

    private static byte[] DecompressBzip2(ReadOnlyMemory<byte> data) { /* SharpCompress BZip2Stream */ }
    private static byte[] DecompressLzma(ReadOnlyMemory<byte> data) { /* SharpCompress LzmaStream */ }
    private static byte[] DecompressZstd(ReadOnlyMemory<byte> data) { /* ZstdSharp DecompressionStream */ }
    private static byte[] DecompressLz4(ReadOnlyMemory<byte> data) { /* SharpCompress LZ4Stream */ }
}
```

### BinaryDecoder.DecodeCompressedField 拡張

#### DecodeSingleField のディスパッチ

```csharp
// 変更前:
FieldType.Zlib or FieldType.Deflate => DecodeCompressedField(field, format, context),

// 変更後:
FieldType.Zlib or FieldType.Deflate or FieldType.Gzip
    or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4
    => DecodeCompressedField(field, format, context),
```

#### algorithm 文字列決定

```csharp
var algorithm = field.Type switch
{
    FieldType.Zlib => "zlib",
    FieldType.Deflate => "deflate",
    FieldType.Gzip => "gzip",
    FieldType.Bzip2 => "bzip2",
    FieldType.Lzma => "lzma",
    FieldType.Zstd => "zstd",
    FieldType.Lz4 => "lz4",
    _ => throw new InvalidOperationException($"Not a compression type: {field.Type}"),
};
```

#### 展開処理の分岐

BCL 系（zlib/deflate/gzip）と Compression 系（bzip2/lzma/zstd/lz4）で展開処理を分岐する。`DecompressStream` ヘルパーを抽出し、BCL 系を統一的に処理する。

```csharp
byte[] decompressed;
try
{
    if (field.Type is FieldType.Zlib or FieldType.Deflate or FieldType.Gzip)
    {
        decompressed = DecompressStream(compressedBytes, field.Type);
    }
    else
    {
        decompressed = BinAnalyzer.Compression.Decompressor.Decompress(compressedBytes, algorithm);
    }
}
catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
{
    throw new DecodeException(
        $"Failed to decompress {algorithm} data: {ex.Message}",
        offset, CurrentPath, algorithm, inner: ex);
}
```

`DecompressStream` ヘルパー:

```csharp
private static byte[] DecompressStream(ReadOnlyMemory<byte> compressedBytes, FieldType type)
{
    using var input = new MemoryStream(compressedBytes.ToArray());
    using Stream decompressor = type switch
    {
        FieldType.Zlib => new ZLibStream(input, CompressionMode.Decompress),
        FieldType.Deflate => new DeflateStream(input, CompressionMode.Decompress),
        FieldType.Gzip => new GZipStream(input, CompressionMode.Decompress),
        _ => throw new InvalidOperationException($"Not a BCL compression type: {type}"),
    };
    using var output = new MemoryStream();
    decompressor.CopyTo(output);
    return output.ToArray();
}
```

### YamlToIrMapper 変更

`ParseFieldType` に5分岐を追加:

```csharp
// 既存:
"zlib" => FieldType.Zlib,
"deflate" => FieldType.Deflate,

// 追加:
"gzip" => FieldType.Gzip,
"bzip2" => FieldType.Bzip2,
"lzma" => FieldType.Lzma,
"zstd" or "zstandard" => FieldType.Zstd,
"lz4" => FieldType.Lz4,
```

### FormatValidator 変更

#### ValidateSizeSpec (VAL007)

サイズ指定が必要な型リストに5型を追加:

```csharp
// 変更前:
if (field.Type is FieldType.Bytes or FieldType.Ascii or FieldType.Utf8
        or FieldType.Utf16Le or FieldType.Utf16Be or FieldType.ShiftJis or FieldType.Latin1
        or FieldType.Bitfield or FieldType.Zlib or FieldType.Deflate)

// 変更後:
if (field.Type is FieldType.Bytes or FieldType.Ascii or FieldType.Utf8
        or FieldType.Utf16Le or FieldType.Utf16Be or FieldType.ShiftJis or FieldType.Latin1
        or FieldType.Bitfield or FieldType.Zlib or FieldType.Deflate
        or FieldType.Gzip or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4)
```

#### ValidateTypeRefCombination (VAL106)

圧縮型 + StructRef の組み合わせは正当なもの（展開後のデータをネスト解析する）。圧縮型を除外条件に追加する。これは既存の Zlib/Deflate でも発生していた警告の修正を含む。

```csharp
// 変更前:
if (field.StructRef is not null && field.Type is not FieldType.Struct and not FieldType.Switch)

// 変更後:
if (field.StructRef is not null
    && field.Type is not FieldType.Struct and not FieldType.Switch
    and not FieldType.Zlib and not FieldType.Deflate
    and not FieldType.Gzip and not FieldType.Bzip2 and not FieldType.Lzma
    and not FieldType.Zstd and not FieldType.Lz4)
```

### 出力フォーマッター

変更不要であることを明記する。

- **TreeOutputFormatter**: `node.Algorithm` を動的に使用済み。新アルゴリズム名がそのまま表示される。
- **JsonOutputFormatter**: `node.Algorithm` を `"algorithm"` フィールドとして出力済み。
- **HtmlOutputFormatter**: `node.Algorithm` を動的に使用済み。
- **DiffEngine**: `DecodedCompressed.Algorithm` の比較は既存のロジックでカバー。

### DSL 構文例

#### gzip（struct 付き）

```yaml
- name: compressed_data
  type: gzip
  size: "{compressed_size}"
  struct: inner_format
```

#### bzip2（raw 展開）

```yaml
- name: bz2_payload
  type: bzip2
  size: "{payload_size}"
```

#### lzma（struct 付き）

```yaml
- name: lzma_section
  type: lzma
  size: "{section_size}"
  struct: debug_info
```

#### zstd（エイリアス使用）

```yaml
- name: zstd_block
  type: zstandard
  size: "{block_size}"
  struct: block_data
```

#### lz4（raw 展開）

```yaml
- name: lz4_frame
  type: lz4
  size: remaining
```

### 代替案

| 案 | 概要 | 採否 | 理由 |
|---|---|---|---|
| 案A | IDecompressor インタフェース + DI | 不採用 | 複雑すぎる。現状の形式数では静的ディスパッチで十分 |
| 案B | Engine に NuGet を直接追加 | 不採用 | BCL-only 原則に違反。Engine の依存関係が増大する |
| **案C（採用）** | BinAnalyzer.Compression 新設 + gzip は BCL | **採用** | 外部依存を分離しつつ、BCL で対応可能な形式は Engine 内で処理 |

---

## 変更・追加ファイル一覧

### 新規ファイル

| ファイル | 内容 |
|---|---|
| `src/BinAnalyzer.Compression/BinAnalyzer.Compression.csproj` | プロジェクトファイル（SharpCompress + ZstdSharp.Port の PackageReference、Core への ProjectReference） |
| `src/BinAnalyzer.Compression/Decompressor.cs` | 展開処理の静的クラス（bzip2/lzma/zstd/lz4） |
| `tests/BinAnalyzer.Compression.Tests/BinAnalyzer.Compression.Tests.csproj` | テストプロジェクトファイル |
| `tests/BinAnalyzer.Compression.Tests/DecompressorTests.cs` | Decompressor の単体テスト（既知データ展開・不正データ例外） |

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `BinAnalyzer.slnx` | BinAnalyzer.Compression プロジェクト + テストプロジェクト追加 |
| `src/BinAnalyzer.Core/Models/FieldType.cs` | Gzip, Bzip2, Lzma, Zstd, Lz4 の5値追加（Deflate の直後） |
| `src/BinAnalyzer.Core/Validation/FormatValidator.cs` | VAL007 型リスト拡張、VAL106 圧縮型除外 |
| `src/BinAnalyzer.Engine/BinAnalyzer.Engine.csproj` | BinAnalyzer.Compression への ProjectReference 追加 |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | DecodeSingleField ディスパッチ拡張、DecodeCompressedField の algorithm 決定・展開処理分岐、DecompressStream ヘルパー抽出 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | ParseFieldType に gzip/bzip2/lzma/zstd(zstandard)/lz4 の5分岐追加 |
| `schemas/bdef.schema.json` | type enum に `"gzip"`, `"bzip2"`, `"lzma"`, `"zstd"`, `"zstandard"`, `"lz4"` 追加 |
| `docs/dsl-reference.md` | フィールド型テーブル・サイズ指定必要型リスト・圧縮データセクション拡張 |
| `docs/parser-design.md` | フィールド型解決テーブルに5型追記 |
| `CLAUDE.md` | プロジェクト構成に BinAnalyzer.Compression の説明追加 |
| `tests/BinAnalyzer.Dsl.Tests/NewTypeParsingTests.cs` | 5型（6型名: gzip/bzip2/lzma/zstd/zstandard/lz4）のパーステスト |
| `tests/BinAnalyzer.Engine.Tests/CompressedFieldTests.cs` | 新圧縮型のデコーダーテスト（5形式のデコード・StructRef ネスト・後方互換・エラー処理） |

---

## テスト計画

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DecompressorTests | DecompressBzip2_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressLzma_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressZstd_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressLz4_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressBzip2_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressLzma_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressZstd_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressLz4_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | Decompress_UnsupportedAlgorithm_ThrowsArgumentException | AC-5 |
| DecompressorTests | DecompressZstandard_Alias_ReturnsExpected | AC-1 |
| CompressedFieldTests | Decode_Gzip_DecompressesCorrectly | AC-1, AC-3 |
| CompressedFieldTests | Decode_Bzip2_DecompressesCorrectly | AC-1, AC-3 |
| CompressedFieldTests | Decode_Lzma_DecompressesCorrectly | AC-1, AC-3 |
| CompressedFieldTests | Decode_Zstd_DecompressesCorrectly | AC-1, AC-3 |
| CompressedFieldTests | Decode_Lz4_DecompressesCorrectly | AC-1, AC-3 |
| CompressedFieldTests | Decode_Gzip_WithStructRef_NestedDecode | AC-2 |
| CompressedFieldTests | Decode_Bzip2_WithStructRef_NestedDecode | AC-2 |
| CompressedFieldTests | Decode_Zstd_WithStructRef_NestedDecode | AC-2 |
| CompressedFieldTests | Decode_Zlib_StillWorks | AC-4 |
| CompressedFieldTests | Decode_Deflate_StillWorks | AC-4 |
| CompressedFieldTests | Decode_Gzip_InvalidData_ThrowsDecodeException | AC-5 |
| CompressedFieldTests | Decode_Bzip2_InvalidData_ThrowsDecodeException | AC-5 |
| NewTypeParsingTests | ParseFieldType_Theory (gzip/bzip2/lzma/zstd/zstandard/lz4) | AC-1 |
| SchemaValidationTests | Schema_AcceptsNewCompressionTypes | AC-6 |

---

## 受入条件の対応付け

| 受入条件 | 実装箇所 | テスト |
|---|---|---|
| AC-1: 5形式でデータが正しく展開 | `Decompressor.Decompress` (bzip2/lzma/zstd/lz4)、`BinaryDecoder.DecompressStream` (gzip)、`DecodeCompressedField` ディスパッチ | DecompressorTests (既知データ展開)、CompressedFieldTests (各型デコード) |
| AC-2: 展開後のネスト解析 | `DecodeCompressedField` の StructRef 処理（既存ロジック再利用） | CompressedFieldTests (WithStructRef) |
| AC-3: DecodedCompressed に正しいアルゴリズム名・展開サイズ | `DecodeCompressedField` の algorithm 決定 switch | CompressedFieldTests (各型デコード、Algorithm/DecompressedSize 検証) |
| AC-4: 既存 zlib/deflate の後方互換性 | `DecompressStream` ヘルパー抽出時に既存ロジック維持 | CompressedFieldTests (Zlib_StillWorks, Deflate_StillWorks) |
| AC-5: 各形式の単体テスト | 上記テスト計画参照 | 全テストクラス |
| AC-6: JSON Schema に新型記載 | `bdef.schema.json` の type enum 拡張 | SchemaValidationTests |
| AC-7: 既存テスト全通過 | 全変更箇所での後方互換性維持、VAL106 修正 | `dotnet test` 全通過 |

---

## 懸念事項

1. **SharpCompress / ZstdSharp の .NET 10 互換性**: プロジェクトは .NET 10 をターゲットとしている。SharpCompress は netstandard2.0/2.1 対応で .NET 10 互換が期待される。ZstdSharp.Port もマネージド実装のため問題ないと見込むが、実装時に確認が必要。

2. **LZMA ストリーム形式のバリアント**: LZMA には raw LZMA（LZMA SDK 形式）と XZ コンテナ形式がある。本要件では raw LZMA を対象とする。XZ 対応は将来の拡張として検討可能。

3. **LZ4 フレーム形式 vs ブロック形式**: LZ4 には LZ4 Frame Format（ヘッダ付き）と LZ4 Block Format（生ブロック）がある。本要件では LZ4 Frame Format を対象とする。既存の `Lz4TestDataGenerator` の資産を活用する。

4. **VAL106 の既存 Zlib/Deflate への影響**: 圧縮型 + StructRef は正当な組み合わせだが、現在の VAL106 は Zlib/Deflate でも警告を出す（既存バグ）。この修正により既存の警告が消失するが、これは意図した動作改善である。

5. **Engine csproj への ProjectReference 追加**: Engine は従来 Core のみに依存していた。Compression への依存追加により、Engine をビルドする際に SharpCompress / ZstdSharp も間接的にリストアされる。CI/CD パイプラインへの影響を確認する。

6. **CLAUDE.md のプロジェクト構成説明の更新**: BinAnalyzer.Compression プロジェクトの追加に伴い、CLAUDE.md のプロジェクト構成セクションに説明を追加する必要がある。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- **LZ4 NuGet 変更**: SharpCompress は LZ4 を独立ストリームとして公開しておらず、`K4os.Compression.LZ4.Streams` に変更
- **catch 句拡張**: SharpCompress が `IOException` を投げるケースがあるため、DecodeCompressedField の catch に `IOException` を追加
- **TestHelpers**: `Decompressor.TestHelpers` ネストクラスとして圧縮ヘルパーを追加（テストデータ生成用）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DecompressorTests | DecompressBzip2_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressLzma_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressZstd_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressLz4_KnownData_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressZstandard_Alias_ReturnsExpected | AC-1 |
| DecompressorTests | DecompressBzip2_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressLzma_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressZstd_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | DecompressLz4_InvalidData_ThrowsException | AC-5 |
| DecompressorTests | Decompress_UnsupportedAlgorithm_ThrowsArgumentException | AC-5 |
| CompressedFieldTests | Decode_Gzip_DecompressesData | AC-1, AC-3 |
| CompressedFieldTests | Decode_Bzip2_DecompressesData | AC-1, AC-3 |
| CompressedFieldTests | Decode_Zstd_DecompressesData | AC-1, AC-3 |
| CompressedFieldTests | Decode_Lz4_DecompressesData | AC-1, AC-3 |
| CompressedFieldTests | Decode_Lzma_DecompressesData | AC-1, AC-3 |
| CompressedFieldTests | Decode_Gzip_WithStructRef_DecodesContent | AC-2 |
| CompressedFieldTests | Decode_Bzip2_WithStructRef_DecodesContent | AC-2 |
| CompressedFieldTests | Decode_Zstd_WithStructRef_DecodesContent | AC-2 |
| CompressedFieldTests | Decode_Gzip_InvalidData_ThrowsDecodeException | AC-5 |
| CompressedFieldTests | Decode_Bzip2_InvalidData_ThrowsDecodeException | AC-5 |
| NewTypeParsingTests | Load_CompressedType_ParsesCorrectly (gzip/bzip2/lzma/zstd/zstandard/lz4) | AC-1 |

### 気づき・今後の課題

- SharpCompress の LZMA エンコーダーは Properties を別途取得する必要がある（LzmaStream コンストラクタでストリームに書かれない）
- SharpCompress は LZ4 の独立ストリームを公開していない。K4os.Compression.LZ4 が LZ4 Frame Format に対応
- BinaryDecoder の catch 句に IOException を追加する必要があった（SharpCompress の BZip2 が IOException を投げる）
