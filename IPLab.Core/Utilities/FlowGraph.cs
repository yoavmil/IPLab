namespace IPLab.Core.Utilities;

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
