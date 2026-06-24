using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace IPLab.Core.Operators;

/// <summary>Finds connected components in a binary image and outputs label map, bounding-box stats, centroids, and an optional colorized label image, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html#ga107a78bf7cd25dec05fb4dfc5c9e765f">OpenCV: connectedComponentsWithStats</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#connectedcomponents">Operator reference</seealso>
public class ConnectedComponentsOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "ConnectedComponents";
    /// <inheritdoc/>
    public string Category  => "Detection";
    /// <inheritdoc/>
    public string Icon      => "component";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",            Label = "Image",              ConnectableType = typeof(Mat) },
        new() { Name = "Connectivity",     Label = "Connectivity",       Type = ParameterType.Enum,
                DefaultValue = "8", Options = ["4", "8"] },
        new() { Name = "OutputLabelImage", Label = "Output Label Image", Type = ParameterType.Bool,
                DefaultValue = true },
        ..RoiParameters.Schema,
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Count",      DataType = typeof(int) },
        new() { Name = "Labels",     DataType = typeof(Mat) },
        new() { Name = "Stats",      DataType = typeof(Mat) },
        new() { Name = "Centroids",  DataType = typeof(Mat) },
        new() { Name = "LabelImage", DataType = typeof(Mat), IsDisplayImage = true },
        ..RoiParameters.OutputPorts,
    ];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image            = (Mat)parameters["Image"]!;
        var connectivity     = (string?)parameters.GetValueOrDefault("Connectivity") ?? "8";
        var outputLabelImage = parameters.GetValueOrDefault("OutputLabelImage") is not false;

        var conn    = connectivity == "4" ? PixelConnectivity.Connectivity4 : PixelConnectivity.Connectivity8;

        var roi = RoiParameters.Extract(parameters);
        using var warped = roi is { Angle: not 0.0 } ? RoiParameters.WarpForRoi(image, roi) : null;
        var effectiveImg = warped ?? image;

        var roiRect = roi is not null ? (Rect?)RoiParameters.Clamp(roi, effectiveImg.Width, effectiveImg.Height) : null;

        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?>
            {
                ["Count"]      = 0,
                ["Labels"]     = new Mat(image.Rows, image.Cols, MatType.CV_32S, Scalar.All(0)),
                ["Stats"]      = new Mat(0, 5, MatType.CV_32S),
                ["Centroids"]  = new Mat(0, 2, MatType.CV_64F),
                ["LabelImage"] = outputLabelImage ? new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black) : null,
            };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        var transform = roiRect.HasValue ? RoiParameters.BuildTransform(roi!, rect) : null;
        using var crop = roiRect.HasValue ? new Mat(effectiveImg, rect) : null;
        var       src  = crop ?? effectiveImg;

        var labels    = new Mat();
        var stats     = new Mat();
        var centroids = new Mat();

        int count = Cv2.ConnectedComponentsWithStats(src, labels, stats, centroids, conn);
        int n     = count - 1; // exclude background label 0

        if (roiRect.HasValue)
        {
            if (roi!.Angle == 0.0)
            {
                // Axis-aligned only: offset bounding box X/Y in stats by the crop origin.
                // For rotated ROI, stats remain in warped-crop frame (documented limitation).
                int offsetX = rect.X;
                int offsetY = rect.Y;
                for (int i = 0; i < count; i++)
                {
                    nint left = stats.Data + (i * 5 + 0) * sizeof(int);
                    nint top  = stats.Data + (i * 5 + 1) * sizeof(int);
                    Marshal.WriteInt32(left, Marshal.ReadInt32(left) + offsetX);
                    Marshal.WriteInt32(top,  Marshal.ReadInt32(top)  + offsetY);
                }
            }

            // BackProject maps centroids from crop coords to image coords for both cases.
            // For angle=0 it degenerates to cropX + rect.X, cropY + rect.Y.
            var centIdx = centroids.GetGenericIndexer<double>();
            for (int i = 0; i < count; i++)
            {
                var bp = RoiParameters.BackProject(centIdx[i, 0], centIdx[i, 1], transform!);
                centIdx[i, 0] = bp.X;
                centIdx[i, 1] = bp.Y;
            }
        }

        // Composite Labels and LabelImage back to original-image space when ROI was used.
        // For axis-aligned ROI (Angle=0), bwdMat is the identity and WarpAffine degenerates to a copy.
        Mat labelsResult = labels;
        Mat? labelImg    = null;

        if (crop is null)
        {
            // No ROI — both outputs are already full-size.
            if (outputLabelImage)
                labelImg = BuildLabelImage(labels, n);
        }
        else
        {
            var center       = new Point2f((float)roi!.CX, (float)roi.CY);
            using var bwdMat = Cv2.GetRotationMatrix2D(center, roi.Angle, 1.0);

            if (outputLabelImage)
            {
                var cropLabel        = BuildLabelImage(labels, n);
                using var warpedLbl  = new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black);
                using var imgDst     = new Mat(warpedLbl, rect);
                cropLabel.CopyTo(imgDst);
                cropLabel.Dispose();
                labelImg = new Mat();
                Cv2.WarpAffine(warpedLbl, labelImg, bwdMat, image.Size(), InterpolationFlags.Nearest, BorderTypes.Constant, Scalar.Black);
            }

            using var warpedLbls = new Mat(image.Rows, image.Cols, MatType.CV_32S, Scalar.All(0));
            using var lblDst     = new Mat(warpedLbls, rect);
            labels.CopyTo(lblDst);
            labelsResult = new Mat();
            Cv2.WarpAffine(warpedLbls, labelsResult, bwdMat, image.Size(), InterpolationFlags.Nearest, BorderTypes.Constant, Scalar.All(0));
            labels.Dispose();
        }

        var outputs = new Dictionary<string, object?>
        {
            ["Count"]      = count,
            ["Labels"]     = labelsResult,
            ["Stats"]      = stats,
            ["Centroids"]  = centroids,
            ["LabelImage"] = labelImg,
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }

    private static Mat BuildLabelImage(Mat labels, int componentCount)
    {
        int n = componentCount;

        // Build label→colorByte lookup — O(n) where n = component count.
        // Index 0 = background, stays 0.
        var colorByteOf = new byte[n + 1];
        var ranks       = Enumerable.Range(1, n).ToArray();
        new Random(n).Shuffle(ranks);
        for (int i = 0; i < n; i++)
            colorByteOf[i + 1] = (byte)(ranks[i] * 255 / n);

        // Single pass over all pixels: label int32 → color byte — O(W*H).
        int pixelCount = labels.Rows * labels.Cols;
        var labelArray = new int[pixelCount];
        Marshal.Copy(labels.Data, labelArray, 0, pixelCount);

        var rankArray = new byte[pixelCount];
        for (int i = 0; i < pixelCount; i++)
            rankArray[i] = colorByteOf[labelArray[i]];

        using var rank8 = new Mat(labels.Rows, labels.Cols, MatType.CV_8U);
        Marshal.Copy(rankArray, 0, rank8.Data, pixelCount);

        // Jet: 0=blue (small) → 255=red (large), no wrap-around.
        var colored = new Mat();
        Cv2.ApplyColorMap(rank8, colored, ColormapTypes.Jet);

        // Zero out background (colorByte=0 still gets a Jet color).
        using var bgMask = new Mat();
        Cv2.Compare(labels, Scalar.All(0), bgMask, CmpTypes.EQ);
        colored.SetTo(Scalar.Black, bgMask);

        return colored;
    }
}
