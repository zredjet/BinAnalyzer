using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Core.Interfaces;

public interface IOutputFormatter
{
    string Format(DecodedStruct root);
}
