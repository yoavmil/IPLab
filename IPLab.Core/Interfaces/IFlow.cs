namespace IPLab.Core.Interfaces;

/// <summary>Container that pairs a flow definition with its visual layout.</summary>
public interface IFlow
{
    /// <summary>The flow's data model: operators, parameters, and wiring.</summary>
    IFlowDef    Def    { get; }
    /// <summary>The visual layout: node positions and connection routing.</summary>
    IFlowLayout Layout { get; }
}
