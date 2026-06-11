namespace IPLab.Core.Utilities;

/// <summary>Graph traversal and sorting utilities for operator dependency graphs.</summary>
public static class FlowGraph
{
    /// <summary>
    /// Returns all ancestor IDs reachable from <paramref name="nodeId"/> by walking edges backwards.
    /// The node itself is not included.
    /// </summary>
    public static HashSet<string> GetAncestors(string nodeId, IEnumerable<(string Source, string Target)> edges)
    {
        var edgeList = edges.ToList();
        var visited  = new HashSet<string>();
        var queue    = new Queue<string>();
        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (src, tgt) in edgeList)
                if (tgt == current && visited.Add(src))
                    queue.Enqueue(src);
        }

        return visited;
    }

    /// <summary>
    /// Returns the given <paramref name="nodeIds"/> sorted in topological order (dependencies first).
    /// Edges that reference nodes outside <paramref name="nodeIds"/> are ignored.
    /// </summary>
    public static IReadOnlyList<string> TopologicalSort(
        IEnumerable<string> nodeIds,
        IEnumerable<(string Source, string Target)> edges)
    {
        var ids       = new HashSet<string>(nodeIds);
        var inDegree  = ids.ToDictionary(id => id, _ => 0);
        var outEdges  = ids.ToDictionary(id => id, _ => new List<string>());

        foreach (var (src, tgt) in edges)
        {
            if (!ids.Contains(src) || !ids.Contains(tgt)) continue;
            inDegree[tgt]++;
            outEdges[src].Add(tgt);
        }

        var queue  = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<string>(ids.Count);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            result.Add(id);
            foreach (var succ in outEdges[id])
                if (--inDegree[succ] == 0)
                    queue.Enqueue(succ);
        }

        return result;
    }

    /// <summary>
    /// Returns <paramref name="nodeId"/> and all descendant IDs reachable by walking edges forward.
    /// </summary>
    public static IReadOnlyList<string> GetSelfAndDescendants(string nodeId, IEnumerable<(string Source, string Target)> edges)
    {
        var edgeList = edges.ToList();
        var visited  = new HashSet<string>();
        var result   = new List<string>();
        var queue    = new Queue<string>();
        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            result.Add(current);
            foreach (var (src, tgt) in edgeList)
                if (src == current)
                    queue.Enqueue(tgt);
        }

        return result;
    }
}
