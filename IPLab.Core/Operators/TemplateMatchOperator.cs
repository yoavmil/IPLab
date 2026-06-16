using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Finds multiple fixed-scale, fixed-orientation template occurrences using masked normalized cross-correlation and an optional axis-aligned ROI.</summary>
/// <seealso href="https://docs.opencv.org/4.x/df/dfb/group__imgproc__object.html#ga586ebfb0a7fb604b35a23d85391329be">OpenCV: matchTemplate</seealso>
public class TemplateMatchOperator : IOperatorType, ICacheInvalidationProvider
{
    /// <inheritdoc/>
    public string TypeName => "TemplateMatch";
    /// <inheritdoc/>
    public string Category => "Detection";
    /// <inheritdoc/>
    public string Icon => "template";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", ConnectableType = typeof(Mat) },
        new() { Name = "TemplatePath", Label = "Template", Type = ParameterType.String, DefaultValue = "" },
        new() { Name = "MinScore", Label = "Minimum Score", Type = ParameterType.Double, DefaultValue = 0.8, Min = 0.0, Max = 1.0 },
        new() { Name = "MaxMatches", Label = "Maximum Matches", Type = ParameterType.Int, ConnectableType = typeof(int), DefaultValue = 100, Min = 0 },
        new() { Name = "OverlapThreshold", Label = "Maximum Overlap", Type = ParameterType.Double, DefaultValue = 0.3, Min = 0.0, Max = 1.0 },
        new() { Name = "RoiCX", Label = "ROI Center X", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiCY", Label = "ROI Center Y", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0 },
        new() { Name = "RoiW", Label = "ROI Width", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
        new() { Name = "RoiH", Label = "ROI Height", Type = ParameterType.Double, ConnectableType = typeof(double), DefaultValue = 0.0, Min = 0.0 },
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Matches", DataType = typeof(Mat) },
        new() { Name = "Rectangles", DataType = typeof(Rect[]) },
    ];

    private sealed record CachedTemplate(
        long LastWriteTicks, long Length, Mat ColorImage, Mat? GrayscaleImage, Mat Mask);
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, CachedTemplate> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.GetValueOrDefault("Image") is not Mat image || image.Empty())
            throw new ArgumentException("TemplateMatch requires a non-empty input image.");

        var path = parameters.GetValueOrDefault("TemplatePath") as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ArgumentException($"TemplateMatch requires an existing template PNG. Path: '{path}'.");

        double minScore = Convert.ToDouble(parameters.GetValueOrDefault("MinScore") ?? 0.8);
        int maxMatches = Convert.ToInt32(parameters.GetValueOrDefault("MaxMatches") ?? 100);
        double overlapThreshold = Convert.ToDouble(parameters.GetValueOrDefault("OverlapThreshold") ?? 0.3);

        if (minScore is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Minimum Score must be between 0 and 1.");
        if (maxMatches < 0)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Maximum Matches cannot be negative.");
        if (overlapThreshold is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Maximum Overlap must be between 0 and 1.");

        using var templateData = LoadTemplate(path, image.Channels());
        if (image.Depth() != templateData.Image.Depth() || image.Channels() != templateData.Image.Channels())
            throw new ArgumentException(
                $"Input image type {image.Type()} does not match template type {templateData.Image.Type()}. " +
                "Convert the image explicitly before TemplateMatch.");

        var roi = RoiParameters.Extract(parameters);
        var searchRect = roi is null
            ? new Rect(0, 0, image.Width, image.Height)
            : RoiParameters.Clamp(roi, image.Width, image.Height);

        if (searchRect.Width <= 0 || searchRect.Height <= 0)
            return EmptyOutputs();
        if (templateData.Image.Width > searchRect.Width || templateData.Image.Height > searchRect.Height)
            throw new ArgumentException(
                $"Template size {templateData.Image.Width}x{templateData.Image.Height} exceeds " +
                $"search area size {searchRect.Width}x{searchRect.Height}.");

        using var searchImage = new Mat(image, searchRect);
        using var response = new Mat();
        // The template editor stores ignored pixels in the PNG alpha channel. CCorrNormed accepts
        // that alpha-derived mask while keeping scores normalized to the user-facing 0..1 range.
        Cv2.MatchTemplate(searchImage, templateData.Image, response,
            TemplateMatchModes.CCorrNormed, templateData.Mask);

        var candidates = FindLocalMaxima(response, templateData.Image.Size(), searchRect, minScore);
        var accepted = SuppressOverlaps(candidates, overlapThreshold, maxMatches);
        return BuildOutputs(accepted);
    }

    private sealed record Candidate(Rect Rectangle, double Score);

    // A 3x3 local-maximum test alone leaves thousands of small response ripples on repetitive
    // images. Require each peak to rise above a template-scale surrounding ring as well. This is
    // deliberately internal: MinScore remains the single user-facing match-quality threshold.
    private const double MinLocalProminence = 0.5;

    private static List<Candidate> FindLocalMaxima(
        Mat response, Size templateSize, Rect searchRect, double minScore)
    {
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var prominenceKernel = BuildProminenceKernel(templateSize);
        using var dilated = new Mat();
        using var surroundingMax = new Mat();
        using var localMax = new Mat();
        using var aboveThreshold = new Mat();
        using var peaks = new Mat();

        Cv2.Dilate(response, dilated, kernel);
        Cv2.Dilate(response, surroundingMax, prominenceKernel);
        Cv2.Compare(response, dilated, localMax, CmpTypes.GE);
        Cv2.Compare(response, Scalar.All(minScore), aboveThreshold, CmpTypes.GE);
        Cv2.BitwiseAnd(localMax, aboveThreshold, peaks);

        var responseIndex = response.GetGenericIndexer<float>();
        var surroundingIndex = surroundingMax.GetGenericIndexer<float>();
        var peakIndex = peaks.GetGenericIndexer<byte>();
        var candidates = new List<Candidate>();
        for (int row = 0; row < peaks.Rows; row++)
        for (int col = 0; col < peaks.Cols; col++)
        {
            if (peakIndex[row, col] == 0) continue;
            float score = responseIndex[row, col];
            if (!float.IsFinite(score) || score > 1.0001f) continue;
            float surrounding = surroundingIndex[row, col];
            double prominence = (score - surrounding) / Math.Max(1e-6, 1.0 - surrounding);
            if (prominence < MinLocalProminence) continue;
            candidates.Add(new Candidate(
                new Rect(searchRect.X + col, searchRect.Y + row, templateSize.Width, templateSize.Height),
                score));
        }

        return candidates.OrderByDescending(c => c.Score).ToList();
    }

    private static Mat BuildProminenceKernel(Size templateSize)
    {
        int radius = Math.Max(3, Math.Min(templateSize.Width, templateSize.Height) / 2);
        int exclusionRadius = Math.Max(1, Math.Min(templateSize.Width, templateSize.Height) / 10);
        int diameter = radius * 2 + 1;
        var kernel = new Mat(diameter, diameter, MatType.CV_8UC1, Scalar.White);
        using var center = new Mat(kernel, new Rect(
            radius - exclusionRadius,
            radius - exclusionRadius,
            exclusionRadius * 2 + 1,
            exclusionRadius * 2 + 1));
        center.SetTo(Scalar.Black);
        return kernel;
    }

    private static List<Candidate> SuppressOverlaps(
        IReadOnlyList<Candidate> candidates, double overlapThreshold, int maxMatches)
    {
        var accepted = new List<Candidate>();
        foreach (var candidate in candidates)
        {
            if (accepted.Any(existing => IntersectionOverUnion(candidate.Rectangle, existing.Rectangle) > overlapThreshold))
                continue;

            accepted.Add(candidate);
            if (maxMatches > 0 && accepted.Count >= maxMatches)
                break;
        }
        return accepted;
    }

    private static double IntersectionOverUnion(Rect a, Rect b)
    {
        int left = Math.Max(a.Left, b.Left);
        int top = Math.Max(a.Top, b.Top);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        int intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        if (intersection == 0) return 0.0;
        return intersection / (double)(a.Width * a.Height + b.Width * b.Height - intersection);
    }

    private static Dictionary<string, object?> BuildOutputs(IReadOnlyList<Candidate> matches)
    {
        var matrix = new Mat(matches.Count, 5, MatType.CV_64F);
        var index = matrix.GetGenericIndexer<double>();
        for (int row = 0; row < matches.Count; row++)
        {
            var match = matches[row];
            index[row, 0] = match.Rectangle.X;
            index[row, 1] = match.Rectangle.Y;
            index[row, 2] = match.Rectangle.Width;
            index[row, 3] = match.Rectangle.Height;
            index[row, 4] = match.Score;
        }

        return new Dictionary<string, object?>
        {
            ["Matches"] = matrix,
            ["Rectangles"] = matches.Select(m => m.Rectangle).ToArray(),
        };
    }

    private static Dictionary<string, object?> EmptyOutputs() => new()
    {
        ["Matches"] = new Mat(0, 5, MatType.CV_64F),
        ["Rectangles"] = Array.Empty<Rect>(),
    };

    private sealed class TemplateLease(Mat image, Mat mask) : IDisposable
    {
        public Mat Image { get; } = image;
        public Mat Mask { get; } = mask;
        public void Dispose()
        {
            Image.Dispose();
            Mask.Dispose();
        }
    }

    private TemplateLease LoadTemplate(string path, int inputChannels)
    {
        // Templates are stored as 8-bit RGBA PNGs: RGB contains the sample and alpha is the mask.
        // Grayscale samples use three equal color planes because PNG/OpenCV has no portable
        // grayscale-plus-alpha Mat representation here. Decoded Mats are cached by full path,
        // modification time, and file length; replaced cache entries must be disposed.
        string fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        lock (_cacheGate)
        {
            if (!_templateCache.TryGetValue(fullPath, out var cached) ||
                cached.LastWriteTicks != info.LastWriteTimeUtc.Ticks || cached.Length != info.Length)
            {
                using var png = Cv2.ImRead(fullPath, ImreadModes.Unchanged);
                if (png.Empty() || png.Depth() != MatType.CV_8U || png.Channels() != 4)
                    throw new ArgumentException("Template PNG must be an 8-bit RGBA image with an alpha mask.");

                var channels = Cv2.Split(png);
                try
                {
                    var colorImage = new Mat();
                    Cv2.Merge(channels.Take(3).ToArray(), colorImage);
                    Mat? grayscaleImage = AreColorChannelsEqual(channels)
                        ? channels[0].Clone()
                        : null;
                    var mask = channels[3].Clone();

                    if (Cv2.CountNonZero(mask) == 0)
                    {
                        colorImage.Dispose();
                        grayscaleImage?.Dispose();
                        mask.Dispose();
                        throw new ArgumentException("Template alpha mask excludes every pixel.");
                    }

                    if (cached is not null)
                    {
                        cached.ColorImage.Dispose();
                        cached.GrayscaleImage?.Dispose();
                        cached.Mask.Dispose();
                    }
                    cached = new CachedTemplate(
                        info.LastWriteTimeUtc.Ticks, info.Length, colorImage, grayscaleImage, mask);
                    _templateCache[fullPath] = cached;
                }
                finally
                {
                    foreach (var channel in channels) channel.Dispose();
                }
            }

            var templateImage = inputChannels switch
            {
                3 => cached.ColorImage.Clone(),
                1 when cached.GrayscaleImage is not null => cached.GrayscaleImage.Clone(),
                1 => throw new ArgumentException(
                    "A grayscale input requires a grayscale template. The template PNG contains different RGB channel values."),
                _ => throw new ArgumentException(
                    $"TemplateMatch supports only 8-bit grayscale or 8-bit three-channel images, not {inputChannels} channels."),
            };
            return new TemplateLease(templateImage, cached.Mask.Clone());
        }
    }

    private static bool AreColorChannelsEqual(IReadOnlyList<Mat> channels)
    {
        using var bgDiff = new Mat();
        using var brDiff = new Mat();
        Cv2.Absdiff(channels[0], channels[1], bgDiff);
        Cv2.Absdiff(channels[0], channels[2], brDiff);
        return Cv2.CountNonZero(bgDiff) == 0 && Cv2.CountNonZero(brDiff) == 0;
    }

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, object?>> GetCacheTokens(IReadOnlyDictionary<string, object?> parameters)
    {
        var path = parameters.GetValueOrDefault("TemplatePath") as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            yield return new("__templateWriteTime", 0L);
            yield return new("__templateLength", 0L);
            yield break;
        }

        var info = new FileInfo(path);
        yield return new("__templateWriteTime", info.LastWriteTimeUtc.Ticks);
        yield return new("__templateLength", info.Length);
    }
}
