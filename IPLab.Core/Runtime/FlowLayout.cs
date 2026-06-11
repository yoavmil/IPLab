using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

/// <summary>Visual layout data for a flow: operator node positions and dependency connection routing.</summary>
public class FlowLayout : IFlowLayout
{
    /// <summary>An empty layout with no positions or routing data.</summary>
    public static FlowLayout Empty { get; } = new([], []);

    /// <inheritdoc/>
    public IReadOnlyList<OperatorLayout>   Operators    { get; }
    /// <inheritdoc/>
    public IReadOnlyList<DependencyLayout> Dependencies { get; }

    /// <summary>Initializes a new <see cref="FlowLayout"/> with the given operator and dependency layout collections.</summary>
    public FlowLayout(IEnumerable<OperatorLayout> operators, IEnumerable<DependencyLayout> dependencies)
    {
        Operators    = operators.ToList().AsReadOnly();
        Dependencies = dependencies.ToList().AsReadOnly();
    }
}
