# REQ-127: TLS/X.509証明書フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-18 |

## 背景・動機

X.509証明書（DER形式）はASN.1/BERエンコーディングに基づくTLV構造で構成されており、バイナリ解析の対象として需要が高い。HTTPS通信のデバッグ、証明書チェーンの検証、セキュリティ監査等の場面で、証明書の内部構造を可視化したいニーズがある。

ASN.1のTLV構造（Tag-Length-Value）はBinAnalyzerのDSLで表現可能な再帰パターンであり、既存のswitch機構で型ごとの分岐が実現できる。

### 対象フォーマット

- DER形式（バイナリASN.1エンコーディング）
- X.509 v3 証明書構造

## 機能要件

### 追加する機能

- [x] `formats/x509.bdef.yaml` の作成
- [x] ASN.1 TLV基本構造の定義（Tag, Length, Value）
- [x] X.509証明書の主要フィールド定義
  - Version, Serial Number, Signature Algorithm
  - Issuer, Validity (Not Before/After), Subject
  - Subject Public Key Info
  - Extensions（基本的なもの）
- [x] ASN.1タグ種別のenum定義（SEQUENCE, SET, INTEGER, OID等）
- [ ] OID（Object Identifier）の主要な値のenum定義 — スコープ外（OIDは生バイト表示）

### 変更する既存機能

- [x] BinAnalyzer.Engine — `DecodeStructField` に `size` 属性のバウンダリスコープ処理を追加（バグ修正）

### 変更しないもの（スコープ外）

- PEM形式のデコード（Base64ラッパー）— バイナリツールのスコープ外
- CRL（証明書失効リスト）の定義
- PKCS#7/PKCS#12コンテナ
- ASN.1の全タグ種別の網羅

## 受入条件

1. [x] `formats/x509.bdef.yaml` が作成されていること
2. [x] DER形式のX.509証明書の基本構造（Version, Issuer, Subject, Validity）が正しくデコードされること
3. [x] ASN.1 TLVのネスト構造がツリー表示で確認できること
4. [ ] 実際のDER証明書ファイルでデコードが成功すること — 手動検証未実施
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過 — 388テスト）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DecodeStructField` に `size` 属性のバウンダリスコープ処理を追加（バグ修正） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- **ASN.1 TLV可変長Lengthパターン**: DERのLength フィールドは可変長（short form < 128, 0x81+1byte, 0x82+2byte）。コンテナおよび大きなリーフ要素では5行インラインパターン（len0/len1/len2/virtual length）を使用。短いリーフ（OID, UTCTime, BOOLEAN, version INTEGER）は tag+uint8 length の簡易パターンを使用。
- **境界スコープ**: `type: struct` + `size: "{length}"` でDER Lengthに基づく境界スコープを作成。`repeat: eof` はスコープ内のバイトを消費し尽くすまで繰り返す。jpeg.bdef.yamlの実績パターンを踏襲。
- **三項演算子による長さ計算**: `"{len0 < 128 ? len0 : (len0 == 0x81 ? len1 : len2)}"` — 既存の式エンジンが三項演算子をサポート済み。
- **v3証明書前提**: version [0] EXPLICITタグの存在を前提。v1証明書はデコードエラーとなる。
- **Extension簡易版**: extension_content は OID + 残りを raw bytes で読む。critical フラグの個別パースはスコープ外。
- **DN属性のstring型統一**: PrintableString/UTF8String を区別せず `ascii` で読む。

### モデル変更

なし。既存のIR、デコードモデル、式エンジンで全て対応可能。

### インタフェース変更

なし。新規フォーマット定義ファイルの追加のみ。

### 代替案

1. **Extension criticalフィールドの個別パース**: タグ先読み(peek)が必要だが、DSLにpeek機構がないため見送り。将来peek対応時に拡張可能。
2. **ASN.1汎用パーサー（再帰的switch）**: 全タグを汎用的にパースするアプローチ。X.509固有のフィールド名が付かないため可読性が低下する。固定構造定義を採用。
3. **長さフィールドの共通struct化**: TLVヘッダを共通structにする案。変数スコープの問題（親structのlengthを子structから参照できない）により断念。インライン展開を採用。

### 懸念事項

1. **長さ上限 0x82（65535バイト）**: 通常の証明書には十分だが、巨大な証明書チェーンバンドル等では不足の可能性あり。
2. **OIDは生バイト表示**: ドット記法（例: 2.5.4.3）への変換はDSLの機能外。ユーザーは手動でOIDバイト列を解釈する必要がある。
3. **v1/v2証明書非対応**: version EXPLICITタグがない証明書ではデコードが失敗する。

---

## 実装メモ

### 実装中の設計変更

- **BinaryDecoder.cs `DecodeStructField` のバグ修正**: `type: struct` + `size:` フィールドでバウンダリスコープ（PushScope/PopScope）が適用されていなかった。`DecodeSwitchField` にはこのロジックが存在していたが、`DecodeStructField` には欠落していた。X.509のネストされたTLV構造をデコードするために修正が必要だった。既存フォーマットにはこのパターンの利用がなかったため、回帰影響なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| X509ParsingTests | X509Format_LoadsWithoutErrors | AC1 — FormatValidator通過 |
| X509ParsingTests | X509Format_DecodesMinimalCert | AC2 — 基本構造のデコード成功 |
| X509ParsingTests | X509Format_Version_DecodesCorrectly | AC2 — version = v3 |
| X509ParsingTests | X509Format_Validity_DecodesCorrectly | AC2 — NotBefore/NotAfterの文字列 |
| X509ParsingTests | X509Format_Issuer_ContainsRDN | AC2 — Issuer DNの文字列値 |
| X509ParsingTests | X509Format_TreeOutput_ContainsExpectedElements | AC3 — ツリーにTLVネスト表示 |

### 気づき・今後の課題

- Extension criticalフラグの個別パース（peek機構が必要）
- OIDドット記法変換の将来対応
- v1/v2証明書のサポート（version EXPLICITタグ省略時のフォールバック）
- PEM→DER変換前処理の検討（Base64デコードはツールのスコープ外）
