using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ThresholdOperator : IOperatorType
{
    public string TypeName  => "Threshold";
    public string Category  => "Filters";
    public string Icon      => "threshold";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",           Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "Method",         Label = "Method",          Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Fixed", Options = ["Fixed", "Otsu", "Triangle", "Adaptive"] },
        new() { Name = "Type",           Label = "Output Type",     Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Binary", Options = ["Binary", "BinaryInv", "Trunc", "ToZero", "ToZeroInv"] },
        new() { Name = "Thresh",         Label = "Threshold",       Type = ParameterType.Double, IsConnectable = false, DefaultValue = 128.0, Min = 0.0, Max = 255.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Fixed"] },
        new() { Name = "AdaptiveMethod", Label = "Adaptive Method", Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "MeanC", Options = ["MeanC", "GaussianC"],
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        new() { Name = "BlockSize",      Label = "Block Size",      Type = ParameterType.Int,    IsConnectable = false, DefaultValue = 11, Min = 3.0, Max = 9999.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        new() { Name = "C",              Label = "C",               Type = ParameterType.Double, IsConnectable = false, DefaultValue = 2.0, Min = -100.0, Max = 100.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<string> OutputPorts => ["Image", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var method = parameters.GetValueOrDefault("Method") as string ?? "Fixed";
        var type   = parameters.GetValueOrDefault("Type")   as string ?? "Binary";

        Mat result;
        if (method == "Adaptive")
        {
            var adaptiveMethod = parameters.GetValueOrDefault("AdaptiveMethod") as string ?? "MeanC";
            var blockSize      = Convert.ToInt32(parameters.GetValueOrDefault("BlockSize") ?? 11);
            var c              = Convert.ToDouble(parameters.GetValueOrDefault("C") ?? 2.0);

            if (blockSize < 3) blockSize = 3;
            if (blockSize % 2 == 0) blockSize++;

            var adaptType  = adaptiveMethod == "GaussianC"
                ? AdaptiveThresholdTypes.GaussianC : AdaptiveThresholdTypes.MeanC;
            var threshType = type == "BinaryInv" ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;

            result = RoiParameters.ApplyImageFilter(image, parameters, src =>
            {
                var r = new Mat();
                Cv2.AdaptiveThreshold(src, r, 255, adaptType, threshType, blockSize, c);
                return r;
            });
        }
        else
        {
            var thresh = Convert.ToDouble(parameters.GetValueOrDefault("Thresh") ?? 128.0);

            var threshType = type switch
            {
                "BinaryInv" => ThresholdTypes.BinaryInv,
                "Trunc"     => ThresholdTypes.Trunc,
                "ToZero"    => ThresholdTypes.Tozero,
                "ToZeroInv" => ThresholdTypes.TozeroInv,
                _           => ThresholdTypes.Binary,
            };
            if (method == "Otsu")     threshType |= ThresholdTypes.Otsu;
            if (method == "Triangle") threshType |= ThresholdTypes.Triangle;

            result = RoiParameters.ApplyImageFilter(image, parameters,
                src => { var r = new Mat(); Cv2.Threshold(src, r, thresh, 255.0, threshType); return r; });
        }

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
