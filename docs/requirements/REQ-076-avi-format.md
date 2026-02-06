# REQ-076: RIFF/AVIフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

RIFF（Resource Interchange File Format）はMicrosoftが定義したコンテナフォーマットで、AVI（Audio Video Interleave）はその上に構築された動画フォーマット。WAVと同系列のRIFFベースであり、ネストしたチャンク構造が特徴。

RIFFチャンクはFourCC識別子（4バイトASCII）+ サイズ + データで構成され、LISTチャンクによるネスト構造を持つ。switch構文によるチャンク種別の分岐解析のショーケースとして適している。

RIFF/AVI構造:
- RIFFヘッダ（"RIFF" + file_size + "AVI "）
- チャンクの繰り返し（chunk_id + chunk_size + data）
- LISTチャンク（ネストしたチャンクを含む）
- avihチャンク（Main AVI Header）

## 機能要件

### 追加する機能

- [ ] `formats/avi.bdef.yaml` フォーマット定義
  - RIFFヘッダ
    - magic（ascii 4、expected "RIFF"）
    - file_size（u32le、ファイルサイズ - 8）
    - form_type（ascii 4、expected "AVI "）
  - チャンク（repeat eof）
    - chunk_id（ascii 4、チャンク識別子）
    - chunk_size（u32le、チャンクデータサイズ）
    - data（switch on chunk_id）
      - "LIST" → list_chunk（リストタイプ + ネストチャンク）
      - "avih" → main_avi_header
      - default → raw_data（bytes size chunk_size）
  - main_avi_header（avihチャンク）
    - dwMicroSecPerFrame（u32、フレーム間隔マイクロ秒）
    - dwMaxBytesPerSec（u32、最大データレート）
    - dwPaddingGranularity（u32、パディング粒度）
    - dwFlags（u32、フラグ）
    - dwTotalFrames（u32、総フレーム数）
    - dwInitialFrames（u32、初期フレーム数）
    - dwStreams（u32、ストリーム数）
    - dwSuggestedBufferSize（u32、推奨バッファサイズ）
    - dwWidth（u32、動画幅）
    - dwHeight（u32、動画高さ）
    - dwReserved（bytes 16、予約領域）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- ストリームヘッダ（strh）/ ストリームフォーマット（strf）の詳細解析
- インデックスチャンク（idx1, indx）の解析
- 実際の音声・映像フレームデータのデコード
- OpenDML拡張（AVI 2.0）への対応
- 2GB超ファイルの対応

## 受入条件

1. [ ] `formats/avi.bdef.yaml` が存在すること
2. [ ] RIFFヘッダの "RIFF" マジックが正しく検証されること
3. [ ] form_typeの "AVI " が正しく検証されること
4. [ ] チャンク構造（chunk_id + chunk_size + data）が繰り返し解析できること
5. [ ] avihチャンクのMain AVI Headerフィールドが正しく解析できること
6. [ ] switchによるチャンク種別の分岐が正しく動作すること
7. [ ] RIFFヘッダ・チャンク構造解析、avih解析が正しく動作すること
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

- [ ] README.md — 対応フォーマットにRIFF/AVIを追記
- [ ] docs/architecture.md — formats/ ディレクトリに avi.bdef.yaml を追記

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
