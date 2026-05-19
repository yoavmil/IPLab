using IPLab.Core.Interfaces;

namespace IPLab.Core.Runtime;

public class Flow : IFlow
{
    public IFlowDef    Def    { get; }
    public IFlowLayout Layout { get; }

    public Flow(IFlowDef def, IFlowLayout? layout = null)
    {
        Def    = def;
        Layout = layout ?? FlowLayout.Empty;
    }
}
