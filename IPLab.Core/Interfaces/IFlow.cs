namespace IPLab.Core.Interfaces;

public interface IFlow
{
    IFlowDef    Def    { get; }
    IFlowLayout Layout { get; }
}
