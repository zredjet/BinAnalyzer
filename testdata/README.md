# testdata

実ファイル検証（REQ-095）・ゴールデンファイルリグレッション（REQ-107/110）用のテストデータ格納ディレクトリ。

## ディレクトリ構成

```
testdata/
├── README.md                  # 本ファイル
├── generate-real-files.sh     # macOS ツールで実ファイル生成
├── golden/                    # ゴールデンファイル（JSON、git コミット済み）
│   ├── png.json
│   ├── bmp.json
│   └── ... (全30フォーマット)
└── real/                      # 生成された実ファイル（.gitignore 対象含む）
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

## CI テスト実行フロー（REQ-110）

CI（GitHub Actions）では、テストが3ステップに分離して実行される:

1. **Unit & Integration Tests** — `Category` Trait なしの一般テスト
2. **Golden File Regression Tests** — `[Trait("Category", "Golden")]` テスト（30フォーマット）
3. **Real File Validation Tests** — `[Trait("Category", "RealFile")]` テスト（30フォーマット）

テストバイナリは `RealFileFixture` + `TestDataGenerator` により CI 環境で実行時に自動生成される。ファイルが不在の場合はテストが即失敗する（サイレントスキップしない）。

## ゴールデンファイル更新手順

デコード出力に影響するコード変更を行った場合:

```bash
# 1. ゴールデンファイルを再生成
UPDATE_GOLDEN=1 dotnet test --filter "Category=Golden"

# 2. 差分を確認
git diff testdata/golden/

# 3. 意図した変更であれば、コード変更とゴールデンファイル更新を同じ PR でコミット
git add testdata/golden/
git commit  # コード変更と一緒に
```

### ローカルのテストファイル再生成

`testdata/real/` のファイルが古い場合や破損した場合:

```bash
# TestDataGenerator 生成ファイルを削除して再生成
rm testdata/real/test.gif testdata/real/test.wav  # etc.
dotnet test --filter "Category=RealFile"           # RealFileFixture が再生成
```

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
