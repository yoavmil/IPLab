using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IFlowDef
{
    IReadOnlyList<IOperator> Operators { get; }
    void AddOperator(IOperator op);
    void RemoveOperator(string operatorId);
    ValidationResult Validate();
}
