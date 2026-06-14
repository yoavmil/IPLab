using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>Mutable collection of operators forming a processing pipeline.</summary>
public interface IFlowDef
{
    /// <summary>All operators in the flow.</summary>
    IReadOnlyList<IOperator> Operators { get; }
    /// <summary>Adds an operator to the flow.</summary>
    void AddOperator(IOperator op);
    /// <summary>Removes the operator with the given ID, if present.</summary>
    void RemoveOperator(string operatorId);
    /// <summary>Validates structural correctness: checks for duplicate IDs, missing dependencies, type mismatches, and cycles.</summary>
    ValidationResult Validate();
}
