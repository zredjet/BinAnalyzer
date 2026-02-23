# REQ-123: ストリーミングデコード

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 取り下げ |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-17 |

## 背景・動機

現在の BinAnalyzer はファイル全体を `ReadOnlyMemory<byte>` としてメモリにロードする。数 GB のファイル（大きな PCAP キャプチャ、ディスクイメージ等）では Out of Memory になる可能性がある。ストリーミングデコードにより、メモリ使用量を制限しながら大容量ファイルを解析可能にする。

### ユースケース

- 大容量 PCAP ファイル（数 GB）のパケット解析
- ディスクイメージ（ISO, raw）のパーティションテーブル解析
- 大きな TAR/ZIP アーカイブの内容一覧
- メモリ制限のある環境（CI コンテナ等）での解析

## 機能要件

### 追加する機能

- [ ] `MemoryMappedFile` ベースの読み取りモード（`File.ReadAllBytes()` の代替）
- [ ] OS 仮想メモリページングによるオンデマンド読み込み
- [ ] 閾値ベース切り替え（<64MB は `ReadAllBytes`、>=64MB は mmap）
- [ ] repeat_until eof の大量要素に対する逐次処理

### 変更する既存機能

なし

### 変更しないもの（スコープ外）

- ネットワークストリームからのリアルタイムデコード
- 非 seekable ストリーム対応（パイプ入力等）
- デコード結果のストリーミング出力（結果ツリー全体はメモリに保持）
- `--max-memory` オプション（mmap では OS にページング委譲するため制御不可）
- 2GB 超ファイル対応（`DecodeContext.Position` が `int` 型のため。別 REQ 相当の大規模リファクタリングが必要）

## 受入条件

1. [ ] 64MB〜2GB のファイルが物理メモリを大量消費せずにデコード可能であること
2. [ ] デコード結果が通常モード（`ReadAllBytes`）と同一であること
3. [ ] 64MB 未満のファイルは従来どおり `ReadAllBytes` で読み込まれること（後方互換性）
4. [ ] 2GB 超のファイルに対して明確なエラーメッセージが表示されること
5. [ ] 既存の小ファイルデコードに性能劣化がないこと
6. [ ] 単体テストが追加されていること
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 | 影響度 |
|---|---|---|
| BinAnalyzer.Core | 変更なし | なし |
| BinAnalyzer.Dsl | 変更なし | なし |
| BinAnalyzer.Engine | `MmapMemoryManager` 新規クラス追加（~60行）。`MemoryMappedFile` から `ReadOnlyMemory<byte>` を提供するカスタム `MemoryManager<byte>` | 小 |
| BinAnalyzer.Output | 変更なし | なし |
| BinAnalyzer.Cli | `File.ReadAllBytes()` → 閾値ベース切り替えロジック追加（~30行） | 小 |

API 変更・モデル変更・DSL 変更は一切不要。既存テストへの影響もなし。

### 変更が必要なドキュメント

- [ ] docs/architecture.md（mmap モードの説明追加）
- [ ] README.md（大容量ファイル対応の制約事項追記）

### 対応方針の選択肢

| 方針 | 理由 |
|---|---|
| **A. 対応見送り（推奨）** | 2GB 制約のため元の動機（数 GB ファイル）を解決できない。コード変更は小さいが、中途半端な改善になる。Priority: 低 のまま保留 |
| B. 限定実装 | 64MB〜2GB 範囲のファイルには効果あり。コード変更が小さいので実装コストは低い |
| C. 将来の long 化と合わせて対応 | `Position` の `long` 化（大規模リファクタ）と合わせて初めて本来の目的を達成可能 |

---

## 設計メモ

### 設計方針

`MemoryMappedFile` + カスタム `MemoryManager<byte>` アプローチを採用する。

- `System.IO.MemoryMappedFiles.MemoryMappedFile` でファイルをメモリマップ
- `MemoryMappedViewAccessor` から `SafeMemoryMappedViewHandle` を取得
- カスタム `MemoryManager<byte>`（`MmapMemoryManager`）でラップし、`ReadOnlyMemory<byte>` として提供
- 既存の `DecodeContext` は `ReadOnlyMemory<byte>` を受け取るため、インタフェース変更不要
- OS の仮想メモリページングにより、アクセスされたページのみが物理メモリにロードされる

閾値ベース切り替え:
- ファイルサイズ < 64MB: 従来の `File.ReadAllBytes()` → `ReadOnlyMemory<byte>`
- ファイルサイズ >= 64MB: `MemoryMappedFile` → `MmapMemoryManager` → `ReadOnlyMemory<byte>`

### モデル変更

なし。IR（中間表現）、デコード結果モデルへの変更は不要。

### インタフェース変更

なし。既存の public インタフェース・メソッドへの変更は不要。`DecodeContext` が受け取る `ReadOnlyMemory<byte>` の供給元が変わるのみで、破壊的変更なし。

### 代替案

1. **IDataSource 抽象化** — `ReadOnlyMemory<byte>` の代わりにストリーム抽象を導入する案。Core/Engine 全体に影響が及び、変更規模が大きい。2GB 制約を超えるために将来必要になる可能性があるが、現時点ではオーバーエンジニアリング。不採用。
2. **チャンク + LRU キャッシュ** — ファイルを固定サイズチャンクに分割し、LRU キャッシュで管理する案。`ReadOnlyMemory<byte>.Span` が連続メモリを前提としているため、チャンク境界をまたぐアクセスが複雑になる。不採用。
3. **Unsafe MemoryManager（ポインタ直接公開）** — `MemoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer` で直接ポインタを取得し、`UnmanagedMemoryManager<byte>` でラップする案。性能面では最適だが、unsafe コードが必要で保守性が低下。不採用。

### 懸念事項

1. **2GB 制限** — `ReadOnlyMemory<byte>` と `DecodeContext.Position` が `int`（Int32）型のため、2GB（`int.MaxValue` = 2,147,483,647 バイト）が上限。元の動機「数 GB のファイル」を完全には解決できない。本格対応には `Position` の `long` 化が必要だが、これは別 REQ 相当の大規模リファクタリングとなる。
2. **IDisposable 管理** — `MemoryMappedFile` と `MemoryMappedViewAccessor` は `IDisposable`。デコード完了後に確実に Dispose する必要がある。CLI 側で `using` ブロックを使用して管理する。
3. **RawBytes ライフタイム** — デコード結果の `DecodedField.RawBytes`（`ReadOnlyMemory<byte>`）が mmap 領域を参照する場合、`MemoryMappedFile` を Dispose した後にアクセスすると例外が発生する。出力フォーマッターが RawBytes を使用する場合、Dispose 前に完了させるか、コピーが必要。
4. **プラットフォーム互換性** — `MemoryMappedFile` は Windows / Linux / macOS で動作するが、動作特性（ページサイズ、フラッシュタイミング等）にプラットフォーム差がある。読み取り専用アクセスでは問題になりにくいが、テストでの確認が必要。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
