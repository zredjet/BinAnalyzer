# REQ-073: GZIPフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

GZIPはRFC 1952で定義される、広く使われる圧縮フォーマット。HTTP圧縮、tarアーカイブの圧縮（.tar.gz）、ログファイルの保存など多岐に利用される。

deflate型フィールドの活用例としても最適であり、フォーマット定義のショーケースとなる。ヘッダにはbitfieldフラグ、条件付きオプションフィールド、asciiz（NULL終端文字列）など、DSLの表現力を実証する要素を含む。

GZIP構造:
- ヘッダ（10バイト固定 + オプションフィールド）
- 圧縮データ（deflate）
- フッタ（8バイト: CRC32 + 元サイズ）

## 機能要件

### 追加する機能

- [ ] `formats/gzip.bdef.yaml` フォーマット定義
  - ヘッダ
    - magic（2バイト: 0x1F, 0x8B）
    - compression_method（u8、通常8=deflate）
    - flags（bitfield: FTEXT bit 0, FHCRC bit 1, FEXTRA bit 2, FNAME bit 3, FCOMMENT bit 4）
    - mtime（u32le、UNIX timestamp）
    - xfl（u8、extra flags）
    - os（u8、enum: FAT=0, Amiga=1, VMS=2, Unix=3, VM/CMS=4, Atari=5, HPFS=6, Macintosh=7, Z-System=8, CP/M=9, TOPS-20=10, NTFS=11, QDOS=12, Acorn=13, unknown=255）
  - オプションフィールド（flagsに応じて条件付き）
    - FEXTRA（flags.FEXTRA=1の場合）: xlen u16le + extra bytes size xlen
    - FNAME（flags.FNAME=1の場合）: 元ファイル名（asciiz、NULL終端）
    - FCOMMENT（flags.FCOMMENT=1の場合）: コメント（asciiz、NULL終端）
  - compressed_data（deflate、remainingサイズ）
  - フッタ
    - crc32（u32le、元データのCRC-32）
    - isize（u32le、元データサイズ mod 2^32）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- マルチメンバーGZIP（複数GZIPストリームの連結）の対応
- FHCRC（ヘッダCRC）の検証
- deflate圧縮データの展開後の内容表示（deflateフィールドとしての展開は行う）
- 非deflate圧縮方式への対応（事実上deflateのみが使用される）

## 受入条件

1. [ ] `formats/gzip.bdef.yaml` が存在すること
2. [ ] ヘッダのマジックナンバー（0x1F8B）が正しく検証されること
3. [ ] flagsのbitfieldが正しく解析・表示されること（FTEXT, FHCRC, FEXTRA, FNAME, FCOMMENT）
4. [ ] osフィールドがenumラベル付きで表示されること
5. [ ] オプションフィールド（FNAME等）がflagsに応じて条件付きで解析されること
6. [ ] compressed_dataがdeflateフィールドとして解析可能であること
7. [ ] フッタのcrc32, isizeが正しく解析されること
8. [ ] GZIPファイルのヘッダ・フッタが解析可能であること
9. [ ] deflate展開が可能であること
10. [ ] フォーマット定義がバリデーションに通ること
11. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにGZIPを追記
- [ ] docs/architecture.md — formats/ ディレクトリに gzip.bdef.yaml を追記

---

## 設計メモ

> 設計Phaseで記入する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
