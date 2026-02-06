using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Interfaces;

public interface IFormatLoader
{
    FormatDefinition Load(string path);
    FormatDefinition LoadFromString(string yaml);
}
