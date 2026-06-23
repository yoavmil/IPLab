using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Applies a morphological operation (erode, dilate, open, close, gradient, top-hat, or black-hat) with a configurable kernel, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d4/d86/group__imgproc__filter.html#ga67493776e3ad1a3df63883829375201f">OpenCV: morphologyEx</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#morphology">Operator reference</seealso>
public class MorphologyOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "Morphology";
    /// <inheritdoc/>
    public string Category => "Filters";
    /// <inheritdoc/>
    public string Icon     => "morphology";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",       Label = "Image",        ConnectableType = typeof(Mat) },
        new() { Name = "Operation",   Label = "Operation",    Type = ParameterType.Enum,
                DefaultValue = "Erode",
                Options = ["Erode", "Dilate", "Open", "Close", "Gradient", "TopHat", "BlackHat"] },
        new() { Name = "KernelShape", Label = "Kernel Shape", Type = ParameterType.Enum,
                DefaultValue = "Rect", Options = ["Rect", "Ellipse", "Cross"] },
        new() { Name = "KernelSize",  Label = "Kernel Size",  Type = ParameterType.Int,
                DefaultValue = 3, Min = 1, Max = 99 },
        new() { Name = "Iterations",  Label = "Iterations",   Type = ParameterType.Int,
                DefaultValue = 1, Min = 1, Max = 20 },
        ..RoiParameters.Schema,
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        var operation = parameters.GetValueOrDefault("Operation") as string ?? "Erode";
        var shape     = parameters.GetValueOrDefault("KernelShape") as string ?? "Rect";
        var size      = Convert.ToInt32(parameters.GetValueOrDefault("KernelSize") ?? 3);
        var iters     = Convert.ToInt32(parameters.GetValueOrDefault("Iterations") ?? 1);

        var morphOp = operation switch
        {
            "Dilate"   => MorphTypes.Dilate,
            "Open"     => MorphTypes.Open,
            "Close"    => MorphTypes.Close,
            "Gradient" => MorphTypes.Gradient,
            "TopHat"   => MorphTypes.TopHat,
            "BlackHat" => MorphTypes.BlackHat,
            _          => MorphTypes.Erode,
        };

        var morphShape = shape switch
        {
            "Ellipse" => MorphShapes.Ellipse,
            "Cross"   => MorphShapes.Cross,
            _         => MorphShapes.Rect,
        };

        using var kernel = Cv2.GetStructuringElement(morphShape, new Size(size, size));

        var resultImage = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.MorphologyEx(src, r, morphOp, kernel, iterations: iters); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = resultImage };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
