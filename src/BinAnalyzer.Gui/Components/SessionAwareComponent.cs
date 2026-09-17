using BinAnalyzer.Gui.State;
using Microsoft.AspNetCore.Components;

namespace BinAnalyzer.Gui.Components;

/// <summary>
/// パラメータを持たず <see cref="GuiSession"/> だけを読むコンポーネントの基底。
/// Blazor は親が再描画されてもパラメータの無い子は再描画しないため、セッション変更を自分で購読する。
/// </summary>
public abstract class SessionAwareComponent : ComponentBase, IDisposable
{
    [Inject] protected GuiSession Session { get; set; } = null!;

    protected override void OnInitialized() => Session.Changed += OnSessionChanged;

    private void OnSessionChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => Session.Changed -= OnSessionChanged;
}
