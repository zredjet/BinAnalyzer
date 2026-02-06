# REQ-077: FLVフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

FLV（Flash Video）はAdobe Flashで使用された動画コンテナフォーマット。レガシーではあるが、タグベースのストリーム構造を持ち、bitfieldの活用例として適している。RTMPストリーミングの基盤フォーマットでもあり、動画配信の解析に有用。

FLV構造:
- ヘッダ（9バイト: シグネチャ "FLV" + バージョン + flags bitfield + data_offset）
- PreviousTagSize0（u32、常に0）
- タグの繰り返し（tag_type + data_size + timestamp + stream_id + data + prev_tag_size）

タグ種別（audio=8, video=9, script=18）のenum表示、bitfieldによるhas_audio/has_videoフラグの解析が特徴。

## 機能要件

### 追加する機能

- [ ] `formats/flv.bdef.yaml` フォーマット定義
  - ヘッダ
    - signature（ascii 3、expected "FLV"）
    - version（u8、通常1）
    - flags（bitfield: has_audio bit 2, has_video bit 0）
    - data_offset（u32、ヘッダサイズ、通常9）
  - prev_tag_size_0（u32、expected 0）
  - タグ（repeat eof）
    - tag_type（u8、enum: audio=8, video=9, script=18）
    - data_size（bytes 3 → u24、タグデータサイズ）
    - timestamp（bytes 3 → u24、タイムスタンプ下位24ビット）
    - timestamp_ext（u8、タイムスタンプ上位8ビット）
    - stream_id（bytes 3、常に0）
    - data（bytes size data_size、タグデータ）
    - prev_tag_size（u32、直前タグの全体サイズ）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- 音声タグデータ（AudioTagHeader）の詳細解析（コーデック種別、サンプリングレート等）
- 映像タグデータ（VideoTagHeader）の詳細解析（コーデック種別、フレームタイプ等）
- ScriptData（AMF0/AMF3エンコーディング）の解析
- H.264/AAC固有のパケットタイプ解析
- FLVストリームの再構築・再生

## 受入条件

1. [ ] `formats/flv.bdef.yaml` が存在すること
2. [ ] ヘッダのシグネチャ "FLV" が正しく検証されること
3. [ ] flagsのbitfield（has_audio, has_video）が正しく解析・表示されること
4. [ ] prev_tag_size_0 が 0 として検証されること
5. [ ] タグのtag_typeがenumラベル付きで表示されること（audio=8, video=9, script=18）
6. [ ] タグ構造が繰り返し解析できること
7. [ ] FLVヘッダ・タグ構造解析、タグ種別enum表示が正しく動作すること
8. [ ] フォーマット定義がバリデーションに通ること
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにFLVを追記
- [ ] docs/architecture.md — formats/ ディレクトリに flv.bdef.yaml を追記

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
