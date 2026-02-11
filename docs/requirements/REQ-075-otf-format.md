# REQ-075: OTF/TTFフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

OTF（OpenType Font）/ TTF（TrueType Font）はデスクトップ・Web・モバイルで最も広く使われるフォントフォーマット。テーブルディレクトリによるオフセットベースの構造を持ち、seekを活用したテーブル解析のショーケースとして最適。

OTF/TTF構造:
- Offset Table（12バイト: sfVersion, numTables, searchRange, entrySelector, rangeShift）
- Table Records（numTables個: tag, checksum, offset, length）
- 各テーブル（head, name, cmap, glyf等、seekでoffset位置にジャンプ）

headテーブルはフォントの基本メタデータ、nameテーブルはフォント名・著作権等の文字列情報を格納する。

## 機能要件

### 追加する機能

- [ ] `formats/otf.bdef.yaml` フォーマット定義
  - Offset Table
    - sfVersion（u32、0x00010000=TrueType, 0x4F54544F="OTTO"=CFF）
    - numTables（u16、テーブル数）
    - searchRange（u16）
    - entrySelector（u16）
    - rangeShift（u16）
  - Table Records（repeat_count numTables）
    - tag（ascii 4、テーブルタグ名）
    - checksum（u32）
    - offset（u32、テーブルデータのファイルオフセット）
    - length（u32、テーブルデータのサイズ）
  - headテーブル（seekでoffset位置にジャンプ）
    - majorVersion（u16）
    - minorVersion（u16）
    - fontRevision（u32、Fixed 16.16）
    - checksumAdjustment（u32）
    - magicNumber（u32、expected 0x5F0F3CF5）
    - flags（u16）
    - unitsPerEm（u16）
    - created（i64、LONGDATETIME）
    - modified（i64、LONGDATETIME）
    - xMin（i16）
    - yMin（i16）
    - xMax（i16）
    - yMax（i16）
    - macStyle（u16、flags: Bold bit 0, Italic bit 1, Underline bit 2, Outline bit 3, Shadow bit 4, Condensed bit 5, Extended bit 6）
    - lowestRecPPEM（u16）
    - fontDirectionHint（i16）
    - indexToLocFormat（i16）
    - glyphDataFormat（i16）
  - nameテーブル（seekでoffset位置にジャンプ）
    - format（u16）
    - count（u16、ネームレコード数）
    - stringOffset（u16、文字列ストレージのオフセット）
    - records（repeat_count count）
      - platformID（u16）
      - encodingID（u16）
      - languageID（u16）
      - nameID（u16、enum: Copyright=0, FontFamily=1, FontSubfamily=2, UniqueID=3, FullName=4, VersionString=5, PostScriptName=6, Trademark=7, Manufacturer=8, Designer=9, Description=10, URLVendor=11, URLDesigner=12, License=13, LicenseURL=14, PreferredFamily=16, PreferredSubfamily=17, SampleText=19）
      - length（u16）
      - offset（u16）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- cmap（文字マッピング）テーブルの詳細解析
- glyf（グリフデータ）テーブルの解析
- CFF/CFF2テーブルの解析
- GPOS/GSUB（OpenType Layout）テーブルの解析
- フォントレンダリング
- TrueType Collection（.ttc）への対応

## 受入条件

1. [ ] `formats/otf.bdef.yaml` が存在すること
2. [ ] Offset Tableが正しく解析できること
3. [ ] Table Records がnumTables個分正しく解析できること
4. [ ] headテーブルへのseek解析ができること
5. [ ] headテーブルのmagicNumber（0x5F0F3CF5）が検証されること
6. [ ] nameテーブルへのseek解析ができること
7. [ ] nameIDがenumラベル付きで表示されること
8. [ ] テーブルディレクトリ解析、head/nameテーブルseek解析が正しく動作すること
9. [ ] フォーマット定義がバリデーションに通ること
10. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにOTF/TTFを追記
- [ ] docs/architecture.md — formats/ ディレクトリに otf.bdef.yaml を追記

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
