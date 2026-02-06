# パーサー詳細設計

BinAnalyzerのパーサーは2つの独立した層で構成される。

1. **DSLパーサー** — YAML形式のフォーマット定義ファイル（`.bdef.yaml`）をIR（中間表現）に変換
2. **式パーサー** — DSL内の動的式（`{length - 4}` 等）を解析してASTを生成

本ドキュメントでは両パーサーの内部設計、データフロー、エラー処理を詳述する。

---

## 1. 全体データフロー

```
.bdef.yaml テキスト
    │
    ▼
┌─────────────────────┐
│ YamlDotNet           │  YAML文法のデシリアライズ（外部ライブラリ）
│ Deserializer         │
└────────┬────────────┘
         │ YamlFormatModel（DTO）
         ▼
┌─────────────────────┐
│ YamlToIrMapper       │  DTO → IR変換、式パース、検証
│                      │
│  ┌─────────────────┐ │
│  │ ExpressionParser │ │  size/repeat/switch内の式をAST化
│  └─────────────────┘ │
└────────┬────────────┘
         │ FormatDefinition（IR）
         ▼
    デコーダーエンジンへ
```

### 責務の分離

| コンポーネント | 配置先 | 役割 |
|---|---|---|
| `YamlFormatLoader` | Dsl | ファイル読み込み → デシリアライズ → マッピング呼び出し |
| `YamlToIrMapper` | Dsl | YAML DTO → IR変換の本体 |
| `FormatValidator` | Core | IR変換後の静的バリデーション（参照整合性、未使用定義検出等） |
| `ExpressionTokenizer` | Core | 式文字列の字句解析 |
| `ExpressionParser` | Core | トークン列からASTを構築 |
| `ExpressionEvaluator` | Engine | ASTをDecodeContext上で評価 |

式の**定義**（AST）はCoreに、**評価**はEngineに配置される。これはDslプロジェクトも式をパースする必要があり（YAML → IR変換時）、かつEngineへの依存を避けるためである。

---

## 2. DSLパーサー

### 2.1 二層モデル: YAML DTOとIR

YAML構文の変更がエンジンに影響しないよう、デシリアライズ先のDTO（`YamlModels/`）とエンジンが使用するIR（`Core/Models/`）は完全に分離している。

```
YamlFormatModel     →  FormatDefinition
YamlFieldModel      →  FieldDefinition
YamlEnumEntry       →  EnumEntry / EnumDefinition
YamlFlagsModel      →  FlagsDefinition
YamlFlagFieldModel  →  FlagFieldDefinition
```

**利点:**
- YAML側のキー名変更（例: `repeat_count` → `count`）がIRに波及しない
- YamlDotNet固有のアトリビュート（`[YamlMember]`）がCoreに漏れない
- IRは常に正規化された状態を保証できる

### 2.2 YamlFormatLoader

エントリポイント。ファイルパスまたはYAML文字列を受け取り、`FormatDefinition` を返す。

```csharp
public sealed class YamlFormatLoader : IFormatLoader
{
    FormatDefinition Load(string path);        // ファイルから
    FormatDefinition LoadFromString(string yaml); // 文字列から（テスト用）
}
```

内部でYamlDotNetの `IDeserializer` を使用。`UnderscoredNamingConvention` を適用し、YAMLのスネークケースキーをC#プロパティにマッピングする。`IgnoreUnmatchedProperties()` により未知のキーは無視される（前方互換性）。

### 2.3 YamlToIrMapper — 変換ロジック

`YamlToIrMapper.Map()` が変換の本体。以下の処理を順次実行する。

#### エンディアン解決

```
"big" | "be" | null  →  Endianness.Big
"little" | "le"       →  Endianness.Little
その他                 →  InvalidOperationException
```

デフォルトはビッグエンディアン。ネットワークバイトオーダーのフォーマット（PNG等）で自然に使える。

#### 列挙型マッピング

```yaml
enums:
  color_type:
    - value: 0
      label: grayscale
```

→ `Dictionary<string, EnumDefinition>`。各エントリは `(value, label, description?)` のタプル。

#### フラグマッピング

```yaml
flags:
  chunk_type_flags:
    bit_size: 32
    fields:
      - name: ancillary
        bit: 5
        set: "yes"
        clear: "no"
```

→ `Dictionary<string, FlagsDefinition>`。`bit_size` はフラグ全体のビット幅、各フィールドは `(name, bit_position, bit_size, set_meaning?, clear_meaning?)` で定義。

#### フィールドマッピング

1フィールドの変換で以下のサブパーサーが呼ばれる:

| サブパーサー | 入力 | 出力 |
|---|---|---|
| `ParseFieldType` | `type` 文字列 | `FieldType` enum |
| `ParseSize` | `size` 文字列 | `(int?, Expression?, bool)` |
| `ParseRepeatMode` | `repeat`/`repeat_count`/`repeat_until` | `RepeatMode` |
| `ParseSwitch` | `switch_on`/`cases`/`default` | `(Expression?, List<SwitchCase>?, string?)` |
| `ParseChecksum` | `checksum` オブジェクト | `ChecksumSpec?` |
| `ParseBitfieldEntries` | `fields` リスト | `IReadOnlyList<BitfieldEntry>?` |

フィールドには以下の追加プロパティもマッピングされる:

| プロパティ | 入力 | 説明 |
|---|---|---|
| `Condition` | `condition` 文字列（式） | フィールドの条件付きデコード（式がfalseなら読み飛ばす） |
| `Expected` | `expected` バイト列 | バイト列フィールドの期待値（バリデーション用） |
| `Description` | `description` 文字列 | フィールドの説明テキスト |

##### フィールド型の解決

大文字小文字を区別しない。省略形も対応:

```
"uint8" | "u8"           →  FieldType.UInt8
"uint16" | "u16"         →  FieldType.UInt16
"uint32" | "u32"         →  FieldType.UInt32
"uint64" | "u64"         →  FieldType.UInt64
"int8" | "i8"            →  FieldType.Int8
"int16" | "i16"          →  FieldType.Int16
"int32" | "i32"          →  FieldType.Int32
"int64" | "i64"          →  FieldType.Int64
"bytes"                  →  FieldType.Bytes
"ascii"                  →  FieldType.Ascii
"utf8"                   →  FieldType.Utf8
"utf16le" | "utf16-le"   →  FieldType.Utf16Le
"utf16be" | "utf16-be"   →  FieldType.Utf16Be
"sjis" | "shift_jis" | "shift-jis"  →  FieldType.ShiftJis
"latin1" | "iso-8859-1"  →  FieldType.Latin1
"struct"                 →  FieldType.Struct
"switch"                 →  FieldType.Switch
"bitfield"               →  FieldType.Bitfield
```

##### サイズの解決

`size` フィールドは3通りの解釈がある:

```
null         →  (null, null, false)        サイズ指定なし（固定長型は不要）
"remaining"  →  (null, null, true)         スコープ内残りバイト数
"8"          →  (8, null, false)           固定値（整数パース成功）
"{length}"   →  (null, Expression, false)  動的式
```

整数パースを先に試み、失敗した場合のみ式パーサーに委譲する。

##### 繰り返しモードの解決

優先順位: `repeat: eof` → `repeat_count` → `repeat_until` → `None`

```
repeat: eof              →  RepeatMode.UntilEof
repeat_count: "{count}"  →  RepeatMode.Count(式)
repeat_until: "{done}"   →  RepeatMode.UntilValue(式)
（いずれもなし）           →  RepeatMode.None
```

`RepeatMode` は判別共用体（sealed record hierarchy）として実装:

```csharp
public abstract record RepeatMode {
    public sealed record None : RepeatMode;
    public sealed record Count(Expression CountExpression) : RepeatMode;
    public sealed record UntilEof : RepeatMode;
    public sealed record UntilValue(Expression Condition) : RepeatMode;
}
```

##### Switch（条件分岐）の解決

```yaml
switch_on: "{type}"
cases:
  "'IHDR'": ihdr
  "'tEXt'": text_chunk
default: raw_data
```

`cases` の各キーは式としてパースされる。これにより整数値（`"1"`）も文字列リテラル（`"'IHDR'"`）も統一的に扱える。

### 2.4 検証

検証は2段階で行われる。

#### YamlToIrMapper の検証（マッピング時）

`YamlToIrMapper` は変換中に以下のバリデーションを実行する:

- **ルート構造体の存在確認** — `root` で指定された構造体名がstructs内に存在するか
- **エンディアン値の検証** — 不明な値に対して例外をスロー
- **フィールド型の検証** — 不明な型名に対して例外をスロー

#### FormatValidator の検証（マッピング後）

`FormatValidator`（`BinAnalyzer.Core.Validation` 名前空間）がIR変換後に静的検証を実行する。CLIでは `--no-validate` オプションでスキップ可能。

**エラー（VAL001〜VAL007）:**
- VAL001: struct型フィールドに `StructRef` がない
- VAL002: `StructRef` が未定義のstructを参照
- VAL003: switchのcaseが未定義のstructを参照
- VAL004: switchのdefaultが未定義のstructを参照
- VAL005: switch型フィールドに `switch_on` がない
- VAL006: switch型フィールドにcasesもdefaultもない
- VAL007: サイズ指定が必要な型にサイズ指定がない

**警告（VAL101〜VAL109）:**
- VAL101: `EnumRef` が未定義のenumを参照
- VAL102: `FlagsRef` が未定義のflagsを参照
- VAL103: `EnumRef` が整数型以外に使用されている
- VAL104: `FlagsRef` がascii型以外に使用されている
- VAL105: switchにdefaultがない
- VAL106: `StructRef` がstruct/switch型以外に使用されている
- VAL107: 未使用のenum定義
- VAL108: 未使用のflags定義
- VAL109: ルートから到達不能なstruct定義

現時点で実行されない検証（将来拡張候補）:
- 循環参照の検出

---

## 3. 式パーサー

DSL内の `{...}` で囲まれた式を解析するミニ言語処理系。字句解析器（Tokenizer）と構文解析器（Parser）で構成される。

### 3.1 式の用途

| 使用箇所 | 例 | 評価結果の型 |
|---|---|---|
| `size` | `{length}`, `{length - 4}` | long（整数） |
| `repeat_count` | `{count}` | long（整数） |
| `repeat_until` | `{type == 0}` | bool |
| `switch_on` | `{type}` | any（文字列または整数） |
| switchの`cases`キー | `'IHDR'`, `1` | any（switch_onと同型） |

### 3.2 字句解析（ExpressionTokenizer）

入力文字列を `List<ExpressionToken>` に変換する。各トークンは `(Type, Value, Position)` の3つ組。

#### トークン種別

| 種別 | パターン | 例 |
|---|---|---|
| `Integer` | `[0-9]+` または `0x[0-9a-fA-F]+` | `42`, `0xFF` |
| `String` | `'...'`（シングルクォート） | `'IHDR'` |
| `Identifier` | `[a-zA-Z_][a-zA-Z0-9_]*` | `length`, `bit_depth` |
| `Plus` | `+` | |
| `Minus` | `-` | |
| `Star` | `*` | |
| `Slash` | `/` | |
| `Percent` | `%` | |
| `EqualEqual` | `==` | |
| `NotEqual` | `!=` | |
| `LessThan` | `<` | |
| `LessThanOrEqual` | `<=` | |
| `GreaterThan` | `>` | |
| `GreaterThanOrEqual` | `>=` | |
| `And` | `and`（キーワード） | |
| `Or` | `or`（キーワード） | |
| `Not` | `not`（キーワード） | |
| `LeftParen` | `(` | |
| `RightParen` | `)` | |
| `Eof` | 入力終端 | |

#### 字句解析の処理フロー

```
入力文字列を先頭から1文字ずつ走査:

  空白文字     → スキップ
  数字         → 整数リテラル読み取り（0x接頭辞ならば16進数）
  シングルクォート → 閉じクォートまで文字列リテラル読み取り
  英字/_       → 識別子読み取り → キーワード判定（and/or/not）
  演算子文字    → 1文字または2文字演算子の判定（先読み1文字）
  その他        → FormatException
```

**先読み（Peek）**: `<=`, `>=`, `==`, `!=` の判定に1文字の先読みを使用。入力範囲外の場合は `'\0'` を返す。

**16進数リテラル**: `0x` 接頭辞を検出した場合、`[0-9a-fA-F]+` を続けて読み取る。

**エラー処理**: 未知の文字、未閉のシングルクォートに対して `FormatException` をスロー。位置情報を含む。

### 3.3 構文解析（ExpressionParser）

再帰下降パーサー。演算子優先順位をパーサーの呼び出し階層で表現する。

#### 文法（BNF風）

優先順位の低い規則から順に記述:

```
or_expr      ::= and_expr ("or" and_expr)*
and_expr     ::= compare_expr ("and" compare_expr)*
compare_expr ::= add_expr (("==" | "!=" | "<" | "<=" | ">" | ">=") add_expr)?
add_expr     ::= mul_expr (("+" | "-") mul_expr)*
mul_expr     ::= unary_expr (("*" | "/" | "%") unary_expr)*
unary_expr   ::= ("-" | "not") unary_expr | primary
primary      ::= INTEGER | STRING | IDENTIFIER | "(" or_expr ")"
```

#### 演算子優先順位表

| 優先度 | 演算子 | 結合性 | 対応メソッド |
|--------|--------|--------|------------|
| 1（最低） | `or` | 左 | `ParseOrExpr` |
| 2 | `and` | 左 | `ParseAndExpr` |
| 3 | `==` `!=` `<` `<=` `>` `>=` | なし（単一） | `ParseCompareExpr` |
| 4 | `+` `-` | 左 | `ParseAddExpr` |
| 5 | `*` `/` `%` | 左 | `ParseMulExpr` |
| 6（最高） | `-`（単項） `not` | 右 | `ParseUnaryExpr` |

**比較演算子は非結合**: `a < b < c` は構文エラーにならないが、`(a < b) < c`（bool < int）として評価され、実用上は意味をなさない。チェーン比較は設計上サポートしない。

#### パース処理の例

入力: `{length - 4}`

```
1. Parse()
   └─ 波括弧を除去 → "length - 4"
   └─ Tokenize → [Identifier("length"), Minus, Integer("4"), Eof]
   └─ ParseOrExpr()
      └─ ParseAndExpr()
         └─ ParseCompareExpr()
            └─ ParseAddExpr()
               ├─ ParseMulExpr()
               │  └─ ParseUnaryExpr()
               │     └─ ParsePrimary() → FieldReference("length")
               ├─ Minus を消費
               └─ ParseMulExpr()
                  └─ ParseUnaryExpr()
                     └─ ParsePrimary() → LiteralInt(4)
               └─ BinaryOp(FieldReference("length"), Subtract, LiteralInt(4))
```

結果のAST:
```
BinaryOp
├── Left: FieldReference("length")
├── Operator: Subtract
└── Right: LiteralInt(4)
```

#### 波括弧の処理

`ExpressionParser.Parse()` は入力が `{...}` で囲まれている場合、自動的に除去する。これにより、DSLから呼ばれる場合（`"{length}"`）と直接呼ばれる場合（`"length"`）の両方に対応する。

### 3.4 AST（抽象構文木）

`ExpressionNode` は判別共用体（sealed record hierarchy）として実装:

```csharp
ExpressionNode
├── LiteralInt(long Value)        // 整数リテラル: 42, 0xFF
├── LiteralString(string Value)   // 文字列リテラル: 'IHDR'
├── FieldReference(string FieldName)  // フィールド参照: length
├── BinaryOp(Left, Operator, Right)   // 二項演算: a + b
└── UnaryOp(Operator, Operand)        // 単項演算: -x, not y
```

`Expression` レコードはパース済みAST（`Root`）と元のテキスト（`OriginalText`）を保持する。元テキストはデバッグ・エラーメッセージ用。

### 3.5 式の評価（ExpressionEvaluator）

ASTをDecodeContextの変数環境上で評価する。Engineプロジェクトに配置。

#### 評価の型システム

式の評価結果は `object` 型で返される。内部的には以下の型を使用:

| AST種別 | 評価結果の型 |
|---|---|
| `LiteralInt` | `long` |
| `LiteralString` | `string` |
| `FieldReference` | デコード時にバインドされた値（`long` または `string`） |
| 算術演算 | `long` |
| 比較演算 | `bool` |
| 論理演算 | `bool` |
| 単項否定 | `long` |
| 単項NOT | `bool` |

#### 型変換ルール

**整数変換** (`ConvertToLong`): `long`, `int`, `uint`, `byte`, `sbyte`, `short`, `ushort`, `ulong` を `long` に変換。デコーダーが生成する多様な整数型に対応。

**ブール変換** (`ConvertToBool`): `bool` はそのまま、`long`/`int` は非ゼロで `true`。

**等値比較** (`AreEqual`): 両辺が `string` なら文字列比較。それ以外は整数変換を試み、失敗なら `Object.Equals`。switchのcase照合（`type == 'IHDR'`）で文字列同士の比較が必須。

#### 変数解決

`FieldReference` ノードの評価時、`DecodeContext.GetVariable()` を呼び出す。スコープスタックを上から順に検索し、最初に見つかった値を返す。見つからなければ `InvalidOperationException`。

```
スコープスタック（上が現在のスコープ）:
  [3] switchスコープ:  { }
  [2] チャンクスコープ: { length=13, type="IHDR" }
  [1] ルートスコープ:   { }

GetVariable("length") → スコープ[3]になし → スコープ[2]で13を発見 → 13を返す
GetVariable("missing") → 全スコープになし → 例外
```

---

## 4. エラー処理

### 4.1 パース時エラー（静的）

DSL読み込み時に検出されるエラー。`FormatException` または `InvalidOperationException` をスロー。

| エラー | 発生箇所 | メッセージ例 |
|---|---|---|
| 不明な文字 | ExpressionTokenizer | `Unexpected character '@' at position 5` |
| 未閉の文字列 | ExpressionTokenizer | `Unterminated string literal at position 0` |
| 予期しないトークン | ExpressionParser | `Unexpected token '+' at position 0` |
| 閉じ括弧の欠落 | ExpressionParser | `Expected ')' at position 8` |
| 不明なフィールド型 | YamlToIrMapper | `Unknown field type: float32` |
| 不明なエンディアン | YamlToIrMapper | `Unknown endianness: middle` |
| ルート構造体の不在 | YamlToIrMapper | `Root struct 'main' not found in struct definitions` |

### 4.2 評価時エラー（動的）

バイナリデコード時に検出されるエラー。`ExpressionEvaluator` 内部では `InvalidOperationException` をスローするが、`BinaryDecoder` の `DecodeField` メソッドがこれをキャッチし、フィールドパスとオフセット情報を付加した `DecodeException` でラップして再スローする。

| エラー | 発生箇所 | メッセージ例 |
|---|---|---|
| 未定義変数 | ExpressionEvaluator | `Variable 'length' not found in current scope` |
| 型変換失敗 | ExpressionEvaluator | `Cannot convert String ('IHDR') to integer` |
| ブール変換失敗 | ExpressionEvaluator | `Cannot convert String to boolean` |
| データ不足 | DecodeContext | `ArgumentOutOfRangeException`（DecodeExceptionにラップ） |

`DecodeException` は以下の情報を保持する:
- `Offset` — エラー発生時のバイトオフセット
- `FieldPath` — ネストしたフィールドパス（例: `header.big_field`）
- `FieldType` — エラーが発生したフィールドの型名
- `Hint` — 任意のヒントメッセージ

CLIでは `DecodeException` をキャッチし、`FormatMessage()` で構造化されたエラーメッセージを表示する。

---

## 5. 拡張ポイント

現在の式パーサーは意図的にシンプルに保たれている。将来的な拡張候補:

| 拡張 | 影響範囲 | 備考 |
|---|---|---|
| ビット演算（`&`, `\|`, `^`, `<<`, `>>`） | Tokenizer + Parser + Evaluator | バイナリ解析で有用 |
| 三項演算子（`a ? b : c`） | Parser + Evaluator | switchのインライン分岐 |
| 組み込み関数（`sizeof()`, `offset()`） | 全層 | メタ情報へのアクセス |
| ドット記法（`header.length`） | Tokenizer + Parser + Evaluator | ネスト構造体のフィールド参照 |
| エラーリカバリ | Parser | 複数エラーの一括報告 |

式パーサーのアーキテクチャ（Tokenizer → Parser → AST → Evaluator）はこれらの拡張に対して開かれている。新しいトークン種別の追加、文法規則の追加、評価ルールの追加がそれぞれ独立して行える。
