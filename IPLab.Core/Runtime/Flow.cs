using IPLab.Core.Interfaces;

namespace IPLab.Core.Runtime;

/// <summary>Pairs a flow definition with its visual layout.</summary>
public class Flow : IFlow
{
    /// <inheritdoc/>
    public IFlowDef    Def    { get; }
    /// <inheritdoc/>
    public IFlowLayout Layout { get; }

    /// <summary>Initializes a new <see cref="Flow"/> with the given definition and optional layout.</summary>
    /// <param name="def">The flow's data model.</param>
    /// <param name="layout">Optional visual layout; defaults to <see cref="FlowLayout.Empty"/>.</param>
    public Flow(IFlowDef def, IFlowLayout? layout = null)
    {
        Def    = def;
        Layout = layout ?? FlowLayout.Empty;
    }
}
