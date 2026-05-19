using IPLab.Core.Interfaces;
using IPLab.Core.Models;

namespace IPLab.Core.Runtime;

public class FlowLayout : IFlowLayout
{
    public static FlowLayout Empty { get; } = new([], []);

    public IReadOnlyList<OperatorLayout>   Operators    { get; }
    public IReadOnlyList<DependencyLayout> Dependencies { get; }

    public FlowLayout(IEnumerable<OperatorLayout> operators, IEnumerable<DependencyLayout> dependencies)
    {
        Operators    = operators.ToList().AsReadOnly();
        Dependencies = dependencies.ToList().AsReadOnly();
    }
}
