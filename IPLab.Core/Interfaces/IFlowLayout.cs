using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

/// <summary>Visual layout data for a flow: operator node positions and dependency connection routing.</summary>
public interface IFlowLayout
{
    /// <summary>Position and layout metadata for each operator node.</summary>
    IReadOnlyList<OperatorLayout>    Operators    { get; }
    /// <summary>Visual routing metadata for each dependency connection.</summary>
    IReadOnlyList<DependencyLayout>  Dependencies { get; }
}
