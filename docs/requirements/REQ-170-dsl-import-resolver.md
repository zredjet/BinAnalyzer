# REQ-170: DSL インポート解決の抽象化（Web / WASM での imports 対応）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

`YamlFormatLoader.LoadFromString` は `imports:` を持つ定義を拒否し、`Load(path)` だけがファイルシステム経由でインポートを解決する。このため Web（WASM）では `imports` を使う 5 フォーマット（avi / heif / mp4 / webp / wav、および共通型ライブラリ REQ-155 を使うもの）が読めない。GUI モード（REQ-168）の Web ホストでも同じ制限が残っている。

インポートの取得手段（ファイル / HTTP / 埋め込み）をローダーから切り離せば、全ホストで同じフォーマット群を扱える。

## 機能要件

### 追加する機能

- [x] `IImportResolver`（`Resolve(basePath, importPath)` + `Task<string?> ReadAsync(resolvedPath)`）を Core に追加
- [x] `YamlFormatLoader.LoadAsync(string yaml, string basePath, IImportResolver resolver)` を追加。循環インポート検出は既存ロジックを流用
- [x] 既定実装 `FileImportResolver`（現在の挙動）と、Web 用 `HttpImportResolver`（`formats/` 配下を相対パスで取得）
- [x] `HttpFormatCatalog`（Web、`FormatService` 経由）と `DirectoryFormatCatalog`（デスクトップ）を新 API に切り替え
- [x] Web の `format-list.json` にインポートを含む定義を再掲載（HEIF が未掲載だったので追加。avi / mp4 / wav / webp は掲載済みだが読み込みで失敗していた）

### 変更する既存機能

- [x] `LoadFromString(yaml)` は互換維持（imports ありなら従来どおり例外）

### 変更しないもの（スコープ外）

- インポートの構文・マージ規則の変更

## 受入条件

1. [x] `imports` を持つ定義を `HttpImportResolver` 経由で読み込み、`Load(path)` と同一の `FormatDefinition` になること
2. [x] 循環インポートが従来どおり検出されること
3. [x] Web GUI で `test.wav` / `test.mp4` を開いてデコードできること（bUnit + StubHttpHandler で共通型ライブラリの取得を検証）
4. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `IImportResolver`、`IFormatLoader.LoadAsync` |
| BinAnalyzer.Dsl | `LoadAsync` + resolver 経由の解決、`FileImportResolver`、`ImportPath` |
| BinAnalyzer.Web | `HttpImportResolver`、`FormatService`（`HttpFormatCatalog` が経由）、`format-list.json` |
| BinAnalyzer.Gui.Desktop | `DirectoryFormatCatalog` |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — imports の解決手段を追記
- [x] docs/architecture.md

---

## 設計メモ

### 設計方針

- **リゾルバは「識別子の解決」と「内容の取得」の 2 メソッドに分ける。** 要件案の `ReadAsync(basePath, importPath)` 1 本だと、循環検出に使う正規化済み識別子（ファイルなら絶対パス、HTTP なら結合済み URL）をローダー側で作れない。`Resolve` が返す識別子をそのまま `visited` のキーと次段の `basePath` に使うことで、ローダーはファイルシステムにも URL にも依存しない。
- **ローダー本体は 1 本の非同期実装に集約する。** `Load(path)` / `LoadFromString(yaml, basePath)` は `FileImportResolver` を渡して `GetAwaiter().GetResult()` で待つ。`FileImportResolver` は同期完了したタスクしか返さないのでデッドロックしない（WASM でも `Load(path)` は使わない）。エラーメッセージ（`循環インポートを検出しました` / `インポートファイルが見つかりません: ... (解決先: ...)`）は従来どおり。
- **相対パスの結合は `ImportPath.Combine` に切り出す。** `.` / `..` / 区切り文字の正規化を字句的に行うので、HTTP でも埋め込みリソースでもファイルシステムと同じ規則になる。`FileImportResolver` は従来どおり `Path.GetFullPath` を使う。
- **Web 側は `FormatService` の中だけを差し替える。** `HttpFormatCatalog` は `FormatService` に委譲しているので、`LoadFormatAsync` を `LoadAsync(yaml, "formats/<file>", HttpImportResolver)` に変えるだけで GUI（`GuiShell`）と旧ページ（`/classic`）の両方が imports 対応になる。

### インタフェース変更

- Core: `IImportResolver { string Resolve(string basePath, string importPath); Task<string?> ReadAsync(string resolvedPath); }`、`IFormatLoader.LoadAsync(string yaml, string basePath, IImportResolver resolver)` を追加。
- Dsl: `FileImportResolver.Instance`、`ImportPath.Combine(basePath, importPath)`。
- Web: `HttpImportResolver(HttpClient)`。
- `DirectoryFormatCatalog.LoadAsync` は `Task.FromResult` 返しから真の `async` に変更（YAML を 1 回だけ読む）。

### 代替案

- Dsl に `IImportResolver` を置く案。`IFormatLoader` が Core にあり `LoadAsync` をインタフェースに加えたかったので Core にした（Core の外部依存ゼロは維持）。
- `LoadFromString(yaml)` で imports を許可し、`FileImportResolver` にフォールバックする案。基準パスが無く解決先が定まらないので、互換のため従来どおり例外にした。

### 懸念事項

- `visited` の比較は従来どおり大文字小文字を無視する。URL は本来ケースセンシティブだが、同じディレクトリに大文字小文字違いの定義を置く運用は無いので許容した。
- ダイヤモンド型インポート（A→B, A→C, B・C→D）は従来どおり「循環」として拒否される（D が 2 回訪問されるため）。名前衝突チェックでどのみち失敗するので挙動は変えていない。

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Dsl.Tests / ImportResolverTests | `LoadAsync_WithInMemoryResolver_MatchesLoadFromFile`（wav / mp4）、`LoadAsync_WithFileImportResolver_MatchesLoad`、`LoadAsync_FileImportResolver_AcceptsRelativeBasePath` | 1 |
| Dsl.Tests / ImportResolverTests | `LoadAsync_ResolvesTransitiveImports_RelativeToEachFile`、`LoadAsync_MissingImport_ThrowsFileNotFound`、`LoadAsync_WithoutImports_DoesNotTouchResolver`、`ImportPath_Combine`（8 ケース） | 1（解決規則） |
| Dsl.Tests / ImportResolverTests | `LoadAsync_CircularImport_Throws`、`LoadAsync_SelfImport_Throws` | 2 |
| Dsl.Tests / ImportParsingTests（既存） | `Load_CircularImport_Throws` ほか — `Load(path)` が新実装に委譲しても通る | 2, 4 |
| Web.Tests / HttpImportResolverTests | `Resolve_CombinesRelativeToImportingFile`、`ReadAsync_ReturnsBody`、`ReadAsync_NotFound_ReturnsNull` | 1 |
| Web.Tests / FormatServiceTests | `LoadFormatAsync_WithImports_FetchesImportedLibraryOverHttp`、`LoadFormatAsync_WithImports_CachesDefinition`、`LoadFormatAsync_MissingImport_ThrowsWithResolvedUrl` | 1, 3 |
| Web.Tests / GuiImportTests（bUnit） | `OpenWav_ResolvesRiffLibraryOverHttp_AndDecodes`、`OpenMp4_ResolvesIsobmffLibraryOverHttp_AndDecodes`（`HttpFormatCatalog` + `StubHttpHandler` で開き `GuiShell` を描画）、`OpenWav_WhenLibraryMissing_ReportsResolvedUrl` | 3 |
| 全体 | `dotnet test` 1,460 件通過 | 4 |

Dsl.Tests / Web.Tests は `formats/wav.bdef.yaml`, `mp4.bdef.yaml`, `common/riff.bdef.yaml`, `common/isobmff.bdef.yaml` を出力ディレクトリにリンクして実物の定義で検証する。

### 気づき・今後の課題

- `testdata/real/test.wav` / `test.mp4` は git 管理外なので、テストでは同じ構造の最小バイト列（48 バイト WAV、ftyp + moov{free} + mdat の MP4）を生成している。
- `HttpImportResolver` は共通ライブラリをフォーマットごとに再取得する（`FormatService` が定義単位でキャッシュするので 1 フォーマットにつき 1 回）。ブラウザキャッシュが効くので現状は問題ないが、定義数が増えたらリゾルバ側にテキストキャッシュを足す。
