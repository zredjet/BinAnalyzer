# REQ-187: チェックサムアルゴリズムの一元管理と、計算できないチェックサムの ✓ 表示の修正

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

チェックサムアルゴリズムの名前が 3 か所で別々に書かれていた（2026-09-24 に確認）。

| 場所 | 持っているもの |
|---|---|
| Core `ChecksumAlgorithms` | 名前と種類（整数系 / ハッシュ系の集合）。検証器（VAL113〜VAL115）と REQ-183 のドキュメント整合性テストが使う |
| Core `ChecksumAlgorithms.DisplayName` | 表示名（別の switch） |
| Engine `BinaryDecoder.VerifyChecksum` / `VerifyHashChecksum` | 名前 → 計算器（別の switch 2 つ） |

さらに、デコーダの switch は該当しない名前を「常に一致」（`_ => actualValue, // 未知: 常にvalid`）として扱っていた。そのため次のいずれでも、計算していないのに **✓ が表示されていた**。

- Core に名前を足して、デコーダの switch に足し忘れた場合（検証も通るので気づけない）
- 利用者の定義で未知のアルゴリズム名を書いた場合。VAL113 は警告なのでデコードは続き、実測で `0xDEADBEEF` に対して `✓ (crc32c)` が出た
- 整数型フィールドにハッシュ系（`sha256` など）を指定した場合（VAL115 のエラーだが、`--no-validate` や GUI では検証せずにデコードする）

GUI のヘックスの注釈（「この行で決まる値」）も、未検証（`ChecksumValid` が null）を ✓ として出していた（`ok == false ? ✗ : ✓`）。

## 機能要件

### 変更する既存機能

- [x] Core `ChecksumAlgorithms` を「名前・種類・表示名」の表 1 つ（`All`）にし、整数系 / ハッシュ系の集合と表示名を表から作る
- [x] Engine に `ChecksumCalculators`（名前 → 計算器の辞書。整数系・ハッシュ系）を置き、デコーダはここだけを引く
- [x] 計算器の無いアルゴリズムは検証しない（`ChecksumValid` を null にし、✓ も ✗ も出さない）
- [x] GUI のヘックスの注釈は、未検証のチェックサムに ✓ ではなく値を出す
- [x] 編集時の再計算の予告（`ChecksumDependencies`）から未検証のチェックサムを外す（`BinaryPatcher` は不一致のものしか再計算しないので、予告と実際を合わせる）

### 変更しないもの（スコープ外）

- 未知のアルゴリズム名を警告（VAL113）からエラーにすること
- 計算器の実装・対応アルゴリズムの追加

## 受入条件

1. [x] Engine の計算器の鍵の集合が Core の整数系 / ハッシュ系の集合と一致することがテストで固定され、どちらか一方にだけ足すとテストが失敗すること
2. [x] 未知のアルゴリズム名、整数型フィールドに付けたハッシュ系、bytes 型に付けた未知の名前のいずれでも、チェックサムが ✓ にならない（未検証になる）こと
3. [x] 既知のアルゴリズムの検証結果（✓ / ✗ と期待値）が変わらないこと
4. [x] GUI のヘックスの注釈と再計算の予告で、未検証のチェックサムが ✓ / 再計算対象として出ないこと
5. [x] 既存テストが全て通過すること

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumAlgorithms` を表に（`ChecksumKind` / `ChecksumAlgorithmInfo`）。公開していた集合・判定メソッドはそのまま |
| BinAnalyzer.Engine | `ChecksumCalculators`（`IntegerChecksum` / `HashChecksum` デリゲート）、`BinaryDecoder` の検証が `bool?` を返す |
| BinAnalyzer.Presentation | `HexRowBuilder` の注釈、`ChecksumDependencies` |
| docs | dsl-reference.md（検証されない場合の表示）、architecture.md（計算器の一覧） |

---

## 設計メモ

### 設計方針

- **名前は Core の表 1 か所、実装は Engine の辞書 1 か所。** Core は Engine を参照できない（検証器が名前を使う）ので、名前と実装を完全に 1 か所にはできない。代わりに Engine の辞書の鍵を Core の定数で書き、鍵の集合の一致をテストで固定する。表示名も Core の表に入れて switch を 1 つ減らす
- **計算できないものは「未検証」。** 「常に一致」は誤った安心を与える。値の検証が要らない（あるいはできない）ことは null で表し、出力は既存の「チェックサムを持たないフィールド」と同じく ✓ / ✗ を出さない

### 代替案

- **Engine 側に名前も持たせ、Core は Engine を参照する。** 依存の向き（Engine → Core）を逆転させることになる。不採用
- **未知のアルゴリズムを例外にする。** 利用者の定義で名前を書き間違えただけでデコード全体が止まる。VAL113 の警告と「未検証」表示で足りる。不採用

---

## 実装メモ

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Engine.Tests / ChecksumCalculatorsTests | IntegerCalculators_CoverExactlyTheIntegerAlgorithms / HashCalculators_CoverExactlyTheHashAlgorithms / EveryCalculator_Runs（CRC-32 の検査値 0xCBF43926） | 1 |
| Engine.Tests / ChecksumCalculatorsTests | AlgorithmWithoutCalculator_IsNotVerified（crc32c / 整数に sha256 / bytes に sha3） | 2 |
| Engine.Tests / ChecksumCalculatorsTests | KnownAlgorithm_WrongValue_IsInvalid、既存の ChecksumDecoderTests | 3 |
| Core.Tests / ChecksumAlgorithmsTests | Table_NamesAreUnique_AndSetsAreDerivedFromIt / DisplayName_FromTable_OrTheNameItself / IsKnown_IgnoresCase | 1 |
| Presentation.Tests / HexRowBuilderTests | UnverifiedChecksum_GhostShowsValue_NotACheckmark | 4 |
| Presentation.Tests / EditabilityTests | Covering_SkipsUnverifiedChecksums | 4 |

### 気づき・今後の課題

- 未知のアルゴリズム名は今も警告（VAL113）止まり。REQ-184 の未知キーと同じく、書き間違いの候補（`crc32c` → ?）を出すかは別途
