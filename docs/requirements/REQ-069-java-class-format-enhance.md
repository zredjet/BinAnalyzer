# REQ-069: Java Classフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-041で実装されたJava Classフォーマット定義（`formats/java-class.bdef.yaml`）はコンスタントプールの主要タグ、フィールド/メソッド/属性の基本構造に対応しているが、以下の点が不完全である。

1. **attribute_infoが全てraw bytes**: Code、ConstantValue、Exceptions等の主要属性が内部構造を持つにもかかわらず、全てraw bytesとして扱われている。特にCode属性はメソッドのバイトコード本体を含む最も重要な属性であり、max_stack/max_locals/code_length等の構造情報が抽出できない。
2. **新しいconstant pool tag欠落**: Java 11以降で追加されたCONSTANT_Dynamic(17)、Java 9で追加されたCONSTANT_Module(19)およびCONSTANT_Package(20)がcp_tag enumに含まれておらず、モジュール対応のclassファイルでデコードエラーが発生する可能性がある。

なお、attribute解析においてはattribute_nameをstring_tableで解決するのが理想だが、constant pool参照の解決が複雑なため（cp_utf8のインデックス参照が必要）、本要望ではdescription追記による対応を基本方針とする。

## 機能要件

### 追加する機能

- [ ] Code attribute構造追加
  - attribute_name_indexの値に基づくswitch（または、descriptionでCode attributeの内部構造を説明）
  - max_stack: uint16
  - max_locals: uint16
  - code_length: uint32
  - code: bytes, size: "{code_length}"
  - exception_table_length: uint16
  - exception_table: struct exception_entry, repeat_count: "{exception_table_length}"
    - start_pc: uint16, end_pc: uint16, handler_pc: uint16, catch_type: uint16
  - attributes_count: uint16
  - attributes: struct attribute_info, repeat_count: "{attributes_count}"（再帰的属性）
- [ ] ConstantValue attribute構造追加
  - constantvalue_index: uint16（コンスタントプールへのインデックス）
- [ ] Exceptions attribute構造追加
  - number_of_exceptions: uint16
  - exception_index_table: uint16, repeat_count: "{number_of_exceptions}"
- [ ] cp_tag enum拡充
  - CONSTANT_Dynamic = 17
  - CONSTANT_Module = 19
  - CONSTANT_Package = 20
- [ ] cp_dynamic構造体追加
  - bootstrap_method_attr_index: uint16
  - name_and_type_index: uint16
- [ ] cp_module構造体追加
  - name_index: uint16
- [ ] cp_package構造体追加
  - name_index: uint16
- [ ] attribute解析の説明追記
  - attribute_infoのinfoフィールドにdescription追加: attribute_name_indexが指すcp_utf8エントリの文字列によって内部構造が決まることを説明

### 変更する既存機能

- [ ] `formats/java-class.bdef.yaml` — cp_tag enum拡充、新constant pool構造体追加、attribute構造のdescription改善

### 変更しないもの（スコープ外）

- constant pool参照の動的解決（attribute_name_indexからcp_utf8への参照チェーン）
- LineNumberTable、LocalVariableTable等のデバッグ系属性の詳細解析
- StackMapTable属性の解析
- バイトコード命令の逆アセンブル
- InnerClasses、EnclosingMethod等のクラス構造属性の解析
- Long/Doubleの2スロット問題への対処（既知の制限事項）

## 受入条件

1. [ ] cp_tag enumにCONSTANT_Dynamic(17), CONSTANT_Module(19), CONSTANT_Package(20)が追加されていること
2. [ ] cp_dynamic構造体（bootstrap_method_attr_index + name_and_type_index）がswitchのcase "17"として追加されていること
3. [ ] cp_module構造体（name_index）がswitchのcase "19"として追加されていること
4. [ ] cp_package構造体（name_index）がswitchのcase "20"として追加されていること
5. [ ] Code attribute、ConstantValue attribute、Exceptions attributeの構造がdescriptionまたは構造体として記載されていること
6. [ ] 既存のコンスタントプール解析（Utf8, Integer, Float, Long, Double, Class, String, Fieldref, Methodref等）に回帰がないこと
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

- [ ] formats/java-class.bdef.yaml — cp_tag enum拡充、新constant pool構造体追加、attribute description改善
- [ ] docs/architecture.md — フォーマット一覧の説明更新（Java Class cp_tag拡充を明記）

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
