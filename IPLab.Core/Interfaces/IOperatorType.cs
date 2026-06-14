using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>Stateless descriptor for one operator type: its metadata, parameter schema, output ports, and execution logic.</summary>
public interface IOperatorType
{
    /// <summary>Stable programmatic type name used for serialization and registry lookup. Never rename after release.</summary>
    string TypeName  { get; }
    /// <summary>Display category for grouping in the UI (e.g. "Filters", "Detection", "I/O").</summary>
    string Category  { get; }
    /// <summary>Icon identifier used by the UI to render a visual symbol for this operator.</summary>
    string Icon      { get; }
    /// <summary>Describes every parameter this operator accepts, including UI hints, defaults, and wiring contracts.</summary>
    IReadOnlyList<ParameterDescriptor> ParameterSchema { get; }
    /// <summary>Describes every output port this operator can produce. Single-port operators return one entry; multi-port operators return one entry per port.</summary>
    IReadOnlyList<OutputPortDescriptor> OutputPorts { get; }
    /// <summary>
    /// Executes the operator with the given resolved parameters.
    /// Single-port operators return the value directly.
    /// Multi-port operators return a <c>Dictionary&lt;string, object?&gt;</c> keyed by port name.
    /// </summary>
    object? Execute(IReadOnlyDictionary<string, object?> parameters);
}
