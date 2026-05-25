using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IFlowEx
{
    IFlowDef Flow { get; }
    Task RunAllAsync(CancellationToken ct = default);
    Task RunSingleAsync(string operatorId, CancellationToken ct = default);
    IReadOnlyDictionary<string, object?> IntermediateResults { get; }
    IReadOnlyDictionary<string, OperatorStatus> Statuses { get; }
    void ClearResults();

    /// <summary>Fired on every status transition. Exception is non-null only when status is Failed.</summary>
    event Action<string, OperatorStatus, Exception?> StatusChanged;
}
