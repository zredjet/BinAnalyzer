using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Interfaces;

public interface IBinaryDecoder
{
    DecodedStruct Decode(ReadOnlyMemory<byte> data, FormatDefinition format);
}
