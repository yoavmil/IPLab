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

    public FlowViewModel(IFlowDef flow, Action<OperatorNodeViewModel>? onOpenSettings = null,
        IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)>? connectionSides = null)
    {
        Json = FlowDefSerializer.Serialize(flow);

        var (inputSides, outputSides) = ComputeConnectorSides(flow, connectionSides);
        var nodeMap = BuildNodes(flow, inputSides, outputSides, onOpenSettings);
        BuildConnections(flow, nodeMap, connectionSides);
    }

    // Returns per-connector sides keyed by (operatorId, portOrParamName).
    private static (Dictionary<(string, string), ConnectionSide> inputs,
                    Dictionary<(string, string), ConnectionSide> outputs)
        ComputeConnectorSides(IFlowDef flow,
            IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)>? connectionSides)
    {
        var inputs  = new Dictionary<(string, string), ConnectionSide>();
        var outputs = new Dictionary<(string, string), ConnectionSide>();

        if (connectionSides is null) return (inputs, outputs);

        foreach (var op in flow.Operators)
            foreach (var param in op.Parameters.Where(p => p.Source is not null))
            {
                var src = param.Source!;
                var dep = op.Dependencies.FirstOrDefault(d => d.OperatorId == src.OperatorId);
                if (dep is null || !connectionSides.TryGetValue(dep.DependencyId, out var sides)) continue;

                outputs[(src.OperatorId, src.Port)] = sides.Source;
                inputs[(op.Id, param.Name)]          = sides.Target;
            }

        return (inputs, outputs);
    }

    private Dictionary<string, OperatorNodeViewModel> BuildNodes(
        IFlowDef flow,
        Dictionary<(string, string), ConnectionSide> inputSides,
        Dictionary<(string, string), ConnectionSide> outputSides,
        Action<OperatorNodeViewModel>? onOpenSettings)
    {
        var levels  = ComputeLevels(flow);
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

            var availableSources = nodeMap.Values
                .SelectMany(n => n.Outputs.Select(o => new SourceRefViewModel(n.Id, n.DisplayName, o.Name)))
                .ToList();

            for (int i = 0; i < ops.Count; i++)
            {
                var opId = ops[i].Id;
                var node = new OperatorNodeViewModel(
                    ops[i], availableSources,
                    getInputSide:  name => inputSides.GetValueOrDefault((opId, name),  ConnectionSide.Top),
                    getOutputSide: name => outputSides.GetValueOrDefault((opId, name), ConnectionSide.Bottom),
                    onOpenSettings: onOpenSettings)
                {
                    Location = new Point(i * xStep + xPad, y)
                };
                Nodes.Add(node);
                nodeMap[opId] = node;
            }
        }

        return nodeMap;
    }

    private void BuildConnections(IFlowDef flow, Dictionary<string, OperatorNodeViewModel> nodeMap,
        IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)>? connectionSides)
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

                if (output is null || input is null) continue;

                var depId = op.Dependencies.FirstOrDefault(d => d.OperatorId == src.OperatorId)?.DependencyId;
                var (sourceSide, targetSide) = depId is not null
                    && connectionSides?.TryGetValue(depId, out var sides) == true
                    ? sides
                    : (ConnectionSide.Bottom, ConnectionSide.Top);

                Connections.Add(new ConnectionViewModel(output, input, sourceSide, targetSide));
            }
        }
    }

    private static Dictionary<string, int> ComputeLevels(IFlowDef flow)
    {
        var levels  = flow.Operators.ToDictionary(o => o.Id, _ => 0);
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
