using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DetectSimpleBlobsOperator : IOperatorType
{
    public string TypeName  => "DetectSimpleBlobs";
    public string Category  => "Detection";
    public string Icon      => "blob";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",              Label = "Image",                ConnectableType = typeof(Mat) },
        new() { Name = "Polarity",           Label = "Polarity",             Type = ParameterType.Enum,
                DefaultValue = "Light on Dark", Options = ["Light on Dark", "Dark on Light"] },
        new() { Name = "MinCircularity",     Label = "Min Circularity",      Type = ParameterType.Double, DefaultValue = 0.8,   Min = 0.0, Max = 1.0 },
        new() { Name = "MinArea",            Label = "Min Area (px²)",       Type = ParameterType.Double, DefaultValue = 50.0   },
        new() { Name = "MaxArea",            Label = "Max Area (px²)",       Type = ParameterType.Double, DefaultValue = 50000.0 },
        new() { Name = "MinDistBetweenBlobs",Label = "Min Dist Between Blobs",Type = ParameterType.Double, DefaultValue = 10.0  },
        new() { Name = "MinThreshold",       Label = "Min Threshold",         Type = ParameterType.Double, DefaultValue = 100.0 },
        new() { Name = "MaxThreshold",       Label = "Max Threshold",         Type = ParameterType.Double, DefaultValue = 200.0 },
        new() { Name = "ThresholdStep",      Label = "Threshold Step",        Type = ParameterType.Double, DefaultValue = 10.0  },
        new() { Name = "MinRepeatability",   Label = "Min Repeatability",     Type = ParameterType.Int,DefaultValue = 2     },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Blobs", DataType = typeof(KeyPoint[]) }, ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image               = (Mat)parameters["Image"]!;
        var polarity            = (string?)parameters.GetValueOrDefault("Polarity") ?? "Light on Dark";
        var minCircularity      = (float)Convert.ToDouble(parameters["MinCircularity"]);
        var minArea             = (float)Convert.ToDouble(parameters["MinArea"]);
        var maxArea             = (float)Convert.ToDouble(parameters["MaxArea"]);
        var minDistBetweenBlobs = (float)Convert.ToDouble(parameters["MinDistBetweenBlobs"]);
        var minThreshold        = (float)Convert.ToDouble(parameters["MinThreshold"]);
        var maxThreshold        = (float)Convert.ToDouble(parameters["MaxThreshold"]);
        var thresholdStep       = (float)Convert.ToDouble(parameters.GetValueOrDefault("ThresholdStep") ?? 10.0);
        var minRepeatability    = (uint)Convert.ToInt32(parameters.GetValueOrDefault("MinRepeatability") ?? 2);

        var p = new SimpleBlobDetector.Params
        {
            FilterByColor        = true,
            BlobColor            = (byte)(polarity == "Dark on Light" ? 0 : 255),
            FilterByCircularity  = true,
            MinCircularity       = minCircularity,
            FilterByArea         = true,
            MinArea              = minArea,
            MaxArea              = maxArea,
            FilterByConvexity    = false,
            FilterByInertia      = false,
            MinDistBetweenBlobs  = minDistBetweenBlobs,
            MinThreshold         = minThreshold,
            MaxThreshold         = maxThreshold,
            ThresholdStep        = thresholdStep,
            MinRepeatability     = minRepeatability
        };

        var roiRect = RoiParameters.Clamp(parameters, image.Width, image.Height);
        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?> { ["Blobs"] = Array.Empty<KeyPoint>() };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        using var crop = roiRect.HasValue ? new Mat(image, rect) : null;
        var       src  = crop ?? image;

        using var detector = SimpleBlobDetector.Create(p);
        var blobs = detector.Detect(src);

        // Translate crop-local coordinates back to full-image coordinates.
        if (roiRect.HasValue)
            blobs = [.. blobs.Select(k => new KeyPoint(k.Pt.X + rect.X, k.Pt.Y + rect.Y,
                                                        k.Size, k.Angle, k.Response, k.Octave, k.ClassId))];

        var outputs = new Dictionary<string, object?> { ["Blobs"] = blobs };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
