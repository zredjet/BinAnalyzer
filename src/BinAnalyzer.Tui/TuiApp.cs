using BinAnalyzer.Core.Decoded;
using Terminal.Gui;

namespace BinAnalyzer.Tui;

public sealed class TuiApp
{
    public void Run(DecodedStruct root, ReadOnlyMemory<byte> data, string fileName, string formatName)
    {
        Application.Init();

        try
        {
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

            var searchBar = new SearchBar(root, state, treePane)
            {
                X = 0,
                Y = Pos.AnchorEnd(1),
                Width = Dim.Fill(),
                Height = 1,
                Visible = false,
            };

            var statusLabel = new Label
            {
                Text = $" {fileName} | \u2191\u2193:Move  \u2192/Enter:Expand  \u2190:Collapse  /:Search  n/N:Next/Prev  e:ExpandAll  c:CollapseAll  q:Quit",
                X = 0,
                Y = Pos.AnchorEnd(0),
                Width = Dim.Fill(),
                Height = 1,
                ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(ColorName16.White, ColorName16.Blue),
                },
            };

            var top = new Toplevel();
            top.Add(treePane, detailPane, hexPane, searchBar, statusLabel);

            // Global key bindings
            top.KeyDown += (_, e) =>
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
                        searchBar.PerformSearch();
                        e.Handled = true;
                    }
                    return;
                }

                switch (e.KeyCode)
                {
                    case KeyCode.Q:
                        Application.RequestStop();
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

            Application.Run(top);
            top.Dispose();
        }
        finally
        {
            Application.Shutdown();
        }
    }
}
