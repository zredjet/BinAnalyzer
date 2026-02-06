# REQ-017: null終端文字列型の追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットではC言語由来のnull終端文字列（NUL文字 `0x00` で終わる可変長文字列）が使用される。例:

- ELFのセクション名テーブル（`.shstrtab`）
- PEのインポート関数名
- ZIPの拡張フィールド内の文字列
- 各種ファイルフォーマットのメタデータ

現在のDSLではサイズを事前に指定する必要があり（`size: 固定値` または `size: "{式}"`）、null終端の可変長文字列を直接表現できない。

## 機能要件

### 追加する機能

- [ ] `FieldType.AsciiZ` 型の追加（null終端ASCII文字列）
  - NUL文字（0x00）まで読み取り、NUL文字を消費するがデコード結果には含めない
  - サイズ指定なしで使用可能
- [ ] `FieldType.Utf8Z` 型の追加（null終端UTF-8文字列）
  - 同上、UTF-8エンコーディングで解釈
- [ ] DecodeContextに `ReadAsciiUntilNull()` / `ReadStringUntilNull(Encoding)` メソッドの追加
- [ ] DSLでの使用
  ```yaml
  - name: section_name
    type: asciiz
  ```

### 変更する既存機能

- [ ] FieldType enum: `AsciiZ`, `Utf8Z` の追加
- [ ] BinaryDecoder: null終端文字列のデコード処理追加
- [ ] YamlToIrMapper: `asciiz`, `utf8z` 型名のマッピング追加
- [ ] TreeOutputFormatter: 既存の文字列表示をそのまま使用（変更不要の見込み）

### 変更しないもの（スコープ外）

- UTF-16のnull終端（2バイトNUL） — 将来課題
- Shift_JIS/Latin1のnull終端 — 将来課題
- サイズ上限の指定（`max_size` 等） — 将来課題

## 受入条件

1. [ ] `asciiz` 型フィールドがNUL文字まで正しく読み取れること
2. [ ] デコード結果の `Value` にNUL文字が含まれないこと
3. [ ] デコード結果の `Size` にNUL文字分の1バイトが含まれること
4. [ ] `utf8z` 型フィールドがNUL文字まで正しく読み取れること（マルチバイト文字対応）
5. [ ] 連続する複数のnull終端文字列が正しくデコードできること
6. [ ] データ末尾にNUL文字がない場合、データ終端まで読み取ること（エラーではなく寛容に処理）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldType enum に AsciiZ, Utf8Z 追加 |
| BinAnalyzer.Dsl | YamlToIrMapper に型マッピング追加 |
| BinAnalyzer.Engine | DecodeContext に ReadUntilNull 追加、BinaryDecoder にデコード処理追加 |
| BinAnalyzer.Output | 変更なし（既存の DecodedString 表示を使用） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型一覧に asciiz, utf8z 追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要
- [ ] docs/parser-design.md — フィールド型マッピング表の更新

---

## 設計メモ

### 設計方針

既存の文字列デコードパターン（`DecodeStringField`）に沿い、サイズ指定不要のnull終端読み取りメソッドを `DecodeContext` に追加する。デコード結果は既存の `DecodedString` をそのまま使用する。

### モデル変更

**FieldType enum に追加:**
```csharp
AsciiZ,   // null終端ASCII
Utf8Z,    // null終端UTF-8
```

### DecodeContext への追加

```csharp
public string ReadAsciiUntilNull()
{
    var start = _position;
    while (_position < CurrentScope.End && _data.Span[_position] != 0)
        _position++;
    var value = Encoding.ASCII.GetString(_data.Span[start.._position]);
    if (_position < CurrentScope.End)
        _position++; // NUL文字を消費
    return value;
}

public string ReadStringUntilNull(Encoding encoding)
{
    var start = _position;
    while (_position < CurrentScope.End && _data.Span[_position] != 0)
        _position++;
    var value = encoding.GetString(_data.Span[start.._position]);
    if (_position < CurrentScope.End)
        _position++; // NUL文字を消費
    return value;
}
```

### BinaryDecoder の変更

`DecodeSingleField` の switch に追加:
```csharp
FieldType.AsciiZ => DecodeNullTerminatedStringField(field, context, Encoding.ASCII, "asciiz"),
FieldType.Utf8Z => DecodeNullTerminatedStringField(field, context, Encoding.UTF8, "utf8z"),
```

新規メソッド `DecodeNullTerminatedStringField`:
- `context.ReadAsciiUntilNull()` / `ReadStringUntilNull()` を呼び出し
- `Size` にはNUL文字を含む全消費バイト数を設定
- 変数バインディングあり（`context.SetVariable`）

### YamlToIrMapper の変更

`ParseFieldType` に追加:
```
"asciiz" → FieldType.AsciiZ
"utf8z"  → FieldType.Utf8Z
```

### インタフェース変更

なし。`DecodedString` を再利用するため、出力フォーマッターへの変更も不要。

### 代替案

**案A: サイズ指定 + NULトリミング（不採用）**
- `type: ascii` + `size: N` + `trim_null: true` のようなオプション。汎用的だが冗長。独立した型として定義する方が明快。

### 懸念事項

1. **マルチバイトNUL**: UTF-8ではNULバイト（0x00）は常に1バイトなのでバイト単位の走査で問題ない。UTF-16のnull終端（2バイト）はスコープ外。

2. **NUL文字がない場合の挙動**: スコープ末尾まで読み取って文字列を返す。エラーにはしない（寛容な処理）。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
