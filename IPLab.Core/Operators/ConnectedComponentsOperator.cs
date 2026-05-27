using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace IPLab.Core.Operators;

public class ConnectedComponentsOperator : IOperatorType
{
    public string TypeName  => "ConnectedComponents";
    public string Category  => "Detection";
    public string Icon      => "component";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",            Label = "Image",             Type = ParameterType.Object, IsConnectable = true },
        new() { Name = "Connectivity",     Label = "Connectivity",      Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "8", Options = ["4", "8"] },
        new() { Name = "OutputLabelImage", Label = "Output Label Image", Type = ParameterType.Bool,   IsConnectable = false,
                DefaultValue = true },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<string> OutputPorts => ["Components", "LabelImage", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image            = (Mat)parameters["Image"]!;
        var connectivity     = (string?)parameters.GetValueOrDefault("Connectivity") ?? "8";
        var outputLabelImage = parameters.GetValueOrDefault("OutputLabelImage") is not false;

        var conn    = connectivity == "4" ? PixelConnectivity.Connectivity4 : PixelConnectivity.Connectivity8;
        var roiRect = RoiParameters.Clamp(parameters, image.Width, image.Height);

        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?>
            {
                ["Components"] = Array.Empty<ConnectedComponentInfo>(),
                ["LabelImage"] = outputLabelImage ? new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black) : null,
            };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect    = roiRect ?? default;
        int offsetX = rect.X;
        int offsetY = rect.Y;

        using var crop = roiRect.HasValue ? new Mat(image, rect) : null;
        var       src  = crop ?? image;

        using var labels    = new Mat();
        using var stats     = new Mat();
        using var centroids = new Mat();

        int count = Cv2.ConnectedComponentsWithStats(src, labels, stats, centroids, conn);

        // Label 0 is the background; start from 1.
        var result = new ConnectedComponentInfo[count - 1];
        for (int i = 1; i < count; i++)
        {
            int left   = stats.At<int>(i, 0) + offsetX;
            int top    = stats.At<int>(i, 1) + offsetY;
            int width  = stats.At<int>(i, 2);
            int height = stats.At<int>(i, 3);
            int area   = stats.At<int>(i, 4);
            float cx   = (float)centroids.At<double>(i, 0) + offsetX;
            float cy   = (float)centroids.At<double>(i, 1) + offsetY;

            result[i - 1] = new ConnectedComponentInfo(
                Label:       i,
                Area:        area,
                BoundingBox: new Rect(left, top, width, height),
                Centroid:    new Point2f(cx, cy));
        }

        Mat? labelImg = null;
        if (outputLabelImage)
        {
            var cropLabel = BuildLabelImage(labels, result, randomColors: true);
            if (crop is not null)
            {
                // Embed the crop-sized label image into a full-size black mat.
                labelImg = new Mat(image.Rows, image.Cols, MatType.CV_8UC3, Scalar.Black);
                using var dst = new Mat(labelImg, rect);
                cropLabel.CopyTo(dst);
                cropLabel.Dispose();
            }
            else
            {
                labelImg = cropLabel;
            }
        }

        var outputs = new Dictionary<string, object?>
        {
            ["Components"] = result,
            ["LabelImage"] = labelImg,
        };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }

    private static Mat BuildLabelImage(Mat labels, ConnectedComponentInfo[] components, bool randomColors)
    {
        int n = components.Length;

        // Build label→colorByte lookup — O(n) where n = component count.
        // Index 0 = background, stays 0.
        var colorByteOf = new byte[n + 1];
        if (randomColors)
        {
            var ranks = Enumerable.Range(1, n).ToArray();
            new Random(n).Shuffle(ranks);
            for (int i = 0; i < n; i++)
                colorByteOf[components[i].Label] = (byte)(ranks[i] * 255 / n);
        }
        else
        {
            var sorted = components.OrderBy(c => c.Area).ToArray();
            for (int r = 0; r < n; r++)
                colorByteOf[sorted[r].Label] = (byte)((r + 1) * 255 / n);
        }

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
