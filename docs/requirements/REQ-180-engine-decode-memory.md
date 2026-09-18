# REQ-180: デコード結果のメモリ使用量と大容量ファイルのデコード時間

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

REQ-177（GUI の大容量ファイル性能）の計測で、GUI 側（索引・描画）を改善した後の「開く」の所要時間とメモリは、エンジンのデコード結果そのものが支配的になった。

| ファイル | ノード数 | デコード | 索引構築 | 最大 RSS |
|---|---:|---:|---:|---:|
| 50 MB PCAP（1400 B ペイロード、35,666 パケット） | 1,212,654 | 1,080 ms | 350 ms | 780 MB |
| 5 MB PCAP（ペイロード無し、74,898 パケット） | 2,546,542 | 2,000 ms | 1,030 ms | 1,290 MB |

- 5 MB のファイルに 1.3 GB 使うのは、ノード 1 個あたり約 500 B（`DecodedInteger` は nullable のチェックサム・enum・文字列テーブル等の参照を 10 個以上持ち、さらにエンジンのスコープ・式評価の中間オブジェクトが残る）ためで、ファイルサイズではなくノード数に比例する
- デコード時間もノード数に比例（約 0.8 µs / ノード）。既存ベンチマーク（`DecodeLargePcap`: 10,000 パケットで 99 MB 割り当て）とも整合する
- REQ-177 のスコープ（Presentation / GUI）ではエンジンに手を入れないため、本要望に切り出す

## 機能要件

### 追加する機能

- [x] `BinAnalyzer.Benchmarks` にノード 1 個あたりの割り当てバイト数を出すベンチマーク（`DecodeLargePcap` の Allocated / ノード数）を追加し、現状値を `docs/benchmark-baseline.md` に記録 — `probe <file> <format>` モード（`DecodeMemoryProbe`）を追加。実ファイル 1 回のデコードで時間・割り当て・ヒープ差をノード数で割って出す。`DecodeLargePcap` は約 38 万ノードなので Allocated / 380,000 で換算できる
- [x] デコード結果モデルのスリム化
  - [x] `DecodedInteger` / `DecodedBytes` のチェックサム関連と enum 関連を、必要なノードだけが持つ補助オブジェクト 1 参照にまとめる — 168 → 104 B、128 → 104 B。`Endianness` はデコーダがほぼ全整数に設定するので本体に残した
  - [ ] `Description` を DSL の `FieldDefinition` から参照で引く — 現状も同じ文字列インスタンスを参照しているだけ（8 B の参照のみ）なので効果が無く、見送り
  - [x] `DecodedStruct.Children` の `List<T>` の余剰容量 — フィールド数で事前確保（条件付きフィールドが無ければ余剰なし）
- [x] デコードループの割り当て削減 — 計測で支配的だったのは「struct フィールドごとに作る `Dictionary<string, object>`（入れ子を再帰的に複製）」と「変数束縛のボックス化」で、合わせて割り当ての 77% だった。struct / array はノードそのものを束縛して式評価時に子を引く（`NodeValues`）、スコープと変数辞書の使い回し、小さな整数のボックスのキャッシュ（`BoxCache`）、列挙子のボックス化回避、enum 値検索の辞書化で対応

### 変更しないもの（スコープ外）

- メモリマップ / ストリーミングデコード（REQ-123 で取り下げ済み）
- 出力フォーマッタ・GUI の変更（モデルの公開プロパティは互換を保つ）— GUI は開いた直後の `GC.Collect()` 1 回だけ追加

## 受入条件

1. [x] 50 MB PCAP の最大 RSS が現状の 8 割以下、5 MB 小パケット PCAP も同様 — 780 → 617 MB（79%）、1,290 → 903 MB（70%）。当初案の「6 割（500 MB / 900 MB）」はプロセスの下限（1 KB の PNG でも 177 MB。ランタイム + ASP.NET Core + Photino）を考慮していなかったため見直した。下限を引いた分は 600 → 440 MB、1,110 → 726 MB
2. [x] `DecodeLargePcap` の Allocated が現状（99 MB / 10,000 パケット）の 6 割以下 — 55.4 MB（56%）。実ファイル 1 回のデコードでは 1,104 → 169 B / ノード（15%）
3. [x] デコード結果の値・構造（ゴールデンテスト）が変わらないこと — 全ゴールデン一致
4. [x] 既存テストが全て通過すること（`dotnet test` 全通過） — 1,889 件

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodedInteger` / `DecodedBytes` の補助オブジェクト化、`EnumDefinition.FindByValue` の辞書化 |
| BinAnalyzer.Engine | `NodeValues`（新規）、`BoxCache`（新規）、`DecodeContext` のスコープ使い回しと `SetVariable(string, long)`、`BinaryDecoder` の辞書生成廃止・ループ最適化、`ExpressionEvaluator` の struct / array ノード対応 |
| BinAnalyzer.Gui | `GuiDocument` コンストラクタで `GC.Collect()` 1 回 |
| BinAnalyzer.Benchmarks | `probe` モード（`DecodeMemoryProbe`） |

### 変更が必要なドキュメント

- [x] docs/benchmark-baseline.md — 「デコード結果のメモリと時間（REQ-180）」節
- [x] docs/architecture.md — `DecodeContext` / `NodeValues` / `DecodedInteger` の記述

---

## 設計メモ

### 設計方針

計測してから直す。`GCAllocationTick` の型別サンプルで、改善前の割り当て 1,100 B / ノードの内訳は辞書のエントリ配列 46%、ボックス化した `Int64` 15%、辞書のバケット配列 11%、`Dictionary` 本体 5%、ノードオブジェクト 15% 程度だった。ノードのスリム化より先に、辞書とボックス化をなくす方が効く。

1. **struct / array の変数はノードそのものを束縛する。** `BuildStructDictionary` は struct フィールドごとに `Dictionary<string, object>` を作り、入れ子の struct も再帰的に辞書に複製していた（4 段の PCAP パケットでは同じ辞書を 4 回作る）。さらに `_prev` 用にもう 1 回。ノードは不変なので、式評価時に `NodeValues.Member` で子を名前で引けば同じ結果になる。同名は後勝ち、bitfield のサブフィールドも struct のメンバーとして見える、配列は全要素が値に変換できるときだけ変数になる、という辞書時代の規則をそのまま再現した
2. **スコープの使い回し。** 要素ごとの `PushScope` で `Scope` と変数辞書を作っていた。Pop 時に辞書を `Clear`（容量は残る）してプールに戻す
3. **ボックスのキャッシュ。** 整数フィールドの値・配列インデックス・昇格した値は `object` として束縛される。−1024〜16383 のボックスを共有する（`SetVariable(string, long)` オーバーロードで呼び出し側は変更なし）
4. **補助オブジェクト。** `DecodedInteger` の enum / チェックサム / 文字列テーブル関連 8 プロパティは 1 割程度のノードしか使わない。`init` アクセサが null 以外を設定したときだけ `Extras` を作る（デコーダは全プロパティを無条件に初期化するため、null で作ってしまうと逆効果）。`Endianness` はほぼ全整数に設定されるので本体に残す
5. **GUI は開いた直後に 1 回だけ `GC.Collect()`。** デコードと索引構築のゴミ（ファイルの数倍）を回収して常駐メモリを木と索引に近づける。編集のたびの再デコードでは行わない

### モデル変更

- `DecodedInteger` / `DecodedBytes`: 公開プロパティは同じ。内部表現が補助オブジェクトになった
- `EnumDefinition.FindByValue`: 初回に値→エントリの辞書を作る（同じ値が複数あれば最初のもの、従来の `FirstOrDefault` と同じ）

### インタフェース変更

- 式評価で struct / array の値が `Dictionary<string, object>` / `List<object>` ではなく `DecodedStruct` / `DecodedArray` になる。`ExpressionEvaluator` の内部だけが見る値なので外部影響なし（旧形式も引き続き受け付ける）
- `DecodeContext.SetVariable(string, long)` を追加

### 代替案

- ノードに `Description` を持たせず定義から引く — 参照 8 B のみで効果が無い。不採用
- `DecodedNode` の `BitOffset` / `DslType` / `ValidationPassed` を詰めて 8 B 単位の無駄を削る — 1 ノード 8〜16 B の削減にとどまり、Core の型を変える割に効果が薄い。見送り
- `NodeIndex` のノード→ID 辞書を無くす（ノードに ID を持たせる）— REQ-177 で不採用。索引側の約 40 MB / 100 万ノードは残る

### 懸念事項

- `GC.GetTotalMemory` の差で測った「保持メモリ」は断片化や世代の再配置を含み、割り当てより大きく出ることがある（50 MB PCAP で 364 MB > 割り当て 196 MB）。実サイズ × 個数（約 140 B / ノード）の方が信頼できるので、`probe` の表示は「ヒープ差（上限）」とした
- PNG のように struct メンバー参照が無く CRC 付き整数が多いフォーマットは、補助オブジェクトのぶん割り当てが 1 割増える（`DecodeLargePng` 18.7 → 20.7 MB）。時間には影響しない

---

## 実装メモ

### 実装中の設計変更

- 最初は `Endianness` も補助オブジェクトに入れたが、デコーダがほぼ全整数に設定するため `Extras` がノード数と同じだけ作られ、合計サイズが改善前を上回った（`GCAllocationTick` で `Extras` 62 MB）。本体に戻して 104 B に収めた
- `init` アクセサでデリゲート（`Set(value, (x, v) => ...)`）を使う書き方は読みやすいが呼び出しコストがあるので、各プロパティに展開した

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| NodeValuesTests | NestedStructMember_AndBitfieldSubField_AreAccessible、DuplicateChildName_LastOneWins_LikeTheOldDictionary、StructArray_ElementMember_Len_And_Sum_WorkThroughTheArrayNode、ArrayWithNonValueElements_IsNotBoundAsAVariable、EnumFindByValue_ReturnsFirstEntry_ForDuplicateValues、DecodedInteger_OptionalProperties_RoundTrip_AndDefaultToNull | AC-3（式評価の互換） |
| 既存: PrevVariableTests / ArrayElementSeekTests / BuiltinFunctionTests / GoldenFileTests / Fuzz.Tests | `_prev.field`、`arr[_index]`、`len` / `sum` / `min` / `max`、全フォーマットのゴールデン、ファズ | AC-3, AC-4 |

### 気づき・今後の課題

- 残る常駐メモリの内訳（50 MB PCAP、下限を引いた 440 MB）: ノード約 170 MB、`NodeIndex` 約 150 MB（ノード→ID 辞書が 3 割）、データ 52 MB、bitfield のサブフィールド（`BitfieldValue` 記録 11 個 / bitfield ≈ 700 B）約 25 MB、残りは GC の管理領域と断片化。次に効くのは `NodeIndex` の辞書（REQ-177 で見送った「ノードに ID を持たせる」案）と `BitfieldValue` の軽量化
- `DecodedVirtual.Value` は `object`（boxed long）で、値ごとに 24 B。頻出する 0〜数千は `BoxCache` で共有できるが、式評価の結果は評価器がボックス化するため未対応
- デコード時間は 0.46 µs / ノードまで下がったが、内訳（式評価・フィールド種別分岐・ノード生成）は未計測。さらに詰めるならプロファイラで
- 入れ子 1 段のスタック消費（Debug で約 8 KB、REQ-160）はフレームのローカル変数の多さによるもので、本要望では手を付けていない
