# REQ-087: repeat_while 繰り返しモード

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のrepeatモードは `count`（回数指定）、`eof`（スコープ終端まで）、`until`（条件が真になるまで）の3種類。しかし、「要素を先読みして、特定の条件を満たす間だけ繰り返す」パターンに対応できない。

- **LZ4**: データブロック列は `block_size` を読んでから、その値が0なら終了。`repeat_until` は要素全体をデコードした後に条件評価するため、block_size==0の空ブロックもデコードしてしまう
- **GIF**: データブロック列はサブブロックサイズバイト+データの繰り返しで、サイズ0が終端

`repeat_until` との違い:
- `repeat_until`: 要素をデコードした**後**に条件評価。条件が真になった要素も配列に含まれる
- `repeat_while`（新規）: 要素をデコードする**前**に条件評価。条件が偽なら要素をデコードしない

```yaml
# LZ4 block list:
- name: blocks
  type: struct
  struct: lz4_block
  repeat: while
  repeat_while: "remaining > 4"  # 少なくともblock_sizeが読める間

# GIF sub-blocks:
- name: sub_blocks
  type: struct
  struct: gif_sub_block
  repeat: while
  repeat_while: "remaining > 0"
```

## 機能要件

### 追加する機能

- [x] 新しいrepeatモード `while` — 各イテレーション開始前に条件を評価し、真の間だけ繰り返す
- [x] `repeat_while: "{condition}"` — 式で繰り返し条件を指定
- [x] 条件式内で `remaining` が使用可能なこと（REQ-084に依存する場合、その後に実装）
- [x] 条件式内で前のイテレーションの要素フィールドが参照可能なこと

### 変更する既存機能

- [x] `RepeatMode` enum に `While` を追加
- [x] `FieldDefinition`: `RepeatWhile` プロパティ追加（Expression型）
- [x] `BinaryDecoder`: repeatループに `while` モードの処理追加
- [x] `YamlFieldModel`: `repeat_while` 属性のマッピング追加
- [x] `FormatValidator`: `repeat: while` + `repeat_while` の組み合わせ検証

### 変更しないもの（スコープ外）

- 既存の `repeat: until` / `repeat_until` の動作は変更しない
- `repeat: while` で要素数0の場合、空配列を返す（エラーにはしない）
- 無限ループ保護は既存のタイムアウト/位置不変検出に依存

## 受入条件

1. [x] `repeat: while` + `repeat_while` が正しく動作すること
2. [x] 条件が最初から偽の場合、空配列が返ること
3. [x] 条件が途中で偽になった場合、それ以前の要素のみ配列に含まれること
4. [x] `remaining` を条件式で使用できること（REQ-084実装後）
5. [x] 前のイテレーションのフィールド値を条件式で参照できること
6. [x] LZ4フォーマット: データブロック列が正しくパースされること
7. [x] FormatValidator: `repeat: while` に `repeat_while` がない場合エラーになること
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | RepeatMode enum追加、FieldDefinition: RepeatWhileプロパティ追加 |
| BinAnalyzer.Dsl | YamlFieldModel: repeat_whileマッピング |
| BinAnalyzer.Engine | BinaryDecoder: whileモードのrepeatループ |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — repeat_whileモードの説明追加

### 変更が必要なフォーマット定義

- [x] `formats/lz4.bdef.yaml` — データブロック列を `repeat_until` で構造化

### 変更が必要なテスト

- [x] Lz4ParsingTests: ブロック構造の検証追加
- [x] 単体テスト: repeat_whileの各種パターン

---

## 設計メモ

- `repeat_while` の条件は各イテレーション開始**前**に評価。条件が最初から偽なら空配列。
- LZ4のブロック終端パターンは `repeat_until` が自然（block_size==0のEndMarkを読んで停止）。`repeat_while` は `remaining` ベースの条件に適する。
- `repeat_while:` は単独でも `repeat: while` + `repeat_while:` でも使用可能。
- IR上では `RepeatMode.While` は必ず Expression を持つため、FormatValidator での追加バリデーションは不要。YAML→IR変換時に検証済み。

---

## 実装メモ

### 実装中の設計変更

- LZ4フォーマットは `repeat_while` ではなく `repeat_until` を使用。block_size==0のEndMarkパターンは `repeat_until: "{block_size_raw == 0}"` がより自然。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| RepeatWhileTests | While_ConditionFalseFromStart_ReturnsEmptyArray | 2 |
| RepeatWhileTests | While_RemainingCondition_DecodesAllElements | 1, 4 |
| RepeatWhileTests | While_RemainingCondition_StopsMidway | 3 |
| RepeatWhileTests | While_PreviousIterationFieldValue_StopsWhenConditionBecomesFalse | 5 |
| RepeatWhileTests | While_WithStructElements_DecodesCorrectly | 1 |
| RepeatWhileTests | While_WithElementSize_DecodesCorrectly | 1 |
| RepeatWhileTests | While_YamlSyntax_RepeatWhileOnly_ParsesCorrectly | 1 |
| RepeatWhileTests | While_YamlSyntax_RepeatWhileWithRepeat_ParsesCorrectly | 1 |
| RepeatWhileTests | While_YamlSyntax_RepeatWhileWithoutExpression_ThrowsError | 7 |
| FormatValidatorTests | VAL110_RepeatWhileWithElementSize_NoWarning | 1 |
| Lz4ParsingTests | Lz4Format_Blocks_EndMarkOnly | 6 |
| Lz4ParsingTests | Lz4Format_Blocks_WithDataBlock | 6 |

### 気づき・今後の課題
