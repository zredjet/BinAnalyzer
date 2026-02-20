# REQ-131: TUI（対話型ターミナルUI）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-20 |

## 背景・動機

現在のBinAnalyzerは全ての出力をワンショットで生成する。大きなバイナリファイルの解析では、ツリーの特定ノードに注目して展開・折りたたみしたり、フィールドを選択して対応するバイト範囲をhexハイライトで確認したい場面がある。

TUI（Terminal User Interface）を導入することで、バイナリ構造の対話的な探索が可能になり、ツールの利用体験が質的に変わる。

### ユースケース

- 大きなバイナリ（数千フィールド）の構造を対話的に探索する
- フィールド選択→hexビュー連動でバイト配置を確認する
- 特定フィールドの検索・フィルタリング

## 機能要件

### 追加する機能

- [x] TUIモードの起動（`--output tui` または専用サブコマンド）
- [x] ツリービュー
  - ノードの展開・折りたたみ（カーソルキーまたはEnter）
  - ノード間の移動（上下キー）
  - 全展開・全折りたたみ
- [x] 詳細ペイン
  - 選択ノードのフィールド情報（型、サイズ、オフセット、値）
  - 対応するhexダンプのハイライト表示
- [x] 検索機能
  - フィールド名によるインクリメンタル検索
  - 検索結果間のジャンプ（次/前）

### 変更する既存機能

- [x] CLIコマンド — TUI出力モードの追加

### 変更しないもの（スコープ外）

- バイナリの編集機能 — 解析ツールとしてのスコープを維持
- マウス操作 — キーボード操作のみで初期実装
- ウィンドウリサイズへの完全対応 — 基本的なリサイズのみ

## 受入条件

1. [x] TUIモードが起動できること
2. [x] デコード結果がツリーとして表示されること
3. [x] ノードの展開・折りたたみがキーボードで操作できること
4. [x] フィールド選択時に詳細情報が表示されること
5. [x] フィールド選択時に対応するhexバイト範囲がハイライトされること
6. [x] フィールド名による検索が動作すること
7. [x] qキーまたはCtrl+CでTUIを終了できること
8. [x] 既存の非TUI出力モードに影響がないこと
9. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Tui（新規） | TUI対話型ターミナルUI |
| BinAnalyzer.Cli | TUI出力モードの追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — TUIコンポーネントの追記
- [x] CLAUDE.md — プロジェクト構成にTUI追記
- [x] README.md — TUIモードの使用法追加

---

## 設計メモ

### 設計方針

**TUIライブラリ: Terminal.Gui v2 alpha** を採用する。最新の develop ビルド（`2.0.0-develop.*`）を使用。

v2 の主要な利点:
- インスタンスベースの Application モデル（`Application.Create().Init()` + `IDisposable`）
- 24-bit TrueColor サポート（RGB直接指定、TextStyle でボールド/イタリック等）
- 全 View にビルトインスクロール（ScrollView ラッパー不要）
- 新しい Key バインディングシステム（Command enum ベース、スコープ対応）
- `Dim.Auto`、`Pos.AnchorEnd`、`Pos.Align` による柔軟なレイアウト
- Adornments（Margin, Border, Padding）による洗練されたスペーシング
- 標準化された `EventHandler<T>` パターン

v2 公式ドキュメントでは新規プロジェクトに v2 alpha を推奨している。

**新規プロジェクト `BinAnalyzer.Tui`** を作成する。BinAnalyzer.Compression と同様、外部NuGet依存（Terminal.Gui）を専用プロジェクトで隔離する。Output は外部NuGet依存ゼロを維持。Core を参照（Output 参照は必要に応じて）。

**画面レイアウト: 3ペイン構成**

```
┌─ Tree ──────────────────────┬─ Detail ─────────────┐
│ ▼ png_file                  │ Field: width          │
│   ▼ ihdr                    │ Type:  uint32         │
│       width = 800       ◄── │ Offset: 0x10 (16)    │
│       height = 600          │ Size:  4 bytes        │
│       bit_depth = 8         │ Value: 800            │
│   ▶ idat                    │                       │
│   ▶ iend                    │                       │
├─ Hex ───────────────────────┴───────────────────────┤
│ 00000010  00 00 03 20 00 00 02 58  08 06 00 00 00 ...│
│           ^^^^^^^^^^^                                │
│                                                      │
├─ Status ────────────────────────────────────────────┤
│ ↑↓:Move  →/Enter:Expand  ←:Collapse  /:Search  q:Quit│
└─────────────────────────────────────────────────────┘
```

- 上段左: ツリービュー（`Dim.Percent(60)`）
- 上段右: 詳細ペイン（`Dim.Fill()`）
- 下段: Hexダンプペイン（`Dim.Percent(30)`）— ビルトインスクロールを活用
- 最下部: StatusBar（キーバインド表示）+ 検索バー

**Application ライフサイクル（v2 パターン）**:
```csharp
using IApplication app = Application.Create().Init();
var toplevel = new TuiToplevel(decoded, data, fileName, formatName);
app.Run(toplevel);
// Dispose で自動クリーンアップ
```

**状態管理**: `TuiState` クラスで一元管理（選択ノード、検索状態）。

**CLI統合**: `--output tui` で起動。既存の出力モード選択パターンに従う。

**キーバインド設計（v2 Command システム）**:

| キー | Command | 動作 |
|---|---|---|
| Up/Down | ナビゲーション | ツリーノード間移動 |
| Right/Enter | Expand | ノード展開 |
| Left | Collapse | ノード折りたたみ |
| Tab | FocusNext | ペイン間移動 |
| e | ExpandAll | 全展開 |
| c | CollapseAll | 全折りたたみ |
| / | Search | 検索バーにフォーカス |
| n | SearchNext | 次の検索結果 |
| N | SearchPrev | 前の検索結果 |
| Escape | Cancel | 検索バーを閉じる |
| q / Ctrl+C | Quit | TUI終了 |

### モデル変更

Core への変更なし。既存 DecodedNode 階層をそのまま使用する。

新規ファイル一覧:
```
src/BinAnalyzer.Tui/
├── BinAnalyzer.Tui.csproj
├── TuiApp.cs                    # エントリポイント: Application 初期化、レイアウト構築
├── TuiState.cs                  # 状態管理: 選択ノード、展開状態、検索状態
├── TreePane.cs                  # ツリービューペイン
├── DetailPane.cs                # フィールド詳細ペイン
├── HexPane.cs                   # Hexダンプペイン（選択フィールドハイライト付き）
├── SearchBar.cs                 # 検索バー（インクリメンタル検索）
├── DecodedNodeTreeBuilder.cs    # ITreeBuilder<DecodedNode> 実装
└── NodeDetailFormatter.cs       # DecodedNode → 詳細表示文字列変換
```

### インタフェース変更

- 既存 public インタフェースへの変更なし（破壊的変更ゼロ）
- `TuiApp` のみ public、他は全て internal
- HexDump/Map と同様、`IOutputFormatter` を実装しない独立クラス

### 代替案

**案A: Output プロジェクトに配置する（不採用）**
- 理由: 非TUIユーザーに Terminal.Gui 依存が伝播する。Output は外部NuGet依存ゼロを維持すべき。

**案B: Spectre.Console を使用する（不採用）**
- 理由: Spectre.Console はTUIフレームワークではなくプログレッシブ出力ライブラリ。イベントループ・フォーカス管理・ウィジェットシステムを自前実装する必要がある。

**案C: サブコマンド化する（不採用）**
- 理由: 既存 `--output` パターンとの一貫性を優先。

**案D: Terminal.Gui v1 (stable) を使用する（不採用）**
- 理由: v2 の API 改善（IDisposable、TrueColor、ビルトインスクロール、Command システム）がTUI開発に大きく寄与。v2 公式も新規プロジェクトには v2 を推奨。alpha であるが、コア API は安定。BinAnalyzer.Tui 内に閉じるため breaking changes の影響は限定的。

**案E: Pure ANSI で自前実装する（不採用）**
- 理由: 入力ループ・仮想スクリーンバッファ・スクロール管理等の全自前実装は工数が見合わない。

### 懸念事項

1. **Terminal.Gui v2 と net10.0 の互換性**: v2 develop ビルドは最新 .NET をターゲット。実際にビルドして確認が必要。
2. **v2 alpha の安定性**: コア API は安定とされるが、beta までに breaking changes の可能性あり。BinAnalyzer.Tui 内に閉じるため影響は限定的。
3. **大規模ツリーのパフォーマンス**: TreeView の `ITreeBuilder` による遅延ロードで初期表示を高速化する。
4. **HexPane の描画パフォーマンス**: v2 のビルトインスクロール + 仮想スクロール方式で対応。選択フィールド前後のみレンダリング。
5. **テスト戦略**: UI 非依存ロジック（NodeDetailFormatter、DecodedNodeTreeBuilder、検索ロジック）を分離してユニットテスト。TuiApp 自体は手動テスト。
6. **CI/CD**: ヘッドレス環境で Terminal.Gui が動作しない可能性。ロジックテストのみ CI 実行。

---

## 実装メモ

### 実装中の設計変更

- Terminal.Gui v2.0.0 stable を採用（develop ビルドではなく stable）。net10.0 で問題なくビルド可能。
- `Application.Create().Init()` パターンではなく `Application.Init()` / `Application.Run(top)` / `Application.Shutdown()` パターンを使用。v2.0.0 で安定している API。
- StatusBar は `Label` で代替実装。v2 の StatusBar API（Shortcut ベース）の複雑さを回避。
- HexPane はファイル全体ではなく選択フィールド前後 ±2KB のウィンドウ表示。大規模ファイルでのパフォーマンスを考慮。
- SearchBar のテキスト変更検出は `Accepting` イベント（Enter キー）で検索実行する方式に変更。
- `KeyCode.Esc`（`Escape` ではない）、`(KeyCode)'/'`（`Slash` ではない）が v2.0.0 の正しい名前。
- `TreeView<T>.GoTo(T)` でノード選択+スクロール（`ScrollToMakeVisible` は存在しない）。
- `Terminal.Gui.Attribute` と `System.Attribute` の名前衝突は完全修飾名で回避。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| NodeDetailFormatterTests | Format_Integer_ReturnsBasicDetails | AC4: フィールド詳細情報表示 |
| NodeDetailFormatterTests | Format_Integer_SmallValue_NoHexEntry | AC4 |
| NodeDetailFormatterTests | Format_Integer_WithEnum_IncludesEnumLabel | AC4 |
| NodeDetailFormatterTests | Format_String_ReturnsValueAndEncoding | AC4 |
| NodeDetailFormatterTests | Format_Struct_ReturnsChildCount | AC4 |
| NodeDetailFormatterTests | Format_Array_ReturnsElementCount | AC4 |
| NodeDetailFormatterTests | Format_Bytes_ReturnsHexPreview | AC4 |
| NodeDetailFormatterTests | Format_Float_ReturnsValue | AC4 |
| NodeDetailFormatterTests | Format_Float64_ReturnsFloat64Type | AC4 |
| NodeDetailFormatterTests | Format_Compressed_ReturnsAlgorithmAndSizes | AC4 |
| NodeDetailFormatterTests | Format_Error_ReturnsErrorMessage | AC4 |
| NodeDetailFormatterTests | Format_WithValidation_IncludesValidationResult | AC4 |
| NodeDetailFormatterTests | Format_Virtual_ReturnsValue | AC4 |
| DecodedNodeTreeBuilderTests | CanExpand_Struct_WithChildren_ReturnsTrue | AC2, AC3 |
| DecodedNodeTreeBuilderTests | CanExpand_Struct_Empty_ReturnsFalse | AC2, AC3 |
| DecodedNodeTreeBuilderTests | CanExpand_Struct_OnlyPadding_ReturnsFalse | AC2, AC3 |
| DecodedNodeTreeBuilderTests | CanExpand_Array_WithElements_ReturnsTrue | AC2, AC3 |
| DecodedNodeTreeBuilderTests | CanExpand_Integer_ReturnsFalse | AC2, AC3 |
| DecodedNodeTreeBuilderTests | GetChildren_Struct_ReturnNonPaddingChildren | AC2 |
| DecodedNodeTreeBuilderTests | GetChildren_Array_ReturnsAllElements | AC2 |
| DecodedNodeTreeBuilderTests | GetChildren_Compressed_WithDecodedContent_ReturnsContentChildren | AC2 |
| DecodedNodeTreeBuilderTests | GetChildren_Integer_ReturnsEmpty | AC2 |
| DecodedNodeTreeBuilderTests | GetDisplayText_* (6 tests) | AC2 |
| TuiStateTests | CollectMatches_FindsByName_CaseInsensitive | AC6 |
| TuiStateTests | CollectMatches_NoMatch_ReturnsEmpty | AC6 |
| TuiStateTests | CollectMatches_PartialMatch_FindsAll | AC6 |
| TuiStateTests | Search_SetsResultsAndIndex | AC6 |
| TuiStateTests | Search_EmptyQuery_ClearsResults | AC6 |
| TuiStateTests | NextSearchResult_CyclesThrough | AC6 |
| TuiStateTests | PreviousSearchResult_CyclesBackward | AC6 |
| TuiStateTests | NextSearchResult_NoResults_ReturnsNull | AC6 |
| TuiStateTests | SelectedNodeChanged_FiresOnChange | AC4 |
| TuiStateTests | SelectedNodeChanged_DoesNotFireOnSameValue | AC4 |
| TuiStateTests | Search_FiresSearchResultsChanged | AC6 |
| TuiStateTests | CollectMatches_InArray_FindsElements | AC6 |
| HexPaneTests | GenerateHexDump_BasicOutput_HasHeaderAndData | AC5 |
| HexPaneTests | GenerateHexDump_HighlightsSelectedBytes | AC5 |
| HexPaneTests | GenerateHexDump_EmptyData_ReturnsHeaderOnly | AC5 |
| HexPaneTests | GenerateHexDump_AsciiColumn_ShowsPrintableChars | AC5 |
| HexPaneTests | GenerateHexDump_SelectionAtOffset_MarkerOnCorrectLine | AC5 |

### 気づき・今後の課題

- マウス操作は未実装（スコープ外通り）。Terminal.Gui v2 はマウスサポートがあるため将来的に追加可能。
- HexPane の per-byte カラーハイライトは未実装。現在は `^^` マーカー行で代替。v2 の `ColorGetter` や custom drawing で改善可能。
- 大規模ファイル（>数MB）ではHexPaneの±2KB ウィンドウが快適だが、全体俯瞰には不十分。将来的にオフセットジャンプ機能の追加を検討。
- Terminal.Gui v2 は TrueColor 対応だが、現在はデフォルトのカラースキームを使用。将来的にカスタムテーマ対応可能。
- CI/CD でのヘッドレスTUIテスト: UI コンポーネント（TreePane等）のテストは手動のみ。ロジックテスト（46件）は CI で実行可能。
