# REQ-057: WAVフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-008で実装されたWAVフォーマット定義（`formats/wav.bdef.yaml`）はRIFFヘッダーとfmtチャンクの基本構造に対応しているが、dataチャンクがswitchで個別認識されずraw_dataにフォールバック、factチャンクやLIST-INFOチャンクが未対応、audio_formatのenum値が不完全、fmtチャンクが16バイト固定で拡張フォーマットに対応できない状態にある。

dataチャンクはWAVファイルの主要構成要素であり、switchで明示的に認識されることで解析結果の可読性が向上する。factチャンクは非PCMフォーマット（ADPCM、IEEE Float等）で必須であり、サンプル数の情報を提供する。LIST-INFOチャンクはアーティスト名やタイトル等のメタデータを保持する。audio_formatもPCM(1)以外の主要フォーマット（ADPCM、IEEE Float、A-law、mu-law、EXTENSIBLE）が不足している。

fmtチャンクについては、PCM以外のフォーマットではcbSize(uint16)と追加フォーマット情報が16バイトの基本フィールドの後に続くが、現在は固定16バイトとして扱われているため、拡張部分が読み飛ばされている。

本要望では、既存の `wav.bdef.yaml` を拡充し、WAV解析の実用性を向上させる。

## 機能要件

### 追加する機能

- [ ] dataチャンク追加
  - switchのcasesに `"'data'"` を追加
  - 中身はraw bytes（`size: remaining`）として表示
- [ ] factチャンク追加
  - switchのcasesに `"'fact'"` を追加
  - num_samples: uint32（サンプル数）
- [ ] LIST-INFOチャンク追加
  - switchのcasesに `"'LIST'"` を追加
  - 基本構造のみ（list_type: ascii 4バイト + sub-chunksはraw_dataとして表示）
- [ ] audio_format enum拡充
  - ADPCM = 2
  - IEEE_FLOAT = 3
  - ALAW = 6
  - MULAW = 7
  - EXTENSIBLE = 0xFFFE
- [ ] fmt拡張対応
  - audio_formatがPCM(1)以外の場合、基本16バイトフィールドの後に追加フィールドを読み取り
  - cbSize: uint16（拡張データのサイズ）
  - extra_format_bytes: bytes（`size: "{cbSize}"`、拡張フォーマットデータ）

### 変更する既存機能

- [ ] `formats/wav.bdef.yaml` — switchのcasesにdata/fact/LISTチャンクを追加、audio_format enumにエントリ追加、fmt_chunkに拡張フィールド対応を追加

### 変更しないもの（スコープ外）

- LIST-INFOチャンク内のsub-chunks（IART、INAM等）の個別パース
- WAVEFORMATEXTENSIBLE構造体の詳細解析（SubFormat GUID等）
- オーディオデータの波形解析・サンプルレベル解析
- RIFFの奇数サイズパディング対応
- cueチャンク、smplチャンク等の追加チャンク

## 受入条件

1. [ ] dataチャンクがswitchで認識され、raw bytesとして表示されること
2. [ ] factチャンクがswitchで認識され、num_samplesがuint32として表示されること
3. [ ] LIST-INFOチャンクがswitchで認識され、list_typeとraw_dataが表示されること
4. [ ] audio_formatにADPCM(2), IEEE_FLOAT(3), ALAW(6), MULAW(7), EXTENSIBLE(0xFFFE)が追加されていること
5. [ ] PCM以外のaudio_formatの場合、cbSizeとextra_format_bytesが読み取り可能であること
6. [ ] 既存のWAV解析機能（RIFFヘッダー、fmtチャンクのPCMパース）に回帰がないこと
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

- [ ] formats/wav.bdef.yaml — data/fact/LISTチャンク追加、audio_format拡充、fmt拡張対応
- [ ] docs/architecture.md — フォーマット一覧の説明更新（WAV拡張対応を明記）

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
