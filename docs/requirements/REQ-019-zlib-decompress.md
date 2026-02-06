# REQ-019: zlib/deflate 圧縮データの展開とネスト解析

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

PNGのIDATチャンクやZIPのファイルデータは圧縮されており、現在は生バイト列として表示されるのみ。圧縮データを展開し、展開後のデータに対して別のフォーマット定義を適用できれば、解析の深度が大幅に向上する。

.NET BCLの `System.IO.Compression.DeflateStream` / `ZLibStream` で実現でき、外部ライブラリ不要。

対象フォーマット:
- PNG IDAT チャンク（zlib圧縮）
- ZIP ファイルデータ（deflate圧縮）
- HTTP gzip レスポンス
- PDFストリーム

## 機能要件

### 追加する機能

- [ ] `FieldType.Zlib` 型の追加（zlib圧縮データの展開）
  - zlibヘッダー（2バイト）+ deflateデータ + チェックサム（4バイト）
  - 展開後のバイト列を `DecodedBytes` として保持
- [ ] `FieldType.Deflate` 型の追加（raw deflateデータの展開）
  - zlibヘッダーなしのdeflateストリーム
- [ ] 展開データに対するネスト解析（オプション）
  - `struct_ref` と組み合わせて展開後データをさらにデコード
  ```yaml
  - name: compressed_data
    type: zlib
    size: "{compressed_length}"
    struct_ref: uncompressed_format
  ```
- [ ] 展開後サイズの表示（圧縮率の参考情報）

### 変更する既存機能

- [ ] FieldType enum: Zlib, Deflate の追加
- [ ] BinaryDecoder: 圧縮データ展開 + オプションのネスト解析
- [ ] YamlToIrMapper: `zlib`, `deflate` 型名のマッピング追加
- [ ] TreeOutputFormatter: 展開データの表示（展開後サイズ、ネスト構造体）

### 変更しないもの（スコープ外）

- gzip形式（gzipヘッダー+deflate+フッター） — 将来課題
- brotli, lz4, zstd 等の他の圧縮アルゴリズム — 将来課題
- 圧縮データの再圧縮（書き出し）

## 受入条件

1. [ ] `zlib` 型フィールドがzlib圧縮データを正しく展開できること
2. [ ] `deflate` 型フィールドがraw deflateデータを正しく展開できること
3. [ ] 展開後のデータサイズがデコード結果に含まれること
4. [ ] `struct_ref` 指定時、展開後データに対してネスト解析が行われること
5. [ ] `struct_ref` 未指定時、展開後データがバイト列として表示されること
6. [ ] 不正な圧縮データに対して適切なエラーメッセージが表示されること
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldType enum に Zlib, Deflate 追加、DecodedCompressed モデル追加 |
| BinAnalyzer.Dsl | YamlToIrMapper に型マッピング追加 |
| BinAnalyzer.Engine | BinaryDecoder に展開 + ネスト解析ロジック追加 |
| BinAnalyzer.Output | TreeOutputFormatter に展開データ表示追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型一覧に zlib, deflate 追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

`System.IO.Compression.ZLibStream` / `DeflateStream` を使用し、外部ライブラリ不要で実装する。展開後データは `DecodedBytes` として保持するか、`struct_ref` 指定時は展開後データに対して `BinaryDecoder` を再帰呼び出しする。

### モデル変更

**FieldType enum に追加:**
```csharp
Zlib,      // zlib圧縮（ヘッダー付き）
Deflate,   // raw deflate
```

**DecodedCompressed（新規）:**
```csharp
// src/BinAnalyzer.Core/Decoded/DecodedCompressed.cs
public sealed class DecodedCompressed : DecodedNode
{
    public required int CompressedSize { get; init; }
    public required int DecompressedSize { get; init; }
    public required string Algorithm { get; init; }   // "zlib" or "deflate"
    public DecodedStruct? DecodedContent { get; init; } // struct_ref指定時
    public ReadOnlyMemory<byte>? RawDecompressed { get; init; } // struct_ref未指定時
}
```

### BinaryDecoder の変更

`DecodeSingleField` の switch に追加:
```csharp
FieldType.Zlib or FieldType.Deflate => DecodeCompressedField(field, format, context),
```

新規メソッド:
```csharp
private DecodedCompressed DecodeCompressedField(
    FieldDefinition field, FormatDefinition format, DecodeContext context)
{
    var offset = context.Position;
    var compressedSize = ResolveSize(field, context);
    var compressedBytes = context.ReadBytes(compressedSize);

    // 展開
    using var input = new MemoryStream(compressedBytes.ToArray());
    using var decompressor = field.Type == FieldType.Zlib
        ? new ZLibStream(input, CompressionMode.Decompress)
        : new DeflateStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();
    decompressor.CopyTo(output);
    var decompressed = output.ToArray();

    // ネスト解析
    DecodedStruct? decodedContent = null;
    ReadOnlyMemory<byte>? rawDecompressed = decompressed;
    if (field.StructRef is not null && format.Structs.TryGetValue(field.StructRef, out var structDef))
    {
        var innerContext = new DecodeContext(decompressed, format.Endianness);
        decodedContent = DecodeStruct(structDef, format, innerContext, field.Name);
        rawDecompressed = null;
    }

    return new DecodedCompressed { ... };
}
```

### 出力フォーマッターの変更

**TreeOutputFormatter:**
- `DecodedCompressed` の表示: `field_name [zlib] (100 bytes → 256 bytes)`
- `DecodedContent` がある場合は子ノードとして展開表示
- `RawDecompressed` の場合はバイト列プレビュー

### YamlToIrMapper の変更

```
"zlib"    → FieldType.Zlib
"deflate" → FieldType.Deflate
```

### FormatValidator の変更

`Zlib` / `Deflate` 型はサイズ指定が必須（VAL007に追加）。

### インタフェース変更

なし。

### 代替案

**案A: 展開を別コマンドとして分離する（不採用）**
- 理由: フォーマット定義の中で圧縮と構造を統一的に記述できることが本機能の価値。

### 懸念事項

1. **メモリ使用量**: 大きな圧縮データの展開はメモリを消費する。初期実装では展開サイズの上限チェック等は行わない（将来課題）。

2. **DecodeContextの再作成**: ネスト解析では新しい `DecodeContext` を作成する。変数バインディングは親コンテキストとは共有されない。これは意図的な設計（展開後データは独立したバイナリ空間）。

3. **BinaryDecoderのステートフル性**: 現在の `BinaryDecoder` は `_pathStack` をインスタンスフィールドとして持つため、ネスト解析で同一インスタンスを再利用する場合、パスの追跡が自然に連結される。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
