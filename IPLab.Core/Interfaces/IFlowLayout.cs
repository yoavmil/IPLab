using IPLab.Core.Models;

namespace IPLab.Core.Interfaces;

public interface IFlowLayout
{
    IReadOnlyList<OperatorLayout>    Operators    { get; }
    IReadOnlyList<DependencyLayout>  Dependencies { get; }
}
