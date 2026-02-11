# 実ファイル検証ステータス

REQ-095 実ファイル検証スイートの各フォーマット検証状況。

## 検証ステータス一覧

| # | フォーマット | ファイル生成方法 | ステータス | 既知の制限・備考 |
|---|---|---|---|---|
| 1 | PNG | sips / TestDataGenerator | OK | |
| 2 | JPEG | sips / TestDataGenerator | OK | |
| 3 | GIF | TestDataGenerator | OK | |
| 4 | BMP | sips / TestDataGenerator | OK | |
| 5 | TIFF | sips / TestDataGenerator | WARN | sips 生成 TIFF のシーク先が範囲外 |
| 6 | ICO | TestDataGenerator | WARN | 変数 `image_magic` のスコープ解決エラー |
| 7 | WebP | TestDataGenerator | OK | |
| 8 | WAV | TestDataGenerator | OK | |
| 9 | MP3 | TestDataGenerator | OK | |
| 10 | FLAC | TestDataGenerator | OK | |
| 11 | MIDI | TestDataGenerator | OK | |
| 12 | ELF | TestDataGenerator | OK | |
| 13 | PE | TestDataGenerator | WARN | 変数 `magic` のスコープ解決エラー |
| 14 | Mach-O | cc / TestDataGenerator | OK | |
| 15 | Java Class | javac / TestDataGenerator | OK | |
| 16 | ZIP | zip / TestDataGenerator | OK | |
| 17 | GZIP | gzip / TestDataGenerator | OK | |
| 18 | TAR | tar / TestDataGenerator | OK | |
| 19 | LZ4 | lz4 / TestDataGenerator | OK | |
| 20 | PCAP | hand-crafted / TestDataGenerator | OK | |
| 21 | SQLite | sqlite3 / TestDataGenerator | WARN | セルペイロード読み取り範囲超過 |
| 22 | PDF | hand-crafted / TestDataGenerator | OK | |
| 23 | OTF | system copy / TestDataGenerator | OK | |
| 24 | ICC | system copy / TestDataGenerator | OK | |
| 25 | AVI | TestDataGenerator | WARN | サブチャンクの境界計算エラー |
| 26 | FLV | TestDataGenerator | OK | |
| 27 | 7z | TestDataGenerator | OK | |
| 28 | WASM | TestDataGenerator | OK | |
| 29 | DNS | TestDataGenerator | OK | |
| 30 | Parquet | TestDataGenerator | OK | |

**ステータス凡例**: OK = エラーなし完了, WARN = リカバリ付き完了, FAIL = デコード失敗, `-` = 未実行

## テスト実行方法

```bash
# 実ファイル生成（シェルスクリプト対応分）
testdata/generate-real-files.sh

# 実ファイルテスト実行
dotnet test --filter "Category=RealFile"

# 詳細ログ付き
dotnet test --filter "Category=RealFile" -v detailed
```

## 既知の制限

テスト実行後に判明した制限事項をここに記録する。

### フォーマット定義の制限

- **ICO**: `image_data` 内で `image_magic` 変数がスコープ外。switch の変数バインディングとバウンダリスコープの境界が原因の可能性。
- **PE**: `optional_header` 内で `magic` 変数がスコープ外。switch 分岐後の変数参照問題。
- **AVI**: サブチャンク (`dwReserved`) の境界計算で残りバイト不足。最小データでのパディング/アライメント問題。
- **TIFF**: IFD のシークオフセットが範囲外 (`0x62000000`)。sips 生成 TIFF 固有のエンディアン解釈問題の可能性。
- **SQLite**: `first_page.cells.payload` の読み取りが範囲超過。sqlite3 生成ファイルのセル構造の複雑さに起因。

### ツール実ファイル特有の問題

- sips 生成の TIFF は Little-Endian だが、IFD オフセットの解釈でエンディアンの不一致が発生する場合がある
- sqlite3 生成ファイルは実際のテーブル・行を含むため、最小合成データよりもセル構造が複雑

## エラー一覧

テスト実行で報告されたデコードエラーの詳細。

### ICO (1 error)
| オフセット | フィールドパス | エラー内容 |
|---|---|---|
| `0x00000016` | `entries.image_data` | Variable 'image_magic' not found in current scope |

### AVI (3 errors)
| オフセット | フィールドパス | エラー内容 |
|---|---|---|
| `0x00000048` | `chunks.data.sub_chunks.data.dwReserved` | Cannot read 16 bytes at position 0x48: only 12 bytes remaining in scope |
| `0x00000058` | `chunks.chunk_size` | Cannot read 4 bytes at position 0x58: only 0 bytes remaining in scope |
| `0x00000058` | `chunks.data` | Cannot push scope of size 64 at position 88: would exceed data length 88 |

### PE (1 error)
| オフセット | フィールドパス | エラー内容 |
|---|---|---|
| `0x00000058` | `optional_header` | Variable 'magic' not found in current scope |

### TIFF (1 error)
| オフセット | フィールドパス | エラー内容 |
|---|---|---|
| `0x00000008` | `ifd0` | Seek offset 1644167168 (0x62000000) is out of range: valid range is 0..3492 (0xDA4) |

### SQLite (3 errors)
| オフセット | フィールドパス | エラー内容 |
|---|---|---|
| `0x00001FFC` | `first_page.cells.payload` | Cannot read 23 bytes at position 0x1FFC: only 4 bytes remaining in scope |
| `0x00001FFE` | `first_page.cells.payload` | Cannot read 101 bytes at position 0x1FFE: only 2 bytes remaining in scope |
| `0x00002000` | `first_page.cells.payload` | Cannot read 108 bytes at position 0x2000: only 0 bytes remaining in scope |
