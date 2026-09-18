# REQ-181: 配布物サイズの削減（trimming / 共有フレームワークの部分参照）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | REQ-176 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

REQ-176 で self-contained 単一ファイルを各 OS 向けに配布するようになったが、サイズが 86〜93 MB ある。主因は GUI の Razor コンポーネントのために `Microsoft.AspNetCore.App` 共有フレームワークが丸ごと同梱されることで、フレームワーク依存なら 17 MB で済む。`docs/benchmark-baseline.md` では「REQ-177 以降の課題」としていたが、REQ-177 は実行時性能に絞ったため本要望に切り出す。

初回起動時のネイティブライブラリ展開（約 1 MB、約 0.9 秒）はサイズとは別の問題で、REQ-176 で計測済み。

## 機能要件

### 追加する機能

- [ ] `PublishTrimmed=true` の適用可否を検証する。Blazor（Razor Class Library の Photino.Blazor ホスト）と YamlDotNet（リフレクション）が trimming で壊れないか、GUI 起動スモーク（REQ-176）と全出力形式のゴールデンテストで確認
- [ ] trimming が使えない場合の代替: `InvariantGlobalization`（ICU の同梱回避）、`UseSystemResourceKeys`、不要な `Microsoft.AspNetCore.App` の部分参照（`FrameworkReference` の見直し）
- [ ] 結果を `docs/benchmark-baseline.md` の「GUI 配布物」に追記

### 変更しないもの（スコープ外）

- ネイティブ AOT（Blazor が対応していない）
- フレームワーク依存への切り替え（.NET ランタイム不要が配布の前提）

## 受入条件

1. [ ] 各 RID の単一ファイルが現状より 30% 以上小さいこと（目安: 60 MB 以下）。達成できない場合は理由と計測値を記録して取り下げ
2. [ ] `release.yml` の GUI 起動スモーク（3 OS）が通ること
3. [ ] 全出力形式・`patch` / `diff` / `validate` / `schema` の既存テストが publish 済みバイナリでも通ること（trimming で消えた型が無いこと）
4. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Cli | publish プロパティ |
| BinAnalyzer.Gui.Desktop | trimming 対応（必要なら `DynamicDependency` 等） |
| .github/workflows/release.yml | publish オプション |

### 変更が必要なドキュメント

- [ ] docs/benchmark-baseline.md
- [ ] README.md — 配布物サイズに言及していれば更新

---

## 設計メモ

### 設計方針
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

- YamlDotNet はリフレクションでモデルをデシリアライズするため、trimming では `DynamicallyAccessedMembers` の注釈か `TrimmerRootDescriptor` が要る

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
