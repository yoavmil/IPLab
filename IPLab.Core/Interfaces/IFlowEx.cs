using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>Async runtime executor for a processing flow.</summary>
public interface IFlowEx
{
    /// <summary>The flow being executed.</summary>
    IFlowDef Flow { get; }
    /// <summary>Runs all operators respecting dependency order, executing independent operators in parallel.</summary>
    Task RunAllAsync(CancellationToken ct = default);
    /// <summary>Runs a single operator by ID without re-running its predecessors.</summary>
    Task RunSingleAsync(string operatorId, CancellationToken ct = default);
    /// <summary>Intermediate results keyed by operator ID. Multi-port operators store a <c>Dictionary&lt;string, object?&gt;</c> keyed by port name.</summary>
    IReadOnlyDictionary<string, object?> IntermediateResults { get; }
    /// <summary>Current execution status for each operator, keyed by operator ID.</summary>
    IReadOnlyDictionary<string, OperatorStatus> Statuses { get; }
    /// <summary>Clears all cached results and resets all operator statuses to <see cref="OperatorStatus.NotRun"/>.</summary>
    void ClearResults();

    /// <summary>Fired on every status transition. The exception argument is non-null only when status is <see cref="OperatorStatus.Failed"/>.</summary>
    event Action<string, OperatorStatus, Exception?> StatusChanged;
}
