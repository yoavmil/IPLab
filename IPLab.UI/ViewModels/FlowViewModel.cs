using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using IPLab.Core.Utilities;

namespace IPLab.UI.ViewModels;

public class FlowViewModel
{
    public ObservableCollection<OperatorNodeViewModel> Nodes       { get; } = [];
    public ObservableCollection<ConnectionViewModel>   Connections { get; } = [];
    public string                                      Json        { get; }
    public ICommand                                    ConnectCommand { get; }
    public ICommand                                    DeleteConnectionCommand { get; }

    private readonly Action<OperatorNodeViewModel>? _onOpenSettings;
    private readonly Action<OperatorNodeViewModel>? _onSelected;

    public FlowViewModel(IFlow flow,
                         Action<OperatorNodeViewModel>? onOpenSettings = null,
                         Action<OperatorNodeViewModel>? onSelected = null)
    {
        _onOpenSettings        = onOpenSettings;
        _onSelected            = onSelected;
        Json                   = FlowDefSerializer.Serialize(flow);
        ConnectCommand         = new RelayCommand<(object, object?)>(OnConnect);
        DeleteConnectionCommand = new RelayCommand<ConnectionViewModel>(OnDeleteConnection);

        var positionsLookup = flow.Layout.Operators
            .ToDictionary(o => o.OperatorId, o => new Point(o.Position.X, o.Position.Y));
        var sidesLookup = flow.Layout.Dependencies
            .ToDictionary(d => d.DependencyId, d => (d.SourceSide, d.TargetSide));

        var nodeMap = BuildNodes(flow.Def, positionsLookup);
        BuildConnections(flow.Def, nodeMap, sidesLookup);
    }

    public void AddNode(IOperatorType type)
    {
        int maxNum = Nodes
            .Select(n => n.Id)
            .Where(id => id.StartsWith("O") && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max();
        var newId = $"O{maxNum + 1}";

        var parameters = type.ParameterSchema
            .Select(p => new ParameterValue { Name = p.Name, Value = p.DefaultValue })
            .ToList();

        var op = new Operator
        {
            Id           = newId,
            DisplayName  = type.TypeName,
            Type         = type,
            Parameters   = parameters,
            Dependencies = []
        };

        var pos = new Point(40 + Nodes.Count * 30, 40 + Nodes.Count * 30);

        var availableSources = Nodes
            .SelectMany(n => n.Operator.Type.OutputPorts
                .Select(port => new SourceRefViewModel(n.Id, n.DisplayName, port)))
            .ToList();

        Nodes.Add(new OperatorNodeViewModel(op, availableSources, _onOpenSettings, _onSelected)
        {
            Location = pos
        });
    }

    private void OnConnect((object Source, object? Target) args)
    {
        if (args.Target is not ConnectorViewModel targetConnector ||
            args.Source is not ConnectorViewModel sourceConnector)
            return;

        var sourceNode = Nodes.FirstOrDefault(n => n.HasConnector(sourceConnector));
        var targetNode = Nodes.FirstOrDefault(n => n.HasConnector(targetConnector));

        if (sourceNode is null || targetNode is null || sourceNode == targetNode) return;

        // Top connector is input-only; prevent it from being used as a source.
        if (sourceConnector == sourceNode.TopConnector) return;

        // Reject connections that would form a cycle.
        if (WouldCreateCycle(sourceNode, targetNode)) return;

        // Replace any existing connection between the same source→target node pair.
        foreach (var old in Connections
            .Where(c => sourceNode.HasConnector(c.Source) && targetNode.HasConnector(c.Target))
            .ToList())
            Connections.Remove(old);

        var depId = $"D_{sourceNode.Id}_{targetNode.Id}";
        Connections.Add(new ConnectionViewModel(sourceConnector, targetConnector, depId));

        // Rebuild available sources for target and all downstream nodes now that the
        // new edge is in place — this adds the full ancestor chain, not just the direct source.
        foreach (var node in GetSelfAndDescendants(targetNode))
            RebuildAvailableSources(node);

        // Default the first unconnected connectable param to the new source's first port.
        var param = targetNode.Parameters.FirstOrDefault(p => p.CanBeWired && !p.IsWired);
        if (param is not null)
        {
            var firstPort = sourceNode.Operator.Type.OutputPorts.FirstOrDefault() ?? string.Empty;
            var sourceRef = param.AvailableSources.FirstOrDefault(s =>
                s.OperatorId == sourceNode.Id && s.Port == firstPort);
            if (sourceRef is not null)
            {
                param.SelectedSource = sourceRef;
                param.IsWired        = true;
            }
        }
    }

    private void OnDeleteConnection(ConnectionViewModel? conn)
    {
        if (conn is null || !Connections.Contains(conn)) return;

        var targetNode = Nodes.FirstOrDefault(n => n.HasConnector(conn.Target));

        Connections.Remove(conn);

        if (targetNode is null) return;

        // Rebuild sources for the target and every node downstream of it.
        foreach (var node in GetSelfAndDescendants(targetNode))
            RebuildAvailableSources(node);
    }

    // Syncs AvailableSources for every connectable parameter on a node to match
    // its current ancestor set: adds newly reachable sources, removes stale ones,
    // and clears wiring that pointed to a now-removed source.
    private void RebuildAvailableSources(OperatorNodeViewModel node)
    {
        var edges     = ConnectionEdges().ToList();
        var ancestors = FlowGraph.GetAncestors(node.Id, edges);

        // Build ordered list: closest ancestor first (reverse topological).
        var ordered = FlowGraph.TopologicalSort(ancestors, edges)
            .Reverse()
            .SelectMany(id =>
            {
                var ancestor = Nodes.FirstOrDefault(n => n.Id == id);
                if (ancestor is null) return Enumerable.Empty<SourceRefViewModel>();
                return ancestor.Operator.Type.OutputPorts
                    .Select(port => new SourceRefViewModel(id, ancestor.DisplayName, port));
            })
            .ToList();

        foreach (var param in node.Parameters.Where(p => p.CanBeWired))
        {
            param.AvailableSources.Clear();
            foreach (var src in ordered)
                param.AvailableSources.Add(src);

            // Re-point SelectedSource at the new instance, or clear stale wiring.
            if (param.IsWired && param.SelectedSource is not null)
            {
                var match = param.AvailableSources.FirstOrDefault(
                    s => s.OperatorId == param.SelectedSource.OperatorId && s.Port == param.SelectedSource.Port);
                if (match is not null)
                    param.SelectedSource = match;
                else
                {
                    param.IsWired        = false;
                    param.SelectedSource = null;
                }
            }
        }
    }

    private IEnumerable<OperatorNodeViewModel> GetSelfAndDescendants(OperatorNodeViewModel start) =>
        FlowGraph.GetSelfAndDescendants(start.Id, ConnectionEdges())
                 .Select(id => Nodes.FirstOrDefault(n => n.Id == id))
                 .OfType<OperatorNodeViewModel>();

    private IEnumerable<(string Source, string Target)> ConnectionEdges() =>
        Connections
            .Select(c => (
                Source: Nodes.FirstOrDefault(n => n.HasConnector(c.Source))?.Id,
                Target: Nodes.FirstOrDefault(n => n.HasConnector(c.Target))?.Id))
            .Where(e => e.Source is not null && e.Target is not null)
            .Select(e => (e.Source!, e.Target!));

    private Dictionary<string, OperatorNodeViewModel> BuildNodes(
        IFlowDef flow,
        IReadOnlyDictionary<string, Point> positionsLookup)
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

            var availableSources = nodeMap.Values
                .OrderByDescending(n => levels[n.Id])
                .SelectMany(n => n.Operator.Type.OutputPorts
                    .Select(port => new SourceRefViewModel(n.Id, n.DisplayName, port)))
                .ToList();

            for (int i = 0; i < ops.Count; i++)
            {
                var autoPos = new Point(i * xStep + xPad, y);
                var pos     = positionsLookup.TryGetValue(ops[i].Id, out var p) ? p : autoPos;

                var node = new OperatorNodeViewModel(ops[i], availableSources, _onOpenSettings, _onSelected)
                {
                    Location = pos
                };
                Nodes.Add(node);
                nodeMap[ops[i].Id] = node;
            }
        }

        return nodeMap;
    }

    private void BuildConnections(IFlowDef flow, Dictionary<string, OperatorNodeViewModel> nodeMap,
        IReadOnlyDictionary<string, (ConnectionSide Source, ConnectionSide Target)> sidesLookup)
    {
        foreach (var op in flow.Operators)
        {
            var targetNode = nodeMap[op.Id];
            foreach (var dep in op.Dependencies)
            {
                if (!nodeMap.TryGetValue(dep.OperatorId, out var sourceNode)) continue;

                var (srcSide, tgtSide) = sidesLookup.TryGetValue(dep.DependencyId, out var s)
                    ? s
                    : (ConnectionSide.Bottom, ConnectionSide.Top);

                Connections.Add(new ConnectionViewModel(
                    GetConnector(sourceNode, srcSide),
                    GetConnector(targetNode, tgtSide),
                    dep.DependencyId));
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

    // Returns true if adding source→target would create a cycle.
    // Checks whether 'source' is already reachable from 'target' via existing connections.
    private bool WouldCreateCycle(OperatorNodeViewModel source, OperatorNodeViewModel target)
    {
        var visited = new HashSet<string>();
        var queue   = new Queue<OperatorNodeViewModel>();
        queue.Enqueue(target);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (!visited.Add(node.Id)) continue;
            if (node == source) return true;

            foreach (var conn in Connections)
            {
                var connSource = Nodes.FirstOrDefault(n => n.HasConnector(conn.Source));
                if (connSource != node) continue;
                var connTarget = Nodes.FirstOrDefault(n => n.HasConnector(conn.Target));
                if (connTarget is not null) queue.Enqueue(connTarget);
            }
        }

        return false;
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
                    if (candidate > levels[op.Id]) { levels[op.Id] = candidate; changed = true; }
                }
        }
        return levels;
    }
}
