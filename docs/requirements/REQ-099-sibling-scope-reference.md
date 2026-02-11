# REQ-099: 兄弟チャンク参照

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-095（実ファイル検証で需要確認後が望ましい）、REQ-098 完了後推奨 |
| フェーズ | C（中量 DSL 拡張） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

一部のバイナリフォーマットでは、あるチャンク/レコードの解釈が、同じ親コンテナ内の別のチャンク/レコードの値に依存する:

- **AVI**: `strf`（ストリームフォーマット）の内部構造は、直前の `strh`（ストリームヘッダ）の `fccType` に依存する（映像=BITMAPINFOHEADER、音声=WAVEFORMATEX）
- **RIFF 系全般**: LIST 内の複数チャンクが相互に参照し合うパターン
- **ELF**: セクションヘッダの `sh_link` が別セクションのインデックスを参照

現在の DSL ではスコープスタックは「自身 → 親 → 祖父母...」の縦方向のみで、兄弟チャンク（同一親の別の子）の値を参照する手段がない。そのため AVI の `strf` は `bytes remaining` に留まっている。

## 機能要件

### 追加する機能

- [ ] 繰り返し配列（`repeat: eof` / `repeat: count` / `repeat: until`）の各要素デコード後、構造体要素内のスカラーフィールド値を親スコープに自動プロモートする
  - プロモート対象: integer, string, float, virtual, bitfield のスカラー値
  - プロモートしない: struct, array 自体（子のスカラーは再帰的にプロモート）
  - 同名変数は後の要素の値で上書きされる（通常の変数解決と同じセマンティクス）
- [ ] 後続要素から、プロモートされた変数を通常の `{変数名}` 式で参照できる
  ```yaml
  # 使用例: AVI の strl LIST 内
  - name: data
    type: switch
    switch_on: "{fccType}"   # 前の要素(strh)からプロモートされた変数
    cases:
      "'vids'": bitmap_info_header
      "'auds'": wave_format_ex
    default: raw_data
  ```
- [ ] 新しい DSL 構文・式構文の追加は不要（既存の `{変数名}` 参照のみで動作）

### 変更しないもの（スコープ外）

- 任意の深さのネスト構造体間の参照（繰り返し配列内の兄弟要素に限定）
- 前方参照（まだデコードされていないフィールドの値）
- struct/array 自体のプロモート（スカラー値のみ）
- FormatValidator での静的検証（既存の変数参照と同じ動的解決）

## 受入条件

1. [ ] 繰り返し配列内で、前要素の境界スコープ（boundary scope）内フィールド値を後続要素から `{変数名}` で参照できること
2. [ ] AVI の `strf` が `strh.fccType` に基づいて構造体を switch 選択できること
3. [ ] 既存の seek + seek_restore パターンが引き続き動作すること
4. [ ] 既存テストが全て通過すること
5. [ ] プロモートされる変数はスカラー値のみであること（struct/array 自体はプロモートしない）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | BinaryDecoder に `PromoteDecodedValues` メソッド追加、`DecodeRepeatedField` の全 RepeatMode 分岐で呼び出し |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| formats/avi.bdef.yaml | stream_format を switch 化（fccType ベース） |

---

## 設計メモ

### 設計方針: デコード済みツリーからのスカラー変数プロモート

繰り返しループで各要素（struct 型）をデコードした後、DecodedNode ツリーを再帰走査し、スカラー値（integer/string/float/virtual/bitfield）を親スコープに SetVariable する。これにより、境界スコープ内で失われた変数が復元され、次の要素から参照可能になる。

**動作イメージ**:
```
repeat: eof で riff_chunk を繰り返し:
  要素0 (strh):
    DecodeStruct(riff_chunk) → chunk_id="strh", chunk_size=N
    data switch → PushScope(N) → DecodeStruct(stream_header) → fccType="vids" → PopScope() → fccType 消滅
    要素デコード完了 → PromoteDecodedValues(decoded_tree, context)
    → fccType="vids" が親スコープに復元
  要素1 (strf):
    DecodeStruct(riff_chunk) → chunk_id="strf", chunk_size=M
    data switch → stream_format 内で switch_on: "{fccType}" → "vids" → bitmap_info_header にディスパッチ!
```

**REQ-098 との一貫性**: REQ-098 ではスカラー配列の要素値を自動的に `List<object>` として親スコープに保存した。本 REQ も同じ「繰り返しループ内での自動変数保存」パターン。

### 代替案

| 案 | 概要 | 不採用理由 |
|---|---|---|
| `_prev.fccType` ドット記法 | 前要素のフィールドを `_prev.field` で参照 | 式エンジンにドット記法（PropertyAccess ノード）の追加が必要。大きな式拡張になる |
| `_siblings[i].field` 配列+ドット記法 | 任意の兄弟要素のフィールドを参照 | ドット記法 + 全要素の辞書保持が必要。複雑すぎる |
| `export: true` DSL プロパティ | フィールドに明示的なエクスポート指示 | 新 YAML プロパティの追加が必要。フォーマット定義側に変更が波及 |
| PopScope 時の変数バブルアップ | スコープ破棄時に変数を親に昇格 | スコープ分離の根本セマンティクスが変わり、既存動作への影響が大きい |

推奨案（PromoteDecodedValues）の利点:

- 新 DSL 構文不要
- 新式構文不要
- エンジンのみの変更（BinaryDecoder に 1 メソッド追加 + 各 RepeatMode 分岐に 1 行追加）
- デコード済みツリーという既存の「真実のソース」を使うため、スコープ機構を変更しない

### モデル変更

なし。FieldDefinition、ExpressionNode、DecodeContext のパブリック API に変更なし。

### 実装の詳細

**新メソッド** `BinaryDecoder.PromoteDecodedValues`:
```csharp
private static void PromoteDecodedValues(DecodedNode node, DecodeContext context)
{
    switch (node)
    {
        case DecodedInteger di:
            context.SetVariable(di.Name, di.Value);
            break;
        case DecodedString ds:
            context.SetVariable(ds.Name, ds.Value);
            break;
        case DecodedFloat df:
            context.SetVariable(df.Name, df.Value);
            break;
        case DecodedVirtual dv:
            context.SetVariable(dv.Name, dv.Value);
            break;
        case DecodedBitfield bf:
            context.SetVariable(bf.Name, bf.RawValue);
            foreach (var field in bf.Fields)
                context.SetVariable(field.Name, field.Value);
            break;
        case DecodedStruct st:
            foreach (var child in st.Children)
                PromoteDecodedValues(child, context);
            break;
        case DecodedArray arr:
            // スカラー配列は REQ-098 で既に処理済み。struct 配列の内部は走査しない
            break;
    }
}
```

**呼び出し箇所**: `DecodeRepeatedField` の全 RepeatMode 分岐で、要素デコード後に呼び出し:
```csharp
var element = DecodeElementWithScope(singleField, format, context, elementSize);
elements.Add(element);

// 構造体要素のスカラー変数を親スコープにプロモート（兄弟参照用）
if (element is DecodedStruct)
    PromoteDecodedValues(element, context);
```

**制約**: `DecodedStruct` の場合のみプロモート。スカラー配列（DecodedInteger の繰り返し等）は REQ-098 の `List<object>` 保存で既にカバー済み。

### 懸念事項

- **名前衝突**: 異なる構造体型の要素が同名フィールドを持つ場合、後の要素の値で上書きされる。ただしこれは通常の変数解決と同じ動作であり、意図通り。
- **パフォーマンス**: 各要素デコード後にツリー走査が追加されるが、SetVariable は O(1) の辞書操作で、典型的な構造体のフィールド数は 10-30 程度。無視できるコスト。
- **意図しない変数リーク**: 深くネストした構造体のフィールドもすべてプロモートされる。ただし、既存のフォーマット定義で `repeat: eof` の構造体要素が深いネストを持つ場合でも、同名フィールドが偶然参照されるリスクは低い（式中で未使用の変数は無害）。

---

## 実装メモ

### 実装中の設計変更

設計メモ通りに実装。設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SiblingReferenceTests | RepeatEof_StructElement_PromotesScalarVariables | 1, 2 |
| SiblingReferenceTests | RepeatCount_StructElement_PromotesScalarVariables | 1 |
| SiblingReferenceTests | RepeatEof_StructElement_LaterElementOverwritesVariable | 1（名前衝突セマンティクス） |
| SiblingReferenceTests | RepeatEof_NestedStructElement_PromotesDeeplyNestedScalars | 1（境界スコープ内の深いフィールド） |
| SiblingReferenceTests | RepeatEof_ScalarElement_NoPromotion | 5 |
| SiblingReferenceTests | SeekRestore_WithPromotion_StillWorks | 3 |

全テスト通過（受入条件 4 も確認済み）。

### 気づき・今後の課題

- PromoteDecodedValues は `DecodedStruct` の場合のみ呼び出すガードを各 RepeatMode 分岐に入れている。スカラー配列は REQ-098 の `List<object>` 保存で既にカバー済みのため、二重処理は発生しない。
- `DecodedArray` のケースでは子要素を走査しない設計とした。ネストされた配列内の構造体フィールドまでプロモートすると、意図しない変数リークのリスクが高くなるため。
- 今後 AVI の `strf` フォーマット定義を switch 化する際に、本機能を実際に活用する（formats/avi.bdef.yaml の更新は別タスク）。
