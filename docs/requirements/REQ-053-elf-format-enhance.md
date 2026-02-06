# REQ-053: ELFフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-010で実装されたELFフォーマット定義（`formats/elf.bdef.yaml`）は64-bitのみ対応しており、セクションヘッダーテーブルが未解析、enum定義も主要な値に限定されている。

実際のELFバイナリ解析では、32-bitバイナリ（組み込みLinux、レガシーシステム）への対応が不可欠であり、セクションヘッダーテーブルはシンボル情報やデバッグ情報の配置を理解する上で重要な構造体である。また、e_machine等のenum値が不足しているため、RISC-V、MIPS、PowerPC等のアーキテクチャのバイナリを解析した際に数値のみが表示され可読性が低い。

本要望では、既存の `elf.bdef.yaml` を拡充し、32-bit対応、セクションヘッダー追加、enum値の充実を行う。

## 機能要件

### 追加する機能

- [ ] セクションヘッダー（elf64_shdr）追加
  - `seek(e_shoff)` でセクションヘッダーテーブルの位置にジャンプ
  - `repeat_count(e_shnum)` でセクションヘッダーエントリを繰り返しデコード
  - sh_name, sh_type, sh_flags, sh_addr, sh_offset, sh_size, sh_link, sh_info, sh_addralign, sh_entsize の各フィールド
- [ ] 32-bit ELF対応
  - ei_classの値に基づくswitchで32-bit構造体を選択
  - elf32_header: e_entry/e_phoff/e_shoff等がuint32
  - elf32_phdr: p_offset/p_vaddr/p_paddr/p_filesz/p_memsz/p_alignがuint32
  - elf32_shdr: sh_addr/sh_offset/sh_size/sh_addralign/sh_entsizeがuint32
- [ ] e_machine enum拡充
  - RISC-V = 243 (EM_RISCV)
  - MIPS = 8 (EM_MIPS)
  - PowerPC = 20 (EM_PPC)
  - SPARC = 2 (EM_SPARC)
  - S390 = 22 (EM_S390)
- [ ] sh_type enum拡充
  - SHT_DYNAMIC = 6
  - SHT_NOTE = 7
  - SHT_NOBITS = 8
  - SHT_REL = 9
  - SHT_RELA = 4
  - SHT_DYNSYM = 11
  - SHT_INIT_ARRAY = 14
  - SHT_FINI_ARRAY = 15
- [ ] p_type enum拡充
  - PT_TLS = 7
  - PT_GNU_EH_FRAME = 0x6474E550
  - PT_GNU_STACK = 0x6474E551
  - PT_GNU_RELRO = 0x6474E552
- [ ] ei_osabi enum拡充
  - FreeBSD = 9 (ELFOSABI_FREEBSD)
  - NetBSD = 2 (ELFOSABI_NETBSD)
  - Solaris = 6 (ELFOSABI_SOLARIS)
- [ ] p_flags bitfield化
  - PF_X = bit 0（実行可能）
  - PF_W = bit 1（書き込み可能）
  - PF_R = bit 2（読み取り可能）

### 変更する既存機能

- [ ] `formats/elf.bdef.yaml` — セクションヘッダー構造体追加、32-bit構造体追加、ei_classによるswitch分岐拡張、各enum定義の拡充、p_flagsのbitfield化

### 変更しないもの（スコープ外）

- ビッグエンディアンELFへの対応（フィールドレベルのエンディアンオーバーライドが必要）
- セクション内容の詳細解析（シンボルテーブル、文字列テーブルの内部解析）
- ダイナミックリンク情報の詳細解析
- リロケーション情報の解析
- デバッグ情報（DWARF）の解析

## 受入条件

1. [ ] セクションヘッダーテーブルが `seek(e_shoff)` + `repeat_count(e_shnum)` で解析可能であること
2. [ ] 32-bit ELFバイナリ（ei_class=1）がelf32_header/elf32_phdr/elf32_shdrでパース可能であること
3. [ ] 64-bit ELFバイナリ（ei_class=2）が引き続き正しくパースできること（既存機能の回帰なし）
4. [ ] e_machineにRISC-V(243), MIPS(8), PowerPC(20), SPARC(2), S390(22)が追加されていること
5. [ ] sh_typeにSHT_DYNAMIC(6), SHT_NOTE(7), SHT_NOBITS(8), SHT_REL(9), SHT_RELA(4), SHT_DYNSYM(11), SHT_INIT_ARRAY(14), SHT_FINI_ARRAY(15)が追加されていること
6. [ ] p_typeにPT_TLS(7), PT_GNU_EH_FRAME(0x6474E550), PT_GNU_STACK(0x6474E551), PT_GNU_RELRO(0x6474E552)が追加されていること
7. [ ] ei_osabiにFreeBSD(9), NetBSD(2), Solaris(6)が追加されていること
8. [ ] p_flagsがbitfieldとして表示されること（PF_X, PF_W, PF_R）
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] formats/elf.bdef.yaml — セクションヘッダー追加、32-bit対応、enum/bitfield拡充
- [ ] docs/architecture.md — フォーマット一覧の説明更新（ELF 32/64-bit対応を明記）

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
