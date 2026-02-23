# REQ-135: sizeとrepeat: eofの共存

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在、フィールドに `size` を指定しつつ `repeat: eof` を併用することができない。`size` はフィールド自体のバイト長として解釈され、配列の境界スコープとしては機能しない。

この制約を回避するため、MIDI・MP3等のフォーマット定義では「switch-as-boundary」ワークアラウンドが使われている。

```yaml
# 現在のワークアラウンド（MIDI）
- name: track_data
  type: switch
  size: "{length}"
  switch_on: "1"      # 常にtrue — switchを境界スコープのためだけに悪用
  cases:
    "1": midi_event_list
```

本来の意図は「`length` バイトの範囲内で要素をEOFまで繰り返しデコードする」であり、以下のように記述できるべきである。

```yaml
# 望ましい記述
- name: events
  type: struct
  struct: midi_event
  size: "{length}"
  repeat: eof
```

### 影響を受ける既存フォーマット定義

| フォーマット | フィールド | ワークアラウンド |
|---|---|---|
| MIDI | `track_data` | `switch_on: "1"` で境界スコープ作成 |
| MP3 | `id3v2_body` | `switch_on: "1"` で境界スコープ作成 |

## 機能要件

### 追加する機能

- [x] struct型フィールドで `size` と `repeat: eof` を同時に指定した場合、`size` を境界スコープとして使用し、その範囲内でEOFまで繰り返しデコードする
- [x] `repeat: eof` 以外の繰り返しモード（`repeat_while`, `repeat_until`）でも `size` と共存できる

### 変更する既存機能

- [x] `BinaryDecoder` — 配列デコード時に `size` 指定があれば境界スコープを作成してから繰り返しを開始する
- [x] `FormatValidator` — `size` + `repeat` の組み合わせをエラーとしない（元々禁止ルールは存在しなかったため変更不要）

### 変更しないもの（スコープ外）

- 非struct型フィールドの `size` + `repeat` の組み合わせ — 整数型等では `size` はフィールド自体のバイト長のまま
- `element_size` の動作 — 要素ごとの境界スコープは既存のまま

## 受入条件

1. [x] `size` + `repeat: eof` を指定したstruct型フィールドが、`size` バイト分の境界内でEOFまでデコードされること
2. [x] `size` + `repeat_while` を指定した場合も境界スコープ内で動作すること
3. [x] `size` + `repeat_until` を指定した場合も境界スコープ内で動作すること
4. [x] 境界を超えるデコードが発生した場合にエラーになること（PushScopeの既存メカニズムで担保）
5. [x] MIDI・MP3のフォーマット定義から `switch_on: "1"` ワークアラウンドを除去して正常に動作すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全1,094テスト通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（FormatValidatorに禁止ルールは元々存在しない） |
| BinAnalyzer.Dsl | 変更なし（YAMLマッピングは既に両プロパティを独立に処理） |
| BinAnalyzer.Engine | `BinaryDecoder` — 配列デコード前の境界スコープ作成ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — size+repeatの共存セマンティクスを記載
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

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

- `FormatValidator` の変更は不要だった（`size` + `repeat` を禁止するルールは元々存在しない）
- `WithoutRepeat` / `WithoutRepeatAndSeek` で各要素フィールドから `Size`/`SizeExpression`/`SizeRemaining` を除去し、二重スコープを防止
- MIDI: 中間struct `midi_event_list` を削除、フィールド名 `track_data` → `events` に変更
- MP3: 中間struct `id3v2_frames` を削除、フィールド名 `id3v2_body` → `frames` に変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SizeRepeatCoexistenceTests | SizeWithRepeatEof_CreatesBoundaryScope | 1 |
| SizeRepeatCoexistenceTests | SizeExpressionWithRepeatEof_CreatesBoundaryScope | 1 |
| SizeRepeatCoexistenceTests | SizeRemainingWithRepeatEof_BehavesAsPlainRepeatEof | 6 |
| SizeRepeatCoexistenceTests | SizeWithRepeatCount_AdvancesToScopeEnd | 1, 4 |
| SizeRepeatCoexistenceTests | SizeWithRepeatWhile_RemainingRefersToScope | 2 |
| SizeRepeatCoexistenceTests | SizeWithRepeatUntilValue_StopsAtConditionOrScopeEnd | 3 |
| SizeRepeatCoexistenceTests | SizeAndElementSize_BothWork | 1 |
| SizeRepeatCoexistenceTests | NoSizeWithRepeat_BehaviorUnchanged | 6 |
| MidiParsingTests | (既存5テスト更新) | 5, 6 |
| Mp3ParsingTests | (既存4テスト — 変更なし) | 5, 6 |
| GoldenFileTests | midi, mp3 ゴールデンファイル再生成 | 5, 6 |

### 気づき・今後の課題

- `repeat_count` + `size` の組み合わせでは、要素が境界を使い切らなくても `PopScope` でスコープ終端まで位置が進む。これは意図した動作（switchの `size` と同じセマンティクス）
