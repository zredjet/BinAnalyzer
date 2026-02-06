# REQ-041: Java classフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

Java classファイルはJVM（Java Virtual Machine）のバイトコードフォーマット。マジック（0xCAFEBABE）+ バージョン + コンスタントプール + アクセスフラグ + フィールド/メソッド/属性で構成される。

コンスタントプールのタグベースswitch構造は、BinAnalyzerの式システムとswitchフィールドの実用的なテストケースとなる。シーケンシャル読み取りで完全に定義可能。

## 機能要件

### 追加する機能

- [ ] `formats/java-class.bdef.yaml` フォーマット定義
  - マジック（0xCAFEBABE）
  - マイナーバージョン、メジャーバージョン
  - コンスタントプール（count-1個のエントリ）
    - タグベースのswitch（Utf8, Integer, Float, Long, Double, Class, String, Fieldref, Methodref, InterfaceMethodref, NameAndType等）
  - アクセスフラグ（flags）
  - this_class, super_class（コンスタントプールインデックス）
  - インタフェース配列
  - フィールド配列（各フィールドのアクセスフラグ、名前インデックス、ディスクリプタインデックス、属性）
  - メソッド配列（同上）
  - 属性配列

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- バイトコード命令の解析
- コンスタントプール参照の解決（インデックス→実際の文字列/クラス名）
- 属性の詳細解析（Code, LineNumberTable等の内部構造）

## 受入条件

1. [ ] `formats/java-class.bdef.yaml` が存在すること
2. [ ] マジック（0xCAFEBABE）とバージョンが正しく解析できること
3. [ ] コンスタントプールのエントリがタグに基づいて正しく解析できること
4. [ ] アクセスフラグが解析できること
5. [ ] フィールド・メソッド配列が正しい個数分解析できること
6. [ ] 有効なJava classファイルでデコードエラーにならないこと
7. [ ] フォーマット定義がバリデーションに通ること
8. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにJava classを追記
- [ ] docs/architecture.md — formats/ ディレクトリに java-class.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: JavaClass
endianness: big
root: java_class
```

**主要struct:**

- **`java_class`** (root) — ファイル全体
  - `magic`: bytes, size=4, expected=[0xCA,0xFE,0xBA,0xBE]
  - `minor_version`: u16
  - `major_version`: u16, enum=java_version
  - `constant_pool_count`: u16
  - `constant_pool`: struct=cp_entry, repeat_count="{constant_pool_count - 1}"
  - `access_flags`: u16, flags=class_access_flags
  - `this_class`: u16
  - `super_class`: u16
  - `interfaces_count`: u16
  - `interfaces`: u16, repeat_count="{interfaces_count}"
  - `fields_count`: u16
  - `fields`: struct=field_info, repeat_count="{fields_count}"
  - `methods_count`: u16
  - `methods`: struct=method_info, repeat_count="{methods_count}"
  - `attributes_count`: u16
  - `attributes`: struct=attribute_info, repeat_count="{attributes_count}"
- **`cp_entry`** — コンスタントプールエントリ
  - `tag`: u8
  - `info`: switch, switch_on="{tag}"
- **`field_info`** / **`method_info`** — フィールド/メソッド情報（同一構造）
  - `access_flags`: u16, flags=member_access_flags
  - `name_index`: u16
  - `descriptor_index`: u16
  - `attributes_count`: u16
  - `attributes`: struct=attribute_info, repeat_count="{attributes_count}"
- **`attribute_info`** — 属性情報
  - `attribute_name_index`: u16
  - `attribute_length`: u32
  - `info`: bytes, size="{attribute_length}"

### enum定義

- **`java_version`**: 45=Java_1_1, 46=Java_1_2, 49=Java_5, 50=Java_6, 51=Java_7, 52=Java_8, 55=Java_11, 61=Java_17, 65=Java_21
- **`cp_tag`**: 1=Utf8, 3=Integer, 4=Float, 5=Long, 6=Double, 7=Class, 8=String, 9=Fieldref, 10=Methodref, 11=InterfaceMethodref, 12=NameAndType, 15=MethodHandle, 16=MethodType, 18=InvokeDynamic

### flags定義

- **`class_access_flags`**: bit_size=16, PUBLIC(0), FINAL(4), SUPER(5), INTERFACE(9), ABSTRACT(10), SYNTHETIC(12), ANNOTATION(13), ENUM(14)
- **`member_access_flags`**: bit_size=16, PUBLIC(0), PRIVATE(1), PROTECTED(2), STATIC(3), FINAL(4), SYNCHRONIZED(5), NATIVE(8), ABSTRACT(10)

### switchの使い方

`cp_entry` の `info` フィールドで `switch_on: "{tag}"` を使用。sizeは指定せず、各caseの構造体が自身のサイズを決定する（コンスタントプールエントリはタグごとに固定サイズ）。

```yaml
cases:
  "1": cp_utf8        # length(u16) + bytes(size=length)
  "3": cp_integer      # value(i32)
  "4": cp_float        # value(f32)
  "5": cp_long         # value(i64)
  "6": cp_double       # value(f64)
  "7": cp_class        # name_index(u16)
  "8": cp_string       # string_index(u16)
  "9": cp_fieldref     # class_index(u16) + name_and_type_index(u16)
  "10": cp_methodref
  "11": cp_interface_methodref
  "12": cp_name_and_type  # name_index(u16) + descriptor_index(u16)
  "15": cp_method_handle  # reference_kind(u8) + reference_index(u16)
  "16": cp_method_type    # descriptor_index(u16)
  "18": cp_invoke_dynamic # bootstrap_method_attr_index(u16) + name_and_type_index(u16)
```

### 繰り返しの方式

- コンスタントプール: `repeat_count: "{constant_pool_count - 1}"`（仕様上、インデックスは1始まり）
- interfaces: `repeat_count: "{interfaces_count}"`（各要素はu16単体）
- fields/methods/attributes: `repeat_count` で各count値を参照

### 注意点・制約

- **Long/Doubleの2スロット問題**: JVM仕様では Long(tag=5) と Double(tag=6) のエントリはコンスタントプールの2スロットを消費する。`repeat_count: "{constant_pool_count - 1}"` ではスロット数ではなくエントリ数で繰り返すため、Long/Double を含む class ファイルでは実際のエントリ数とカウントが一致せず、デコードが途中でずれる。これはDSLの繰り返し機構の制約であり、既知の制限事項として記録する。Long/Double を含まない単純な class ファイルでは正常に動作する。
- **field_info と method_info は同一構造**: 両方とも access_flags + name_index + descriptor_index + attributes_count + attributes[] の構成。同じstruct定義を `member_info` として共有し、fields と methods の両方で参照する方式が効率的。
- **属性の内部構造**: Code, LineNumberTable 等の属性は内部構造を持つが、第1弾では `info` を生bytesとして扱う。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
