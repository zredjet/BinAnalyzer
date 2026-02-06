# REQ-080: WASMフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

WebAssembly（WASM）はブラウザおよびサーバーサイド（WASI）で実行可能なバイナリフォーマット。Web開発、エッジコンピューティング、プラグインシステム等で急速に普及している。

セクションベースの構造を持ち、section_idによるswitch分岐の活用例として適している。ただし、WASMバイナリの内部ではLEB128（可変長整数エンコーディング）が多用されるため、現DSLの制約として明記が必要。

WASM構造:
- ヘッダ（8バイト: magic "\0asm" + version u32le）
- セクションの繰り返し（section_id + section_size + section_data）

## 機能要件

### 追加する機能

- [ ] `formats/wasm.bdef.yaml` フォーマット定義
  - ヘッダ
    - magic（bytes 4、expected [0x00, 0x61, 0x73, 0x6D]、"\0asm"）
    - version（u32le、validate "{version == 1}"）
  - セクション（repeat eof）
    - section_id（u8、enum: custom=0, type=1, import=2, function=3, table=4, memory=5, global=6, export=7, start=8, element=9, code=10, data=11）
    - section_size（u32、セクションデータサイズ — LEB128エンコーディングは現DSL未対応のためu32で近似）
    - section_data（bytes size section_size、セクションデータ）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- LEB128エンコーディングの対応（現DSLでは未サポート。section_sizeをu32で近似するため、実際のWASMバイナリでは正確にパースできない場合がある）
- 各セクション内部の詳細解析（Type Section, Import Section, Function Section等の内部構造）
- カスタムセクション（name section等）の解析
- WASMモジュールの検証（型チェック等）
- WASM 2.0（Component Model）への対応
- WAT（WebAssembly Text Format）との相互変換

## 受入条件

1. [ ] `formats/wasm.bdef.yaml` が存在すること
2. [ ] ヘッダのmagic（\0asm）が正しく検証されること
3. [ ] versionフィールドが validate で検証されること
4. [ ] section_idがenumラベル付きで表示されること（custom=0, type=1, ... data=11）
5. [ ] セクション構造（section_id + section_size + section_data）が繰り返し解析できること
6. [ ] LEB128エンコーディングの制約が明記されていること
7. [ ] WASMヘッダ・セクション構造解析、section_id enum表示が正しく動作すること
8. [ ] フォーマット定義がバリデーションに通ること
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

### 変更が必要なドキュメント

- [ ] README.md — 対応フォーマットにWASMを追記
- [ ] docs/architecture.md — formats/ ディレクトリに wasm.bdef.yaml を追記

---

## 設計メモ

> 設計Phaseで記入する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
