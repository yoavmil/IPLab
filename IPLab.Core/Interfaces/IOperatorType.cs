using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IOperatorType
{
    string TypeName  { get; }
    string Category  { get; }
    string Icon      { get; }
    IReadOnlyList<ParameterDescriptor> ParameterSchema { get; }
    IReadOnlyList<OutputPortDescriptor> OutputPorts { get; }
    object? Execute(IReadOnlyDictionary<string, object?> parameters);
}
