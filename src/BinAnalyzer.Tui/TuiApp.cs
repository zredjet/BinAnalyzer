using BinAnalyzer.Core.Decoded;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BinAnalyzer.Tui;

public sealed class TuiApp
{
    public void Run(DecodedStruct root, ReadOnlyMemory<byte> data, string fileName, string formatName)
    {
        // Terminal.Gui 2.5 instance-based application model: Create().Init() returns an
        // IApplication whose Dispose() restores the terminal (replaces Application.Init/Shutdown).
        using IApplication app = Application.Create().Init();

        var state = new TuiState();

        // Create panes
        var treePane = new TreePane(root, state)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(60),
            Height = Dim.Percent(70),
        };

        var detailPane = new DetailPane(state)
        {
            X = Pos.Right(treePane),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(70),
        };

        var hexPane = new HexPane(data, state)
        {
            X = 0,
            Y = Pos.Bottom(treePane),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        var statusLabel = new Label
        {
            Text = $" {fileName} | \u2191\u2193:Move  \u2192/Enter:Expand  \u2190:Collapse  /:Search  n/N:Next/Prev  e:ExpandAll  c:CollapseAll  q:Quit",
            X = 0,
            Y = Pos.AnchorEnd(),
            Width = Dim.Fill(),
            Height = 1,
        };
        statusLabel.SetScheme(new Scheme(new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.Blue)));

        // The search bar shares the bottom row with the status label and replaces it while visible.
        var searchBar = new SearchBar(root, state, treePane)
        {
            X = 0,
            Y = Pos.AnchorEnd(),
            Width = Dim.Fill(),
            Height = 1,
            Visible = false,
        };
        searchBar.VisibleChanged += (_, _) => statusLabel.Visible = !searchBar.Visible;

        // Runnable is the borderless root view that IApplication.Run drives (replaces Toplevel).
        using var top = new Runnable();
        top.Add(treePane, detailPane, hexPane, statusLabel, searchBar);

        // Global key bindings. Registered on the application keyboard, which raises KeyDown
        // before the focused view sees the key. This is required in Terminal.Gui 2.5 because
        // TreeView/ListView consume printable keys for letter-based navigation, so a handler on
        // the root view would never see "/", "q", "n", "e" or "c".
        app.Keyboard.KeyDown += (_, e) =>
        {
            if (searchBar.HasFocus)
            {
                if (e.KeyCode == KeyCode.Esc)
                {
                    searchBar.HideBar();
                    treePane.SetFocus();
                    e.Handled = true;
                }
                else if (e.KeyCode == KeyCode.Enter)
                {
                    searchBar.Submit();
                    e.Handled = true;
                }
                return;
            }

            switch (e.KeyCode)
            {
                case KeyCode.Q:
                    app.RequestStop();
                    e.Handled = true;
                    break;
                case (KeyCode)'/':
                    searchBar.ShowAndFocus();
                    e.Handled = true;
                    break;
                case KeyCode.N:
                {
                    var next = state.NextSearchResult();
                    if (next is not null)
                        treePane.GoTo(next);
                    e.Handled = true;
                    break;
                }
                case KeyCode.N | KeyCode.ShiftMask:
                {
                    var prev = state.PreviousSearchResult();
                    if (prev is not null)
                        treePane.GoTo(prev);
                    e.Handled = true;
                    break;
                }
                case KeyCode.E:
                    treePane.ExpandAll();
                    e.Handled = true;
                    break;
                case KeyCode.C:
                    treePane.CollapseAll();
                    e.Handled = true;
                    break;
            }
        };

        app.Run(top);
    }
}
