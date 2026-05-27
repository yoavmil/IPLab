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
        ..RoiParameters.Schema,
    ];

    public IReadOnlyList<string> OutputPorts => ["Image", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        var operation = parameters.GetValueOrDefault("Operation") as string ?? "Erode";
        var shape     = parameters.GetValueOrDefault("KernelShape") as string ?? "Rect";
        var size      = Convert.ToInt32(parameters.GetValueOrDefault("KernelSize") ?? 3);
        var iters     = Convert.ToInt32(parameters.GetValueOrDefault("Iterations") ?? 1);
        var roi       = RoiParameters.Extract(parameters);

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

        Mat resultImage;
        if (roi is null)
        {
            resultImage = new Mat();
            Cv2.MorphologyEx(image, resultImage, morphOp, kernel, iterations: iters);
        }
        else
        {
            int x = Math.Max(0, (int)roi.X);
            int y = Math.Max(0, (int)roi.Y);
            int w = Math.Min((int)roi.Width,  image.Width  - x);
            int h = Math.Min((int)roi.Height, image.Height - y);
            if (w <= 0 || h <= 0)
            {
                resultImage = image.Clone();
            }
            else
            {
                var rect = new Rect(x, y, w, h);
                using var roiSrc    = new Mat(image, rect);
                var       roiResult = new Mat();
                Cv2.MorphologyEx(roiSrc, roiResult, morphOp, kernel, iterations: iters);

                resultImage = image.Clone();
                using var dstRoi = new Mat(resultImage, rect);
                roiResult.CopyTo(dstRoi);
                roiResult.Dispose();
            }
        }

        var outputs = new Dictionary<string, object?> { ["Image"] = resultImage };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
