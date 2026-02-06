using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Core;

/// <summary>
/// DecodedNodeツリーをPathFilterで刈り込むヘルパー。
/// </summary>
public static class NodeFilterHelper
{
    public static DecodedStruct? FilterTree(DecodedStruct root, PathFilter filter)
    {
        return FilterStruct(root, root.Name, filter);
    }

    private static DecodedStruct? FilterStruct(DecodedStruct node, string path, PathFilter filter)
    {
        var filteredChildren = new List<DecodedNode>();

        foreach (var child in node.Children)
        {
            var childPath = $"{path}.{child.Name}";
            var filtered = FilterNode(child, childPath, filter);
            if (filtered is not null)
                filteredChildren.Add(filtered);
        }

        if (filteredChildren.Count == 0 && !filter.Matches(path))
            return null;

        return new DecodedStruct
        {
            Name = node.Name,
            StructType = node.StructType,
            Offset = node.Offset,
            Size = node.Size,
            Children = filteredChildren,
            Description = node.Description,
            IsPadding = node.IsPadding,
        };
    }

    private static DecodedNode? FilterNode(DecodedNode node, string path, PathFilter filter)
    {
        switch (node)
        {
            case DecodedStruct structNode:
                return FilterStruct(structNode, path, filter);

            case DecodedArray arrayNode:
                return FilterArray(arrayNode, path, filter);

            case DecodedCompressed compressedNode:
                return FilterCompressed(compressedNode, path, filter);

            default:
                // リーフノード: マッチすれば保持
                return filter.Matches(path) ? node : null;
        }
    }

    private static DecodedArray? FilterArray(DecodedArray node, string path, PathFilter filter)
    {
        var filteredElements = new List<DecodedNode>();

        for (var i = 0; i < node.Elements.Count; i++)
        {
            var elementPath = $"{path}.{i}";
            var element = node.Elements[i];

            if (element is DecodedStruct structElement)
            {
                var filtered = FilterStruct(structElement, elementPath, filter);
                if (filtered is not null)
                    filteredElements.Add(filtered);
            }
            else
            {
                // Non-struct array element
                if (filter.Matches(elementPath))
                    filteredElements.Add(element);
            }
        }

        if (filteredElements.Count == 0 && !filter.Matches(path))
            return null;

        return new DecodedArray
        {
            Name = node.Name,
            Offset = node.Offset,
            Size = node.Size,
            Elements = filteredElements,
            Description = node.Description,
            IsPadding = node.IsPadding,
        };
    }

    private static DecodedNode? FilterCompressed(DecodedCompressed node, string path, PathFilter filter)
    {
        if (node.DecodedContent is not null)
        {
            var filteredContent = FilterStruct(node.DecodedContent, path, filter);
            if (filteredContent is not null)
            {
                return new DecodedCompressed
                {
                    Name = node.Name,
                    Offset = node.Offset,
                    Size = node.Size,
                    CompressedSize = node.CompressedSize,
                    DecompressedSize = node.DecompressedSize,
                    Algorithm = node.Algorithm,
                    DecodedContent = filteredContent,
                    RawDecompressed = node.RawDecompressed,
                    Description = node.Description,
                    IsPadding = node.IsPadding,
                };
            }
        }

        // Treat compressed without decoded content as a leaf
        return filter.Matches(path) ? node : null;
    }
}
