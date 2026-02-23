# REQ-142: ステートフル変数

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-22 |

## 背景・動機

一部のバイナリフォーマットでは、前のレコードの状態が次のレコードのデコードに影響する「ステートフル」なパースが必要になる。

### 典型例: MIDIランニングステータス

MIDIファイルフォーマットでは、イベントのステータスバイト（コマンド種別）を省略できる「ランニングステータス」が仕様に含まれる。前のイベントと同じステータスバイトの場合、バイトを省略してデータバイトのみを記述する。

```
# 通常: ステータスバイト + データバイト
90 3C 7F   # Note On, C4, velocity 127
90 40 7F   # Note On, E4, velocity 127

# ランニングステータス: ステータスバイトを省略
90 3C 7F   # Note On, C4, velocity 127
40 7F      # 前のステータス(90)を引き継ぎ → Note On, E4, velocity 127
```

現在のDSLではフィールドのデコード順は前方参照のみ可能だが、「前の繰り返し要素の値を記憶して次の要素で参照する」仕組みがない。

### 他のステートフルパターン

| フォーマット | パターン | 説明 |
|---|---|---|
| MIDI | ランニングステータス | 前のイベントのステータスバイトを引き継ぐ |
| Protobuf | パックされた繰り返しフィールド | 前のフィールド番号によってデコード方法が変わる |
| DEFLATE | ハフマンテーブル | 前のブロックのテーブルが次のブロックに影響 |

## 機能要件

### 追加する機能

- [x] `state` プロパティ — フィールドの値を名前付き状態変数として保存する（`state: "last_status"`）
- [x] 状態変数の参照 — 式内で `@state_name` として参照可能
- [x] 状態変数の条件付き更新 — `state_if: "{condition}"` で条件を満たすときのみ状態を更新
- [x] 状態変数の初期値 — `state_default: value` でデコード開始時の初期値を指定

### 変更する既存機能

- [x] `DecodeContext` — 状態変数ストア（スコープとは独立、永続的）を追加
- [x] `ExpressionEvaluator` — `@` プレフィックス付き変数の評価
- [x] `ExpressionParser` — `@` プレフィックス付き変数のパース

### 変更しないもの（スコープ外）

- 通常の変数スコープの動作 — 変更なし
- 状態変数のファイル横断永続化 — スコープはデコードセッション内

## 受入条件

1. [x] `state: "name"` でフィールド値が状態変数に保存されること
2. [x] `@name` で状態変数を式から参照できること
3. [x] 状態変数がrepeatの各イテレーションをまたいで維持されること
4. [x] `state_if` で条件付き更新が動作すること
5. [x] `state_default` で初期値が設定されること
6. [x] MIDIランニングステータスの解析が可能になること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` — `State`, `StateIf`, `StateDefault` プロパティ追加。`ExpressionParser` — `@` プレフィックス |
| BinAnalyzer.Dsl | `YamlFieldModel` — `state`, `state_if`, `state_default` マッピング |
| BinAnalyzer.Engine | `DecodeContext` — 状態変数ストア。`ExpressionEvaluator` — `@` 変数の評価。`BinaryDecoder` — 状態変数の更新 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — ステートフル変数の説明追加
- [x] docs/architecture.md — 状態管理の説明追加
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- `@` プレフィックスによる名前空間分離: 通常のフィールド変数 `{field_name}` との名前衝突を防止
- `DecodeContext` にスコープスタックとは独立した `Dictionary<string, object>` を追加（`_stringTables` と同パターン）
- 状態更新タイミング: デコード前に `state_default` 初期化、デコード後に `state_if` 条件付き保存

### モデル変更

- `FieldDefinition` に `State`, `StateIf`, `StateDefault` プロパティ追加
- `ExpressionNode.StateReference` ASTノード追加
- `ExpressionTokenType.AtIdentifier` トークン型追加

### インタフェース変更

なし（内部実装のみ）

### 代替案
### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

なし。計画通り実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| StateReferenceExpressionTests | Tokenizer_AtPrefix_ProducesAtIdentifierToken | 2 |
| StateReferenceExpressionTests | Tokenizer_AtWithoutName_ThrowsFormatException | 2 |
| StateReferenceExpressionTests | Tokenizer_AtAtEndOfInput_ThrowsFormatException | 2 |
| StateReferenceExpressionTests | Parser_AtPrefix_ProducesStateReference | 2 |
| StateReferenceExpressionTests | Parser_AtInComplexExpression | 2 |
| StateReferenceExpressionTests | Parser_AtInArithmetic | 2 |
| StateReferenceExpressionTests | Tokenizer_AtWithUnderscore_ProducesAtIdentifierToken | 2 |
| StatefulVariableTests | State_SavesFieldValue | 1 |
| StatefulVariableTests | StateRef_ReferencesStateVariable | 2 |
| StatefulVariableTests | State_PersistsAcrossRepeatIterations | 3 |
| StatefulVariableTests | StateIf_ConditionalUpdate_TrueCase | 4 |
| StatefulVariableTests | StateIf_ConditionalUpdate_FalseCase | 4 |
| StatefulVariableTests | StateDefault_ProvidesInitialValue | 5 |
| StatefulVariableTests | StateDefault_NotOverwrittenOnSubsequentIterations | 5 |
| StatefulVariableTests | UndefinedStateRef_ThrowsClearError | 2 |
| StatefulVariableTests | RunningStatus_EndToEnd | 6 |
| StatefulVariableTests | State_WorksAcrossNestedStructs | 3 |
| StatefulVariableParsingTests | YamlParsing_State_MapsCorrectly | 1 |
| StatefulVariableParsingTests | YamlParsing_StateDefault_Integer | 5 |
| StatefulVariableParsingTests | YamlParsing_StateOnly_NoCondition | 1 |
| StateValidationTests | StateIf_WithoutState_ReturnsError | — |
| StateValidationTests | StateDefault_WithoutState_ReturnsWarning | — |
| StateValidationTests | State_WithStateIf_NoError | — |
| StateValidationTests | State_WithStateDefault_NoWarning | — |

### 気づき・今後の課題
