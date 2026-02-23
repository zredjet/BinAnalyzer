# REQ-145: ビットストリーム内での条件分岐・繰り返し

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-22 |

## 背景・動機

現在のビットストリームモード（`bitstream: true`）では、構造体内のフィールドは全て整数型でなければならない。`if` による条件分岐、`switch` による型切り替え、`repeat` による繰り返しはサポートされていない。

この制約により、以下のようなビットレベルの構造を持つフォーマットの解析が困難である。

### 影響を受けるフォーマット

| フォーマット | 構造 | 必要な制御フロー |
|---|---|---|
| H.264/H.265 NAL | ビットレベルのパラメータセット | 条件分岐（profile_idcによるフィールド有無） |
| MPEG Audio | ビットレベルヘッダ + サイドインフォメーション | 条件分岐（チャンネルモードによるフィールド構成変化） |
| JPEG | ハフマンテーブル | 繰り返し（ビット単位でコード長テーブルを読む） |
| VP8/VP9 | ビットストリームヘッダ | 条件分岐 + 繰り返し |

## 機能要件

### 追加する機能

- [x] ビットストリーム内での `if` 条件分岐 — 条件によりビットフィールドの読み取りをスキップ
- [x] ビットストリーム内での `switch` — ビット値に基づく構造体の切り替え（切り替え先もビットストリーム）
- [x] ビットストリーム内での `repeat_count` — 指定回数のビットフィールド繰り返し
- [x] ビットストリーム内での `virtual` フィールド — ビット値の計算

### 変更する既存機能

- [x] `BinaryDecoder` — ビットストリームモード中のif/switch/repeat処理を有効化
- [x] `FormatValidator` — ビットストリーム構造体内の非整数型フィールドの制限を緩和（if/switch/virtual/structを許可）

### 変更しないもの（スコープ外）

- バイト単位のデコードとの混在（ビットストリーム内で突然バイトモードに戻る） — スコープ外
- ビットストリーム内の圧縮フィールド — スコープ外
- ビットストリーム内の文字列フィールド — スコープ外

## 受入条件

1. [x] ビットストリーム構造体内で `if` によるフィールドスキップが動作すること
2. [x] ビットストリーム構造体内で `switch` による構造体切り替えが動作すること（切り替え先もビットストリーム）
3. [x] ビットストリーム構造体内で `repeat_count` によるフィールド繰り返しが動作すること
4. [x] ビットストリーム構造体内で `virtual` フィールドが動作すること
5. [x] ビットオフセットが制御フロー後も正しく追跡されること
6. [x] 既存のビットストリーム動作（整数フィールドのみ）が維持されること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FormatValidator` — ビットストリーム内の許可型リスト拡張 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `BinaryDecoder` — ビットストリームモードでのif/switch/repeat/virtual処理 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — ビットストリームモードの制御フロー説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- `DecodeContext` の `EnterBitstreamMode`/`ExitBitstreamMode` を再入可能にし、`_bitstreamDepth` カウンターでネスト管理。最外層のみ `BitReader` の生成・破棄とバイトアラインを行う。
- `YamlToIrMapper` の許可型リストを拡張し、`virtual`/`switch`/`struct` をビットストリーム構造体で許可。
- `BinaryDecoder` のバイト単位スコープ制御（`PushScope`/`PopScope`）をビットストリームモード中はスキップ。ビットストリームモードではビット単位でストリーミング読み取りするため、バイト境界のスコープは不適切。
- `repeat_count` ではフィールドの `Size` がビット幅を表すため、`WithoutRepeat` でサイズを剥がした後に復元する。

### モデル変更

なし（既存の `FieldType.Virtual`/`FieldType.Switch`/`FieldType.Struct` をそのまま利用）。

### インタフェース変更

なし（`DecodeContext` の公開 API は変わらない）。

### 懸念事項

- ビットストリーム構造体の switch/struct が非ビットストリーム構造体を参照する場合、バイト単位デコードとビットストリームが混在する。VAL118 警告で注意喚起するが、実行時の動作は未定義。

---

## 実装メモ

### 実装中の設計変更

- `WithoutRepeat` がフィールドの `Size`/`SizeExpression` を null にするため、ビットストリームモードの `repeat_count` で要素デコード時にサイズ不明エラーが発生。→ `DecodeRepeatedField` でビットストリームモード時に元のサイズを復元するロジックを追加。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitstreamDecoderTests | IfCondition_SkipsField_InBitstream | AC1 |
| BitstreamDecoderTests | IfCondition_IncludesField_InBitstream | AC1 |
| BitstreamDecoderTests | RepeatCount_IntegerFields_InBitstream | AC3 |
| BitstreamDecoderTests | Switch_SelectsBitstreamStruct | AC2 |
| BitstreamDecoderTests | Virtual_ComputedValue_InBitstream | AC4 |
| BitstreamDecoderTests | NestedBitstreamStruct_ContinuesBitPosition | AC5 |
| BitstreamDecoderTests | BitOffset_CorrectAfterControlFlow | AC5 |
| BitstreamParsingTests | Load_BitstreamWithVirtual_Succeeds | AC4 |
| BitstreamParsingTests | Load_BitstreamWithSwitch_Succeeds | AC2 |
| BitstreamParsingTests | Load_BitstreamWithStruct_Succeeds | AC5 |
| FormatValidatorTests | Validate_BitstreamStructRefToNonBitstream_Warning | - |
| FormatValidatorTests | Validate_BitstreamSwitchToNonBitstream_Warning | - |
| FormatValidatorTests | Validate_BitstreamStructRefToBitstream_NoWarning | - |

### 気づき・今後の課題

- `repeat: eof` / `repeat: while` のビットストリーム対応は今回スコープ外。ビット単位の「残りサイズ」概念が必要。
- ビットストリーム↔非ビットストリーム混在時の挙動は未定義。将来的にエラーに昇格する可能性あり。
