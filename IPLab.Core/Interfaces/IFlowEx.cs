using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>Async runtime executor for a processing flow.</summary>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/scripts/Waviness/Program.cs">Programmatic usage example (Waviness script)</seealso>
public interface IFlowEx
{
    /// <summary>The flow being executed.</summary>
    IFlowDef Flow { get; }
    /// <summary>Runs all operators respecting dependency order, executing independent operators in parallel.</summary>
    Task RunAllAsync(CancellationToken ct = default);
    /// <summary>Runs a single operator by ID without re-running its predecessors.</summary>
    Task RunSingleAsync(string operatorId, CancellationToken ct = default);
    /// <summary>
    /// Intermediate results keyed by operator ID.
    /// Multi-port operators (those with more than one output port) store a <c>Dictionary&lt;string, object?&gt;</c>
    /// keyed by the port's <c>Name</c> string. Single-port operators store the raw value directly.
    /// </summary>
    IReadOnlyDictionary<string, object?> IntermediateResults { get; }
    /// <summary>Current execution status for each operator, keyed by operator ID.</summary>
    IReadOnlyDictionary<string, OperatorStatus> Statuses { get; }
    /// <summary>Clears all cached results and resets all operator statuses to <see cref="OperatorStatus.NotRun"/>.</summary>
    void ClearResults();

    /// <summary>
    /// Fired on every status transition.
    /// Subscriber signature: <c>(string operatorId, OperatorStatus status, Exception? exception)</c>.
    /// The <c>exception</c> argument is non-null only when <c>status</c> is <see cref="OperatorStatus.Failed"/>.
    /// </summary>
    event Action<string, OperatorStatus, Exception?> StatusChanged;
}
