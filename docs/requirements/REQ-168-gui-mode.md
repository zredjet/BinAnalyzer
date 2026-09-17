# REQ-168: GUIモード（Blazor 共有コンポーネント + Photino.Blazor デスクトップ）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | ready |
| 優先度 | 高 |
| 依存 | REQ-167（Presentation 層） |
| 作成日 | 2026-09-17 |
| 更新日 | 2026-09-17 |

## 背景・動機

BinAnalyzer の対話的 UI は TUI（Terminal.Gui）と Web（Blazor WebAssembly、HTML フォーマッタ出力を iframe 表示するだけ）のみで、構造とバイト列を同時に見ながら辿れるグラフィカルな画面がない。

「構造を見るバイナリエディタ」の画面イメージ（Prism モックアップ）を元に、以下を満たす GUI モードを追加する。

- 1 つのデコード結果モデルから **ヘックス／構造ツリー／インスペクター／構造マップ／パンくず／ステータス** を描画し、ホバー・選択が全ビューで連動する
- ヘックスはフィールド種別ごとの「意味の色」（magic / length / tag / number / string / crc / compressed / padding）で塗り分け、行末にその行で決まる値を注釈表示する
- 右ペインで **構造 / 定義（YAML、選択フィールド行をハイライト）/ 差分（2ファイルの構造差分）** を切り替える
- TUI と同様に CLI から `-o gui` で起動でき、同じ画面を既存の Web（WASM）サイトでも提供する

### 技術選定（決定済み）

| 論点 | 決定 | 理由 |
|---|---|---|
| UI 技術 | Blazor コンポーネント（Razor Class Library）を Web(WASM) と Photino.Blazor デスクトップの両方でホスト | モックが HTML/CSS なのでほぼ 1:1 で移植可能。macOS 開発・ubuntu CI で成立。bUnit でテスト可能 |
| WinUI 3 | 不採用 | Windows 専用。この開発環境ではビルド・テスト不能 |
| Avalonia | 不採用 | クロスプラットフォームだがモックの CSS を流用できず全面再構築になる |
| 起動 | `-o gui` でインプロセス起動（`-o tui` と同じ） | 単一バイナリ、ファイル・フォーマット解決を CLI と共有 |

本要望は draft の **REQ-148（Web UI の 3 ペイン化・Diff 対応）を吸収**する。REQ-148 は取り下げ。

## 機能要件

### 追加する機能

**プロジェクト**
- [ ] `src/BinAnalyzer.Gui` — Razor Class Library。コンポーネント・CSS・ホスト抽象・セッション状態
- [ ] `src/BinAnalyzer.Gui.Desktop` — Photino.Blazor ホスト（`GuiApp.Run(GuiLaunchOptions)`）
- [ ] `tests/BinAnalyzer.Gui.Tests` — bUnit + xunit

**ViewModel 層（`BinAnalyzer.Presentation` に追加、UI 非依存）**
- [ ] `FieldKind` 列挙と `FieldKindMapper.Map(node, parent)` — ノードから意味種別（Magic/Len/Tag/Num/Str/Crc/Zip/Pad/Struct/Array/Bitfield/Flags/Virtual/Error）を決定
- [ ] `NodeIndex` — ノード ID、親、パス文字列（`chunks[1].data.width`、`DiffEngine` と同形式）、祖先 ID パス、Offset 順の葉一覧、バイト→葉の二分探索
- [ ] `HexRowBuilder` — 16 バイト/行の `HexRow`（セル: 値・ノード ID・種別・フィールド開始/終了フラグ、ASCII、ゴースト注釈）を行番号から遅延生成
- [ ] `StructureMapBuilder` — ルート直下（配列は要素ごと）のセグメントと、その直下フィールドのバンド
- [ ] `BreadcrumbBuilder` — 選択ノードの祖先チェーン
- [ ] `ChecksumSummary` — チェックサム一致数 / 圧縮ストリーム数（InfoBar 用）
- [ ] `YamlFieldLocator` — YAML テキストから `structs.<struct>` ブロック内の `- name: <field>` 行を探す（DSL パーサーは変更しない）
- [ ] `YamlHighlighter` — 行単位の簡易トークナイザ（key / string / comment / tag）

**ホスト抽象・状態（`BinAnalyzer.Gui`）**
- [ ] `IFormatCatalog`（一覧・読込（IR + YAML 文字列）・拡張子検出）、`IFileSource`（ネイティブピッカー有無・ファイル取得）
- [ ] `GuiDocument`（タブ 1 枚分の状態: データ・フォーマット・エンディアン上書き・デコード結果・索引・選択・ホバー・展開状態・検索）、`GuiSession`（タブ集合・アクティブ・右ペイン種別・差分状態）
- [ ] `DecodeService` — `DecodeWithRecovery(ErrorMode.Continue)` で部分的に壊れたファイルも描画、所要時間を計測

**コンポーネント（モックの領域に対応）**
- [ ] タイトルバー＋ファイルタブ（複数ファイルをタブで開く・閉じる）
- [ ] コマンドバー: フォーマット選択（拡張子による自動検出チップ）、エンディアン（既定 / BE / LE）、ライブデコード切替＋所要時間、差分ボタン、フィールド検索（`**.width` パターン、Ctrl+K）
- [ ] パンくず（クリックで祖先を選択）
- [ ] ヘックスビュー: 仮想化された行、意味色、フィールド境界の角丸、ゴースト注釈、ホバー/選択ハイライト、選択位置へのスクロール
- [ ] InfoBar: チェックサム一致数、圧縮ストリームの有無、デコードエラー件数
- [ ] 右ペイン「構造」: ツリー（展開/折りたたみ、選択で祖先自動展開）＋インスペクター（型 / 位置 / 生バイト / 値 / enum / フラグ / 検証 / 説明）
- [ ] 右ペイン「定義」: YAML 全文表示、選択フィールドの行をハイライトしてスクロール
- [ ] 右ペイン「差分」: 別タブ（または新規ファイル）との `DiffEngine` 結果を行表示、行クリックで該当ノード選択
- [ ] 構造マップ（フッター）: セグメント幅がバイト数に比例、ホバー/選択連動、クリックで選択
- [ ] ステータスバー: 選択パス・オフセット・サイズ・型、ファイルサイズ、フォーマット名、エンディアン、検証数

**ホスト**
- [ ] Web: `BinAnalyzer.Web` のトップページを新 GUI に置換（旧ページは `/classic` に残す）。`HttpFormatCatalog` / `BrowserFileSource`
- [ ] デスクトップ: `EmbeddedWebRootFileProvider` でアセンブリ埋め込みの静的資産を配信（物理 `wwwroot` 不要）、`DirectoryFormatCatalog`（exe 隣の `formats/` + `-f` 指定）、`PhotinoFileSource`（非同期ダイアログ）
- [ ] CLI: `-o gui` で `GuiApp.Run` を呼ぶ。`Program.cs` を明示 `[STAThread] Main` に変換

### 変更する既存機能

- [ ] `DecodeOptions` に `Endianness?` を追加し、`BinaryDecoder` が `DecodeContext` 生成時に `options?.Endianness ?? format.Endianness` を使う（struct/field 単位の上書きは従来どおり優先）
- [ ] `BinAnalyzer.Web` の `Program.cs` / `index.html` / `FormatService`（YAML 文字列の取得を追加）
- [ ] `BinAnalyzer.Cli` の出力形式一覧・ヘルプに `gui` を追加

### 変更しないもの（スコープ外）

- 値の編集・CRC 再計算・書き戻し（REQ-164 patch 依存の後続要望）
- DSL パーサーでの YAML 行番号保持（`YamlFieldLocator` のテキスト探索で代替。不足が出れば後続要望）
- スキーマ図ボタン、ブックマーク、フォーマット定義ライブラリ画面、フォーマット定義エディタ（REQ-162）
- 展開ストリーム専用 UI（ツリーで `DecodedCompressed.DecodedContent` を辿れることで足りる）
- `imports:` を使うフォーマットの Web(WASM) での読込（既存の制限。デスクトップは `Load(path)` で動作）
- Mica 等の OS ネイティブ素材（CSS で近似）
- Photino ホストの自動 UI テスト（ヘッドレス CI に WebKit がない。コンパイル保証＋手動スモーク）

## 受入条件

**ViewModel（Presentation.Tests、bUnit 不要）**
1. [ ] `FieldKindMapper` — padding > error > compressed > checksum > magic > tag > len > 型既定 の優先順で種別を返すこと（規則ごとに 1 テスト）
2. [ ] `NodeIndex.PathOf` が `DiffEngine` の `FieldPath` と同一形式（`chunks[1].data.width`、compressed 内も継続）を返し、`ByPath` で往復できること
3. [ ] `NodeIndex.LeafAt` が葉の境界・隙間バイト・BitOffset 兄弟を正しく扱うこと
4. [ ] `HexRowBuilder` — 末尾行の空セル、行を跨ぐフィールドの開始/終了フラグ、ゴースト注釈（Magic/padding 除外・上限件数）、ASCII の非印字文字置換
5. [ ] `StructureMapBuilder` — 配列子は要素ごとにセグメント化、バンドの合計サイズ = セグメントサイズ、短い文字列子がラベルになる
6. [ ] `BreadcrumbBuilder` / `ChecksumSummary` が期待値を返すこと
7. [ ] `YamlFieldLocator` — リスト形式・`fields:` 形式・引用符付き名・コメント行・存在しない struct（null）・`formats/png.bdef.yaml` の `width` 行

**エンジン**
8. [ ] `DecodeOptions.Endianness = Little` で BE 既定のフォーマットを LE で読むこと。struct 単位の `endianness:` 指定は上書きされないこと

**コンポーネント（Gui.Tests、bUnit）**
9. [ ] ツリーでノードを選択すると、ヘックスの該当バイトに `sel` クラスが付き、その数がノードの Size と一致すること
10. [ ] ホバーで `HighlightStyle` が該当ノード ID を含む CSS ルールを出力すること
11. [ ] 選択時にパンくずが祖先チェーンを表示し、クリックで祖先が選択されること
12. [ ] インスペクターが integer（型/位置/生バイト/値/検証）、enum ラベル、flags チップを描画すること
13. [ ] 定義タブで選択フィールドの行に `cur` クラスが付くこと
14. [ ] 2 ドキュメントを比較すると差分行（from/to）と件数が表示され、行クリックで左ドキュメントの該当ノードが選択されること
15. [ ] フォーマット変更・エンディアン変更で再デコードされ、選択がパスで復元されること
16. [ ] フィールド検索 `**.width` で最初の一致ノードが選択されること
17. [ ] 構造マップのセグメント幅（`--w`）がバイト数に比例すること
18. [ ] `GuiSession` — 開く→タブ追加、閉じる→隣接タブがアクティブ、再デコードで差分状態が無効化されること
19. [ ] `EmbeddedWebRootFileProvider` が `index.html` と `_content/BinAnalyzer.Gui/gui.css` を解決すること

**ホスト（手動スモーク、macOS + Windows）**
20. [ ] `dotnet run --project src/BinAnalyzer.Web` でブラウザ上に新 GUI が表示され、PNG を開いて全ビューが連動すること
21. [ ] `binanalyzer testdata/real/test.png -f formats/png.bdef.yaml -o gui` でデスクトップ窓が開き、閉じると終了コード 0 で戻ること
22. [ ] `dotnet publish src/BinAnalyzer.Cli -r <rid> -c Release` が成功すること
23. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodeOptions.Endianness` 追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DecodeContext` 生成時のエンディアン解決（2 行） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Presentation | ViewModel 群を追加 |
| BinAnalyzer.Gui | **新規**（RCL） |
| BinAnalyzer.Gui.Desktop | **新規**（Photino.Blazor ホスト） |
| BinAnalyzer.Web | トップページを新 GUI へ置換、ホスト実装追加 |
| BinAnalyzer.Cli | `-o gui` 分岐、`[STAThread] Main` |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 変更不要
- [ ] docs/architecture.md — プロジェクト構成・依存関係に Gui / Gui.Desktop を追加
- [ ] docs/cli-usage.md — `### gui` セクション（Linux は `libwebkit2gtk-4.1` が必要）
- [ ] CLAUDE.md — プロジェクト構成
- [ ] README.md — GUI モードの紹介

---

## 設計メモ

### 設計方針

**構成**

```
Cli → Dsl, Engine, Output, Tui, Gui.Desktop
Gui.Desktop → Gui (+ Photino.Blazor, Microsoft.AspNetCore.Components.WebView 10.x)
Gui → Presentation, Dsl, Engine, Output (+ Microsoft.AspNetCore.Components.Web 10.x)
Web → Gui
Tui → Presentation
Presentation → Core
```

**ホバー連動は再描画しない。** セルに `data-p="/0/3/7/17/"`（祖先 ID パス）を持たせ、`HighlightStyle` コンポーネントが `<style>` に `[data-p*="/17/"]{…}` 1 ルールを出すだけ。選択は本物の `sel` クラス（テストで検証しやすい）で、`HexRowView.ShouldRender` は新旧選択範囲と重なる行のみ true。

**ヘックスの仮想化。** `<Virtualize ItemsProvider ItemSize="26">` で `HexRowBuilder.Build(rowIndex)` を必要な行だけ生成する。100 MB = 650 万行を実体化しない。

**定義ビュー。** DSL パーサーに行番号を持たせず、`YamlFieldLocator` がテキストから `structs:` 配下の struct ブロックと `- name:` 行を探す。struct 名は選択ノードの最寄り祖先 `DecodedStruct.StructType`（ルートは `format.RootStruct`）。見つからなければハイライトなしで全文表示。

**差分。** `DiffEngine.Compare` の `DiffResult` にそのままバインド。第 2 ファイルは「別タブ」または「別のファイルを開いて比較」。再デコードで差分は無効化。

**デスクトップ。** `EmbeddedWebRootFileProvider : IFileProvider` で Gui アセンブリの `EmbeddedResource`（`_content/BinAnalyzer.Gui/*`）と Desktop の `index.html` を配信し、`PhotinoBlazorAppBuilder.CreateDefault(fileProvider, args)` に渡す。CLI の単一ファイル publish を壊さない。`-o gui` はインプロセスで `GuiApp.Run` を呼ぶ（System.CommandLine の `Invoke` は同期なので Photino の `WaitForClose()` がメインスレッドでブロックする要件を満たす。**この分岐を async 化しないこと**）。

### モデル変更

- `DecodeOptions.Endianness: Endianness?` を追加（Core）。IR / Decoded モデルは変更なし。

### インタフェース変更

- `BinaryDecoder` の public シグネチャは不変。`DecodeOptions` の追加プロパティは init-only で後方互換。
- `BinAnalyzer.Web.Pages.Home` は `/classic` に移動（既存 bUnit テストはコンポーネント型を直接描画するため影響なし）。

### 代替案

- **WinUI 3**: Windows 専用、この環境で不可 → 不採用。
- **Avalonia**: CSS を流用できず全面再構築 → 不採用。
- **Photino を別 exe にして `Process.Start`**: exe 探索・引数受け渡しが増える。publish サイズや STA で問題が出た場合の切替先として `GuiLaunchOptions` 境界を残す。
- **YamlDotNet の `Mark` で行番号を IR に持たせる**: Dsl/Core/Mapper の変更 + import 対応で約 1 日。重複名の厳密解決以外に利点がない → 後続要望。
- **ホバーでクラス付与（全行再描画）**: 行数 × セル数の再描画コストが大きい → `<style>` ルール方式。

### 懸念事項

- **Photino.Blazor 4.0.13（2025-01）は net8/net9 のみ対象**。net10 では net9 アセットが読み込まれ、`Microsoft.AspNetCore.Components.WebView` を直接 10.x 参照して統一する見込みだが未検証。**実装の最初にスパイクで確認**し、不可なら (a) Photino.Blazor をソースからプロジェクト参照、(b) `-o gui` がローカル静的 HTTP サーバーで WASM 版を配信してブラウザを開く、のいずれかへ切替える（コンポーネントは全て流用可能）。
- Windows WebView2 は STA を要求 → 明示 `[STAThread] Main`。Windows で手動スモーク。
- Photino.NET の同期 `ShowOpenFile` は Photino.Blazor 下でクラッシュするとソースに注記あり → 非同期版を使う。
- 単一ファイル publish では `Photino.Native` が初回起動時に展開される。Linux は `libwebkit2gtk-4.1` が必要。
- `TreatWarningsAsErrors` + 浮動 `10.*-*` パッケージで Razor アナライザ警告（BL0007 等）がビルドを止める可能性。
- 再デコード後はノード参照が変わる → 選択はパスで復元（キー付き配列パスは解決できない場合がある。許容）。
- bUnit で `<Virtualize>` が不安定な場合は `HexRowView` 単体テストへフォールバック。

---

## 実装メモ

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
