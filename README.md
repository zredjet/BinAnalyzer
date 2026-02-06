# BinAnalyzer

汎用バイナリ構造解析ツール。YAML-DSL（`.bdef.yaml`）でバイナリフォーマットを定義し、任意のバイナリファイルを構造的に解析・ツリー表示します。

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

## フォーマット定義

バイナリフォーマットはYAML-DSLファイル（`.bdef.yaml`）で定義します。完全な仕様は [DSLリファレンス](docs/dsl-reference.md) を参照してください。

PNGフォーマット定義が `formats/png.bdef.yaml` に同梱されています。

## アーキテクチャ

設計の詳細は [アーキテクチャ](docs/architecture.md) を参照してください。

## ビルド・テスト

```bash
dotnet build
dotnet test
```

## 必要環境

- .NET 10 SDK
