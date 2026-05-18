using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IPLab.Core.Interfaces;
using IPLab.Core.Serialization;

namespace IPLab.ViewModels;

public class FlowViewModel
{
    public ObservableCollection<OperatorNodeViewModel> Nodes       { get; } = [];
    public ObservableCollection<ConnectionViewModel>   Connections { get; } = [];
    public string                                      Json        { get; }
    public ICommand                                    ConnectCommand { get; }

    public FlowViewModel(IFlowDef flow, Action<OperatorNodeViewModel>? onOpenSettings = null,
        IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)>? connectionSides = null)
    {
        Json           = FlowDefSerializer.Serialize(flow);
        ConnectCommand = new RelayCommand<(object, object?)>(OnConnect);

        var nodeMap = BuildNodes(flow, onOpenSettings);
        BuildConnections(flow, nodeMap, connectionSides);
    }

    private void OnConnect((object Source, object? Target) args)
    {
        if (args.Target is not ConnectorViewModel targetConnector ||
            args.Source is not ConnectorViewModel sourceConnector)
            return;

        var sourceNode = Nodes.FirstOrDefault(n => n.HasConnector(sourceConnector));
        var targetNode = Nodes.FirstOrDefault(n => n.HasConnector(targetConnector));

        if (sourceNode is null || targetNode is null || sourceNode == targetNode) return;

        // Req 3: replace any existing connection between the same source→target node pair.
        foreach (var old in Connections
            .Where(c => sourceNode.HasConnector(c.Source) && targetNode.HasConnector(c.Target))
            .ToList())
            Connections.Remove(old);

        // Wire the first connectable parameter of the target to the source's first output.
        var param = targetNode.Parameters.FirstOrDefault(p => p.CanBeWired);
        if (param is not null)
        {
            var port      = sourceNode.Operator.Type.OutputPorts.FirstOrDefault() ?? string.Empty;
            var sourceRef = new SourceRefViewModel(sourceNode.Id, sourceNode.DisplayName, port);

            if (!param.AvailableSources.Any(s => s.OperatorId == sourceRef.OperatorId && s.Port == sourceRef.Port))
                param.AvailableSources.Add(sourceRef);

            param.SelectedSource = param.AvailableSources.First(s => s.OperatorId == sourceRef.OperatorId && s.Port == sourceRef.Port);
            param.IsWired        = true;
        }

        Connections.Add(new ConnectionViewModel(sourceConnector, targetConnector));
    }

    private Dictionary<string, OperatorNodeViewModel> BuildNodes(
        IFlowDef flow, Action<OperatorNodeViewModel>? onOpenSettings)
    {
        var levels  = ComputeLevels(flow);
        var byLevel = flow.Operators.GroupBy(o => levels[o.Id]).OrderBy(g => g.Key);

        const double xStep = 240;
        const double yStep = 180;
        const double xPad  = 40;
        const double yPad  = 40;

        var nodeMap = new Dictionary<string, OperatorNodeViewModel>();

        foreach (var levelGroup in byLevel)
        {
            var ops = levelGroup.ToList();
            double y = levelGroup.Key * yStep + yPad;

            // Sources from all already-placed nodes.
            var availableSources = nodeMap.Values
                .SelectMany(n => n.Operator.Type.OutputPorts
                    .Select(port => new SourceRefViewModel(n.Id, n.DisplayName, port)))
                .ToList();

            for (int i = 0; i < ops.Count; i++)
            {
                var node = new OperatorNodeViewModel(ops[i], availableSources, onOpenSettings)
                {
                    Location = new Point(i * xStep + xPad, y)
                };
                Nodes.Add(node);
                nodeMap[ops[i].Id] = node;
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
            foreach (var dep in op.Dependencies)
            {
                if (!nodeMap.TryGetValue(dep.OperatorId, out var sourceNode)) continue;

                var (srcSide, tgtSide) = connectionSides?.TryGetValue(dep.DependencyId, out var s) == true
                    ? s
                    : (ConnectionSide.Bottom, ConnectionSide.Top);

                Connections.Add(new ConnectionViewModel(
                    GetConnector(sourceNode, srcSide),
                    GetConnector(targetNode, tgtSide)));
            }
        }
    }

    private static ConnectorViewModel GetConnector(OperatorNodeViewModel node, ConnectionSide side) => side switch
    {
        ConnectionSide.Top    => node.TopConnector,
        ConnectionSide.Bottom => node.BottomConnector,
        ConnectionSide.Left   => node.LeftConnector,
        ConnectionSide.Right  => node.RightConnector,
        _                     => node.BottomConnector
    };

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
                    if (candidate > levels[op.Id]) { levels[op.Id] = candidate; changed = true; }
                }
        }
        return levels;
    }
}
