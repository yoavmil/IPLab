using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DetectBlobsOperator : IOperatorType
{
    public string TypeName  => "DetectBlobs";
    public string Category  => "Detection";
    public string Icon      => "blob";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",              Label = "Image",                Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "MinCircularity",     Label = "Min Circularity",      Type = ParameterType.Double, IsConnectable = false, DefaultValue = 0.8,   Min = 0.0, Max = 1.0 },
        new() { Name = "MinArea",            Label = "Min Area (px²)",       Type = ParameterType.Double, IsConnectable = false, DefaultValue = 50.0   },
        new() { Name = "MaxArea",            Label = "Max Area (px²)",       Type = ParameterType.Double, IsConnectable = false, DefaultValue = 50000.0 },
        new() { Name = "MinDistBetweenBlobs",Label = "Min Dist Between Blobs",Type = ParameterType.Double, IsConnectable = false, DefaultValue = 10.0  },
        new() { Name = "MinThreshold",       Label = "Min Threshold",         Type = ParameterType.Double, IsConnectable = false, DefaultValue = 100.0 },
        new() { Name = "MaxThreshold",       Label = "Max Threshold",         Type = ParameterType.Double, IsConnectable = false, DefaultValue = 200.0 }
    ];
    public IReadOnlyList<string> OutputPorts => ["Blobs"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image               = (Mat)parameters["Image"]!;
        var minCircularity      = (float)Convert.ToDouble(parameters["MinCircularity"]);
        var minArea             = (float)Convert.ToDouble(parameters["MinArea"]);
        var maxArea             = (float)Convert.ToDouble(parameters["MaxArea"]);
        var minDistBetweenBlobs = (float)Convert.ToDouble(parameters["MinDistBetweenBlobs"]);
        var minThreshold        = (float)Convert.ToDouble(parameters["MinThreshold"]);
        var maxThreshold        = (float)Convert.ToDouble(parameters["MaxThreshold"]);

        var p = new SimpleBlobDetector.Params
        {
            FilterByColor        = true,
            BlobColor            = 255,
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
            ThresholdStep        = maxThreshold - minThreshold,
            MinRepeatability     = 1
        };

        using var detector = SimpleBlobDetector.Create(p);
        return detector.Detect(image);
    }
}
