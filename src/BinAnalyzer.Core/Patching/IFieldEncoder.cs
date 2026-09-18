using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Core.Patching;

/// <summary>
/// デコード済みノードの「型・サイズ・エンディアン」に従って、ユーザー入力文字列を同じ長さのバイト列に変換する。
/// GUI の値編集（REQ-169）と <c>patch</c> コマンド（REQ-164）で共有する。
/// </summary>
public interface IFieldEncoder
{
    /// <summary>ノードが本エンコーダで書き換え可能か。理由が必要なら <paramref name="reason"/> に入る。</summary>
    bool CanEncode(DecodedNode node, out string? reason);

    /// <summary>入力文字列をノードのバイト列にエンコードする。長さは常に <c>node.Size</c> に一致する。</summary>
    FieldEncodeResult Encode(DecodedNode node, string input);

    /// <summary>編集欄の初期値として使う、現在値の文字列表現。</summary>
    string InitialText(DecodedNode node);
}
