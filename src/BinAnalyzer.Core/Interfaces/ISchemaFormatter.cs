using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Core.Interfaces;

public interface ISchemaFormatter
{
    string Format(FormatDefinition format);
}
