# REQ-065: MIDIフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-038で実装されたMIDIフォーマット定義（`formats/midi.bdef.yaml`）では、トラックデータが完全にraw bytesとして表示されるのみであり、MThd（ヘッダチャンク）のバリデーションやdivisionフィールドの詳細解析も不足している。

MIDIファイルのMThdヘッダは仕様上header_lengthが常に6であるべきだが、現在はこのバリデーションがない。divisionフィールドはbit 15でタイミングモード（PPQN/SMPTE）を切り替えるbitfieldだが、現在は単純な整数値として表示される。また、midi_format enumやdivisionフィールドにdescriptionが付与されておらず、解析結果の可読性が低い。

トラックデータの詳細パースには可変長量（VLQ）エンコーディングの対応が必要であり、現DSLでは未対応のため本スコープ外とするが、descriptionに構造の説明を追記することで情報量を向上させる。

## 機能要件

### 追加する機能

- [ ] MThd header_length validate追加
  - `validate "{header_length == 6}"` でヘッダ長が仕様通り6であることを検証
- [ ] divisionフィールドのbitfield化
  - timing_mode (bit 15) — 0=ticks per quarter note (PPQN), 1=SMPTE
  - value (bits 14:0) — PPQNモード時はticks per quarter note、SMPTEモード時はフレームレートとticks per frame
- [ ] midi_format enum改善
  - 0 = "Single track" — description追加
  - 1 = "Multiple tracks, synchronous" — description追加
  - 2 = "Multiple tracks, independent" — description追加
- [ ] PPQN（ticks_per_quarter_note）のdescription追加
  - divisionフィールドまたは関連フィールドにPPQNの意味を説明するdescriptionを付与
- [ ] トラックデータのdescription追記
  - トラックデータ（raw bytes）のdescriptionに、内部構造の説明を追記
  - MIDIイベント（ノートオン/オフ、コントロールチェンジ等）、メタイベント（テンポ、拍子等）、VLQデルタタイムで構成される旨を記載

### 変更する既存機能

- [ ] `formats/midi.bdef.yaml` — header_length validate追加、divisionのbitfield化、midi_format enum description追加、トラックデータdescription追記

### 変更しないもの（スコープ外）

- トラックデータの詳細パース（可変長量（VLQ）エンコーディングは現DSL未対応）
- MIDIイベント（ノートオン/オフ、コントロールチェンジ等）の個別解析
- メタイベント（テンポ、拍子、キー等）の個別解析
- SysExメッセージの解析
- ランニングステータスの処理

## 受入条件

1. [ ] MThd header_lengthに `validate "{header_length == 6}"` が追加されていること
2. [ ] divisionフィールドがbitfieldとして表示されること（timing_mode bit 15、value bits 14:0）
3. [ ] midi_format enumに各値のdescription（"Single track", "Multiple tracks, synchronous", "Multiple tracks, independent"）が付与されていること
4. [ ] PPQN関連のdescriptionがdivisionフィールドまたは関連箇所に追加されていること
5. [ ] トラックデータのdescriptionに内部構造の説明が追記されていること
6. [ ] 既存のMThd/MTrkチャンク解析に回帰がないこと
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

- [ ] formats/midi.bdef.yaml — validate追加、divisionのbitfield化、enum description追加、トラックデータdescription追記
- [ ] docs/architecture.md — フォーマット一覧の説明更新（MIDI validate/bitfield対応を明記）

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
