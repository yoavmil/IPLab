using System.Collections.ObjectModel;
using System.Windows;
using IPLab.Core.Interfaces;
using IPLab.Core.Serialization;

namespace IPLab.ViewModels;

public class FlowViewModel
{
    public ObservableCollection<OperatorNodeViewModel> Nodes       { get; } = [];
    public ObservableCollection<ConnectionViewModel>   Connections { get; } = [];
    public string                                      Json        { get; }

    public FlowViewModel(IFlowDef flow)
    {
        Json = FlowDefSerializer.Serialize(flow);

        var nodeMap = BuildNodes(flow);
        BuildConnections(flow, nodeMap);
    }

    private Dictionary<string, OperatorNodeViewModel> BuildNodes(IFlowDef flow)
    {
        var levels = ComputeLevels(flow);
        var byLevel = flow.Operators
            .GroupBy(o => levels[o.Id])
            .OrderBy(g => g.Key);

        const double xStep = 240;
        const double yStep = 180;
        const double xPad  = 40;
        const double yPad  = 40;

        var nodeMap = new Dictionary<string, OperatorNodeViewModel>();

        foreach (var levelGroup in byLevel)
        {
            var ops = levelGroup.ToList();
            double y = levelGroup.Key * yStep + yPad;

            for (int i = 0; i < ops.Count; i++)
            {
                var node = new OperatorNodeViewModel(ops[i])
                {
                    Location = new Point(i * xStep + xPad, y)
                };
                Nodes.Add(node);
                nodeMap[ops[i].Id] = node;
            }
        }

        return nodeMap;
    }

    private void BuildConnections(IFlowDef flow, Dictionary<string, OperatorNodeViewModel> nodeMap)
    {
        foreach (var op in flow.Operators)
        {
            var targetNode = nodeMap[op.Id];
            foreach (var param in op.Parameters.Where(p => p.Source is not null))
            {
                var src        = param.Source!;
                var sourceNode = nodeMap[src.OperatorId];
                var output     = sourceNode.Outputs.FirstOrDefault(c => c.Name == src.Port);
                var input      = targetNode.Inputs.FirstOrDefault(c => c.Name == param.Name);

                if (output is not null && input is not null)
                    Connections.Add(new ConnectionViewModel(output, input));
            }
        }
    }

    private static Dictionary<string, int> ComputeLevels(IFlowDef flow)
    {
        var levels = flow.Operators.ToDictionary(o => o.Id, _ => 0);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var op in flow.Operators)
                foreach (var dep in op.Dependencies)
                {
                    var candidate = levels[dep.OperatorId] + 1;
                    if (candidate > levels[op.Id])
                    {
                        levels[op.Id] = candidate;
                        changed = true;
                    }
                }
        }
        return levels;
    }
}
