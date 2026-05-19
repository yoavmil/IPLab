using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IFlowEx
{
    IFlowDef Flow { get; }
    Task RunAllAsync();
    Task RunSingleAsync(string operatorId);
    IReadOnlyDictionary<string, object?> IntermediateResults { get; }
    IReadOnlyDictionary<string, OperatorStatus> Statuses { get; }
    void ClearResults();

    /// <summary>Fired on every status transition. Exception is non-null only when status is Failed.</summary>
    event Action<string, OperatorStatus, Exception?> StatusChanged;
}
