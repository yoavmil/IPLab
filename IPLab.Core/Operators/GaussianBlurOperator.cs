using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class GaussianBlurOperator : IOperatorType
{
    public string TypeName => "GaussianBlur";
    public string Category => "Filters";
    public string Icon     => "blur";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",      Label = "Image",       ConnectableType = typeof(Mat) },
        new() { Name = "KernelSize", Label = "Kernel Size", Type = ParameterType.Int,DefaultValue = 5, Min = 1, Max = 31 },
        new() { Name = "Sigma",      Label = "Sigma",       Type = ParameterType.Double, DefaultValue = 0.0, Min = 0.0, Max = 50.0 },
        ..RoiParameters.Schema,
    ];

    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat) }, ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var size   = Convert.ToInt32(parameters.GetValueOrDefault("KernelSize") ?? 5);
        var sigma  = Convert.ToDouble(parameters.GetValueOrDefault("Sigma")      ?? 0.0);

        if (size  < 0) throw new ArgumentOutOfRangeException(nameof(parameters), "KernelSize must be >= 0.");
        if (sigma < 0) throw new ArgumentOutOfRangeException(nameof(parameters), "Sigma must be >= 0.");

        // Kernel size must be odd
        if (size % 2 == 0) size++;

        var result = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.GaussianBlur(src, r, new Size(size, size), sigma); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
