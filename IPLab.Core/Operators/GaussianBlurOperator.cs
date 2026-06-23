using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Applies Gaussian blur to the input image, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d4/d86/group__imgproc__filter.html#gaabe8c836e97159a9193fb0b11ac52cf1">OpenCV: GaussianBlur</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#gaussianblur">Operator reference</seealso>
public class GaussianBlurOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName => "GaussianBlur";
    /// <inheritdoc/>
    public string Category => "Filters";
    /// <inheritdoc/>
    public string Icon     => "blur";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",      Label = "Image",       ConnectableType = typeof(Mat) },
        new() { Name = "KernelSize", Label = "Kernel Size", Type = ParameterType.Int,DefaultValue = 5, Min = 1 },
        new() { Name = "Sigma",      Label = "Sigma",       Type = ParameterType.Double, DefaultValue = 0.0, Min = 0.0 },
        ..RoiParameters.Schema,
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
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
