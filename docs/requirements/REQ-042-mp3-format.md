# REQ-042: MP3フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし（第2弾でREQ-044に依存） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

MP3（MPEG Audio Layer III）は最も普及した音声圧縮フォーマット。ID3v2タグ（メタデータ）+ MPEGフレームで構成される。

第1弾ではID3v2タグヘッダと先頭MPEGフレームヘッダの解析に限定する。ID3v2タグはシンクセーフ整数やフレーム構造の解析が良いテストケースとなる。第2弾（オフセットジャンプ実装後）でID3v1タグ（ファイル末尾128バイト）やXingヘッダ等に対応する。

## 機能要件

### 追加する機能

- [ ] `formats/mp3.bdef.yaml` フォーマット定義（第1弾）
  - ID3v2ヘッダ（"ID3" magic, version, flags, size）
  - ID3v2サイズはシンクセーフ整数（各バイトの下位7ビットのみ使用）— virtualフィールドで計算
  - ID3v2フレームの繰り返し（フレームID 4バイト + size 4バイト + flags 2バイト + data）
  - 先頭MPEGフレームヘッダ（sync word, version, layer, bitrate, sample rate, padding, channel mode）
  - ビットレート・サンプルレートのenumマッピング

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- MPEGフレームデータ（音声データ）の解析
- ID3v1タグ（ファイル末尾、オフセットジャンプが必要）
- Xing/VBRIヘッダの解析
- 全MPEGフレームの走査
- ID3v2.2（旧バージョン、3バイトフレームID）

## 受入条件

1. [ ] `formats/mp3.bdef.yaml` が存在すること
2. [ ] ID3v2ヘッダが正しく解析できること
3. [ ] ID3v2フレーム（TIT2, TPE1, TALB等）が識別・解析できること
4. [ ] 先頭MPEGフレームヘッダのビットレート・サンプルレートが抽出できること
5. [ ] 有効なMP3ファイルでデコードエラーにならないこと
6. [ ] フォーマット定義がバリデーションに通ること
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

### 変更が必要なドキュメント

- [ ] README.md — 対応フォーマットにMP3を追記
- [ ] docs/architecture.md — formats/ ディレクトリに mp3.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: MP3
endianness: big
root: mp3
```

**主要struct:**

- **`mp3`** (root) — ファイル全体
  - `id3v2_header`: struct=id3v2_header
  - `id3v2_body`: bytes, size="{tag_size}" （ID3v2タグ本体、シンクセーフ整数から算出）
  - `mpeg_frame_header`: struct=mpeg_frame_header （先頭MPEGフレームヘッダ）
- **`id3v2_header`** — ID3v2ヘッダ（10バイト）
  - `magic`: ascii, size=3 （"ID3" を期待）
  - `version`: u8
  - `revision`: u8
  - `flags`: bitfield, size=1 (unsynchronisation[7], extended_header[6], experimental[5], footer[4])
  - `size_b0`: u8
  - `size_b1`: u8
  - `size_b2`: u8
  - `size_b3`: u8
  - `tag_size`: virtual, value="{(size_b0 << 21) | (size_b1 << 14) | (size_b2 << 7) | size_b3}"
- **`mpeg_frame_header`** — MPEGフレームヘッダ（4バイト）
  - `header`: bitfield, size=4
    - sync: bits "31:21" （11ビット、全て1）
    - version: bits "20:19", enum=mpeg_version
    - layer: bits "18:17", enum=mpeg_layer
    - protection: bits "16"
    - bitrate_index: bits "15:12", enum=mp3_bitrate
    - sample_rate_index: bits "11:10", enum=mp3_sample_rate
    - padding: bits "9"
    - private_bit: bits "8"
    - channel_mode: bits "7:6", enum=channel_mode
    - mode_extension: bits "5:4"
    - copyright: bits "3"
    - original: bits "2"
    - emphasis: bits "1:0"

### enum定義

- **`mpeg_version`**: 0=MPEG2.5, 2=MPEG2, 3=MPEG1
- **`mpeg_layer`**: 1=Layer_III, 2=Layer_II, 3=Layer_I
- **`mp3_bitrate`** (MPEG1 Layer III): 1=32kbps, 2=40kbps, 3=48kbps, 4=56kbps, 5=64kbps, 6=80kbps, 7=96kbps, 8=112kbps, 9=128kbps, 10=160kbps, 11=192kbps, 12=224kbps, 13=256kbps, 14=320kbps
- **`mp3_sample_rate`** (MPEG1): 0=44100Hz, 1=48000Hz, 2=32000Hz
- **`channel_mode`**: 0=stereo, 1=joint_stereo, 2=dual_channel, 3=mono

### switchの使い方

switchは使用しない。ID3v2フレームの内部構造分岐は第1弾ではスコープ外とし、ID3v2タグ本体は生bytesとして読み取る。

### 繰り返しの方式

第1弾ではID3v2タグ本体を一括bytes読み取りとし、内部フレームの個別繰り返しは行わない。将来的にID3v2フレーム解析を追加する場合は、`id3v2_body` をstruct化し、内部フレームの `repeat: eof`（バウンダリスコープ内のEOF）で繰り返す方式とする。

### 注意点・制約

- **シンクセーフ整数**: ID3v2のサイズフィールドは各バイトの下位7ビットのみ使用する特殊なエンコーディング。4バイトを個別の u8 として読み取り、virtualフィールドでビットシフト演算（`<<`, `|`）を使って実サイズを計算する。DSLのビット演算子で対応可能。
- **ID3v2タグ本体の扱い**: tag_size で算出したサイズ分を bytes で一括読み取り。ID3v2フレーム（TIT2, TPE1 等）の個別解析は第2弾以降で対応。
- **MPEGフレームヘッダ**: 4バイトをbitfield として読み取り、各ビット範囲を抽出する。sync word (11bits=0x7FF) のバリデーションは bitfield のフィールドレベルでは行えないため、表示のみとする。
- **ビットレートenum**: MPEG1 Layer III 用のビットレートテーブルのみ定義。MPEG2やLayer I/II のテーブルは異なるが、最も一般的なMPEG1 Layer IIIに限定する。
- **ID3v2.2非対応**: 旧バージョン（3バイトフレームID）はスコープ外。version フィールドの値が3（ID3v2.3）または4（ID3v2.4）を前提とする。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
