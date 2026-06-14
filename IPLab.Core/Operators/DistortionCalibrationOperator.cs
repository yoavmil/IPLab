using System.Text.Json;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Detects checkerboard corner features using a rotation-invariant saddle filter and builds a
/// sparse corner-correspondence calibration file for use with <see cref="UndistortOperator"/>.
/// The output image is the saddle-filter heatmap or an annotated label overlay.
/// </summary>
public class DistortionCalibrationOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "DistortionCalibration";
    /// <inheritdoc/>
    public string Category => "Calibration";
    /// <inheritdoc/>
    public string Icon     => "calibration";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",                 ConnectableType = typeof(Mat) },
        new() { Name = "KernelHalfSize", Label = "Square Half-Size (px)", Type = ParameterType.Int,    DefaultValue = 7,    Min = 2 },
        new() { Name = "MinResponse",    Label = "Min Response",          Type = ParameterType.Double, DefaultValue = 0.45, Min = 0.0, Max = 1.0 },
        new() { Name = "ShowHeatmap",    Label = "Show Response Heatmap", Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "ShowLabels",     Label = "Show Grid Indices",     Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "AnchorX",        Label = "Anchor X",              Type = ParameterType.Double, DefaultValue = 0.5,  Min = 0.0, Max = 1.0 },
        new() { Name = "AnchorY",        Label = "Anchor Y",              Type = ParameterType.Double, DefaultValue = 0.5,  Min = 0.0, Max = 1.0 },
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
        new() { Name = "ObjectPoints",     DataType = typeof(Point3f[]) },
        new() { Name = "InlierCount",      DataType = typeof(int) },
        new() { Name = "Found",            DataType = typeof(bool) },
        new() { Name = "RotationAngleDeg", DataType = typeof(double) },
        new() { Name = "CalibFilePath",    DataType = typeof(string) },
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image       = (Mat)parameters["Image"]!;
        int halfSize    = Math.Max(2, Convert.ToInt32(parameters.GetValueOrDefault("KernelHalfSize") ?? 7));
        var minResp     = (float)Convert.ToDouble(parameters.GetValueOrDefault("MinResponse") ?? 0.45);
        bool showHeat   = Convert.ToBoolean(parameters.GetValueOrDefault("ShowHeatmap")  ?? false);
        bool showLabels = Convert.ToBoolean(parameters.GetValueOrDefault("ShowLabels")   ?? false);
        double anchorX  = Convert.ToDouble(parameters.GetValueOrDefault("AnchorX")       ?? 0.5);
        double anchorY  = Convert.ToDouble(parameters.GetValueOrDefault("AnchorY")       ?? 0.5);
        var outPath     = parameters.GetValueOrDefault("OutputFilePath") as string;

        if (image.Channels() != 1)
            throw new ArgumentException("DistortionCalibration requires a single-channel (grayscale) image.");

        using var imageF = new Mat();
        image.ConvertTo(imageF, MatType.CV_32F);

        // K₀ (axis-aligned) and K₄₅ (diagonal) are orthogonal checkerboard kernels.
        // sqrt(R₀² + R₄₅²) is invariant to checkerboard rotation angle.
        using var k0  = BuildCheckerboardKernel(halfSize, diagonal: false);
        using var k45 = BuildCheckerboardKernel(halfSize, diagonal: true);

        using var r0  = new Mat();
        using var r45 = new Mat();
        Cv2.Filter2D(imageF, r0,  MatType.CV_32F, k0);
        Cv2.Filter2D(imageF, r45, MatType.CV_32F, k45);

        using var mag = new Mat();
        Cv2.Magnitude(r0, r45, mag);

        var corners = FindPeaks(mag, halfSize, minResp);

        if (corners.Length > 0)
        {
            int win = Math.Max(2, halfSize / 2);
            corners = Cv2.CornerSubPix(imageF, corners,
                new Size(win, win), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 40, 0.001));
        }

        var grid      = InferGrid(corners, image.Width, image.Height, halfSize, anchorX, anchorY);
        var gridLines = BuildGridLines(grid);

        string? calibPath = null;
        if (grid.Found && !string.IsNullOrWhiteSpace(outPath))
        {
            var calibCorners = new List<CornerRecord>(grid.Cells.Length);
            for (int k = 0; k < grid.Cells.Length; k++)
                calibCorners.Add(new CornerRecord
                {
                    Col  = grid.Cells[k].I,
                    Row  = grid.Cells[k].J,
                    ImgX = grid.ImagePoints[k].X,
                    ImgY = grid.ImagePoints[k].Y,
                });

            CalibrationHelpers.Save(new CalibrationData
            {
                ImageWidth       = image.Width,
                ImageHeight      = image.Height,
                AnchorX          = anchorX,
                AnchorY          = anchorY,
                RotationAngleDeg = grid.RotationAngleDeg,
                Corners          = calibCorners,
            }, outPath);
            calibPath = outPath;
        }

        return new Dictionary<string, object?>
        {
            ["Image"]            = showHeat   ? BuildHeatmap(mag)
                                : showLabels ? BuildLabelImage(image, grid)
                                :              null,
            ["GridCorners"]      = grid.ImagePoints,
            ["GridLines"]        = gridLines,
            ["Corners"]          = corners,
            ["ObjectPoints"]     = grid.ObjectPoints,
            ["InlierCount"]      = grid.ImagePoints.Length,
            ["Found"]            = grid.Found,
            ["RotationAngleDeg"] = grid.RotationAngleDeg,
            ["CalibFilePath"]    = calibPath,
        };
    }

    // ── constants ────────────────────────────────────────────────────────────

    private const int    MinInliers        = 12;
    private const double MinFill           = 0.5;
    private const double LocalTolFraction  = 0.35;
    private const double OutlierTolFraction = 0.05;
    // Maximum number of seeds to try; ordered nearest-to-anchor first.
    private const int    MaxSeeds          = 49;

    // ── private record types ─────────────────────────────────────────────────

    private sealed record GridInference(
        bool Found,
        Point2f[] ImagePoints,
        Point3f[] ObjectPoints,
        Cell[] Cells,
        double RotationAngleDeg)
    {
        public static GridInference Empty { get; } = new(false, [], [], [], 0.0);
    }

    private sealed record CandidateGrid(
        int InlierCount,
        double MeanResidual,
        double Fill,
        Point2f Origin,
        CandidateVector AxisA,
        CandidateVector AxisB,
        IReadOnlyDictionary<Cell, MatchedCell> Cells);

    private readonly record struct CandidateVector(Point2f Value, double Length);
    private readonly record struct Cell(int I, int J);
    private readonly record struct MatchedCell(Point2f Point, double Residual);

    // ── detection ────────────────────────────────────────────────────────────

    // Builds a (halfSize*2)×(halfSize*2) checkerboard saddle kernel, normalized so L1 = 1.
    // K₀ (diagonal=false): four rectangular quadrants, alternating ±1.
    // K₄₅ (diagonal=true): four diagonal sectors — top/bottom ±1, left/right ∓1.
    static Mat BuildCheckerboardKernel(int halfSize, bool diagonal)
    {
        int ksize   = halfSize * 2;
        var kernel  = new Mat(ksize, ksize, MatType.CV_32F);
        var indexer = kernel.GetGenericIndexer<float>();
        int count   = 0;

        for (int r = 0; r < ksize; r++)
        for (int c = 0; c < ksize; c++)
        {
            float dr = r - halfSize + 0.5f;
            float dc = c - halfSize + 0.5f;
            float val;

            if (diagonal)
            {
                float d = MathF.Abs(dr) - MathF.Abs(dc);
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

    // Dilation-based NMS, then threshold relative to the global max.
    static Point2f[] FindPeaks(Mat mag, int suppressionRadius, float relativeThreshold)
    {
        Cv2.MinMaxLoc(mag, out _, out double maxVal);
        if (maxVal <= 0) return [];

        int nmsSize = suppressionRadius * 2 + 1;
        using var se      = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(nmsSize, nmsSize));
        using var dilated = new Mat();
        Cv2.Dilate(mag, dilated, se);

        using var isLocalMax = new Mat();
        Cv2.Compare(mag, dilated, isLocalMax, CmpTypes.GE);

        using var magU8 = new Mat();
        Cv2.Normalize(mag, magU8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        using var aboveThresh = new Mat();
        Cv2.Threshold(magU8, aboveThresh, relativeThreshold * 255.0, 255, ThresholdTypes.Binary);

        using var peaks = new Mat();
        Cv2.BitwiseAnd(isLocalMax, aboveThresh, peaks);

        var result  = new List<Point2f>();
        var pkIndex = peaks.GetGenericIndexer<byte>();
        for (int r = 0; r < peaks.Rows; r++)
        for (int c = 0; c < peaks.Cols; c++)
            if (pkIndex[r, c] != 0)
                result.Add(new Point2f(c, r));
        return [.. result];
    }

    // ── pitch pre-filter ─────────────────────────────────────────────────────

    // Median of K=4 nearest-neighbour distances across all corners.
    // For a checkerboard the 4 nearest neighbours are the adjacent crossings, all at pitch P.
    static float EstimatePitch(Point2f[] corners)
    {
        const int K = 4;
        var distances = new List<float>(corners.Length * K);

        for (int i = 0; i < corners.Length; i++)
        {
            var dists = new List<float>(corners.Length - 1);
            for (int j = 0; j < corners.Length; j++)
            {
                if (j == i) continue;
                float dx = corners[i].X - corners[j].X;
                float dy = corners[i].Y - corners[j].Y;
                dists.Add(MathF.Sqrt(dx * dx + dy * dy));
            }
            dists.Sort();
            int take = Math.Min(K, dists.Count);
            for (int k = 0; k < take; k++)
                distances.Add(dists[k]);
        }

        if (distances.Count == 0) return 1f;
        distances.Sort();
        return distances[distances.Count / 2];
    }

    // Discard corners that have fewer than 2 neighbours within [0.6P, 1.4P].
    // This removes isolated false positives (no grid-adjacent neighbours at the right distance)
    // without discarding corners inside the board.
    static Point2f[] FilterByPitchConsistency(Point2f[] corners, float pitch)
    {
        double loSq = (double)(pitch * 0.6f) * (pitch * 0.6f);
        double hiSq = (double)(pitch * 1.4f) * (pitch * 1.4f);

        var result = new List<Point2f>(corners.Length);
        for (int i = 0; i < corners.Length; i++)
        {
            int count = 0;
            for (int j = 0; j < corners.Length; j++)
            {
                if (j == i) continue;
                double dx = corners[i].X - corners[j].X;
                double dy = corners[i].Y - corners[j].Y;
                double dSq = dx * dx + dy * dy;
                if (dSq >= loSq && dSq <= hiSq && ++count >= 2) break;
            }
            if (count >= 2) result.Add(corners[i]);
        }
        return [.. result];
    }

    // ── grid inference ────────────────────────────────────────────────────────

    static GridInference InferGrid(
        Point2f[] rawCorners, int width, int height, int edgeMargin,
        double anchorX, double anchorY)
    {
        if (rawCorners.Length < MinInliers) return GridInference.Empty;

        // Pre-filter: remove isolated corners that have no pitch-consistent neighbours.
        float pitch    = EstimatePitch(rawCorners);
        var   corners  = FilterByPitchConsistency(rawCorners, pitch);
        if (corners.Length < MinInliers) return GridInference.Empty;

        // Anchor-first seed ordering: try corners nearest the user's anchor point first so
        // the winning basis is anchored in the region the user cares about most.
        var anchorPt = new Point2f((float)(anchorX * width), (float)(anchorY * height));
        var orderedSeeds = corners
            .Select((p, i) => (i, dSq: DistanceSquared(p, anchorPt)))
            .OrderBy(x => x.dSq)
            .Take(MaxSeeds)
            .Select(x => x.i)
            .ToArray();

        CandidateGrid? best = null;
        foreach (int idx in orderedSeeds)
        {
            var candidate = TryBuildGridFromOrigin(corners, idx, pitch);
            if (candidate is not null && IsBetter(candidate, best))
                best = candidate;
        }

        if (best is null) return GridInference.Empty;

        // Grow the grid outward from the winner, tracking lens distortion cell-by-cell.
        var index = new CornerIndex(corners,
            Math.Max(1.0, Math.Min(best.AxisA.Length, best.AxisB.Length)));
        var grown = RegionGrow(corners, index, best.Origin, best.AxisA, best.AxisB);

        RejectOutliers(grown, Math.Min(best.AxisA.Length, best.AxisB.Length));
        if (grown.Count < MinInliers) return GridInference.Empty;

        // Drop corners within edgeMargin of any border — saddle kernel bias.
        if (edgeMargin > 0)
        {
            var edgeCells = grown
                .Where(kv =>
                    kv.Value.Point.X < edgeMargin || kv.Value.Point.X >= width  - edgeMargin ||
                    kv.Value.Point.Y < edgeMargin || kv.Value.Point.Y >= height - edgeMargin)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var cell in edgeCells) grown.Remove(cell);
            if (grown.Count < MinInliers) return GridInference.Empty;
        }

        // Orient axes: I→right (positive image X), J→up (negative image Y).
        {
            double mI = grown.Average(kv => (double)kv.Key.I);
            double mJ = grown.Average(kv => (double)kv.Key.J);
            double mX = grown.Average(kv => (double)kv.Value.Point.X);
            double mY = grown.Average(kv => (double)kv.Value.Point.Y);
            bool flipI = grown.Sum(kv => (kv.Key.I - mI) * (kv.Value.Point.X - mX)) < 0;
            bool flipJ = grown.Sum(kv => (kv.Key.J - mJ) * (kv.Value.Point.Y - mY)) > 0;
            if (flipI || flipJ)
            {
                var reoriented = new Dictionary<Cell, MatchedCell>(grown.Count);
                foreach (var (cell, match) in grown)
                    reoriented[new Cell(flipI ? -cell.I : cell.I, flipJ ? -cell.J : cell.J)] = match;
                grown = reoriented;
            }
        }

        double rotAngle = ComputeRotationAngle(grown);

        int minI = grown.Keys.Min(c => c.I);
        int minJ = grown.Keys.Min(c => c.J);

        int n            = grown.Count;
        var imagePoints  = new Point2f[n];
        var objectPoints = new Point3f[n];
        var cellList     = new Cell[n];
        int idx2         = 0;
        foreach (var (cell, match) in grown)
        {
            int ni = cell.I - minI;
            int nj = cell.J - minJ;
            imagePoints[idx2]  = match.Point;
            objectPoints[idx2] = new Point3f(ni, nj, 0f);
            cellList[idx2]     = new Cell(ni, nj);
            idx2++;
        }

        return new GridInference(true, imagePoints, objectPoints, cellList, rotAngle);
    }

    // Builds a grid hypothesis from the corner at originIndex.
    // Only considers axis candidates whose distance is within [0.7P, 1.3P] (pitch constraint),
    // then checks perpendicularity (|cos θ| < 0.5). This replaces the old MaxA/MaxB approach
    // and eliminates false-positive axes whose pitch is a fraction or multiple of the true pitch.
    static CandidateGrid? TryBuildGridFromOrigin(Point2f[] corners, int originIndex, float pitch)
    {
        var origin = corners[originIndex];
        double loSq = (double)(pitch * 0.7f) * (pitch * 0.7f);
        double hiSq = (double)(pitch * 1.3f) * (pitch * 1.3f);

        var neighbors = corners
            .Select((p, i) => (p, i, dSq: DistanceSquared(p, origin)))
            .Where(x => x.i != originIndex && x.dSq >= loSq && x.dSq <= hiSq)
            .OrderBy(x => x.dSq)
            .Select(x => (x.p, x.i, d: Math.Sqrt(x.dSq)))
            .ToArray();

        if (neighbors.Length < 2) return null;

        CandidateGrid? best = null;

        for (int ai = 0; ai < neighbors.Length; ai++)
        {
            var (pa, _, lenA) = neighbors[ai];
            var axisA = pa - origin;

            for (int bi = ai + 1; bi < neighbors.Length; bi++)
            {
                var (pb, _, lenB) = neighbors[bi];
                var axisB  = pb - origin;
                double cos = Math.Abs((double)(axisA.X * axisB.X + axisA.Y * axisB.Y))
                           / (lenA * lenB);
                if (cos > 0.5) continue;

                var candidate = ScoreGridCandidate(origin,
                    new CandidateVector(axisA, lenA),
                    new CandidateVector(axisB, lenB),
                    corners);
                if (candidate is not null && IsBetter(candidate, best))
                    best = candidate;
            }
        }

        return best;
    }

    static CandidateGrid? ScoreGridCandidate(
        Point2f origin, CandidateVector axisA, CandidateVector axisB, Point2f[] corners)
    {
        double det = Cross(axisA.Value, axisB.Value);
        if (Math.Abs(det) < 1e-6) return null;

        double tolerance   = Math.Max(2.0, Math.Min(axisA.Length, axisB.Length) * 0.2);
        double toleranceSq = tolerance * tolerance;
        var cells          = new Dictionary<Cell, MatchedCell>();

        foreach (var point in corners)
        {
            var    rel = point - origin;
            double fa  = Cross(rel, axisB.Value) / det;
            double fb  = Cross(axisA.Value, rel) / det;
            int    i   = (int)Math.Round(fa, MidpointRounding.AwayFromZero);
            int    j   = (int)Math.Round(fb, MidpointRounding.AwayFromZero);

            var    predicted  = origin + axisA.Value * i + axisB.Value * j;
            double residualSq = DistanceSquared(point, predicted);
            if (residualSq > toleranceSq) continue;

            var cell     = new Cell(i, j);
            var residual = Math.Sqrt(residualSq);
            if (!cells.TryGetValue(cell, out var existing) || residual < existing.Residual)
                cells[cell] = new MatchedCell(point, residual);
        }

        if (cells.Count < MinInliers) return null;

        int minI = cells.Keys.Min(c => c.I), maxI = cells.Keys.Max(c => c.I);
        int minJ = cells.Keys.Min(c => c.J), maxJ = cells.Keys.Max(c => c.J);
        int cols = maxI - minI + 1, rows = maxJ - minJ + 1;
        if (cols < 2 || rows < 2) return null;

        double fill = (double)cells.Count / (cols * rows);
        if (fill < MinFill) return null;

        double meanResidual = cells.Values.Sum(m => m.Residual) / cells.Count;
        return new CandidateGrid(cells.Count, meanResidual, fill, origin, axisA, axisB, cells);
    }

    static bool IsBetter(CandidateGrid candidate, CandidateGrid? best)
    {
        if (best is null) return true;
        double scoreC = candidate.InlierCount * candidate.Fill;
        double scoreB = best.InlierCount      * best.Fill;
        if (Math.Abs(scoreC - scoreB) > 1.0) return scoreC > scoreB;
        return candidate.MeanResidual < best.MeanResidual;
    }

    // ── region grow + outlier rejection ───────────────────────────────────────

    static Dictionary<Cell, MatchedCell> RegionGrow(
        Point2f[] corners, CornerIndex index, Point2f origin,
        CandidateVector axisA, CandidateVector axisB)
    {
        double pitch     = Math.Min(axisA.Length, axisB.Length);
        double tolerance = Math.Max(2.0, pitch * LocalTolFraction);

        var cells = new Dictionary<Cell, MatchedCell>();
        var used  = new HashSet<int>();

        int seedIdx = index.FindNearest(origin, tolerance, used);
        if (seedIdx < 0) return cells;
        cells[new Cell(0, 0)] = new MatchedCell(corners[seedIdx], 0.0);
        used.Add(seedIdx);

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
                    step = di != 0 ? axisA.Value * di : axisB.Value * dj;

                var predicted = pc + step;
                int idx       = index.FindNearest(predicted, tolerance, used);
                if (idx < 0) continue;

                cells[nc] = new MatchedCell(corners[idx], DistanceSquared(corners[idx], predicted));
                used.Add(idx);
                queue.Enqueue(nc);
            }
        }

        return cells;
    }

    static void RejectOutliers(Dictionary<Cell, MatchedCell> cells, double pitch)
    {
        double tol   = Math.Max(1.5, pitch * OutlierTolFraction);
        double tolSq = tol * tol;

        while (cells.Count > MinInliers)
        {
            Cell   worst    = default;
            double worstDev = tolSq;
            bool   found    = false;

            foreach (var (cell, match) in cells)
            {
                if (!TryPredictFromNeighbors(cells, cell, out var predicted)) continue;
                double devSq = DistanceSquared(match.Point, predicted);
                if (devSq > worstDev) { worstDev = devSq; worst = cell; found = true; }
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

    // ── spatial index ─────────────────────────────────────────────────────────

    private sealed class CornerIndex
    {
        private readonly Dictionary<(int, int), List<int>> _buckets = [];
        private readonly Point2f[] _points;
        private readonly double    _cellSize;

        public CornerIndex(Point2f[] points, double cellSize)
        {
            _points   = points;
            _cellSize = cellSize;
            for (int i = 0; i < points.Length; i++)
            {
                var key = Key(points[i]);
                if (!_buckets.TryGetValue(key, out var list))
                    _buckets[key] = list = [];
                list.Add(i);
            }
        }

        private (int, int) Key(Point2f p) =>
            ((int)Math.Floor(p.X / _cellSize), (int)Math.Floor(p.Y / _cellSize));

        public int FindNearest(Point2f target, double maxDist, HashSet<int> used)
        {
            int bx = (int)Math.Floor(target.X / _cellSize);
            int by = (int)Math.Floor(target.Y / _cellSize);
            int r  = (int)Math.Ceiling(maxDist / _cellSize);

            double bestSq = maxDist * maxDist;
            int    best   = -1;
            for (int gx = bx - r; gx <= bx + r; gx++)
            for (int gy = by - r; gy <= by + r; gy++)
            {
                if (!_buckets.TryGetValue((gx, gy), out var list)) continue;
                foreach (int i in list)
                {
                    if (used.Contains(i)) continue;
                    double dSq = DistanceSquared(_points[i], target);
                    if (dSq < bestSq) { bestSq = dSq; best = i; }
                }
            }
            return best;
        }
    }

    // ── post-processing ───────────────────────────────────────────────────────

    // Mean direction of the I-axis (horizontal) step vectors, in degrees CW from image right.
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
        return count == 0 ? 0.0 : Math.Atan2(sumSin / count, sumCos / count) * 180.0 / Math.PI;
    }

    // ── display helpers ───────────────────────────────────────────────────────

    static Mat BuildLabelImage(Mat grayImage, GridInference grid)
    {
        var annotated = new Mat();
        Cv2.CvtColor(grayImage, annotated, ColorConversionCodes.GRAY2BGR);
        if (!grid.Found) return annotated;

        var byCell = new Dictionary<Cell, (Point2f ImgPt, Point3f ObjPt)>(grid.Cells.Length);
        for (int k = 0; k < grid.Cells.Length; k++)
            byCell[grid.Cells[k]] = (grid.ImagePoints[k], grid.ObjectPoints[k]);

        foreach (var (cell, (imgPt, _)) in byCell)
        {
            if (byCell.TryGetValue(new Cell(cell.I + 1, cell.J), out var r))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(r.ImgPt), Scalar.LimeGreen, 1);
            if (byCell.TryGetValue(new Cell(cell.I, cell.J + 1), out var d))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(d.ImgPt), Scalar.LimeGreen, 1);
        }

        for (int k = 0; k < grid.ImagePoints.Length; k++)
        {
            var ctr   = ToPoint(grid.ImagePoints[k]);
            var obj   = grid.ObjectPoints[k];
            var label = $"{(int)Math.Round(obj.X)},{(int)Math.Round(obj.Y)}";
            Cv2.DrawMarker(annotated, ctr, Scalar.Yellow, MarkerTypes.Cross, 10, 1);
            var textOrg = new Point(ctr.X + 4, ctr.Y - 4);
            Cv2.PutText(annotated, label, textOrg, HersheyFonts.HersheyPlain, 0.9, Scalar.Black,  2, LineTypes.AntiAlias);
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

    static double Cross(Point2f a, Point2f b)           => Point2f.CrossProduct(a, b);
    static double DistanceSquared(Point2f a, Point2f b) { double dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }
    static Point  ToPoint(Point2f p)                    => new((int)Math.Round(p.X), (int)Math.Round(p.Y));
}

// ── public types (also used by UndistortOperator) ────────────────────────────

/// <summary>
/// Sparse corner-correspondence calibration data produced by <see cref="DistortionCalibrationOperator"/>
/// and consumed by <see cref="UndistortOperator"/> to build a dense bilinear warp map.
/// </summary>
public class CalibrationData
{
    /// <summary>Width of the image used during calibration, in pixels.</summary>
    public int    ImageWidth       { get; set; }
    /// <summary>Height of the image used during calibration, in pixels.</summary>
    public int    ImageHeight      { get; set; }
    /// <summary>Normalised horizontal anchor [0,1]: maps to the output pixel column <c>AnchorX × W</c>.</summary>
    public double AnchorX          { get; set; }
    /// <summary>Normalised vertical anchor [0,1]: maps to the output pixel row <c>AnchorY × H</c>.</summary>
    public double AnchorY          { get; set; }
    /// <summary>Angle of the checkerboard I-axis relative to image horizontal, in degrees clockwise.</summary>
    public double RotationAngleDeg { get; set; }
    /// <summary>Detected corners: integer grid coordinate → sub-pixel image position.</summary>
    public List<CornerRecord> Corners { get; set; } = [];
}

/// <summary>One detected checkerboard corner: its integer grid position and sub-pixel image location.</summary>
public class CornerRecord
{
    /// <summary>Grid column index (I axis, increasing rightward).</summary>
    public int    Col  { get; set; }
    /// <summary>Grid row index (J axis, increasing upward).</summary>
    public int    Row  { get; set; }
    /// <summary>Sub-pixel image X coordinate.</summary>
    public double ImgX { get; set; }
    /// <summary>Sub-pixel image Y coordinate.</summary>
    public double ImgY { get; set; }
}

/// <summary>Serialisation helpers for <see cref="CalibrationData"/>.</summary>
internal static class CalibrationHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Save(CalibrationData data, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
    }

    public static CalibrationData Load(string path) =>
        JsonSerializer.Deserialize<CalibrationData>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Failed to parse calibration file: {path}");
}
