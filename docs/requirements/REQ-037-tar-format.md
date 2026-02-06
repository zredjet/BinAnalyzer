# REQ-037: TARフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

TAR（Tape Archive）は512バイト固定ヘッダ + データブロックの繰り返しで構成されるアーカイブフォーマット。POSIX標準（UStar）で広く使われる。

ヘッダ内のフィールドがASCII文字列（8進数表現）であるため、文字列フィールドの実用的なテストケースとなる。ヌル名前ヘッダで終端する構造は repeat: until_eof との相性が良い。

## 機能要件

### 追加する機能

- [ ] `formats/tar.bdef.yaml` フォーマット定義
  - TARヘッダ（512バイト固定）
    - ファイル名（100バイト ASCII）
    - ファイルモード、uid、gid（各8バイト ASCII 8進数）
    - ファイルサイズ（12バイト ASCII 8進数）
    - 修正時刻（12バイト ASCII 8進数）
    - チェックサム（8バイト）
    - タイプフラグ（1バイト）
    - リンク名（100バイト）
    - UStar拡張（magic "ustar", version, uname, gname, devmajor, devminor, prefix）
  - データブロック（サイズはヘッダのfilesize、512バイト境界にパディング）
  - エントリの繰り返し（repeat: until_eof）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- tar.gz / tar.bz2 / tar.xz の外側の圧縮層の解析
- ヘッダチェックサムの検証
- GNU tar拡張ヘッダ、pax拡張ヘッダ

## 受入条件

1. [ ] `formats/tar.bdef.yaml` が存在すること
2. [ ] TARヘッダの各フィールドが正しく解析できること
3. [ ] UStar magic が識別できること
4. [ ] ファイルデータブロックがヘッダのサイズに基づいて正しく読み取れること
5. [ ] 複数エントリのTARファイルで全エントリが解析できること
6. [ ] 有効なTARファイルでデコードエラーにならないこと
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

- [ ] README.md — 対応フォーマットにTARを追記
- [ ] docs/architecture.md — formats/ ディレクトリに tar.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: TAR
endianness: big
root: tar

structs:
  tar            — entries(struct tar_entry, repeat: eof)
  tar_entry      — header(struct tar_header, size固定512) + data(bytes, remaining ※後述)
  tar_header     — name(ascii,100) + mode(ascii,8) + uid(ascii,8) + gid(ascii,8)
                   + size(ascii,12) + mtime(ascii,12) + checksum(ascii,8) + typeflag(u8)
                   + linkname(ascii,100) + magic(ascii,6) + version(ascii,2)
                   + uname(ascii,32) + gname(ascii,32) + devmajor(ascii,8) + devminor(ascii,8)
                   + prefix(ascii,155) + padding(bytes,12, padding:true)
  raw_data       — data(bytes, remaining)
```

### 主要struct

- **tar**: ルート構造体。tar_entryをrepeat: eofで繰り返す。TARファイル末尾には通常2つの512バイトゼロブロックがあるが、repeat: eofにより自然にファイル末尾で終了する。
- **tar_entry**: ヘッダ(512バイト固定) + データブロック。element_sizeまたはバウンダリスコープでヘッダサイズを制約する。
- **tar_header**: 512バイト固定長。各フィールドはASCII文字列で、数値は8進数テキスト表現。合計500バイト分のフィールド + 12バイトのパディング。

### enumの使い方

```yaml
enums:
  tar_typeflag:
    - value: 0x30    # '0'
      label: regular_file
    - value: 0x31    # '1'
      label: hard_link
    - value: 0x32    # '2'
      label: symbolic_link
    - value: 0x33    # '3'
      label: character_device
    - value: 0x34    # '4'
      label: block_device
    - value: 0x35    # '5'
      label: directory
    - value: 0x00
      label: regular_file_legacy
      description: "古い形式の通常ファイル（ヌル文字）"
```

typeflag(u8)に適用。ASCII文字 '0'='0x30' 等の値をenumラベルで表示する。

### 繰り返しの方式

- **entries**: `repeat: eof` でファイル末尾までエントリを繰り返す。TARファイル末尾のゼロ埋めブロック（1024バイト）もtar_entryとして読み取られるが、nameが空文字列のエントリとして表示される。
- **element_sizeの活用**: tar_entryの各要素が512バイト境界にアラインされる必要がある。ただしデータ部のサイズが動的に決まるため、element_sizeではなく構造体レベルのalignを使用する方法も検討する（後述の制約参照）。

### 注意点・制約

1. **最大の制約: sizeフィールドがASCII 8進数**: TARヘッダのsizeフィールドは「0000644\0」のようなASCII 8進数文字列。BinAnalyzerの式エンジンは8進数文字列から整数への変換をサポートしていないため、sizeの値をデータブロックのバイト数として直接利用できない。
2. **データブロックの扱い — 設計方針**: sizeフィールドの動的解釈ができないため、以下のアプローチを採用する:
   - **ヘッダのみ解析方式**: tar_headerを512バイトのバウンダリスコープ（size: "512" でネスト構造体として読み取り）で定義し、データブロック部分はtar_entry内でremainingとして次のヘッダまでの全データを読み取る。ただし、この方式ではデータ部と次のヘッダの境界が不明になる。
   - **推奨方式**: tar_entryにelement_sizeを使わず、tar_headerのみを512バイト固定として解析し、ヘッダ後の残りデータは一括raw bytesとして扱う。具体的には、ルート構造体で `repeat: eof` + 各エントリでheader(struct, size: "512") + remaining_data(bytes, remaining)とする。ただし、この場合最初のエントリでremainingがファイル末尾までになってしまう。
   - **最も現実的な方式**: tar全体をrepeat: eofでtar_headerのみを512バイト単位で繰り返す。element_size: "512"を指定し、ヘッダフィールドの合計が512バイトになるようpadding含めて定義する。データブロックもヘッダと同じ512バイト単位のブロックとして読まれるが、ヘッダではないブロックはnameが空（ゼロ埋め）のエントリとして表示される。ヘッダとデータの区別はつかないが、全バイトが構造的に読み取られる。
3. **512バイトアライメント**: 構造体レベルの `align: 512` を活用できる可能性がある。tar_header構造体にalign: 512を設定すれば、repeatで各要素の開始が512バイト境界に揃う。ただしデータブロックのスキップには対応できない。
4. **ゼロ埋め終端ブロック**: TARファイルは末尾に512バイトのゼロブロックが2つ続く。repeat: eofで読み取ると、nameが空のエントリが2つ表示される。これは許容範囲とする。
5. **magicフィールドのバリデーション**: UStarフォーマットではmagic="ustar"（6バイト、末尾ヌルまたはスペース含む）。expected指定でバリデーション可能だが、古いTARではmagicが異なるため、expectedは付けずenumまたはdescriptionで補足する。
6. **最終的な推奨構造**: ヘッダ解析に注力し、データブロックの正確な分離は行わない。tar_headerのelement_size: "512"で512バイト単位の読み取りを繰り返し、ヘッダ内の各フィールド（name, mode, uid, gid, size, mtime等）をASCII文字列として表示する。データブロック部分は「ヘッダではないブロック」として表示されるが、ファイル名やサイズ等の主要情報は正しく抽出できる。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
