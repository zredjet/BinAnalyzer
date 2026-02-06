# REQ-060: MP3フォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のmp3.bdef.yaml（REQ-042で導入）では、ID3v2タグ本体が一括bytesとして読み取られており、個別フレーム（TIT2, TPE1, TALB等）の構造が解析されていない。ID3v1タグ（ファイル末尾128バイト）やXing/VBRIヘッダも未対応である。

また、MPEGフレームヘッダは4バイトのbitfieldとして定義されているが、ID3v2ボディ内のフレーム構造が未解析のため、楽曲タイトル・アーティスト名等のメタデータを確認できない。MP3ファイルのメタデータ解析において、ID3v2フレームの個別解析は最も基本的な要件であり、拡充の価値が高い。

## 機能要件

### 追加する機能

- [ ] ID3v2フレーム構造追加
  - `frame_id`: ascii, size=4（フレーム識別子: TIT2, TPE1, TALB等）
  - `frame_size`: u32（フレームデータサイズ）
  - `frame_flags`: u16（フレームフラグ）
  - `frame_data`: bytes, size=frame_size（フレームデータ本体）
  - id3v2_bodyをstruct化し、内部フレームをrepeat(eof)で繰り返し（バウンダリスコープ内のEOFまで）
- [ ] mpeg_frame_headerのbitfield化
  - `sync`: bits 15:5（同期ワード、全1を期待）
  - `mpeg_version`: bits 4:3
  - `layer`: bits 3:2（bitfieldとして表示、layer enumではなくビット値）
  - `protection`: bit 0
- [ ] audio_properties bitfield
  - `bitrate_index`: bits 7:4
  - `sample_rate_index`: bits 3:2
  - `padding`: bit 1
  - `private`: bit 0
- [ ] channel_mode_and_extension bitfield
  - `channel_mode`: bits 7:6
  - `mode_extension`: bits 5:4
  - `copyright`: bit 3
  - `original`: bit 2
  - `emphasis`: bits 1:0

### 変更する既存機能

- [ ] `formats/mp3.bdef.yaml` — id3v2_bodyのstruct化、フレーム構造追加、mpeg_frame_header再構成
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新（MP3エントリの拡充内容反映）

### 変更しないもの（スコープ外）

- ID3v1タグ（ファイル末尾128バイトの逆方向パースは現DSL未対応）
- Xing/VBRIヘッダの解析
- MPEGフレームデータ（音声データ）の解析
- 全MPEGフレームの走査
- ID3v2.2（旧バージョン、3バイトフレームID）
- ID3v2フレームデータの内容別解析（テキストフレームのエンコーディング判定等）

## 受入条件

1. [ ] ID3v2フレームが個別に解析可能であり、frame_id/frame_size/frame_flags/frame_dataが各フレームごとに表示されること
2. [ ] id3v2_bodyがrepeat(eof)で繰り返され、複数のID3v2フレームが連続して解析されること
3. [ ] mpeg_frame_headerがbitfieldとして表示され、sync/mpeg_version/layer/protectionが個別ビットフィールドとして確認できること
4. [ ] audio_properties bitfieldでbitrate_index/sample_rate_index/padding/privateが個別に表示されること
5. [ ] channel_mode_and_extension bitfieldでchannel_mode/mode_extension/copyright/original/emphasisが個別に表示されること
6. [ ] 有効なMP3ファイルで拡充後もデコードエラーにならないこと
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] `formats/mp3.bdef.yaml` — ID3v2フレーム構造追加、mpeg_frame_header bitfield再構成
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
