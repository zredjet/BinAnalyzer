using Microsoft.AspNetCore.Components;

namespace BinAnalyzer.Gui.Components;

/// <summary>
/// <c>data-n</c> 属性を持つ要素（ヘックスのセル・構造マップのセグメント等）で起きたイベント。
/// gui.js が <c>mouseover</c> / <c>click</c> を <c>nodehover</c> / <c>nodeclick</c> として登録し、
/// イベント対象から最も近い <c>[data-n]</c> のノード ID を <see cref="NodeId"/> に載せる（無ければ -1）。
/// セルごとにハンドラを持たせず、コンテナ 1 つで受けるためのもの（REQ-177）。
/// </summary>
public sealed class NodeEventArgs : EventArgs
{
    public int NodeId { get; set; } = -1;
}

// Razor コンパイラは 4 引数のコンストラクタ形でしかイベントハンドラ属性を認識しない（2 引数だと素の属性として出力される）
[EventHandler("onnodehover", typeof(NodeEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onnodeclick", typeof(NodeEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}
