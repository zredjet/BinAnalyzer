# REQ-127: TLS/X.509証明書フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-17 |

## 背景・動機

X.509証明書（DER形式）はASN.1/BERエンコーディングに基づくTLV構造で構成されており、バイナリ解析の対象として需要が高い。HTTPS通信のデバッグ、証明書チェーンの検証、セキュリティ監査等の場面で、証明書の内部構造を可視化したいニーズがある。

ASN.1のTLV構造（Tag-Length-Value）はBinAnalyzerのDSLで表現可能な再帰パターンであり、既存のswitch機構で型ごとの分岐が実現できる。

### 対象フォーマット

- DER形式（バイナリASN.1エンコーディング）
- X.509 v3 証明書構造

## 機能要件

### 追加する機能

- [ ] `formats/x509.bdef.yaml` の作成
- [ ] ASN.1 TLV基本構造の定義（Tag, Length, Value）
- [ ] X.509証明書の主要フィールド定義
  - Version, Serial Number, Signature Algorithm
  - Issuer, Validity (Not Before/After), Subject
  - Subject Public Key Info
  - Extensions（基本的なもの）
- [ ] ASN.1タグ種別のenum定義（SEQUENCE, SET, INTEGER, OID等）
- [ ] OID（Object Identifier）の主要な値のenum定義

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- PEM形式のデコード（Base64ラッパー）— バイナリツールのスコープ外
- CRL（証明書失効リスト）の定義
- PKCS#7/PKCS#12コンテナ
- ASN.1の全タグ種別の網羅

## 受入条件

1. [ ] `formats/x509.bdef.yaml` が作成されていること
2. [ ] DER形式のX.509証明書の基本構造（Version, Issuer, Subject, Validity）が正しくデコードされること
3. [ ] ASN.1 TLVのネスト構造がツリー表示で確認できること
4. [ ] 実際のDER証明書ファイルでデコードが成功すること
5. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 変更不要
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
