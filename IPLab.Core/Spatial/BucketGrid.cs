using OpenCvSharp;

namespace IPLab.Core.Spatial;

/// <summary>
/// Immutable fixed-cell spatial index for a set of <see cref="Point2f"/> points.
/// Nearest-neighbour and range queries cost O(1) when the query radius is close to the
/// cell size (9-bucket lookup). Best suited for algorithms that repeatedly query the same
/// point set with a roughly-fixed radius — construct once, query many times.
/// </summary>
public readonly struct BucketGrid
{
    private readonly Dictionary<(int X, int Y), List<int>> _buckets;
    private readonly double _cellSize;
    private readonly Point2f[] _points;

    /// <summary>Number of points in the index.</summary>
    public int Count => _points.Length;

    /// <summary>Returns the point at the given index.</summary>
    public Point2f this[int index] => _points[index];

    /// <summary>
    /// Builds the index from <paramref name="points"/> with the given <paramref name="cellSize"/>.
    /// Choose <c>cellSize ≈ expected query radius</c> to keep each lookup at 9 buckets.
    /// </summary>
    public BucketGrid(Point2f[] points, double cellSize)
    {
        _points   = points;
        _cellSize = Math.Max(1.0, cellSize);
        _buckets  = [];
        for (int i = 0; i < points.Length; i++)
        {
            var key = BucketOf(points[i]);
            if (!_buckets.TryGetValue(key, out var list))
                _buckets[key] = list = [];
            list.Add(i);
        }
    }

    /// <summary>
    /// Returns the indices of all points within <paramref name="radius"/> of <paramref name="target"/>.
    /// </summary>
    public List<int> Query(Point2f target, double radius)
    {
        var results = new List<int>();
        int r = (int)Math.Ceiling(radius / _cellSize);
        var (cx, cy) = BucketOf(target);
        double rSq = radius * radius;
        for (int bx = cx - r; bx <= cx + r; bx++)
        for (int by = cy - r; by <= cy + r; by++)
        {
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
            foreach (int i in list)
            {
                double dx = _points[i].X - target.X, dy = _points[i].Y - target.Y;
                if (dx * dx + dy * dy <= rSq)
                    results.Add(i);
            }
        }
        return results;
    }

    /// <summary>
    /// Returns the index of the nearest point strictly within <paramref name="maxDist"/>
    /// of <paramref name="target"/> that is not flagged in <paramref name="used"/>,
    /// or <c>-1</c> if no such point exists.
    /// Pass <c>null</c> for <paramref name="used"/> to search all points without exclusion.
    /// </summary>
    public int FindNearest(Point2f target, double maxDist, bool[]? used = null)
    {
        int r = (int)Math.Ceiling(maxDist / _cellSize);
        var (cx, cy) = BucketOf(target);
        double bestSq = maxDist * maxDist;
        int best = -1;
        for (int bx = cx - r; bx <= cx + r; bx++)
        for (int by = cy - r; by <= cy + r; by++)
        {
            if (!_buckets.TryGetValue((bx, by), out var list)) continue;
            foreach (int i in list)
            {
                if (used is not null && used[i]) continue;
                double dx = _points[i].X - target.X, dy = _points[i].Y - target.Y;
                double dSq = dx * dx + dy * dy;
                if (dSq < bestSq) { bestSq = dSq; best = i; }
            }
        }
        return best;
    }

    private (int X, int Y) BucketOf(Point2f p) =>
        ((int)Math.Floor(p.X / _cellSize), (int)Math.Floor(p.Y / _cellSize));
}
