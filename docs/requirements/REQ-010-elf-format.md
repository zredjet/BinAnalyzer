# REQ-010: ELFフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-002（ビットフィールド型）、REQ-006（条件付きフィールド: 32/64ビット分岐で有用） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

ELF（Executable and Linkable Format）はLinux/Unix系OSの実行ファイルおよび共有ライブラリの標準フォーマット。32ビットと64ビットの2つのバリアントがあり、多数のenum値とフラグフィールドを含む。

BinAnalyzerのDSLで最も複雑なフォーマットの一つであり、switchや条件付きフィールド等の高度な機能の実証に最適。

ELF構造:
- ELFヘッダー（e_ident[16] + ヘッダーフィールド群）
- プログラムヘッダーテーブル（オプション）
- セクションヘッダーテーブル（オプション）

## 機能要件

### 追加する機能

- [ ] `formats/elf.bdef.yaml` の新規作成
- [ ] 対応するELF構造:
  - ELFヘッダー
    - e_ident: マジックナンバー（\x7FELF）、クラス（32/64）、データ（LE/BE）、バージョン、OS/ABI
    - e_type、e_machine、e_version
    - e_entry、e_phoff、e_shoff（クラスに応じて32/64ビット）
    - e_flags、e_ehsize、e_phentsize、e_phnum、e_shentsize、e_shnum、e_shstrndx
  - プログラムヘッダーエントリ（repeat: countで繰り返し）
    - p_type、p_flags、p_offset、p_vaddr、p_paddr、p_filesz、p_memsz、p_align
  - セクションヘッダーエントリ（repeat: countで繰り返し）
    - sh_name、sh_type、sh_flags、sh_addr、sh_offset、sh_size等
- [ ] ELF用のenum定義:
  - e_type（ET_NONE, ET_REL, ET_EXEC, ET_DYN, ET_CORE）
  - e_machine（EM_386, EM_X86_64, EM_ARM, EM_AARCH64等、主要なもの）
  - p_type（PT_NULL, PT_LOAD, PT_DYNAMIC, PT_INTERP等）
  - sh_type（SHT_NULL, SHT_PROGBITS, SHT_SYMTAB, SHT_STRTAB等）
- [ ] 32ビットと64ビットの分岐（e_ident[4]のクラス値に基づくswitch）
- [ ] テストデータ生成: `ElfTestDataGenerator` の作成（最小限のELFヘッダー）

### 変更する既存機能

- なし

### 変更しないもの（スコープ外）

- セクションの内容解析（シンボルテーブル、文字列テーブル等の内部解析）
- ダイナミックリンク情報の詳細解析
- リロケーション情報の解析
- デバッグ情報（DWARF）の解析

## 受入条件

1. [ ] `formats/elf.bdef.yaml` が `YamlFormatLoader` で正しく読み込めること
2. [ ] バリデーター（REQ-001実装済みの場合）でエラーが0件であること
3. [ ] 最小ELFバイナリ（64bit、ヘッダーのみ）がデコードできること
4. [ ] マジックナンバー \x7FELF が検証されること
5. [ ] e_typeとe_machineのenum表示が正しいこと
6. [ ] プログラムヘッダーがrepeat: countで正しく繰り返しデコードされること
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| BinAnalyzer.Integration.Tests | `ElfTestDataGenerator`, ELFパーステスト追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 対応フォーマット一覧にELFを追記

---

## 設計メモ

### 設計方針

フォーマット定義ファイル（`formats/elf.bdef.yaml`）の追加。ELFはe_identのクラスフィールド（32/64ビット）によってヘッダーサイズが変わるため、switchで分岐する。

ELFのエンディアンは e_ident[5] で決定されるが、DSLではフォーマット全体のエンディアンを1つしか指定できない。初期実装では `endianness: little`（x86_64、AArch64の一般的なケース）固定とする。

### DSL構造設計（概要）

```yaml
name: ELF
endianness: little
root: elf

enums:
  ei_class:
    - value: 1
      label: ELFCLASS32
    - value: 2
      label: ELFCLASS64
  ei_data:
    - value: 1
      label: ELFDATA2LSB
    - value: 2
      label: ELFDATA2MSB
  e_type:
    - value: 0
      label: ET_NONE
    - value: 1
      label: ET_REL
    - value: 2
      label: ET_EXEC
    - value: 3
      label: ET_DYN
    - value: 4
      label: ET_CORE
  e_machine:
    - value: 3
      label: EM_386
    - value: 40
      label: EM_ARM
    - value: 62
      label: EM_X86_64
    - value: 183
      label: EM_AARCH64
  # p_type, sh_type も同様に定義

structs:
  elf:
    - name: e_ident
      type: struct
      struct: elf_ident
    - name: header
      type: switch
      switch_on: "{e_ident.ei_class}"  # ※ ネスト参照が必要
      cases:
        "1": elf32_header
        "2": elf64_header
      # ... 以下省略
```

### テストデータ設計

`ElfTestDataGenerator.CreateMinimalElf64()`:
- 最小の有効なELF64ヘッダー（64バイト）
- マジックナンバー \x7FELF + ELF64クラス + リトルエンディアン
- e_type = ET_EXEC, e_machine = EM_X86_64
- プログラムヘッダー・セクションヘッダーなし（phnum=0, shnum=0）

### 代替案

なし。

### 懸念事項

1. **ネストしたフィールド参照**: `switch_on: "{e_ident.ei_class}"` のようなドット記法でのネスト参照が現在の式システムで対応されていない。**これは設計上の課題**であり、以下の対策が必要:
   - 対策A: e_identをstruct内で展開し、ei_classを直接トップレベル変数としてバインドする（構造は失うがシンプル）
   - 対策B: 式パーサーにドット記法を追加する（汎用的だが実装コスト高）
   - 対策C: e_identのデコード時にei_classをフラットに変数バインドする仕組みを追加
   - **実装時に決定する**

2. **32/64ビット分岐の複雑さ**: プログラムヘッダーとセクションヘッダーも32/64でサイズが異なる。大量のswitch定義が必要になり、DSLの可読性が低下する可能性。

3. **エンディアン切替**: ビッグエンディアンのELF（MIPS等）には対応できない。フィールドレベルのエンディアンオーバーライド（REQ対象外の機能8）が必要だが、初期実装ではスコープ外。

---

## 実装メモ

### 実装中の設計変更

- **ネストフィールド参照の懸念は不要**: `DecodeStruct`がスコープをpushしないため、`elf_ident`の子フィールド（`ei_class`等）の変数バインディングは親スコープにそのまま設定される。`switch_on: "{ei_class}"`が直接動作し、対策A/B/Cのいずれも不要だった。
- REQ-009で実装したswitchのsize省略機能をそのまま活用。ELFヘッダーの32/64ビット分岐でsizeなしswitchを使用。
- `repeat_count: "{e_phnum}"`で`e_phnum`変数を参照。switchのsize省略によりelf64_headerの変数がそのまま親スコープに残るため、参照可能。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `formats/elf.bdef.yaml` | ELFフォーマット定義（新規） |
| `tests/.../ElfTestDataGenerator.cs` | 最小ELF64バイナリ生成（新規） |
| `tests/.../ElfParsingTests.cs` | ELF統合テスト6件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ElfParsingTests | ElfFormat_LoadsWithoutErrors | AC-1, AC-2 |
| ElfParsingTests | ElfFormat_DecodesMinimalElf64 | AC-3 |
| ElfParsingTests | ElfFormat_Ident_DecodesCorrectly | AC-4 |
| ElfParsingTests | ElfFormat_Header_DecodesCorrectly | AC-5 |
| ElfParsingTests | ElfFormat_ProgramHeaders_DecodesCorrectly | AC-6 |
| ElfParsingTests | ElfFormat_TreeOutput_ContainsExpectedElements | AC-5, AC-6 |

### 受入条件の充足状況

- [x] AC-1: `formats/elf.bdef.yaml` が正しく読み込める
- [x] AC-2: バリデーターでエラー0件
- [x] AC-3: 最小ELFバイナリがデコードできる
- [x] AC-4: マジックナンバー \x7FELF が検証される
- [x] AC-5: e_type/e_machineのenum表示が正しい
- [x] AC-6: プログラムヘッダーがrepeat_countで正しく繰り返しデコードされる
- [x] AC-7: 既存テスト全て通過（187テスト合格）

### 気づき・今後の課題

- 32ビットELF対応にはelf32_header/elf32_phdr structの追加とswitchケースの追加が必要。
- ビッグエンディアンELF（MIPS等）にはフィールドレベルのエンディアンオーバーライドが必要。
- セクションヘッダーテーブルはe_shoff指定の任意のオフセットにあるため、シーク機能なしでは対応困難。
