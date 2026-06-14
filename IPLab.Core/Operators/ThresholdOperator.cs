using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Applies fixed, Otsu, Triangle, or adaptive thresholding to produce a binary image, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d7/d1b/group__imgproc__misc.html#gae8a4a146d1ca78c626a53577199e9c57">OpenCV: threshold</seealso>
/// <seealso href="https://docs.opencv.org/4.x/d7/d1b/group__imgproc__misc.html#ga72b913f352e4a1b1b397736707afcde3">OpenCV: adaptiveThreshold</seealso>
public class ThresholdOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "Threshold";
    /// <inheritdoc/>
    public string Category  => "Filters";
    /// <inheritdoc/>
    public string Icon      => "threshold";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",          Label = "Image",           ConnectableType = typeof(Mat) },
        new() { Name = "Method",         Label = "Method",          Type = ParameterType.Enum,
                DefaultValue = "Fixed", Options = ["Fixed", "Otsu", "Triangle", "Adaptive"] },
        new() { Name = "Type",           Label = "Output Type",     Type = ParameterType.Enum,
                DefaultValue = "Binary", Options = ["Binary", "BinaryInv", "Trunc", "ToZero", "ToZeroInv"] },
        new() { Name = "Thresh",         Label = "Threshold",       Type = ParameterType.Double, DefaultValue = 128.0, Min = 0.0, Max = 255.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Fixed"] },
        new() { Name = "AdaptiveMethod", Label = "Adaptive Method", Type = ParameterType.Enum,
                DefaultValue = "MeanC", Options = ["MeanC", "GaussianC"],
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        new() { Name = "BlockSize",      Label = "Block Size",      Type = ParameterType.Int,DefaultValue = 11, Min = 3.0, Max = 9999.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        new() { Name = "C",              Label = "C",               Type = ParameterType.Double, DefaultValue = 2.0, Min = -100.0, Max = 100.0,
                ShowWhenParam = "Method", ShowWhenValues = ["Adaptive"] },
        ..RoiParameters.Schema,
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
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
