# BinAnalyzer

[![CI](https://github.com/zredjet/BinAnalyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/zredjet/BinAnalyzer/actions/workflows/ci.yml)

汎用バイナリ構造解析ツール。YAML-DSL（`.bdef.yaml`）でバイナリフォーマットを定義し、任意のバイナリファイルを構造的に解析・ツリー表示します。

## 機能一覧

- **豊富なフィールド型** — 整数（u8〜u64, i8〜i64）、浮動小数点（float32, float64）、文字列（ascii, utf8, asciiz, utf8z, utf16le/be, sjis, latin1）、バイト列、構造体、switch、計算フィールド（virtual）
- **圧縮データ対応** — zlib / deflate 圧縮フィールドの展開・ネスト解析
- **7種類の出力形式** — tree（デフォルト）, json, hexdump, html（検索機能付き）, map, csv, tsv
- **出力フィルタ** — `--filter` でフィールドパスパターンを指定して出力を絞り込み（`*`, `**` ワイルドカード対応）
- **構造的差分比較** — `diff` サブコマンドで2つのバイナリの差分を表示（フラット形式 / ツリー形式）
- **カラー出力** — 端末でのANSIカラー表示（auto / always / never）
- **式・演算子** — 算術・比較・論理演算に加え、ビット演算（`&`, `|`, `^`, `<<`, `>>`）をサポート
- **enum / flags / bitfield** — 値のラベルマッピング、ビットフラグ解析
- **条件フィールド / チェックサム** — `if` による条件スキップ、CRC-32チェックサム検証
- **DSLインポート** — 共通定義を別ファイルに分離し `imports` で再利用
- **アライメント / パディング** — フィールド・構造体レベルのバイト境界調整、パディング非表示
- **エンディアン切り替え** — 構造体・フィールドレベルでエンディアンを上書き（優先順位: フィールド > 構造体 > フォーマットデフォルト）
- **カスタムバリデーション** — `validate` 式でデコード後の値を検証し ✓/✗ で表示
- **エラー回復** — `--on-error continue` でデコードエラー後も解析を継続
- **文字列テーブル参照** — ELF `.strtab` 等の文字列テーブルを整数フィールドから参照し文字列に解決

## クイックスタート

```bash
# ビルド
dotnet build

# PNGファイルを解析
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml
```

### 出力例

```
PNG [0x00000000] (45 bytes)
├── signature [0x00000000] (8 bytes): 89 50 4E 47 0D 0A 1A 0A  ✓
└── chunks [0x00000008] (37 bytes) [2 items]
    ├── #0 → chunk [0x00000008] (25 bytes)
    │   ├── length: 13
    │   ├── type: "IHDR"  [ancillary=no private=no reserved=no safe_to_copy=no]
    │   ├── data → ihdr [0x00000010] (13 bytes)
    │   │   ├── width: 1
    │   │   ├── height: 1
    │   │   ├── bit_depth: 8
    │   │   ├── color_type: 2 "truecolor"
    │   │   ├── compression: 0 "deflate"
    │   │   ├── filter: 0 "adaptive"
    │   │   └── interlace: 0 "none"
    │   └── crc: 2423739358 (0x907753DE)
    └── #1 → chunk [0x00000021] (12 bytes)
        ├── length: 0
        ├── type: "IEND"  [ancillary=no private=no reserved=no safe_to_copy=no]
        ├── data → raw_data [0x00000029] (0 bytes)
        └── crc: 2923585666 (0xAE426082)
```

## 使用方法

```bash
# 基本的な解析（ツリー表示）
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml

# JSON形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o json

# ヘックスダンプ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o hexdump

# HTMLレポート生成
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o html > report.html

# ビジュアルマップ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o map

# CSV形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv

# 出力フィルタで特定フィールドのみ抽出
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv --filter "**.width" --filter "**.height"

# 2つのファイルの差分比較（フラット形式）
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml

# 2つのファイルの差分比較（ツリー形式）
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml --output tree

# カラー出力を強制
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --color always
```

全コマンド・オプションの詳細は [CLIリファレンス](docs/cli-usage.md) を参照してください。

## フォーマット定義

バイナリフォーマットはYAML-DSLファイル（`.bdef.yaml`）で定義します。完全な仕様は [DSLリファレンス](docs/dsl-reference.md) を参照してください。

サンプルフォーマット定義が `formats/` ディレクトリに同梱されています（PNG, BMP, WAV, ZIP, ELF, PDF, JPEG, ICO, MIDI, PCAP, WebP, MP3, TAR, FLAC, Java Class, GIF, PE, Mach-O, SQLite, TIFF）。

### エディタ補完（JSON Schema）

`schemas/bdef.schema.json` にJSON Schemaが含まれています。VS Code + [YAML拡張](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) をお使いの場合、`.vscode/settings.json` に以下を追加すると `*.bdef.yaml` ファイルでオートコンプリート・バリデーションが有効になります:

```json
{
  "yaml.schemas": {
    "./schemas/bdef.schema.json": "*.bdef.yaml"
  }
}
```

## アーキテクチャ

設計の詳細は [アーキテクチャ](docs/architecture.md) を参照してください。

## ビルド・テスト

```bash
dotnet build
dotnet test
```

## 必要環境

- .NET 10 SDK
