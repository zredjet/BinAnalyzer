# REQ-038: MIDIフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

MIDI（Musical Instrument Digital Interface）ファイルはMThd（ヘッダチャンク）+ MTrk（トラックチャンク）の連鎖で構成される。PNGのチャンク構造に類似したチャンクベースのフォーマットであり、BinAnalyzerの既存機能で定義可能。

ヘッダチャンクにはフォーマットタイプ、トラック数、分解能が含まれ、トラックチャンクにはMIDIイベントが格納される。

## 機能要件

### 追加する機能

- [ ] `formats/midi.bdef.yaml` フォーマット定義
  - MThd チャンク（magic "MThd", length=6, format, ntrks, division）
  - MTrk チャンクの繰り返し（magic "MTrk", length, data）
  - MIDIイベントデータはraw bytesとして扱う（可変長デルタタイム等の解析は複雑なため）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- MIDIイベント（ノートオン/オフ、コントロールチェンジ等）の個別解析
- 可変長数値（Variable-Length Quantity）のデコード
- SMF（Standard MIDI File）以外のMIDI形式

## 受入条件

1. [ ] `formats/midi.bdef.yaml` が存在すること
2. [ ] MThdチャンクのformat, ntrks, divisionが正しく解析できること
3. [ ] MTrkチャンクが正しい個数分解析できること
4. [ ] 各トラックのデータ長が正しく読み取れること
5. [ ] 有効なMIDIファイルでデコードエラーにならないこと
6. [ ] フォーマット定義がバリデーションに通ること
7. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにMIDIを追記
- [ ] docs/architecture.md — formats/ ディレクトリに midi.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: MIDI
endianness: big
root: midi
```

**主要struct:**

- **`midi`** (root) — ファイル全体
  - `mthd_magic`: bytes, size=4, expected=[0x4D,0x54,0x68,0x64] ("MThd")
  - `mthd_length`: u32 (常に6)
  - `format`: u16, enum=midi_format
  - `ntrks`: u16
  - `division`: u16
  - `tracks`: struct=mtrk, repeat_count="{ntrks}"
- **`mtrk`** — トラックチャンク
  - `magic`: bytes, size=4, expected=[0x4D,0x54,0x72,0x6B] ("MTrk")
  - `length`: u32
  - `data`: bytes, size="{length}"

### enum定義

- **`midi_format`**: 0=single_track, 1=multi_track_sync, 2=multi_track_async

### switchの使い方

switchは使用しない。MThd/MTrkは固定順序で出現するため、フラットなフィールド列で記述する。

### 繰り返しの方式

MTrkチャンクの繰り返しに `repeat_count: "{ntrks}"` を使用。ヘッダの `ntrks` フィールドで個数が定まるため、count方式が最適。

### 注意点・制約

- MIDIイベント（可変長デルタタイム + イベントバイト列）の内部解析はスコープ外。MTrkの `data` は生bytesとして出力する。
- MThd部分はchunk構造にせずフラットに展開する。length=6は固定であり、switchで分岐する必要がない。
- division フィールドは最上位ビットでティック/SMPTE形式が切り替わるが、第1弾ではbitfield分解せず u16 のまま表示する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
