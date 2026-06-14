using System.Text.Json;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>
/// Detects checkerboard corner features using a rotation-invariant saddle filter and returns
/// their positions for inspection.
/// </summary>
/// <remarks>
/// Applies two orthogonal checkerboard kernels (axis-aligned K₀ and diagonal K₄₅) and combines
/// their responses as sqrt(R₀² + R₄₅²), giving a response invariant to checkerboard rotation angle.
/// The display image shows the magnitude response as a heatmap with detected peaks overlaid.
/// </remarks>
public class DistortionCalibrationOperatorOld : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "DistortionCalibration";
    /// <inheritdoc/>
    public string Category  => "Calibration";
    /// <inheritdoc/>
    public string Icon      => "calibration";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",                 ConnectableType = typeof(Mat) },
        new() { Name = "KernelHalfSize", Label = "Square Half-Size (px)", Type = ParameterType.Int,    DefaultValue = 7,    Min = 2 },
        new() { Name = "MinResponse",    Label = "Min Response",          Type = ParameterType.Double, DefaultValue = 0.45, Min = 0.0, Max = 1.0 },
        new() { Name = "ShowHeatmap",    Label = "Show Response Heatmap", Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "ShowLabels",     Label = "Show Grid Indices",     Type = ParameterType.Bool,   DefaultValue = false },
        new() { Name = "OutputFilePath", Label = "Output File Path",      Type = ParameterType.String },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Image",             DataType = typeof(Mat),                IsDisplayImage = true },
        // GridCorners precedes Corners so the inspector's first-Point2f[] overlay draws the matched
        // grid crossings (the calibration correspondences), not every raw peak.
        new() { Name = "GridCorners",       DataType = typeof(Point2f[]) },
        new() { Name = "GridLines",         DataType = typeof(LineSegmentPoint[]) },
        new() { Name = "Corners",           DataType = typeof(Point2f[]) },
        new() { Name = "ObjectPoints",      DataType = typeof(Point3f[]) },
        new() { Name = "GridColumns",       DataType = typeof(int) },
        new() { Name = "GridRows",          DataType = typeof(int) },
        new() { Name = "InlierCount",       DataType = typeof(int) },
        new() { Name = "Found",             DataType = typeof(bool) },
        new() { Name = "ReprojectionError", DataType = typeof(double) },
        new() { Name = "DistCoeffs",        DataType = typeof(double[]) },
        new() { Name = "CalibFilePath",     DataType = typeof(string) },
    ];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        int halfSize  = Math.Max(2, Convert.ToInt32(parameters.GetValueOrDefault("KernelHalfSize") ?? 7));
        var minResp   = (float)Convert.ToDouble(parameters.GetValueOrDefault("MinResponse") ?? 0.45);
        bool showHeat   = Convert.ToBoolean(parameters.GetValueOrDefault("ShowHeatmap") ?? false);
        bool showLabels = Convert.ToBoolean(parameters.GetValueOrDefault("ShowLabels")  ?? false);
        var outPath     = parameters.GetValueOrDefault("OutputFilePath") as string;

        if (image.Channels() != 1)
            throw new ArgumentException("DistortionCalibration requires a single-channel (grayscale) image.");

        using var imageF = new Mat();
        image.ConvertTo(imageF, MatType.CV_32F);

        // K₀ (axis-aligned) and K₄₅ (diagonal) are orthogonal checkerboard kernels.
        // Their responses satisfy R₀(θ) ∝ cos(2θ) and R₄₅(θ) ∝ sin(2θ), so
        // sqrt(R₀² + R₄₅²) is constant regardless of checkerboard rotation angle.
        using var k0  = BuildCheckerboardKernel(halfSize, diagonal: false);
        using var k45 = BuildCheckerboardKernel(halfSize, diagonal: true);

        using var r0  = new Mat();
        using var r45 = new Mat();
        Cv2.Filter2D(imageF, r0,  MatType.CV_32F, k0);
        Cv2.Filter2D(imageF, r45, MatType.CV_32F, k45);

        using var mag = new Mat();
        Cv2.Magnitude(r0, r45, mag);

        var corners   = FindPeaks(mag, halfSize, minResp);

        // FindPeaks locates crossings only to integer pixels (dilation NMS). Refine to subpixel on
        // the ORIGINAL image — cornerSubPix solves the saddle-point gradient condition, which is
        // orientation-independent so it handles rotated boards. Window stays under the half-pitch
        // (neighbour crossing is ~2·halfSize away) so it never latches onto an adjacent corner.
        // Measured ~4× lower RMS reprojection error than integer peaks, so it is always applied.
        if (corners.Length > 0)
        {
            int win = Math.Max(2, halfSize / 2);
            corners = Cv2.CornerSubPix(imageF, corners,
                new Size(win, win), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 40, 0.001));
        }

        var grid      = InferGrid(corners, image.Width, image.Height, halfSize);
        var gridLines = BuildGridLines(grid);

        // Phase 2: calibrate from the recovered (imagePoint, objectPoint) correspondences.
        double   rms        = double.NaN;
        double[]? distCoeffs = null;
        string?  calibPath  = null;
        if (grid.Found && grid.ImagePoints.Length >= MinInliers)
        {
            var (camera, dist, error) = Calibrate(grid.ImagePoints, grid.ObjectPoints, image.Size());
            rms        = error;
            distCoeffs = dist;
            if (!string.IsNullOrWhiteSpace(outPath))
            {
                CalibrationHelpers.SaveCalibration(new CalibrationData
                {
                    ImageWidth        = image.Width,
                    ImageHeight       = image.Height,
                    CameraMatrix      = CalibrationHelpers.RectToJagged(camera),
                    DistCoeffs        = dist,
                    ReprojectionError = error,
                    CreatedAt         = DateTime.UtcNow.ToString("o"),
                }, outPath);
                calibPath = outPath;
            }
        }

        return new Dictionary<string, object?>
        {
            ["Image"]             = showHeat   ? BuildHeatmap(mag)
                                : showLabels ? BuildLabelImage(image, grid)
                                :              null,
            ["GridCorners"]       = grid.ImagePoints,
            ["GridLines"]         = gridLines,
            ["Corners"]           = corners,
            ["ObjectPoints"]      = grid.ObjectPoints,
            ["GridColumns"]       = grid.Columns,
            ["GridRows"]          = grid.Rows,
            ["InlierCount"]       = grid.ImagePoints.Length,
            ["Found"]             = grid.Found,
            ["ReprojectionError"] = rms,
            ["DistCoeffs"]        = distCoeffs,
            ["CalibFilePath"]     = calibPath,
        };
    }

    // Runs cv::calibrateCamera on a single board view. objectPoints carry the integer grid lattice
    // (z = 0, unit square size — fine since we only need intrinsics + distortion, not scale), paired
    // with the detected image points. Returns the 3×3 camera matrix, [k1,k2,p1,p2,k3] distortion,
    // and the RMS reprojection error in pixels.
    static (double[,] Camera, double[] Dist, double Rms) Calibrate(
        Point2f[] imagePoints, Point3f[] objectPoints, Size imageSize)
    {
        var camera = new double[3, 3];
        var dist   = new double[5];
        double rms = Cv2.CalibrateCamera(
            [objectPoints], [imagePoints], imageSize,
            camera, dist, out _, out _, CalibrationFlags.None);
        return (camera, dist, rms);
    }

    // Builds a (halfSize*2) × (halfSize*2) checkerboard saddle kernel, normalized so L1 = 1.
    //
    // K₀ (diagonal=false): four rectangular quadrants with alternating ±1 signs.
    //   Responds maximally when aligned with the checkerboard axes (0°/90°).
    //
    // K₄₅ (diagonal=true): four diagonal sectors — top/bottom ±1, left/right ∓1.
    //   Pixels exactly on the kernel diagonals (|dr| == |dc|) are set to 0 and excluded
    //   from the normalization count, so both kernels have the same effective gain for a
    //   perfectly matching checkerboard pattern.
    //   Responds maximally when the checkerboard is rotated 45°.
    static Mat BuildCheckerboardKernel(int halfSize, bool diagonal)
    {
        int ksize = halfSize * 2;
        var kernel  = new Mat(ksize, ksize, MatType.CV_32F);
        var indexer = kernel.GetGenericIndexer<float>();
        int count   = 0;

        for (int r = 0; r < ksize; r++)
        {
            for (int c = 0; c < ksize; c++)
            {
                // Use half-pixel offsets so no pixel ever falls exactly on the kernel centre,
                // eliminating sign ambiguity on the axes for K₀.
                float dr = r - halfSize + 0.5f;
                float dc = c - halfSize + 0.5f;
                float val;

                if (diagonal)
                {
                    // Top/bottom sectors (+1) vs left/right sectors (−1).
                    // Pixels on the kernel diagonals (|dr| == |dc|) are neutral (0).
                    float d = MathF.Abs(dr) - MathF.Abs(dc);
                    val = d > 0f ? 1f : d < 0f ? -1f : 0f;
                }
                else
                {
                    // Top-left and bottom-right quadrants (+1); top-right and bottom-left (−1).
                    val = (dr < 0f) == (dc < 0f) ? 1f : -1f;
                }

                indexer[r, c] = val;
                if (val != 0f) count++;
            }
        }

        float norm = 1f / count;
        for (int r = 0; r < ksize; r++)
            for (int c = 0; c < ksize; c++)
                if (indexer[r, c] != 0f) indexer[r, c] *= norm;

        return kernel;
    }

    // Finds local maxima in the magnitude response using dilation-based non-maximum suppression,
    // then keeps only those above relativeThreshold * maxResponse.
    static Point2f[] FindPeaks(Mat mag, int suppressionRadius, float relativeThreshold)
    {
        Cv2.MinMaxLoc(mag, out _, out double maxVal);
        if (maxVal <= 0) return [];

        int nmsSize = suppressionRadius * 2 + 1;
        using var se      = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(nmsSize, nmsSize));
        using var dilated = new Mat();
        Cv2.Dilate(mag, dilated, se);

        // A pixel is a local maximum when it equals the neighbourhood maximum.
        using var isLocalMax = new Mat();
        Cv2.Compare(mag, dilated, isLocalMax, CmpTypes.GE);  // CV_8U, 255 at local maxima

        // Threshold relative to the global maximum (normalize to [0,255] first for integer comparison).
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

    private sealed record GridInference(
        bool Found,
        int Columns,
        int Rows,
        Point2f[] ImagePoints,   // detected inlier corners
        Point3f[] ObjectPoints,  // matching (col, row, 0) grid coordinates
        Cell[] Cells)            // integer grid coordinate per inlier (parallel to ImagePoints)
    {
        public static GridInference Empty { get; } = new(false, 0, 0, [], [], []);
    }

    // A basis hypothesis and the corners that fall on its grid. We do NOT carve out a filled
    // rectangle — every inlier is a usable (imagePoint, objectPoint) correspondence regardless of
    // where it sits in the lattice, which is all cv::calibrateCamera needs.
    private sealed record CandidateGrid(
        int InlierCount,
        double MeanResidual,
        double Fill,             // InlierCount / bounding-box area — rejects coincidental sparse hits
        Point2f Origin,          // seed for region-growing the final correspondences
        CandidateVector AxisA,
        CandidateVector AxisB,
        IReadOnlyDictionary<Cell, MatchedCell> Cells);

    private readonly record struct CandidateVector(Point2f Value, double Length);
    private readonly record struct Cell(int I, int J);
    private readonly record struct MatchedCell(Point2f Point, double Residual);

    // Minimum inliers and bounding-box fill for a basis to be accepted as a real checkerboard.
    // Fill kills bases whose pitch is a fraction of the true pitch (those leave most cells empty).
    private const int    MinInliers = 12;
    private const double MinFill    = 0.5;

    // Acceptance radius for region-growing, as a fraction of the local pitch. Because each cell is
    // predicted only one pitch from a matched neighbour, the true residual is sub-pixel; this bound
    // only needs to stay below half a pitch so a neighbouring crossing is never grabbed by mistake.
    private const double LocalTolFraction = 0.35;

    // Outlier rejection bound, as a fraction of pitch. A cell whose detected corner is farther than
    // this from its neighbour-consensus lattice position is dropped. Midpoint/linear predictions are
    // accurate to the lens-distortion curvature (~1–2 px), so this can be well under half a pitch.
    private const double OutlierTolFraction = 0.05;

    // RANSAC seed grid side. Seeds are placed at pixel-space positions (7×7 = 49 cells) and
    // snapped to the nearest detected corner. This keeps the seed set stable regardless of how
    // many corners were detected — a count change only shifts the stride in index-space, but
    // pixel-space coverage stays uniform. Each seed is tried at most once (deduped by index).
    private const int SeedGridN = 7;

    static GridInference InferGrid(Point2f[] corners, int width, int height, int edgeMargin)
    {
        if (corners.Length < MinInliers) return GridInference.Empty;

        // Deterministic RANSAC: seeds are the nearest corner to each cell centre of a uniform
        // SeedGridN×SeedGridN pixel grid. No Random — results are fully reproducible. Best-of-N
        // by inlier count → fill → mean residual.
        CandidateGrid? best  = null;
        var            seeds = new HashSet<int>();
        for (int gy = 0; gy < SeedGridN; gy++)
        for (int gx = 0; gx < SeedGridN; gx++)
        {
            var target = new Point2f((gx + 0.5f) * width / SeedGridN,
                                     (gy + 0.5f) * height / SeedGridN);
            int idx = -1; double bestSq = double.MaxValue;
            for (int ci = 0; ci < corners.Length; ci++)
            {
                double dSq = DistanceSquared(corners[ci], target);
                if (dSq < bestSq) { bestSq = dSq; idx = ci; }
            }
            if (idx < 0 || !seeds.Add(idx)) continue;
            var candidate = TryBuildGridFromOrigin(corners, idx);
            if (candidate is not null && IsBetter(candidate, best))
                best = candidate;
        }

        if (best is null) return GridInference.Empty;

        // The RANSAC winner only gives a good *local* basis near its origin. Grow the grid outward
        // from that seed, predicting each cell from its already-matched neighbours, so the match
        // tracks lens distortion to the image corners instead of extrapolating one global basis.
        var index = new CornerIndex(corners,
            Math.Max(1.0, Math.Min(best.AxisA.Length, best.AxisB.Length)));
        var grown = RegionGrow(corners, index, best.Origin, best.AxisA, best.AxisB);

        // Region-growing snaps each cell to the nearest corner within tolerance. Where the true
        // crossing was never detected (the saddle kernel hangs off the image edge), it can instead
        // grab a spurious texture/marker corner — producing outliers, almost always at the border.
        // Drop cells whose position disagrees with their neighbours' lattice prediction.
        RejectOutliers(grown, Math.Min(best.AxisA.Length, best.AxisB.Length));
        if (grown.Count < MinInliers) return GridInference.Empty;

        // Corners within edgeMargin (== kernel half-size) of any border have biased positions: there
        // the saddle kernel and the cornerSubPix window hang off the image and are computed on
        // border-replicated pixels. They were kept through inference so seeding and growth stay
        // connected, but they are dropped here — a biased point is worse than a missing one for
        // calibration. Filtering at this stage (not before inference) avoids destabilising the grid.
        if (edgeMargin > 0)
        {
            var edgeCells = grown
                .Where(kv => kv.Value.Point.X < edgeMargin || kv.Value.Point.X >= width  - edgeMargin
                          || kv.Value.Point.Y < edgeMargin || kv.Value.Point.Y >= height - edgeMargin)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var cell in edgeCells) grown.Remove(cell);
            if (grown.Count < MinInliers) return GridInference.Empty;
        }

        // Orient axes: I increases rightward (positive image X), J increases upward
        // (negative image Y, since image Y grows downward). Detect current orientation via
        // the covariance of each cell index against its image coordinate; negate any axis
        // that points the wrong way. calibrateCamera is unaffected — it only needs
        // consistent imagePoint↔objectPoint correspondences, not a specific axis direction.
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

        // Normalise grid coordinates so the lattice starts at (0,0) and emit parallel
        // image-point / object-point arrays for calibration.
        int minI = grown.Keys.Min(c => c.I);
        int minJ = grown.Keys.Min(c => c.J);
        int maxI = grown.Keys.Max(c => c.I);
        int maxJ = grown.Keys.Max(c => c.J);

        int n = grown.Count;
        var imagePoints  = new Point2f[n];
        var objectPoints = new Point3f[n];
        var cellList     = new Cell[n];
        int k = 0;
        foreach (var (cell, match) in grown)
        {
            imagePoints[k]  = match.Point;
            objectPoints[k] = new Point3f(cell.I - minI, cell.J - minJ, 0f);
            cellList[k]     = cell;
            k++;
        }

        return new GridInference(true, maxI - minI + 1, maxJ - minJ + 1,
            imagePoints, objectPoints, cellList);
    }

    // Grows the grid outward from a seed corner by breadth-first flood fill. Each unmatched
    // neighbour cell is predicted from the current cell plus a LOCAL step vector: the measured
    // displacement to the cell on the opposite side when available, otherwise the seed axis. Because
    // every prediction extrapolates only one pitch from a known corner, the residual stays
    // sub-pixel everywhere — at the centre and at the distorted image corners alike — so the
    // acceptance tolerance can be tight without dropping far cells.
    static Dictionary<Cell, MatchedCell> RegionGrow(
        Point2f[] corners, CornerIndex index, Point2f origin, CandidateVector axisA, CandidateVector axisB)
    {
        double pitch     = Math.Min(axisA.Length, axisB.Length);
        double tolerance = Math.Max(2.0, pitch * LocalTolFraction);

        var cells = new Dictionary<Cell, MatchedCell>();
        var used  = new HashSet<int>();

        int seedIdx = index.FindNearest(origin, tolerance, used);
        if (seedIdx < 0) return cells;
        var seed = new Cell(0, 0);
        cells[seed] = new MatchedCell(corners[seedIdx], 0.0);
        used.Add(seedIdx);

        var queue = new Queue<Cell>();
        queue.Enqueue(seed);

        ReadOnlySpan<(int di, int dj)> dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.Count > 0)
        {
            var c  = queue.Dequeue();
            var pc = cells[c].Point;
            foreach (var (di, dj) in dirs)
            {
                var nc = new Cell(c.I + di, c.J + dj);
                if (cells.ContainsKey(nc)) continue;

                // Local step: prefer the measured displacement from the opposite neighbour.
                Point2f step;
                var prev = new Cell(c.I - di, c.J - dj);
                if (cells.TryGetValue(prev, out var pm))
                    step = pc - pm.Point;
                else
                    step = di != 0 ? axisA.Value * di : axisB.Value * dj;

                var predicted = pc + step;
                int idx = index.FindNearest(predicted, tolerance, used);
                if (idx < 0) continue;

                cells[nc] = new MatchedCell(corners[idx], DistanceSquared(corners[idx], predicted));
                used.Add(idx);
                queue.Enqueue(nc);
            }
        }

        return cells;
    }

    // Removes cells whose detected corner disagrees with where its neighbours say the lattice point
    // should be. On a real grid a crossing lies at the midpoint of its two opposite neighbours (or,
    // at a border, on the constant-velocity extrapolation of two inner neighbours). A spurious
    // corner grabbed during growth breaks this. The worst-deviating cell is removed one at a time so
    // a single outlier skewing one neighbour's prediction can't cascade into dropping good cells.
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

    // Predicts a cell's lattice position from its neighbours: the midpoint of two opposite
    // neighbours where both exist (very accurate), else the constant-velocity extrapolation of two
    // consecutive inner neighbours (for border cells). Averages the horizontal and vertical
    // estimates when both are available.
    static bool TryPredictFromNeighbors(IReadOnlyDictionary<Cell, MatchedCell> cells, Cell c, out Point2f predicted)
    {
        Point2f sum = default;
        int count = 0;
        if (TryAxisPredict(cells, c, 1, 0, out var ph)) { sum += ph; count++; }
        if (TryAxisPredict(cells, c, 0, 1, out var pv)) { sum += pv; count++; }
        if (count == 0) { predicted = default; return false; }
        predicted = new Point2f(sum.X / count, sum.Y / count);
        return true;
    }

    static bool TryAxisPredict(IReadOnlyDictionary<Cell, MatchedCell> cells, Cell c, int di, int dj, out Point2f predicted)
    {
        var plus  = new Cell(c.I + di, c.J + dj);
        var minus = new Cell(c.I - di, c.J - dj);

        // Both sides present → midpoint (an even-spacing prediction, robust to distortion).
        if (cells.TryGetValue(plus, out var mp) && cells.TryGetValue(minus, out var mm))
        {
            predicted = new Point2f((mp.Point.X + mm.Point.X) / 2f, (mp.Point.Y + mm.Point.Y) / 2f);
            return true;
        }

        // One side has two consecutive cells → linear (constant-velocity) extrapolation.
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

    // Uniform-grid spatial index over the corner set: O(1) average nearest-corner queries within a
    // small radius, so region-growing's many local lookups stay cheap. Bucket size ≈ one pitch, so
    // a query within the (sub-pitch) tolerance touches at most a 3×3 block of buckets.
    private sealed class CornerIndex
    {
        private readonly Dictionary<(int, int), List<int>> _buckets = [];
        private readonly Point2f[] _points;
        private readonly double _cellSize;

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

        // Nearest unused corner to target within maxDist, or -1.
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

    static CandidateGrid? TryBuildGridFromOrigin(Point2f[] corners, int originIndex)
    {
        var origin = corners[originIndex];

        // Sort all other corners by distance so we can try multiple (axisA, axisB) combinations.
        // Searching a single nearest neighbour for each axis is fragile: a fiducial corner or a
        // gap in detection lands the wrong pitch, and the correct basis is never tested.
        var neighbors = corners
            .Select((p, i) => (p, i, dSq: DistanceSquared(p, origin)))
            .Where(x => x.i != originIndex && x.dSq > 0.25)
            .OrderBy(x => x.dSq)
            .Select(x => (x.p, x.i, d: Math.Sqrt(x.dSq)))
            .ToArray();

        if (neighbors.Length < 2) return null;

        const int MaxA = 8;
        const int MaxB = 12;

        CandidateGrid? best = null;

        for (int ai = 0; ai < Math.Min(MaxA, neighbors.Length); ai++)
        {
            var (pa, _, lenA) = neighbors[ai];
            var axisA = pa - origin;

            int bCount = 0;
            for (int bi = 0; bi < neighbors.Length && bCount < MaxB; bi++)
            {
                if (bi == ai) continue;
                var (pb, _, lenB) = neighbors[bi];
                if (lenB < lenA * 0.35 || lenB > lenA * 3.0) continue;

                var axisB = pb - origin;
                double cosAngle = Math.Abs((double)(axisA.X * axisB.X + axisA.Y * axisB.Y))
                                / (lenA * lenB);
                if (cosAngle > 0.5) continue;

                bCount++;
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
        Point2f origin,
        CandidateVector axisA,
        CandidateVector axisB,
        Point2f[] corners)
    {
        double det = Cross(axisA.Value, axisB.Value);
        if (Math.Abs(det) < 1e-6) return null;

        // Tolerance is a fraction of the smaller pitch. The grid is predicted from a single linear
        // basis, so far-from-origin cells drift from extrapolation error plus corner lens
        // distortion; 20% keeps those distant corners as inliers (e.g. CB_2's top-right column).
        double MaxGridResidualFraction = 0.2;
        double tolerance   = Math.Max(2.0, Math.Min(axisA.Length, axisB.Length) * MaxGridResidualFraction);
        double toleranceSq = tolerance * tolerance;
        var cells = new Dictionary<Cell, MatchedCell>();

        foreach (var point in corners)
        {
            var rel = point - origin;
            double fa = Cross(rel, axisB.Value) / det;
            double fb = Cross(axisA.Value, rel) / det;
            int i = (int)Math.Round(fa, MidpointRounding.AwayFromZero);
            int j = (int)Math.Round(fb, MidpointRounding.AwayFromZero);

            var predicted  = origin + axisA.Value * i + axisB.Value * j;
            double residualSq = DistanceSquared(point, predicted);
            if (residualSq > toleranceSq) continue;

            var cell     = new Cell(i, j);
            var residual = Math.Sqrt(residualSq);
            if (!cells.TryGetValue(cell, out var existing) || residual < existing.Residual)
                cells[cell] = new MatchedCell(point, residual);
        }

        if (cells.Count < MinInliers) return null;

        int minI = cells.Keys.Min(c => c.I);
        int maxI = cells.Keys.Max(c => c.I);
        int minJ = cells.Keys.Min(c => c.J);
        int maxJ = cells.Keys.Max(c => c.J);
        int cols = maxI - minI + 1;
        int rows = maxJ - minJ + 1;
        if (cols < 2 || rows < 2) return null;

        double fill = (double)cells.Count / (cols * rows);
        if (fill < MinFill) return null;

        double residualSum = cells.Values.Sum(m => m.Residual);
        return new CandidateGrid(cells.Count, residualSum / cells.Count, fill,
            origin, axisA, axisB, cells);
    }

    static bool IsBetter(CandidateGrid candidate, CandidateGrid? best)
    {
        if (best is null) return true;
        // Score = InlierCount × Fill. InlierCount alone is gamed by noisy detections: a wrong
        // grid whose cells happen to land near false-positive corners picks up extra "inliers"
        // and beats the correct grid. Multiplying by Fill penalises that because its bounding-box
        // area inflates when axes are misaligned. The correct basis maximises both simultaneously.
        double scoreC = candidate.InlierCount * candidate.Fill;
        double scoreB = best.InlierCount      * best.Fill;
        if (Math.Abs(scoreC - scoreB) > 1.0) return scoreC > scoreB;
        return candidate.MeanResidual < best.MeanResidual;
    }

    static double Cross(Point2f a, Point2f b) => Point2f.CrossProduct(a, b);
    static double DistanceSquared(Point2f a, Point2f b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    // Annotated debug image: source grayscale promoted to BGR with grid lines and (col,row) index
    // labels drawn at each matched corner. Intended for debugging the index-space assignment.
    static Mat BuildLabelImage(Mat grayImage, GridInference grid)
    {
        var annotated = new Mat();
        Cv2.CvtColor(grayImage, annotated, ColorConversionCodes.GRAY2BGR);
        if (!grid.Found) return annotated;

        var byCell = new Dictionary<Cell, (Point2f ImgPt, Point3f ObjPt)>(grid.Cells.Length);
        for (int k = 0; k < grid.Cells.Length; k++)
            byCell[grid.Cells[k]] = (grid.ImagePoints[k], grid.ObjectPoints[k]);

        // Grid lines first so corner markers and text sit on top.
        foreach (var (cell, (imgPt, _)) in byCell)
        {
            if (byCell.TryGetValue(new Cell(cell.I + 1, cell.J), out var r))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(r.ImgPt), Scalar.LimeGreen, 1);
            if (byCell.TryGetValue(new Cell(cell.I, cell.J + 1), out var d))
                Cv2.Line(annotated, ToPoint(imgPt), ToPoint(d.ImgPt), Scalar.LimeGreen, 1);
        }

        for (int k = 0; k < grid.ImagePoints.Length; k++)
        {
            var ctr  = ToPoint(grid.ImagePoints[k]);
            var obj  = grid.ObjectPoints[k];
            var label = $"{(int)Math.Round(obj.X)},{(int)Math.Round(obj.Y)}";
            Cv2.DrawMarker(annotated, ctr, Scalar.Yellow, MarkerTypes.Cross, 10, 1);
            // Draw label twice for legibility: dark shadow then bright foreground.
            var textOrg = new Point(ctr.X + 4, ctr.Y - 4);
            Cv2.PutText(annotated, label, textOrg, HersheyFonts.HersheyPlain, 0.9, Scalar.Black,    2, LineTypes.AntiAlias);
            Cv2.PutText(annotated, label, textOrg, HersheyFonts.HersheyPlain, 0.9, Scalar.Yellow, 1, LineTypes.AntiAlias);
        }
        return annotated;
    }

    // Magnitude response rendered as a Hot colormap heatmap (no overlays — the grid crosses and
    // lines are emitted as vector outputs and drawn by the inspector on top of this image).
    static Mat BuildHeatmap(Mat mag)
    {
        using var magU8 = new Mat();
        Cv2.Normalize(mag, magU8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
        var heatmap = new Mat();
        Cv2.ApplyColorMap(magU8, heatmap, ColormapTypes.Hot);
        return heatmap;
    }

    // Builds the grid edges as line segments: from each occupied cell to its right and down
    // neighbour wherever both are present. Missing cells leave gaps — no interpolation, the inliers
    // are exactly what was detected. Returned for the inspector to overlay.
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

    static Point ToPoint(Point2f point) =>
        new((int)Math.Round(point.X), (int)Math.Round(point.Y));
}

/// <summary>
/// Output of <c>cv::calibrateCamera</c> needed for undistortion. Saved and loaded as camelCase JSON.
/// </summary>
/// <remarks>
/// Properties use <c>double[][]</c> (jagged) rather than <c>double[,]</c> (rectangular) because
/// <c>System.Text.Json</c> cannot round-trip rectangular 2D arrays without a custom converter.
/// </remarks>
public class CalibrationData
{
    /// <summary>Width of the image used during calibration, in pixels.</summary>
    public int        ImageWidth        { get; set; }
    /// <summary>Height of the image used during calibration, in pixels.</summary>
    public int        ImageHeight       { get; set; }
    /// <summary>
    /// 3×3 camera (intrinsic) matrix stored row-major as a jagged array.
    /// Row 0: <c>[fx, 0, cx]</c> — Row 1: <c>[0, fy, cy]</c> — Row 2: <c>[0, 0, 1]</c>.
    /// </summary>
    public double[][] CameraMatrix      { get; set; } = [];
    /// <summary>
    /// Distortion coefficients returned by <c>cv::calibrateCamera</c>: <c>[k1, k2, p1, p2, k3]</c>.
    /// </summary>
    public double[]   DistCoeffs        { get; set; } = [];
    /// <summary>RMS reprojection error (pixels) returned by <c>cv::calibrateCamera</c>.</summary>
    public double     ReprojectionError { get; set; }
    /// <summary>ISO 8601 UTC timestamp written when the file was saved.</summary>
    public string?    CreatedAt         { get; set; }
}

internal static class CalibrationHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static double[][] RectToJagged(double[,] rect)
    {
        int rows = rect.GetLength(0), cols = rect.GetLength(1);
        var j = new double[rows][];
        for (int r = 0; r < rows; r++)
        {
            j[r] = new double[cols];
            for (int c = 0; c < cols; c++) j[r][c] = rect[r, c];
        }
        return j;
    }

    public static double[,] JaggedToRect(double[][] j)
    {
        int rows = j.Length, cols = j[0].Length;
        var rect = new double[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++) rect[r, c] = j[r][c];
        return rect;
    }

    public static void SaveCalibration(CalibrationData data, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
    }

    public static CalibrationData LoadCalibration(string path) =>
        JsonSerializer.Deserialize<CalibrationData>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Failed to parse calibration file: {path}");
}
