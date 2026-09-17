using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation;

public enum GhostStyle { Value, Ok, Ng }

/// <summary>ヘックス行の右側に出す「この行で決まる値」の注釈。</summary>
public sealed record GhostAnnotation(int NodeId, string Name, string ValueText, GhostStyle Style);

/// <summary>ヘックス 1 セル。<see cref="NodeId"/> が -1 ならどのフィールドにも属さない隙間バイト。</summary>
public readonly record struct HexCell(byte Value, int NodeId, FieldKind Kind, bool FieldStart, bool FieldEnd, string AncestorPath);

/// <summary>ヘックス 1 行（16 バイト）。ファイル末尾を超えるセルは null。</summary>
public sealed record HexRow(long Offset, HexCell?[] Cells, string Ascii, IReadOnlyList<GhostAnnotation> Ghosts);

/// <summary>ヘックス行を行番号から遅延生成する。全行を実体化しない。</summary>
public sealed class HexRowBuilder
{
    public const int BytesPerRow = 16;
    public const int MaxGhosts = 6;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly NodeIndex _index;

    public HexRowBuilder(ReadOnlyMemory<byte> data, NodeIndex index)
    {
        _data = data;
        _index = index;
    }

    public long Length => _data.Length;
    public int RowCount => (int)((_data.Length + BytesPerRow - 1) / BytesPerRow);
    public static int RowIndexOf(long offset) => (int)(offset / BytesPerRow);

    public HexRow Build(int rowIndex)
    {
        var rowStart = (long)rowIndex * BytesPerRow;
        var rowEnd = Math.Min(rowStart + BytesPerRow, _data.Length);
        var cells = new HexCell?[BytesPerRow];
        var ascii = new char[BytesPerRow];
        var span = _data.Span;

        for (var i = 0; i < BytesPerRow; i++)
        {
            var off = rowStart + i;
            if (off >= rowEnd)
            {
                ascii[i] = ' ';
                continue;
            }
            var b = span[(int)off];
            ascii[i] = b is >= 0x20 and < 0x7F ? (char)b : '\u00B7';
            var leaf = _index.LeafAt(off);
            if (leaf is { } l)
                cells[i] = new HexCell(b, l.Id, l.Kind, off == l.Offset, off == l.End - 1, _index.AncestorIdPath(l.Id));
            else
                cells[i] = new HexCell(b, -1, FieldKind.Bytes, false, false, "/");
        }

        var ghosts = new List<GhostAnnotation>();
        var leaves = _index.Leaves;
        for (var k = _index.FirstLeafIndexAtOrAfter(rowStart); k < leaves.Count && leaves[k].Offset < rowEnd; k++)
        {
            var leaf = leaves[k];
            if (leaf.Kind is FieldKind.Pad)
                continue;
            var node = _index.ById(leaf.Id);
            var ghost = MakeGhost(leaf, node);
            if (ghost is null)
                continue;
            ghosts.Add(ghost);
            if (ghosts.Count >= MaxGhosts)
                break;
        }

        return new HexRow(rowStart, cells, new string(ascii, 0, (int)(rowEnd - rowStart)), ghosts);
    }

    private static GhostAnnotation? MakeGhost(LeafSpan leaf, DecodedNode node)
    {
        switch (leaf.Kind)
        {
            case FieldKind.Magic:
                return node.ValidationPassed is { } vp
                    ? new GhostAnnotation(leaf.Id, node.Name, "", vp ? GhostStyle.Ok : GhostStyle.Ng)
                    : null;
            case FieldKind.Crc:
            {
                var ok = node switch
                {
                    DecodedInteger i => i.ChecksumValid,
                    DecodedBytes b => b.ChecksumValid,
                    _ => null,
                };
                return new GhostAnnotation(leaf.Id, node.Name, "", ok == false ? GhostStyle.Ng : GhostStyle.Ok);
            }
            case FieldKind.Error:
                return new GhostAnnotation(leaf.Id, node.Name, NodeDisplayText.ValueOnly(node), GhostStyle.Ng);
            default:
            {
                var text = NodeDisplayText.ValueOnly(node);
                if (text.Length == 0)
                    return null;
                if (text.Length > 40)
                    text = text[..39] + "\u2026";
                var style = node.ValidationPassed == false ? GhostStyle.Ng : GhostStyle.Value;
                return new GhostAnnotation(leaf.Id, node.Name, text, style);
            }
        }
    }
}
