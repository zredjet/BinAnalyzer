# testdata

実ファイル検証（REQ-095）用のテストファイル格納ディレクトリ。

## ディレクトリ構成

```
testdata/
├── README.md                  # 本ファイル
├── generate-real-files.sh     # macOS ツールで実ファイル生成
└── real/                      # 生成された実ファイル
    ├── test.png               # sips
    ├── test.jpg               # sips
    ├── test.bmp               # sips
    ├── test.tiff              # sips
    ├── test.gz                # gzip
    ├── test.tar               # tar
    ├── test.zip               # zip
    ├── test.sqlite            # sqlite3
    ├── test.macho             # cc
    ├── test.class             # javac
    ├── test.lz4               # lz4 (homebrew)
    ├── test.icc               # system copy
    ├── test.otf               # system copy
    ├── test.pdf               # hand-crafted
    ├── test.pcap              # hand-crafted (python3)
    └── (以下は RealFileFixture が TestDataGenerator で自動生成)
        test.gif, test.wav, test.mp3, test.flac,
        test.avi, test.flv, test.midi, test.webp,
        test.ico, test.elf, test.pe, test.wasm,
        test.dns, test.7z, test.parquet
```

## 使用方法

### シェルスクリプトでの生成

```bash
chmod +x testdata/generate-real-files.sh
testdata/generate-real-files.sh
```

macOS 標準ツール（sips, gzip, tar, zip, sqlite3, cc, javac）と一部の追加ツール（lz4）を使用して実ファイルを生成する。

### テスト実行時の自動生成

シェルスクリプトで生成できないフォーマットは、テスト実行時に `RealFileFixture` が既存の `TestDataGenerator` を使って自動生成する。ファイルが既に存在する場合はスキップされる。

### テスト実行

```bash
# 実ファイルテストのみ
dotnet test --filter "Category=RealFile"

# 詳細出力（デコードエラーログ確認）
dotnet test --filter "Category=RealFile" -v detailed
```

## ファイル管理方針

- `testdata/real/` は git 管理する（合計サイズ < 2MB 目標）
- シェルスクリプト生成ファイル: git にコミット
- TestDataGenerator 生成ファイル: `.gitignore` で除外（テスト時に自動生成）
- 本物の実ファイルが入手でき次第、差し替え可能

## 依存ツール

| ツール | 用途 | インストール |
|---|---|---|
| sips | PNG, JPEG, BMP, TIFF | macOS 標準 |
| gzip | GZIP | macOS 標準 |
| tar | TAR | macOS 標準 |
| zip | ZIP | macOS 標準 |
| sqlite3 | SQLite | macOS 標準 / mise |
| cc | Mach-O | Xcode CLI Tools |
| javac | Java Class | macOS 標準 |
| lz4 | LZ4 | `brew install lz4` |
| python3 | PCAP, BMP(中間) | macOS 標準 |
