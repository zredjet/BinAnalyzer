# REQ-140: 遅延デコード・部分デコード

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 取り下げ |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在のデコーダはバイナリファイル全体を一度にデコードし、全ノードのツリーをメモリ上に展開する。小〜中規模ファイル（数MB以下）では問題ないが、以下のケースでパフォーマンスとメモリ消費の問題が発生する。

### 巨大ファイル

SQLiteデータベース、PEバイナリ、大量セグメントを含むELF等、数百MB〜数GBのファイルでは全ノードのデコードが非現実的。

### TUI/Webでの体感速度

TUIやWeb UIでは、ユーザーがツリーノードを展開するまで子ノードの詳細は不要。初期表示に必要なのはトップレベルの構造だけである。

### CLI出力フィルタの非効率

`--filter` オプションは出力フォーマッタで適用されるため、フィルタに関係ないサブツリーも全てデコードされる。

## 機能要件

### 追加する機能

- [ ] 遅延デコード — struct型フィールドのデコードを遅延し、アクセス時に初めてデコードする仕組み
- [ ] 部分デコード — `--filter` パターンに基づき、不要なサブツリーのデコードをスキップする仕組み
- [ ] デコード深度制限 — `--depth N` オプションで指定した深さまでしかデコードしない

### 変更する既存機能

- [ ] `DecodedStruct` — 子ノードの遅延ロードをサポート（未展開状態を表現）
- [ ] `BinaryDecoder` — デコード深度の追跡と遅延デコードのためのコールバック/ファクトリの提供
- [ ] TUI — ノード展開時の遅延デコードトリガー
- [ ] Web UI — ノード展開時の遅延デコードトリガー

### 変更しないもの（スコープ外）

- ストリーミングI/O — ファイル全体をメモリに読む前提は維持（mmap等は将来課題）
- 式の遅延評価 — 式で参照される変数は即座にバインドする必要があるため対象外
- JSON/CSV等の一括出力 — 全ノードが必要なため遅延の恩恵が少ない

## 受入条件

1. [ ] `--depth 1` 指定時にトップレベル構造のみがデコードされること
2. [ ] TUIでツリーノードを展開した際に、そのノードの子が遅延デコードされること
3. [ ] `--filter "header.**"` 指定時に、header以外のサブツリーがデコードされないこと
4. [ ] 遅延デコードの結果が通常デコードと同一であること
5. [ ] 巨大ファイル（100MB以上）のトップレベルデコードが数秒以内に完了すること
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodedStruct` — 遅延ロード対応。デコードオプション型追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `BinaryDecoder` — 深度追跡、遅延デコードファクトリ、フィルタベースのスキップ |
| BinAnalyzer.Output | フィルタ適用ポイントの移動検討 |
| BinAnalyzer.Tui | ノード展開時のデコードトリガー |
| BinAnalyzer.Web | ノード展開時のデコードトリガー |
| BinAnalyzer.Cli | `--depth` オプション追加 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 変更不要
- [ ] docs/architecture.md — 遅延デコードのアーキテクチャ説明追加
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — `--depth` オプションの説明追加
- [ ] docs/cli-usage.md — `--depth` オプションの説明追加

---

## 設計メモ

### 設計方針

- `DecodedLazy` ノード型を新設し、深度制限を超えたstruct/switchフィールドのプレースホルダーとする
- `BinaryDecoder` のインスタンス変数 `_currentDepth` / `_maxDepth` で深度を追跡。`DecodeStruct()` の入口でインクリメント、出口でデクリメント
- Lazy化条件: `_currentDepth >= _maxDepth` かつ フィールドのサイズが既知（`Size`, `SizeExpression`, `SizeRemaining` のいずれか）
- Lazy化しない例外: `IsStringTable = true` の構造体（文字列テーブル登録が必要）、ビットストリームモード内のフィールド、サイズ不明のフィールド
- Lazy展開クロージャは元データ全体・FormatDefinition・StructDefinition・オフセット・サイズ・エンディアン・テンプレート引数をキャプチャ。展開時に新規 `BinaryDecoder` + `DecodeContext` を作成し `Seek(offset)` → `PushScope(size)` → `DecodeStruct()` を実行
- `Lazy<T>` でスレッドセーフなキャッシュ付き展開

### モデル変更

- `DecodedLazy(Func<DecodedStruct> expandFunc)` — `DecodedNode` を継承。`StructType`, `FieldCount` プロパティ。`Expand()` でオンデマンドデコード
- `DecodeOptions { MaxDepth: int? }` — デコードオプション。`null` = 無制限（後方互換）
- `IBinaryDecoder` に `Decode(data, format, options)` オーバーロード追加
- `BinaryDecoder` に `DecodeLazyExpansion()` 内部メソッド追加

### インタフェース変更

- `IBinaryDecoder.Decode(ReadOnlyMemory<byte>, FormatDefinition, DecodeOptions)` 追加
- `BinaryDecoder.DecodeWithRecovery(data, format, errorMode, options)` オーバーロード追加
- 既存の `Decode(data, format)` は `Decode(data, format, DecodeOptions.Default)` に委譲（後方互換）

### 代替案

- DecodedStruct.Children を遅延ロード可能にする案 → 却下（既存の `IReadOnlyList<DecodedNode>` を変更すると全消費者に影響。独立した `DecodedLazy` ノード型の方がswitch式で明示的に扱え、既存コードへの影響が限定的）
- 「高速デコード」でバイト読み取りのみ行い変数をバインドするがノード生成しない案 → 却下（計算コスト削減にならず、実装複雑性が高い）
- フィルタベースの部分デコード → 将来課題に延期（変数スコープの依存関係解析が複雑）

### 懸念事項

- **変数スコープの制限**: Lazy化されたフィールドは `context.SetVariable()` を呼ばない。後続の兄弟フィールドがそのメンバーを参照する式（`{header.version}` 等）は失敗する。`--depth` は主に表示用途のため許容。将来的に兄弟フィールドの式を静的解析し参照がある場合は強制デコードする改善が可能
- **_prev変数**: 繰り返しフィールドの個別要素をLazy化すると `_prev` が未設定になる。Phase 1では配列要素のLazy化は対象外
- **出力フォーマッター**: 全フォーマッター（Tree, JSON, HTML, CSV, Map, HexDump, Diff等）に `DecodedLazy` ケースの追加が必要。変更箇所が多い

---

## 実装メモ

### 実装中の設計変更

取り下げのため未実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| （取り下げのため未実装） | | |

### 気づき・今後の課題

- 出力フォーマッター全てに `DecodedLazy` ケース追加が必要で影響範囲が広い
- サイズ不明のstructフィールドはLazy化できないため、効果が限定的なフォーマットもある
- `--filter` 最適化（部分デコード）は変数依存関係の静的解析が必要で、遅延デコードとは別フェーズで取り組むべき
