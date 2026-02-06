# REQ-011: Hex dump出力モード

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

バイナリ解析ツールとして、デコード結果をツリーで表示するだけでなく、元のバイナリデータの16進ダンプと構造情報を対応付けて表示する機能が有用。

フォーマット定義のデバッグ、未知のチャンクの内容確認、デコード結果とバイト列の対応確認など、ツリー表示では得にくい情報を提供する。

出力イメージ:
```
Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII            Field
────────  ───────────────────────  ───────────────────────  ───────────────  ─────────────
00000000  89 50 4E 47 0D 0A 1A 0A                           .PNG....         signature
00000008  00 00 00 0D                                        ....             chunks[0].length
0000000C  49 48 44 52                                        IHDR             chunks[0].type
00000010  00 00 00 01 00 00 00 01  08 02 00 00 00            .........        chunks[0].data
0000001D  90 77 53 DE                                        .wS.             chunks[0].crc
```

## 機能要件

### 追加する機能

- [ ] `HexDumpOutputFormatter` クラスの新規作成（`BinAnalyzer.Output` に配置）
  - `IOutputFormatter` を実装
  - `DecodedStruct` と元バイナリデータを入力とし、hex dump文字列を返す
- [ ] 出力形式:
  - オフセット列（8桁16進数）
  - バイトデータ列（16バイト/行、8バイトごとにスペース区切り）
  - ASCII表示列（印字可能文字はそのまま、それ以外は `.`）
  - フィールド名列（対応するデコード済みフィールドの名前パス）
- [ ] フィールド境界の視覚的区切り（行を分けるか、区切り線を入れる）
- [ ] CLIの `--output` オプションに `hexdump` を追加

### 変更する既存機能

- [ ] `IOutputFormatter` インタフェースの検討: hex dumpは元バイナリデータも必要なため、シグネチャの拡張が必要になる可能性がある
- [ ] `Program.cs`（CLI）: `--output hexdump` の追加

### 変更しないもの（スコープ外）

- カラー表示（REQ-012で別途対応）
- インタラクティブ操作（フィールド選択、ジャンプ等）
- 部分表示（特定オフセット範囲のみのダンプ）

## 受入条件

1. [ ] `HexDumpOutputFormatter` がhex dump文字列を生成できること
2. [ ] オフセット、バイトデータ、ASCII表示、フィールド名が各列に表示されること
3. [ ] 16バイト/行のフォーマットであること
4. [ ] 印字不能バイトが `.` で表示されること
5. [ ] フィールド名がネストしたパスで表示されること（例: `chunks[0].length`）
6. [ ] フィールド境界で行が区切られること（同じフィールドのバイトは連続行に表示）
7. [ ] CLIで `--output hexdump` を指定するとhex dump出力になること
8. [ ] PNGテストデータでhex dump出力が生成されること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `IOutputFormatter` の拡張検討 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | `HexDumpOutputFormatter` の新規追加 |
| BinAnalyzer.Cli | `--output hexdump` の追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 出力形式の説明にhex dumpを追記

---

## 設計メモ

### 設計方針

hex dump出力は元バイナリデータとデコード結果の両方が必要。現在の `IOutputFormatter.Format(DecodedStruct)` ではバイナリデータにアクセスできないため、インタフェースの拡張が必要。

**アプローチ**: `IOutputFormatter` は変更せず、`HexDumpOutputFormatter` は別のインタフェースまたは専用メソッドシグネチャで実装する。CLIで直接呼び出す。

```csharp
public sealed class HexDumpOutputFormatter
{
    public string Format(DecodedStruct root, ReadOnlyMemory<byte> data);
}
```

理由: IOutputFormatterを変更すると既存のTreeOutputFormatterとJsonOutputFormatterに不要なパラメータが追加される。hex dumpは特殊な出力形式であり、専用クラスとして扱う方が適切。

### モデル変更

新規追加のみ。既存モデルへの変更なし。

```
src/BinAnalyzer.Output/
└── HexDumpOutputFormatter.cs    # 新規追加
```

### 出力アルゴリズム

1. デコード結果ツリーを走査し、全リーフノードのオフセット→フィールドパスのマッピングを構築
2. バイナリデータを16バイトずつ走査
3. 各バイトがどのフィールドに属するかをマッピングから取得
4. フィールド境界で行を区切る（同一フィールド内は連続行）

フィールドパスの構築: `chunks[0].length`, `chunks[0].type` のように、struct名と配列インデックスを連結。

### インタフェース変更

- `IOutputFormatter` への変更なし
- CLI側で `--output hexdump` の場合に `HexDumpOutputFormatter` を直接インスタンス化し、`Format(decoded, data)` を呼び出す

### 代替案

**案A: IOutputFormatterにデータパラメータを追加する（不採用）**
- 理由: TreeOutputFormatterとJsonOutputFormatterにとって不要なパラメータ。インタフェースの汚染。

**案B: DecodedNodeにバイナリデータの参照を持たせる（不採用）**
- 理由: DecodedBytesは既にRawBytesを持つが、DecodedIntegerやDecodedStringは持たない。全ノードにバイナリ参照を追加するのは過剰。

### 懸念事項

1. **パフォーマンス**: 大きなバイナリファイルのhex dumpは出力行数が多くなる。StringBuilderで構築するため、メモリ上に全出力を保持する。ストリーミング出力は将来課題。

2. **フィールド境界の視覚的区切り**: 空行で区切るか、区切り線を入れるか。実装時に見た目を調整して決定。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通り `IOutputFormatter` は変更せず、`HexDumpOutputFormatter` は専用のシグネチャ `Format(DecodedStruct, ReadOnlyMemory<byte>)` で実装。
- CLI側で `outputFormat == "hexdump"` の場合のみ専用パスで呼び出し。
- フィールド境界での行区切りは空行ではなく、フィールド単位での行分割で対応（同一フィールド内は連続行、フィールドが変わると新しい行グループ）。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Output/HexDumpOutputFormatter.cs` | Hex dump出力フォーマッター（新規） |
| `src/BinAnalyzer.Cli/Program.cs` | `--output hexdump` 対応追加 |
| `tests/.../HexDumpOutputTests.cs` | HexDump統合テスト8件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| HexDumpOutputTests | HexDump_ContainsOffsetColumn | AC-1, AC-2 |
| HexDumpOutputTests | HexDump_ContainsHexBytes | AC-2 |
| HexDumpOutputTests | HexDump_ContainsAsciiDisplay | AC-2 |
| HexDumpOutputTests | HexDump_ContainsFieldNames | AC-2, AC-5 |
| HexDumpOutputTests | HexDump_NonPrintableBytesShownAsDot | AC-4 |
| HexDumpOutputTests | HexDump_ContainsHeaderLine | AC-2 |
| HexDumpOutputTests | HexDump_NestedFieldPath_ContainsDotNotation | AC-5 |
| HexDumpOutputTests | HexDump_Bmp_CoversAllBytes | AC-6, AC-8 |

### 受入条件の充足状況

- [x] AC-1: HexDumpOutputFormatterがhex dump文字列を生成できる
- [x] AC-2: オフセット、バイトデータ、ASCII、フィールド名が各列に表示される
- [x] AC-3: 16バイト/行フォーマット
- [x] AC-4: 印字不能バイトが `.` で表示される
- [x] AC-5: フィールド名がネストしたパスで表示される
- [x] AC-6: フィールド境界で行が区切られる
- [x] AC-7: CLIで `--output hexdump` が使える
- [x] AC-8: PNGテストデータでhex dump出力が生成される
- [x] AC-9: 既存テスト全て通過（175テスト合格）

### 気づき・今後の課題

- 大きなバイナリファイルの場合、全出力をStringBuilderに保持するためメモリ使用量が多くなる。ストリーミング出力は将来課題。
