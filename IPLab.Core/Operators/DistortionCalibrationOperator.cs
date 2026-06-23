using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Spatial;
using IPLab.Core.Utilities;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Detects checkerboard corner features using a rotation-invariant saddle filter and builds a
/// sparse corner-correspondence calibration file (<see cref="CalibrationData"/>) for use with
/// <see cref="UndistortOperator"/>. The output image is the saddle-filter heatmap or an annotated
/// label overlay. When the <c>CalibFilePath</c> output is set, the file written to it is a
/// JSON-serialised <see cref="CalibrationData"/>.
/// </summary>
public class DistortionCalibrationOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "DistortionCalibration";
    /// <inheritdoc/>
    public string Category => "Calibration";
    /// <inheritdoc/>
    public string Icon => "calibration";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",                 ConnectableType = typeof(Mat) },
        new() { Name = "KernelHalfSize", Label = "Square Half-Size (px)", Type = ParameterType.Int,    DefaultValue = 7,    Min = 2 },
        new() { Name = "MinResponse",    Label = "Min Response",           Type = ParameterType.Double, DefaultValue = 0.45, Min = 0.0, Max = 1.0 },
        new() { Name = "ShowHeatmap",    Label = "Show Response Heatmap", Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "ShowLabels",     Label = "Show Grid Indices",     Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "AnchorX",        Label = "Anchor X",              Type = ParameterType.Double, DefaultValue = 0.5,  Min = 0.0, Max = 1.0 },
        new() { Name = "AnchorY",        Label = "Anchor Y",              Type = ParameterType.Double, DefaultValue = 0.5,  Min = 0.0, Max = 1.0 },
        // Optional: when > 0, the physical edge length of one checkerboard square (mm) is divided
        // by the measured pixel pitch to emit the MmPerPixel scale output.
        new() { Name = "SquareSizeMm",   Label = "Square Size (mm)",      Type = ParameterType.Double, DefaultValue = 0.0,  Min = 0.0 },
        new() { Name = "OutputFilePath", Label = "Output File Path",      Type = ParameterType.String },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image",            DataType = typeof(Mat),                IsDisplayImage = true },
        // GridCorners precedes Corners so the inspector's first-Point2f[] overlay draws the matched
        // grid crossings (the calibration correspondences), not every raw peak.
        new() { Name = "GridCorners",      DataType = typeof(Point2f[]) },
        new() { Name = "GridLines",        DataType = typeof(LineSegmentPoint[]) },
        new() { Name = "Corners",          DataType = typeof(Point2f[]) },
        new() { Name = "InlierCount",      DataType = typeof(int) },
        new() { Name = "RotationAngleDeg", DataType = typeof(double) },
        new() { Name = "MmPerPixel",       DataType = typeof(double) },
        new() { Name = "CalibFilePath",    DataType = typeof(string) },
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;
        int halfSizeParam = Convert.ToInt32(parameters.GetValueOrDefault("KernelHalfSize") ?? 0);
        var minResp = (float)Convert.ToDouble(parameters.GetValueOrDefault("MinResponse") ?? 0.0);
        bool showHeat   = Convert.ToBoolean(parameters.GetValueOrDefault("ShowHeatmap")  ?? false);
        bool showLabels = Convert.ToBoolean(parameters.GetValueOrDefault("ShowLabels")   ?? false);
        double anchorX  = Convert.ToDouble(parameters.GetValueOrDefault("AnchorX") ?? 0.5);
        double anchorY  = Convert.ToDouble(parameters.GetValueOrDefault("AnchorY") ?? 0.5);
        double squareSizeMm = Convert.ToDouble(parameters.GetValueOrDefault("SquareSizeMm") ?? 0.0);
        var outPath = parameters.GetValueOrDefault("OutputFilePath") as string;

        if (image.Channels() != 1)
            throw new ArgumentException("DistortionCalibration requires a single-channel (grayscale) image.");

        using var imageF = new Mat();
        image.ConvertTo(imageF, MatType.CV_32F);

        int halfSize = Math.Max(2, halfSizeParam);

        // ComputeSaddleMagnitude returns a Mat the caller must dispose.
        // K₀ (axis-aligned) and K₄₅ (diagonal) are orthogonal checkerboard kernels.
        // sqrt(R₀² + R₄₅²) is invariant to checkerboard rotation angle.
        var mag = ComputeSaddleMagnitude(imageF, halfSize);
        try
        {
            var corners = FindPeaks(mag, minResp);

            if (corners.Length > 0)
            {
                int win = Math.Max(2, halfSize / 2);
                corners = Cv2.CornerSubPix(imageF, corners,
                    new Size(win, win), new Size(-1, -1),
                    new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 40, 0.001));
            }

            var grid = InferGrid(corners, image.Width, image.Height, halfSize, anchorX, anchorY);
            if (!grid.Found)
                throw new InvalidOperationException("Checkerboard grid not found: too few inlier corners.");

            var gridLines = BuildGridLines(grid);

            // mm/pixel = physical square edge length / measured pixel pitch. Only meaningful
            // when the user supplies a positive square size; otherwise the scale is unknown.
            double? mmPerPixel = null;
            if (squareSizeMm > 0)
            {
                float pixelPitch = ComputeGridPitch(grid);
                if (pixelPitch > 0) mmPerPixel = squareSizeMm / pixelPitch;
            }

            string? calibPath = null;
            if (!string.IsNullOrWhiteSpace(outPath))
            {
                // Build cornerMap from detected corners only (meanCol/Row must reflect
                // detected corners before gap-filling changes the key set).
                var cornerMap = new Dictionary<(int Col, int Row), Point2f>(grid.Cells.Length);
                for (int k = 0; k < grid.Cells.Length; k++)
                    cornerMap[(grid.Cells[k].I, grid.Cells[k].J)] = grid.ImagePoints[k];

                float meanCol = (float)cornerMap.Keys.Average(k => k.Col);
                float meanRow = (float)cornerMap.Keys.Average(k => k.Row);

                FillGaps(cornerMap);
                float pitch = ComputePitch(cornerMap);

                float ax = (float)(anchorX * image.Width);
                float ay = (float)(anchorY * image.Height);
                int targetColMin = (int)Math.Floor  ((                0 - ax) / pitch + meanCol) - 1;
                int targetColMax = (int)Math.Ceiling((image.Width  - 1 - ax) / pitch + meanCol) + 1;
                int targetRowMin = (int)Math.Floor  ((ay - (image.Height - 1)) / pitch + meanRow) - 1;
                int targetRowMax = (int)Math.Ceiling((ay -                  0) / pitch + meanRow) + 1;
                ExtendBoundary(cornerMap, targetColMin, targetColMax, targetRowMin, targetRowMax);

                var calibCorners = cornerMap
                    .Select(kv => new CornerRecord
                    {
                        Col  = kv.Key.Col,
                        Row  = kv.Key.Row,
                        ImgX = kv.Value.X,
                        ImgY = kv.Value.Y,
                    })
                    .ToList();

                CalibrationHelpers.Save(new CalibrationData
                {
                    ImageWidth       = image.Width,
                    ImageHeight      = image.Height,
                    AnchorX          = anchorX,
                    AnchorY          = anchorY,
                    RotationAngleDeg = grid.RotationAngleDeg,
                    Pitch            = pitch,
                    MeanCol          = meanCol,
                    MeanRow          = meanRow,
                    Corners          = calibCorners,
                }, outPath!);
                calibPath = outPath;
            }

            return new Dictionary<string, object?>
            {
                ["Image"]          = showHeat   ? BuildHeatmap(mag)
                                   : showLabels ? BuildLabelImage(image, grid)
                                   : null,
                ["GridCorners"]    = grid.ImagePoints,
                ["GridLines"]      = gridLines,
                ["Corners"]        = corners,
                ["InlierCount"]    = grid.ImagePoints.Length,
                ["RotationAngleDeg"] = grid.RotationAngleDeg,
                ["MmPerPixel"]     = mmPerPixel,
                ["CalibFilePath"]  = calibPath,
            };
        }
        finally
        {
            mag.Dispose();
        }
    }

    // Returns a new Mat (caller must dispose) containing the saddle-filter magnitude response.
    static Mat ComputeSaddleMagnitude(Mat imageF, int halfSize)
    {
        using var k0  = BuildCheckerboardKernel(halfSize, diagonal: false);
        using var k45 = BuildCheckerboardKernel(halfSize, diagonal: true);
        using var r0  = new Mat();
        using var r45 = new Mat();
        Cv2.Filter2D(imageF, r0,  MatType.CV_32F, k0);
        Cv2.Filter2D(imageF, r45, MatType.CV_32F, k45);
        var mag = new Mat();
        Cv2.Magnitude(r0, r45, mag);
        return mag;
    }

    // ── constants ────────────────────────────────────────────────────────────

    private const int    MinInliers           = 12;
    private const double LocalTolFraction     = 0.35;  // RegionGrow acceptance tolerance
    private const double LookupRadiusFraction = 0.5;   // RegionGrow search radius; also bucket cellSize (→ 9-bucket lookup)
    // Cell size used for the raw-corner grid (EstimatePitch + FilterByPitchConsistency).
    // 64 px matches EstimatePitch's initial search envelope so the first Query ring = 9 buckets.
    private const double RawGridCellSize = 64.0;
    private const double OutlierTolFraction   = 0.05;
    // Maximum number of seeds to try; ordered nearest-to-anchor first.
    private const int MaxSeeds = 49;

    // ── private record types ─────────────────────────────────────────────────

    private sealed record GridInference(
        bool Found,
        Point2f[] ImagePoints,
        Cell[] Cells,
        double RotationAngleDeg)
    {
        public static GridInference Empty { get; } = new(false, [], [], 0.0);
    }

    private readonly record struct Cell(int I, int J);
    // CornerIdx is stored so replacement logic can correctly update the used set.
    private readonly record struct MatchedCell(Point2f Point, double Residual, int CornerIdx);

    // ── detection ────────────────────────────────────────────────────────────

    // Builds a (halfSize*2)×(halfSize*2) checkerboard saddle kernel, normalized so L1 = 1.
    // K₀ (diagonal=false): four rectangular quadrants, alternating ±1.
    // K₄₅ (diagonal=true): four diagonal sectors — top/bottom ±1, left/right ∓1.
    static Mat BuildCheckerboardKernel(int halfSize, bool diagonal)
    {
        int ksize  = halfSize * 2;
        var kernel = new Mat(ksize, ksize, MatType.CV_32F);
        var indexer = kernel.GetGenericIndexer<float>();
        int count = 0;

        for (int r = 0; r < ksize; r++)
            for (int c = 0; c < ksize; c++)
            {
                float dr = r - halfSize + 0.5f;
                float dc = c - halfSize + 0.5f;
                float val;

                if (diagonal)
                {
                    float d = Math.Abs(dr) - Math.Abs(dc);
                    val = d > 0f ? 1f : d < 0f ? -1f : 0f;
                }
                else
                {
                    val = (dr < 0f) == (dc < 0f) ? 1f : -1f;
                }

                indexer[r, c] = val;
                if (val != 0f) count++;
            }

        float norm = 1f / count;
        for (int r = 0; r < ksize; r++)
            for (int c = 0; c < ksize; c++)
                if (indexer[r, c] != 0f) indexer[r, c] *= norm;

        return kernel;
    }

    // 3×3 local maxima (8-connected) above a threshold relative to the global maximum.
    // Using the immediate neighbourhood instead of a large fixed window means no corner
    // is suppressed by a stronger adjacent corner — even at the image border, where the
    // saddle response is slightly weaker due to partial filter support.
    static Point2f[] FindPeaks(Mat mag, float relativeThreshold)
    {
        Cv2.MinMaxLoc(mag, out _, out double maxVal);
        if (maxVal <= 0) return [];

        using var se3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var dilated = new Mat();
        Cv2.Dilate(mag, dilated, se3, borderType: BorderTypes.Isolated);

        using var isLocalMax = new Mat();
        Cv2.Compare(mag, dilated, isLocalMax, CmpTypes.GE);

        using var magU8 = new Mat();
        Cv2.Normalize(mag, magU8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        using var aboveThresh = new Mat();
        Cv2.Threshold(magU8, aboveThresh, relativeThreshold * 255.0, 255, ThresholdTypes.Binary);

        using var peaks = new Mat();
        Cv2.BitwiseAnd(isLocalMax, aboveThresh, peaks);

        var pts = peaks.FindNonZeroPoints();
        var result = new Point2f[pts.Length];
        for (int i = 0; i < pts.Length; i++) result[i] = new Point2f(pts[i].X, pts[i].Y);
        return result;
       
    }

    // K=4 nearest-neighbour distance median using BucketGrid, O(N).
    // Growing search radius avoids assuming a pitch scale in advance.
    static float EstimatePitch(BucketGrid rawGrid)
    {
        const int K = 4;
        var distances = new List<float>(rawGrid.Count * K);
        for (int i = 0; i < rawGrid.Count; i++)
        {
            var pt = rawGrid[i];
            for (double r = RawGridCellSize; r <= 4096.0; r *= 2.0)
            {
                var buf = rawGrid.Query(pt, r);
                var nearest = buf
                    .Where(j => j != i)
                    .Select(j => { double dx = rawGrid[j].X - pt.X, dy = rawGrid[j].Y - pt.Y; return (float)Math.Sqrt(dx * dx + dy * dy); })
                    .OrderBy(d => d)
                    .Take(K)
                    .ToList();
                if (nearest.Count >= K) { distances.AddRange(nearest); break; }
            }
        }
        if (distances.Count == 0) return 1f;
        distances.Sort();
        return distances[distances.Count / 2];
    }

    // Discard corners with fewer than 2 neighbours in [0.6P, 1.4P] using BucketGrid, O(N).
    // Removes isolated false positives that have no grid-adjacent neighbours at the right pitch distance.
    static Point2f[] FilterByPitchConsistency(BucketGrid rawGrid, float pitch)
    {
        double lo = pitch * 0.6, hi = pitch * 1.4;
        double loSq = lo * lo, hiSq = hi * hi;
        var result = new List<Point2f>(rawGrid.Count);
        for (int i = 0; i < rawGrid.Count; i++)
        {
            var pt = rawGrid[i];
            var buf = rawGrid.Query(pt, hi);
            int count = 0;
            foreach (int j in buf)
            {
                if (j == i) continue;
                double dx = rawGrid[j].X - pt.X, dy = rawGrid[j].Y - pt.Y;
                double dSq = dx * dx + dy * dy;
                if (dSq >= loSq && dSq <= hiSq && ++count >= 2) break;
            }
            if (count >= 2) result.Add(pt);
        }
        return [.. result];
    }

    // ── grid inference ────────────────────────────────────────────────────────────

    // Swap axes so axisA is the more horizontal vector, then flip each to canonical direction:
    // axisA points right (positive image X), axisB points upward (negative image Y).
    static (Point2f axisA, Point2f axisB) CanonicalizeAxes(
        Point2f va, double lenA, Point2f vb, double lenB)
    {
        if (Math.Abs(vb.X) / lenB > Math.Abs(va.X) / lenA)
            (va, vb) = (vb, va);
        if (va.X < 0) va = new Point2f(-va.X, -va.Y);
        if (vb.Y > 0) vb = new Point2f(-vb.X, -vb.Y);
        return (va, vb);
    }


    // Score = count × fill; higher score favours grids that are both dense and complete.
    static double ScoreGrid(Dictionary<Cell, MatchedCell> cells)
    {
        if (cells.Count < MinInliers) return 0;
        int minI = int.MaxValue, maxI = int.MinValue, minJ = int.MaxValue, maxJ = int.MinValue;
        foreach (var c in cells.Keys)
        {
            if (c.I < minI) minI = c.I; if (c.I > maxI) maxI = c.I;
            if (c.J < minJ) minJ = c.J; if (c.J > maxJ) maxJ = c.J;
        }
        int cols = maxI - minI + 1, rows = maxJ - minJ + 1;
        return cells.Count * ((double)cells.Count / (cols * rows));
    }

    static GridInference InferGrid(
        Point2f[] rawCorners, int width, int height, int edgeMargin,
        double anchorX, double anchorY)
    {
        if (rawCorners.Length < MinInliers) return GridInference.Empty;

        var rawGrid = new BucketGrid(rawCorners, RawGridCellSize);
        float pitch  = EstimatePitch(rawGrid);
        var corners  = FilterByPitchConsistency(rawGrid, pitch);
        if (corners.Length < MinInliers) return GridInference.Empty;

        double lookupRadius = Math.Max(2.0, pitch * LookupRadiusFraction);
        var grid = new BucketGrid(corners, lookupRadius);

        double loSq = (double)(pitch * 0.7f) * (pitch * 0.7f);
        double hiSq = (double)(pitch * 1.3f) * (pitch * 1.3f);

        var anchorPt = new Point2f((float)(anchorX * width), (float)(anchorY * height));
        var orderedSeeds = Enumerable.Range(0, grid.Count)
            .Select(i => (i, dSq: DistSq(grid[i], anchorPt)))
            .OrderBy(x => x.dSq)
            .Take(MaxSeeds)
            .Select(x => x.i)
            .ToArray();

        Dictionary<Cell, MatchedCell>? best = null;
        double bestScore = 0;
        var usedBuf = new bool[grid.Count];

        foreach (int seedIdx in orderedSeeds)
        {
            var origin = grid[seedIdx];
            var neighborBuf = grid.Query(origin, pitch * 1.3f);

            var neighbors = neighborBuf
                .Where(i => i != seedIdx)
                .Select(i => (i, pt: grid[i], dSq: DistSq(grid[i], origin)))
                .Where(x => x.dSq >= loSq && x.dSq <= hiSq)
                .Select(x => (x.pt, x.i, len: Math.Sqrt(x.dSq)))
                .ToArray();

            // After CanonicalizeAxes, all truly-perpendicular pairs from the same checkerboard
            // produce identical axes — trying every pair is redundant. Pick the single most-
            // perpendicular pair (minimum |cos|) per seed.
            int bestAi = -1, bestBi = -1;
            double bestAbsCos = 0.5; // pairs with |cos| ≥ 0.5 are not perpendicular enough
            for (int ai = 0; ai < neighbors.Length; ai++)
            {
                var va = neighbors[ai].pt - origin;
                for (int bi = ai + 1; bi < neighbors.Length; bi++)
                {
                    var vb = neighbors[bi].pt - origin;
                    double cos = Math.Abs(va.X * vb.X + va.Y * vb.Y)
                                 / (neighbors[ai].len * neighbors[bi].len);
                    if (cos < bestAbsCos) { bestAbsCos = cos; bestAi = ai; bestBi = bi; }
                }
            }
            if (bestAi < 0) continue;

            {
                var (pa, _, lenA) = neighbors[bestAi];
                var (pb, _, lenB) = neighbors[bestBi];
                var (axisA, axisB) = CanonicalizeAxes(pa - origin, lenA, pb - origin, lenB);
                Array.Clear(usedBuf, 0, usedBuf.Length);
                var grown = RegionGrow(grid, origin, axisA, axisB, pitch, usedBuf);
                double score = ScoreGrid(grown);
                if (score > bestScore) { best = grown; bestScore = score; }
            }
        }

        if (best is null || best.Count < MinInliers) return GridInference.Empty;

        RejectOutliers(best, pitch);
        if (best.Count < MinInliers) return GridInference.Empty;

        // Drop corners within edgeMargin of any border — saddle kernel bias near image edges.
        if (edgeMargin > 0)
        {
            var edgeCells = best
                .Where(kv =>
                    kv.Value.Point.X < edgeMargin || kv.Value.Point.X >= width  - edgeMargin ||
                    kv.Value.Point.Y < edgeMargin || kv.Value.Point.Y >= height - edgeMargin)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var cell in edgeCells) best.Remove(cell);
            if (best.Count < MinInliers) return GridInference.Empty;
        }

        double rotAngle = ComputeRotationAngle(best);

        int minI = best.Keys.Min(c => c.I);
        int minJ = best.Keys.Min(c => c.J);

        // Sort by row then column for stable, debuggable output.
        var sorted = best.OrderBy(kv => kv.Key.J - minJ).ThenBy(kv => kv.Key.I - minI).ToArray();
        int n = sorted.Length;
        var imagePoints  = new Point2f[n];
        var cellList     = new Cell[n];
        for (int k = 0; k < n; k++)
        {
            var (cell, match) = sorted[k];
            int ni = cell.I - minI, nj = cell.J - minJ;
            imagePoints[k]  = match.Point;
            cellList[k]     = new Cell(ni, nj);
        }

        return new GridInference(true, imagePoints, cellList, rotAngle);
    }

    // ── region grow + outlier rejection ───────────────────────────────────────

    // Grows a grid BFS from origin using axes already canonicalized (axisA→right, axisB→up).
    // For each predicted cell position, queries all candidates within lookupRadius and picks
    // the one with the lowest residual — never "first match wins."
    // Caller must zero-fill usedBuf before each call; it is reused across seeds to avoid allocations.
    static Dictionary<Cell, MatchedCell> RegionGrow(
        BucketGrid grid, Point2f origin, Point2f axisA, Point2f axisB, double pitch, bool[] usedBuf)
    {
        double lookupRadius = Math.Max(2.0, pitch * LookupRadiusFraction);
        double acceptTol    = Math.Max(2.0, pitch * LocalTolFraction);

        var cells = new Dictionary<Cell, MatchedCell>();

        int seedIdx = grid.FindNearest(origin, lookupRadius, usedBuf);
        if (seedIdx < 0) return cells;
        double seedRes = Math.Sqrt(DistSq(grid[seedIdx], origin));
        if (seedRes > acceptTol) return cells;

        cells[new Cell(0, 0)] = new MatchedCell(grid[seedIdx], seedRes, seedIdx);
        usedBuf[seedIdx] = true;

        var queue = new Queue<Cell>();
        queue.Enqueue(new Cell(0, 0));

        ReadOnlySpan<(int di, int dj)> dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.Count > 0)
        {
            var c  = queue.Dequeue();
            var pc = cells[c].Point;
            foreach (var (di, dj) in dirs)
            {
                var nc = new Cell(c.I + di, c.J + dj);
                if (cells.ContainsKey(nc)) continue;

                Point2f step;
                var prev = new Cell(c.I - di, c.J - dj);
                if (cells.TryGetValue(prev, out var pm))
                    step = pc - pm.Point;
                else
                    step = di != 0 ? axisA * di : axisB * dj;

                var predicted = pc + step;
                int idx = grid.FindNearest(predicted, lookupRadius, usedBuf);
                if (idx < 0) continue;

                double residual = Math.Sqrt(DistSq(grid[idx], predicted));
                if (residual > acceptTol) continue;

                cells[nc] = new MatchedCell(grid[idx], residual, idx);
                usedBuf[idx] = true;
                queue.Enqueue(nc);
            }
        }

        return cells;
    }

    // Remove the worst outlier (largest deviation from neighbour-predicted position) until
    // all remaining cells are within OutlierTolFraction × pitch of their predicted positions.
    static void RejectOutliers(Dictionary<Cell, MatchedCell> cells, double pitch)
    {
        double tol = Math.Max(1.5, pitch * OutlierTolFraction);

        while (cells.Count > MinInliers)
        {
            Cell worst   = default;
            double worstDev = tol;
            bool found   = false;

            foreach (var (cell, match) in cells)
            {
                if (!TryPredictFromNeighbors(cells, cell, out var predicted)) continue;
                double dev = Math.Sqrt(DistSq(match.Point, predicted));
                if (dev > worstDev) { worstDev = dev; worst = cell; found = true; }
            }

            if (!found) break;
            cells.Remove(worst);
        }
    }

    static bool TryPredictFromNeighbors(
        IReadOnlyDictionary<Cell, MatchedCell> cells, Cell c, out Point2f predicted)
    {
        Point2f sum = default;
        int count = 0;
        if (TryAxisPredict(cells, c, 1, 0, out var ph)) { sum += ph; count++; }
        if (TryAxisPredict(cells, c, 0, 1, out var pv)) { sum += pv; count++; }
        if (count == 0) { predicted = default; return false; }
        predicted = new Point2f(sum.X / count, sum.Y / count);
        return true;
    }

    static bool TryAxisPredict(
        IReadOnlyDictionary<Cell, MatchedCell> cells, Cell c, int di, int dj, out Point2f predicted)
    {
        var plus  = new Cell(c.I + di, c.J + dj);
        var minus = new Cell(c.I - di, c.J - dj);

        if (cells.TryGetValue(plus, out var mp) && cells.TryGetValue(minus, out var mm))
        {
            predicted = new Point2f((mp.Point.X + mm.Point.X) / 2f, (mp.Point.Y + mm.Point.Y) / 2f);
            return true;
        }

        var plus2 = new Cell(c.I + 2 * di, c.J + 2 * dj);
        if (cells.TryGetValue(plus, out mp) && cells.TryGetValue(plus2, out var mp2))
        {
            predicted = mp.Point * 2f - mp2.Point;
            return true;
        }
        var minus2 = new Cell(c.I - 2 * di, c.J - 2 * dj);
        if (cells.TryGetValue(minus, out mm) && cells.TryGetValue(minus2, out var mm2))
        {
            predicted = mm.Point * 2f - mm2.Point;
            return true;
        }

        predicted = default;
        return false;
    }

    // ── post-processing ───────────────────────────────────────────────────────

    // Mean direction of the I-axis step vectors, normalized to -90..+90°.
    // After axis canonicalization axisA points right, so the result is near 0° for aligned boards.
    static double ComputeRotationAngle(Dictionary<Cell, MatchedCell> grown)
    {
        double sumCos = 0, sumSin = 0;
        int count = 0;
        foreach (var (cell, match) in grown)
        {
            if (!grown.TryGetValue(new Cell(cell.I + 1, cell.J), out var next)) continue;
            var step = next.Point - match.Point;
            double len = Math.Sqrt(step.X * step.X + step.Y * step.Y);
            if (len < 1.0) continue;
            sumCos += step.X / len;
            sumSin += step.Y / len;
            count++;
        }
        if (count == 0) return 0.0;
        double angle = Math.Atan2(sumSin / count, sumCos / count) * 180.0 / Math.PI;
        if (angle >  90.0) angle -= 180.0;
        if (angle < -90.0) angle += 180.0;
        return angle;
    }

    // ── display helpers ───────────────────────────────────────────────────────

    static Mat BuildLabelImage(Mat grayImage, GridInference grid)
    {
        var annotated = new Mat();
        Cv2.CvtColor(grayImage, annotated, ColorConversionCodes.GRAY2BGR);
        if (!grid.Found) return annotated;

        var byCell = new Dictionary<Cell, Point2f>(grid.Cells.Length);
        for (int k = 0; k < grid.Cells.Length; k++)
            byCell[grid.Cells[k]] = grid.ImagePoints[k];

        foreach (var (cell, imgPt) in byCell)
        {
            if (byCell.TryGetValue(new Cell(cell.I + 1, cell.J), out var r))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(r), Scalar.LimeGreen, 1);
            if (byCell.TryGetValue(new Cell(cell.I, cell.J + 1), out var d))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(d), Scalar.LimeGreen, 1);
        }

        for (int k = 0; k < grid.ImagePoints.Length; k++)
        {
            var ctr = ToPoint(grid.ImagePoints[k]);
            var cell = grid.Cells[k];
            var label = $"{cell.I},{cell.J}";
            Cv2.DrawMarker(annotated, ctr, Scalar.Yellow, MarkerTypes.Cross, 10, 1);
            var textOrg = new OpenCvSharp.Point(ctr.X + 4, ctr.Y - 4);
            Cv2.PutText(annotated, label, textOrg, HersheyFonts.HersheyPlain, 0.9, Scalar.Black, 2, LineTypes.AntiAlias);
            Cv2.PutText(annotated, label, textOrg, HersheyFonts.HersheyPlain, 0.9, Scalar.Yellow, 1, LineTypes.AntiAlias);
        }
        return annotated;
    }

    static Mat BuildHeatmap(Mat mag)
    {
        using var magU8 = new Mat();
        Cv2.Normalize(mag, magU8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        var heatmap = new Mat();
        Cv2.ApplyColorMap(magU8, heatmap, ColormapTypes.Hot);
        return heatmap;
    }

    static LineSegmentPoint[] BuildGridLines(GridInference grid)
    {
        if (!grid.Found) return [];

        var byCell = new Dictionary<Cell, Point2f>(grid.Cells.Length);
        for (int k = 0; k < grid.Cells.Length; k++)
            byCell[grid.Cells[k]] = grid.ImagePoints[k];

        var lines = new List<LineSegmentPoint>();
        foreach (var (cell, pt) in byCell)
        {
            if (byCell.TryGetValue(new Cell(cell.I + 1, cell.J), out var right))
                lines.Add(new LineSegmentPoint(ToPoint(pt), ToPoint(right)));
            if (byCell.TryGetValue(new Cell(cell.I, cell.J + 1), out var down))
                lines.Add(new LineSegmentPoint(ToPoint(pt), ToPoint(down)));
        }
        return [.. lines];
    }

    // ── utilities ─────────────────────────────────────────────────────────────

    static double DistSq(Point2f a, Point2f b) { double dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }
    static OpenCvSharp.Point ToPoint(Point2f p) => new((int)Math.Round(p.X), (int)Math.Round(p.Y));

    // ── grid post-processing (also used when saving calibration files) ────────

    // Fills missing grid cells by interpolation from neighbours.
    // Only fills within the bounding box of the original detected corners so the loop converges.
    // Prefers midpoint of opposite neighbours; falls back to constant-velocity extrapolation.
    // Iterates until no further cells can be filled.
    static void FillGaps(Dictionary<(int Col, int Row), Point2f> corners)
    {
        if (corners.Count == 0) return;

        int minCol = corners.Keys.Min(k => k.Col);
        int maxCol = corners.Keys.Max(k => k.Col);
        int minRow = corners.Keys.Min(k => k.Row);
        int maxRow = corners.Keys.Max(k => k.Row);

        bool anyFilled;
        do
        {
            anyFilled = false;
            var candidates = new HashSet<(int Col, int Row)>();
            foreach (var (col, row) in corners.Keys)
            {
                if (col + 1 <= maxCol) candidates.Add((col + 1, row));
                if (col - 1 >= minCol) candidates.Add((col - 1, row));
                if (row + 1 <= maxRow) candidates.Add((col, row + 1));
                if (row - 1 >= minRow) candidates.Add((col, row - 1));
            }
            candidates.ExceptWith(corners.Keys);

            var toAdd = new Dictionary<(int Col, int Row), Point2f>();
            foreach (var target in candidates)
            {
                if (TryFillCell(corners, target, out var filled))
                {
                    toAdd[target] = filled;
                    anyFilled = true;
                }
            }
            foreach (var (k, v) in toAdd) corners[k] = v;
        } while (anyFilled);
    }

    static bool TryFillCell(
        Dictionary<(int Col, int Row), Point2f> corners,
        (int Col, int Row) t, out Point2f result)
    {
        (int col, int row) = t;
        Point2f sum  = default;
        int     count = 0;

        if (corners.TryGetValue((col - 1, row), out var lft) &&
            corners.TryGetValue((col + 1, row), out var rgt))
        { sum += new Point2f((lft.X + rgt.X) * 0.5f, (lft.Y + rgt.Y) * 0.5f); count++; }

        if (corners.TryGetValue((col, row - 1), out var dn) &&
            corners.TryGetValue((col, row + 1), out var up))
        { sum += new Point2f((dn.X + up.X) * 0.5f, (dn.Y + up.Y) * 0.5f); count++; }

        if (count > 0)
        {
            result = count == 1 ? sum : new Point2f(sum.X * 0.5f, sum.Y * 0.5f);
            return true;
        }

        if (corners.TryGetValue((col - 1, row), out lft) && corners.TryGetValue((col - 2, row), out var lft2))
        { result = lft * 2f - lft2; return true; }
        if (corners.TryGetValue((col + 1, row), out rgt) && corners.TryGetValue((col + 2, row), out var rgt2))
        { result = rgt * 2f - rgt2; return true; }
        if (corners.TryGetValue((col, row - 1), out dn) && corners.TryGetValue((col, row - 2), out var dn2))
        { result = dn * 2f - dn2; return true; }
        if (corners.TryGetValue((col, row + 1), out up) && corners.TryGetValue((col, row + 2), out var up2))
        { result = up * 2f - up2; return true; }

        result = default;
        return false;
    }

    // Median adjacent-corner spacing (pixels) of an inferred grid, used to convert a physical
    // square size into a mm/pixel scale. Reuses ComputePitch over the grid's matched cells.
    static float ComputeGridPitch(GridInference grid)
    {
        var corners = new Dictionary<(int Col, int Row), Point2f>(grid.Cells.Length);
        for (int k = 0; k < grid.Cells.Length; k++)
            corners[(grid.Cells[k].I, grid.Cells[k].J)] = grid.ImagePoints[k];
        return ComputePitch(corners);
    }

    // Median of all adjacent corner distances (horizontal and vertical pairs).
    static float ComputePitch(Dictionary<(int Col, int Row), Point2f> corners)
    {
        var dists = new List<float>();
        foreach (var ((col, row), pt) in corners)
        {
            if (corners.TryGetValue((col + 1, row), out var right))
            {
                float dx = pt.X - right.X, dy = pt.Y - right.Y;
                dists.Add((float)Math.Sqrt(dx * dx + dy * dy));
            }
            if (corners.TryGetValue((col, row + 1), out var up))
            {
                float dx = pt.X - up.X, dy = pt.Y - up.Y;
                dists.Add((float)Math.Sqrt(dx * dx + dy * dy));
            }
        }
        if (dists.Count == 0) return 1f;
        dists.Sort();
        return dists[dists.Count / 2];
    }

    // Extrapolates the corner map outward to cover [targetColMin,targetColMax] × [targetRowMin,targetRowMax].
    // Step 1 extends each detected row horizontally using the two boundary columns for velocity.
    // Step 2 extends each column (including newly-added ones) vertically.
    static void ExtendBoundary(
        Dictionary<(int Col, int Row), Point2f> corners,
        int targetColMin, int targetColMax,
        int targetRowMin, int targetRowMax)
    {
        foreach (int row in corners.Keys.Select(k => k.Row).Distinct().ToList())
        {
            var cols = corners.Keys
                .Where(k => k.Row == row).Select(k => k.Col).OrderBy(c => c).ToList();
            if (cols.Count < 2) continue;

            int c0 = cols[0], c1 = cols[1];
            var p0 = corners[(c0, row)]; var p1 = corners[(c1, row)];
            float lvelX = (p0.X - p1.X) / (c0 - c1);
            float lvelY = (p0.Y - p1.Y) / (c0 - c1);
            var prev = p0;
            for (int c = c0 - 1; c >= targetColMin; c--)
            {
                prev = new Point2f(prev.X + lvelX, prev.Y + lvelY);
                corners[(c, row)] = prev;
            }

            int cr0 = cols[^1], cr1 = cols[^2];
            var pr0 = corners[(cr0, row)]; var pr1 = corners[(cr1, row)];
            float rvelX = (pr0.X - pr1.X) / (cr0 - cr1);
            float rvelY = (pr0.Y - pr1.Y) / (cr0 - cr1);
            prev = pr0;
            for (int c = cr0 + 1; c <= targetColMax; c++)
            {
                prev = new Point2f(prev.X + rvelX, prev.Y + rvelY);
                corners[(c, row)] = prev;
            }
        }

        foreach (int col in corners.Keys.Select(k => k.Col).Distinct().ToList())
        {
            var rows = corners.Keys
                .Where(k => k.Col == col).Select(k => k.Row).OrderBy(r => r).ToList();
            if (rows.Count < 2) continue;

            int r0 = rows[0], r1 = rows[1];
            var p0 = corners[(col, r0)]; var p1 = corners[(col, r1)];
            float dvelX = (p0.X - p1.X) / (r0 - r1);
            float dvelY = (p0.Y - p1.Y) / (r0 - r1);
            var prev = p0;
            for (int r = r0 - 1; r >= targetRowMin; r--)
            {
                prev = new Point2f(prev.X + dvelX, prev.Y + dvelY);
                corners[(col, r)] = prev;
            }

            int rt0 = rows[^1], rt1 = rows[^2];
            var pt0 = corners[(col, rt0)]; var pt1 = corners[(col, rt1)];
            float uvelX = (pt0.X - pt1.X) / (rt0 - rt1);
            float uvelY = (pt0.Y - pt1.Y) / (rt0 - rt1);
            prev = pt0;
            for (int r = rt0 + 1; r <= targetRowMax; r++)
            {
                prev = new Point2f(prev.X + uvelX, prev.Y + uvelY);
                corners[(col, r)] = prev;
            }
        }
    }
}

