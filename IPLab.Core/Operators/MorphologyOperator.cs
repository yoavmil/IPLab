using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class MorphologyOperator : IOperatorType
{
    public string TypeName => "Morphology";
    public string Category => "Filters";
    public string Icon     => "morphology";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",       Label = "Image",        Type = ParameterType.Object, IsConnectable = true },
        new() { Name = "Operation",   Label = "Operation",    Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Erode",
                Options = ["Erode", "Dilate", "Open", "Close", "Gradient", "TopHat", "BlackHat"] },
        new() { Name = "KernelShape", Label = "Kernel Shape", Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Rect", Options = ["Rect", "Ellipse", "Cross"] },
        new() { Name = "KernelSize",  Label = "Kernel Size",  Type = ParameterType.Int,    IsConnectable = false,
                DefaultValue = 3, Min = 1, Max = 99 },
        new() { Name = "Iterations",  Label = "Iterations",   Type = ParameterType.Int,    IsConnectable = false,
                DefaultValue = 1, Min = 1, Max = 20 },
    ];

    public IReadOnlyList<string> OutputPorts => ["Image"];

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
        var output = new Mat();
        Cv2.MorphologyEx(image, output, morphOp, kernel, iterations: iters);
        return output;
    }
}
