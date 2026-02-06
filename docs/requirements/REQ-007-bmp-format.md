# REQ-007: BMPフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-002（ビットフィールド型があると圧縮方式等の表現が豊かになる） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

BMPはWindowsの標準画像フォーマットであり、構造が比較的単純でありながら、可変長ヘッダーやカラーテーブルなど解析ツールの検証に適した要素を含む。PNGに次ぐ2番目のフォーマット定義として、DSLの表現力を実証するのに最適。

BMP構造:
- ファイルヘッダー（14バイト固定）
- DIBヘッダー（可変長: BITMAPINFOHEADER=40バイト等）
- カラーテーブル（オプション）
- ピクセルデータ

## 機能要件

### 追加する機能

- [ ] `formats/bmp.bdef.yaml` の新規作成
- [ ] 対応するBMP構造:
  - ファイルヘッダー（マジックナンバー "BM"、ファイルサイズ、予約領域、ピクセルデータオフセット）
  - BITMAPINFOHEADER（幅、高さ、色深度、圧縮方式、画像サイズ等）
  - カラーテーブル（色深度≤8の場合）
  - ピクセルデータ（raw bytesとして表示）
- [ ] BMP用のenum定義:
  - 圧縮方式（BI_RGB, BI_RLE8, BI_RLE4, BI_BITFIELDS等）
  - カラースペース（LCS_CALIBRATED_RGB等、V4/V5ヘッダーの場合）
- [ ] テストデータ生成: `BmpTestDataGenerator` の作成（最小限のBMPバイナリ）

### 変更する既存機能

- なし（新しいフォーマット定義ファイルの追加のみ）

### 変更しないもの（スコープ外）

- BITMAPV4HEADER / BITMAPV5HEADER への対応（基本のBITMAPINFOHEADERのみ）
- ピクセルデータの画像としての解釈
- RLE圧縮データの展開

## 受入条件

1. [ ] `formats/bmp.bdef.yaml` が `YamlFormatLoader` で正しく読み込めること
2. [ ] バリデーター（REQ-001実装済みの場合）でエラーが0件であること
3. [ ] 最小BMPバイナリ（1x1ピクセル、24bit）がデコードできること
4. [ ] ファイルヘッダーのマジックナンバー "BM" が検証されること（expected）
5. [ ] DIBヘッダーの各フィールド（幅、高さ、色深度、圧縮方式等）が正しくデコードされること
6. [ ] 圧縮方式のenum表示が正しいこと（0 = "BI_RGB" 等）
7. [ ] ツリー出力が構造的に正しいこと
8. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| BinAnalyzer.Integration.Tests | `BmpTestDataGenerator`, BMPパーステスト追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 対応フォーマット一覧にBMPを追記

---

## 設計メモ

### 設計方針

フォーマット定義ファイル（`formats/bmp.bdef.yaml`）の追加のみ。エンジンやモデルへの変更は不要（既存機能で表現可能）。

エンディアン: `endianness: little`（BMPはリトルエンディアン）

### DSL構造設計

```yaml
name: BMP
endianness: little
root: bmp

enums:
  compression:
    - value: 0
      label: BI_RGB
    - value: 1
      label: BI_RLE8
    - value: 2
      label: BI_RLE4
    - value: 3
      label: BI_BITFIELDS

structs:
  bmp:
    - name: file_header
      type: struct
      struct: bmp_file_header
    - name: dib_header
      type: struct
      struct: bitmapinfoheader
    - name: pixel_data
      type: bytes
      size: remaining

  bmp_file_header:
    - name: signature
      type: ascii
      size: "2"
      expected: [0x42, 0x4D]   # "BM"
    - name: file_size
      type: uint32
    - name: reserved1
      type: uint16
    - name: reserved2
      type: uint16
    - name: pixel_offset
      type: uint32

  bitmapinfoheader:
    - name: header_size
      type: uint32
    - name: width
      type: int32
    - name: height
      type: int32
    - name: planes
      type: uint16
    - name: bits_per_pixel
      type: uint16
    - name: compression
      type: uint32
      enum: compression
    - name: image_size
      type: uint32
    - name: x_pixels_per_meter
      type: int32
    - name: y_pixels_per_meter
      type: int32
    - name: colors_used
      type: uint32
    - name: colors_important
      type: uint32
```

### テストデータ設計

`BmpTestDataGenerator.CreateMinimalBmp()`:
- 1x1ピクセル、24bit、BI_RGB圧縮なし
- ファイルヘッダー（14バイト）+ DIBヘッダー（40バイト）+ ピクセルデータ（4バイト: RGB + パディング1バイト）
- 合計58バイト

### 代替案

なし（フォーマット定義の追加のみのため設計判断は最小限）。

### 懸念事項

1. **カラーテーブルの扱い**: bits_per_pixel ≤ 8 の場合、DIBヘッダーの後にカラーテーブルが存在する。条件付きフィールド（REQ-006）があれば `if: "{bits_per_pixel <= 8}"` で対応可能。REQ-006未実装の場合は、24bit BMPのみの対応とし、カラーテーブルはスコープ外とする。

2. **pixel_data のサイズ**: `size: remaining` で残り全バイトを取得する設計。実際のピクセルデータサイズは `pixel_offset` 以降だが、file_header + dib_header 以降が残りデータとなるため、パディング等が含まれる可能性がある。初期実装では許容する。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。signatureフィールドはasciiではなくbytes+expectedで検証（asciiにするとflags等の不要な処理が入る可能性があるため）。
- カラーテーブルは初期スコープ外として省略。REQ-006の条件付きフィールドで将来追加可能。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `formats/bmp.bdef.yaml` | 新規: BMPフォーマット定義 |
| `tests/.../BmpTestDataGenerator.cs` | 新規: 1x1 24bit BMPバイナリ生成 |
| `tests/.../BmpParsingTests.cs` | 新規: 5テスト |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BmpParsingTests | BmpFormat_LoadsWithoutErrors | AC-1, AC-2 |
| BmpParsingTests | BmpFormat_DecodesMinimalBmp | AC-3 |
| BmpParsingTests | BmpFormat_FileHeader_DecodesCorrectly | AC-4 |
| BmpParsingTests | BmpFormat_DibHeader_DecodesCorrectly | AC-5, AC-6 |
| BmpParsingTests | BmpFormat_TreeOutput_ContainsExpectedElements | AC-7 |

### 受入条件の充足状況

- [x] AC-1〜AC-8: 全て充足。161テスト合格。

### 気づき・今後の課題

- V4/V5ヘッダー対応は header_size の値に応じた switch で将来拡張可能。
- カラーテーブルは `if: "{bits_per_pixel <= 8}"` で条件付き追加可能（REQ-006実装済み）。