# REQ-128: FATファイルシステムフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-18 |

## 背景・動機

FAT（File Allocation Table）はUSBメモリ、SDカード、組み込みデバイスで広く使われるファイルシステム。ファームウェア開発、フォレンジック解析、組み込みシステムのデバッグにおいてFATイメージの構造解析が必要になる場面がある。

FAT12/FAT16/FAT32は構造が比較的シンプルで、BPB（BIOS Parameter Block）、FAT領域、ルートディレクトリ、データ領域から構成される。BinAnalyzerのDSLで十分表現可能。

### 段階的アプローチ

FAT → NTFS → ext4 の順で対応し、まずFATから着手する。NTFS/ext4は将来の別REQとする。

## 機能要件

### 追加する機能

- [x] `formats/fat.bdef.yaml` の作成
- [x] BPB（BIOS Parameter Block）の定義
  - ジャンプ命令、OEM名、セクタサイズ、クラスタサイズ等
- [x] FAT12/FAT16/FAT32共通ヘッダとFAT32拡張ヘッダの定義
- [x] ルートディレクトリエントリの定義
  - ショートファイル名（8.3形式）
  - 属性フラグ（読み取り専用、隠し、システム、ディレクトリ等）
  - タイムスタンプ（作成、更新、アクセス — DOS datetime形式）
- [x] FAT種別判定のためのenum/switch定義

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- NTFS、ext4の定義 — 将来の別REQ
- LFN（ロングファイルネーム）エントリの完全デコード — 将来課題
- FATチェーンの追跡（データ領域のファイル復元） — ツールのスコープ外
- exFATの定義

## 受入条件

1. [x] `formats/fat.bdef.yaml` が作成されていること
2. [x] BPBの主要フィールド（セクタサイズ、クラスタサイズ、FAT数、ルートエントリ数等）が正しくデコードされること
3. [x] ルートディレクトリエントリのファイル名・属性・タイムスタンプがデコードされること
4. [x] FAT32拡張ヘッダが正しく分岐・デコードされること
5. [x] 実際のFATイメージでデコードが成功すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **FAT32判定ヒューリスティック**: `root_entry_count == 0` でFAT32を判定（クラスタ数計算はDSLで不可）。virtual field `fat_type_hint` で0/1に変換し、switchで分岐
- **FAT12/16統合**: 拡張BPBの構造が同一のためFAT12/16を1ブランチに統合。`fs_type_string` フィールドで"FAT12"/"FAT16"を表示
- **ブートセクタ構造**: 共通BPB（36バイト）をフラット展開 → virtual discriminant → switch `size: "476"` でバウンダリスコープ内に拡張BPB + boot_code + 0x55AAシグネチャを格納
- **switchのスコープ制約**: 拡張BPBの変数（`fat_size_32`等）はswitchのバウンダリスコープ内に閉じるが、FAT12/16のルートディレクトリseekには共通BPBの変数のみ使用するため問題なし
- **ルートディレクトリ**: FAT12/16のみ対象。`seek: "{(reserved_sector_count + num_fats * fat_size_16) * bytes_per_sector}"` + `size: "{root_entry_count * 32}"` で位置決め。FAT32は `root_entry_count == 0` により `if` でスキップ
- **DOSタイムスタンプ**: ビットシフト演算のvirtual fieldで年月日時分秒を展開（例: `mod_year = ((modification_date >> 9) & 0x7F) + 1980`）
- **属性フラグ**: `flags` 定義で `READ_ONLY`, `HIDDEN`, `SYSTEM`, `VOLUME_LABEL`, `DIRECTORY`, `ARCHIVE` のビットマッピング

### モデル変更

なし。既存のIR、デコードエンジン、式エンジンで全て対応可能。

### インタフェース変更

なし。

### 代替案

1. **共通BPBもバウンダリスコープ内に配置する案** — 変数が外部からアクセスできなくなり、ルートディレクトリのseek式が書けない。不採用
2. **FAT12/16/32を3分岐にする案** — FAT12/16の拡張BPBは同一構造のため不要な重複。不採用
3. **FSInfoセクタのパース** — `fs_info_sector` がswitchスコープ内のため外部からseekできない。将来の課題とする

### 懸念事項

1. LFNエントリ（attributes=0x0F）は通常のdir_entryとしてパースされ、filenameにゴミが表示される（スコープ外のため許容）
2. filename[0]=0x00（空きエントリ）や0xE5（削除済み）もパースされる（フィルタリングはDSLの範囲外）

---

## 実装メモ

### 実装中の設計変更

- `flags: dir_attributes` は整数フィールド（uint8）に適用しているが、現在のBinaryDecoderは整数型フィールドのflags展開を行わない（ASCII型のみ対応）。テストではDecodedIntegerの生値で検証。flags定義自体はDSL上の自己文書化として残す

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FatParsingTests | FatFormat_LoadsWithoutErrors | AC1 |
| FatParsingTests | FatFormat_Fat16_BpbFields_DecodeCorrectly | AC2 |
| FatParsingTests | FatFormat_Fat16_RootDirectory_DecodesCorrectly | AC3 |
| FatParsingTests | FatFormat_Fat16_DirectoryAttributes_DecodeCorrectly | AC3 |
| FatParsingTests | FatFormat_Fat16_Timestamps_DecodeCorrectly | AC3 |
| FatParsingTests | FatFormat_Fat32_ExtendedBpb_DecodesCorrectly | AC4 |
| FatParsingTests | FatFormat_TreeOutput_ContainsExpectedElements | AC2+AC3 |

### 気づき・今後の課題

- BinaryDecoderの整数型フィールドに対するflags展開（DecodedFlags生成）が未実装。将来的にPEのcharacteristics等と合わせて対応が望ましい
