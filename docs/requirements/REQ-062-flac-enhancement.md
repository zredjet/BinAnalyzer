# REQ-062: FLACフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のflac.bdef.yaml（REQ-043で導入）では、STREAMINFOのsample_rate/channels/bps/total_samplesを含む8バイトのパック領域がstream_info_bitsとして生bytesのまま出力されており、個別フィールドの抽出ができていない。また、VORBIS_COMMENTブロックはリトルエンディアン制約により生bytesとして扱われているが、REQ-045（per-field endianness）の実装により構造的デコードが可能となった。

picture_type enumも基本的な値（0-6）のみで、7-20の値（Lead artist, Conductor, Band logotype等）が未定義である。CUESHEETメタデータブロック（type=5）も未対応である。

FLAC音声ファイルの解析において、サンプルレート・チャンネル数・ビット深度の確認やVorbis Commentからのメタデータ抽出は基本的な要件であり、拡充の価値が高い。

## 機能要件

### 追加する機能

- [ ] STREAMINFO改善
  - stream_info_bitsの8バイトをbitfield化
    - min_block_size(u16) + max_block_size(u16) は既存のまま維持
    - 残り8バイト（min_frame_size + max_frame_size + sample_rate/channels/bps/total_samples）の再構成:
      - `min_frame_size`: 24bit（bytes 3個、virtualフィールドで結合）
      - `max_frame_size`: 24bit（bytes 3個、virtualフィールドで結合）
      - `sample_rate_channels_bps_samples`: bytes 8, bitfield型で解析
        - `sample_rate`: bits 59:40（20bit）
        - `channels`: bits 39:37（3bit、実値は+1）
        - `bps`: bits 36:33（5bit、実値は+1）
        - `total_samples`: bits 32:0（36bit）
    - 現DSLのbitfieldは最大64bitのため、この8バイトはbitfield型（size=8）で解析可能
- [ ] Vorbis Comment改善
  - 構造的デコードへの移行（生bytes → 構造体）
  - `vendor_length`: u32, endianness=little
  - `vendor_string`: utf8, size=vendor_length
  - `comment_count`: u32, endianness=little
  - `comments`: struct=vorbis_comment_entry, repeat_count=comment_count
  - vorbis_comment_entry構造体:
    - `length`: u32, endianness=little
    - `value`: utf8, size=length
  - 構造体レベルでendianness: littleを指定
- [ ] picture_type enum拡充（values 7-20）
  - 7 = Lead artist
  - 8 = Artist
  - 9 = Conductor
  - 10 = Band
  - 11 = Composer
  - 12 = Lyricist
  - 13 = Recording location
  - 14 = During recording
  - 15 = During performance
  - 16 = Movie screen capture
  - 17 = A bright coloured fish
  - 18 = Illustration
  - 19 = Band logotype
  - 20 = Publisher logotype
- [ ] CUESHEETメタデータブロック追加（block_type=5）
  - `media_catalog`: ascii, size=128（メディアカタログ番号）
  - `lead_in_samples`: u64（リードインサンプル数）
  - `is_cd`: bitfield, 1bit（CDフラグ）
  - `reserved`: bytes（予約領域）
  - `num_tracks`: u8（トラック数）
  - `tracks`: struct=cuesheet_track, repeat_count=num_tracks

### 変更する既存機能

- [ ] `formats/flac.bdef.yaml` — STREAMINFO bitfield化、Vorbis Comment構造化、picture_type enum拡充、CUESHEET追加
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新（FLACエントリの拡充内容反映）

### 変更しないもの（スコープ外）

- オーディオフレームのデコード
- FLAC圧縮データの展開
- Ogg FLACコンテナ
- CUESHEETトラック内のインデックスポイント詳細解析
- Vorbis Commentのキー=値形式の意味的解析

## 受入条件

1. [ ] STREAMINFOのsample_rate/channels/bps/total_samplesがbitfieldとして個別フィールド表示されること
2. [ ] min_frame_size/max_frame_sizeが24bitフィールドとして正しく計算・表示されること
3. [ ] Vorbis Commentがフレーム解析可能であり、vendor_string/comment_count/各commentのvalue（utf8）が表示されること
4. [ ] Vorbis Comment内のフィールドがリトルエンディアンで正しくデコードされること
5. [ ] picture_type enumにvalues 7-20が追加され、ラベルが表示されること
6. [ ] CUESHEETメタデータブロック（type=5）がswitch caseに追加され、基本フィールドが解析されること
7. [ ] 有効なFLACファイルで拡充後もデコードエラーにならないこと
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なファイル

- [ ] `formats/flac.bdef.yaml` — STREAMINFO bitfield化、Vorbis Comment構造化、enum拡充、CUESHEET追加
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
