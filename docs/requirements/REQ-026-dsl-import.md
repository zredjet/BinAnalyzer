# REQ-026: DSLインポート機能

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

フォーマット定義が5つに増え（PNG, ZIP, ELF, WAV, BMP）、共通パターンが見えてきた。例えば「マジックバイト検証→ヘッダー→可変長チャンク列」はPNG/WAV/BMP等に共通する。また、複数フォーマットで使われるenumやflags（圧縮方式、エンディアン指定等）を個別に再定義している。

インポート機能により:
- 共通の構造体・enum・flags定義を1ファイルにまとめて再利用
- フォーマット定義の重複排除と保守性向上
- コミュニティでの定義共有が容易になる

## 機能要件

### 追加する機能

- [ ] `imports` セクションの導入
  ```yaml
  imports:
    - path: common/compression.bdef.yaml
    - path: common/checksums.bdef.yaml
  ```
- [ ] インポート対象
  - structs: インポート元の構造体をそのまま利用可能
  - enums: インポート元の列挙型をそのまま利用可能
  - flags: インポート元のフラグ定義をそのまま利用可能
- [ ] パス解決
  - 相対パス: インポート元ファイルの位置を基準に解決
  - 循環インポートの検出とエラー
- [ ] 名前衝突の扱い
  - インポート元とインポート先で同名の定義がある場合はエラー

### 変更する既存機能

- [ ] YamlModels: `imports` フィールドの追加
- [ ] YamlFormatLoader: インポートファイルの読み込み・マージ処理
- [ ] FormatValidator: インポートパスの存在チェック、循環検出

### 変更しないもの（スコープ外）

- 名前空間（`compression.deflate_method` のようなプレフィックス付き参照） — 将来課題
- 選択的インポート（特定の定義のみインポート） — 将来課題
- URL/リモートインポート — 将来課題
- インポート元のトップレベル設定（endianness等）のマージ — インポート先の設定が常に優先

## 受入条件

1. [ ] `imports` で指定したファイルのstructs/enums/flagsが利用できること
2. [ ] 相対パスが正しく解決されること
3. [ ] 循環インポートがエラーになること
4. [ ] 同名定義の衝突がエラーになること
5. [ ] 存在しないインポートパスがエラーになること
6. [ ] 多段インポート（A→B→Cのチェーン）が動作すること
7. [ ] インポート元のendianness/root/nameはインポート先に影響しないこと
8. [ ] `--no-validate` 指定時もインポート解決は行われること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（IRは変更不要。マージ後のFormatDefinitionは既存構造で表現可能） |
| BinAnalyzer.Dsl | YamlModels にimportsフィールド追加、YamlFormatLoader にインポート解決・マージロジック追加 |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — importsセクションの説明追加
- [ ] docs/architecture.md — DSLパーサーのインポート解決フロー追記
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

インポート解決は **DSLパーサー層（YamlFormatLoader）で完結** させる。IR（FormatDefinition）にはインポートの概念を持ち込まず、マージ済みの単一FormatDefinitionをEngineに渡す。これにより:
- Engineは変更不要
- Outputは変更不要
- 既存のFormatValidatorもそのまま動作（マージ後のFormatDefinitionを検証）

インポート解決のフロー:

```
main.bdef.yaml
  → YamlFormatLoader.Load(path)
    → Deserialize → YamlFormatModel（imports含む）
    → ResolveImports(model, basePath, visitedPaths)
      → 各インポートファイルを再帰的にLoad
      → 循環検出: visitedPaths (HashSet<string>) で既出パスをチェック
      → マージ: structs/enums/flags を辞書マージ、名前衝突でエラー
    → YamlToIrMapper.Map(mergedModel)
  → FormatDefinition（マージ済み）
```

### 構成

```
src/BinAnalyzer.Dsl/
├── YamlModels/
│   └── YamlFormatModel.cs       # imports フィールド追加
├── YamlFormatLoader.cs          # ResolveImports メソッド追加
└── YamlToIrMapper.cs            # 変更なし
```

### モデル変更

**YamlFormatModel に追加:**

```csharp
public sealed class YamlFormatModel
{
    // 既存フィールド...
    public List<YamlImportModel>? Imports { get; set; }  // 追加
}

// 新規
public sealed class YamlImportModel
{
    public string Path { get; set; } = "";
}
```

YAML構文:

```yaml
imports:
  - path: common/compression.bdef.yaml
  - path: common/checksums.bdef.yaml

name: MyFormat
endianness: big
root: main
structs:
  main:
    - name: data
      type: struct
      struct: compressed_block   # compression.bdef.yaml から利用
```

**IRモデル（FormatDefinition等）**: 変更なし。マージ結果は既存の `IReadOnlyDictionary<string, StructDefinition>` 等で表現可能。

### YamlFormatLoader の変更

現在の `Load` メソッド:

```csharp
public FormatDefinition Load(string path)
{
    var yaml = File.ReadAllText(path);
    return LoadFromString(yaml);
}
```

変更後:

```csharp
public FormatDefinition Load(string path)
{
    var resolvedPath = Path.GetFullPath(path);
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var model = LoadAndResolveImports(resolvedPath, visited);
    return YamlToIrMapper.Map(model);
}

private YamlFormatModel LoadAndResolveImports(string absolutePath, HashSet<string> visited)
{
    // 循環検出
    if (!visited.Add(absolutePath))
        throw new InvalidOperationException(
            $"Circular import detected: {absolutePath}");

    var yaml = File.ReadAllText(absolutePath);
    var model = Deserializer.Deserialize<YamlFormatModel>(yaml);

    if (model.Imports is null or { Count: 0 })
        return model;

    var baseDir = Path.GetDirectoryName(absolutePath)!;

    foreach (var import in model.Imports)
    {
        var importPath = Path.GetFullPath(Path.Combine(baseDir, import.Path));

        if (!File.Exists(importPath))
            throw new FileNotFoundException(
                $"Import file not found: {import.Path} (resolved to {importPath})");

        var imported = LoadAndResolveImports(importPath, visited);
        MergeDefinitions(model, imported, import.Path);
    }

    return model;
}

private static void MergeDefinitions(
    YamlFormatModel target, YamlFormatModel source, string sourcePath)
{
    // Structs マージ
    if (source.Structs is { Count: > 0 })
    {
        foreach (var (name, fields) in source.Structs)
        {
            if (target.Structs.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Duplicate struct '{name}' in import '{sourcePath}'");
            target.Structs[name] = fields;
        }
    }

    // Enums マージ
    if (source.Enums is { Count: > 0 })
    {
        target.Enums ??= new();
        foreach (var (name, entries) in source.Enums)
        {
            if (target.Enums.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Duplicate enum '{name}' in import '{sourcePath}'");
            target.Enums[name] = entries;
        }
    }

    // Flags マージ
    if (source.Flags is { Count: > 0 })
    {
        target.Flags ??= new();
        foreach (var (name, flagsDef) in source.Flags)
        {
            if (target.Flags.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Duplicate flags '{name}' in import '{sourcePath}'");
            target.Flags[name] = flagsDef;
        }
    }

    // name, endianness, root はマージしない（インポート元の値は無視）
}
```

### LoadFromString への影響

`LoadFromString` はファイルパスを持たないため、インポート解決不可。以下の対応:
- `LoadFromString` ではインポートが含まれている場合にエラー（パス解決不可）
- テスト用に `LoadFromString(yaml, basePath)` オーバーロードを追加し、basePath 相対でインポート解決

```csharp
public FormatDefinition LoadFromString(string yaml)
{
    var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
    if (model.Imports is { Count: > 0 })
        throw new InvalidOperationException(
            "Imports require a file path. Use Load(path) instead.");
    return YamlToIrMapper.Map(model);
}

public FormatDefinition LoadFromString(string yaml, string basePath)
{
    var model = Deserializer.Deserialize<YamlFormatModel>(yaml);
    if (model.Imports is { Count: > 0 })
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        visited.Add(Path.GetFullPath(basePath));  // 仮想的な自身のパス
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(basePath))!;
        foreach (var import in model.Imports)
        {
            var importPath = Path.GetFullPath(Path.Combine(baseDir, import.Path));
            if (!File.Exists(importPath))
                throw new FileNotFoundException(
                    $"Import file not found: {import.Path}");
            var imported = LoadAndResolveImports(importPath, visited);
            MergeDefinitions(model, imported, import.Path);
        }
    }
    return YamlToIrMapper.Map(model);
}
```

### IFormatLoader インタフェース変更

現在:

```csharp
public interface IFormatLoader
{
    FormatDefinition Load(string path);
    FormatDefinition LoadFromString(string yaml);
}
```

追加:

```csharp
public interface IFormatLoader
{
    FormatDefinition Load(string path);
    FormatDefinition LoadFromString(string yaml);
    FormatDefinition LoadFromString(string yaml, string basePath);  // 追加
}
```

破壊的変更: インタフェースにメソッド追加。ただしIFormatLoaderの実装は `YamlFormatLoader` のみであり影響は限定的。

### 代替案

**案A: IRレベルでインポートを表現する（不採用）**
- FormatDefinition に `ImportedFrom` メタデータを持たせ、Engineが参照解決する方式。
- 不採用理由: Engine/Outputに波及し、変更範囲が大きい。DSL層でマージする方がシンプルで、既存コンポーネントへの影響がゼロ。

**案B: 名前空間付きインポート（不採用・将来課題）**
- `imports: [{path: foo.yaml, as: foo}]` で `foo.struct_name` のように参照する方式。
- 不採用理由: 名前衝突をエラーにする単純な方式で初期実装する。名前空間は利用パターンが明確になってから検討。

**案C: インライン展開（`include` 相当）（不採用）**
- C言語の `#include` のようにYAMLを文字列レベルで展開する方式。
- 不採用理由: YAML構文解析後に辞書マージする方がエラー検出しやすく、循環検出も容易。

### 懸念事項

1. **ファイルシステム依存**: `LoadFromString` がインポートを解決するにはファイルシステムアクセスが必要。テストでは `basePath` オーバーロードと一時ファイルで対応する。

2. **パス正規化**: OS間でのパス区切り（`/` vs `\`）。`Path.GetFullPath` と `Path.Combine` を使用することで、.NETランタイムが正規化を担保する。`HashSet` の比較は `OrdinalIgnoreCase` で大文字小文字を無視（Windows互換）。

3. **インポート元の `root` / `endianness`**: インポート元ファイルにも `root` や `endianness` が記述されている可能性がある。これらはインポート先のトップレベル設定として使われない（無視される）。ただし、インポート元ファイルが単独でも有効なフォーマット定義として使えるよう、`root` の存在は許容する。

4. **YamlDotNetの `IgnoreUnmatchedProperties`**: 既に設定済みのため、インポート元に将来追加される未知のフィールドがあってもデシリアライズエラーにならない。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
