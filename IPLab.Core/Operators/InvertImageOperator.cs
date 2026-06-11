using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Inverts pixel values (bitwise NOT) of the input image, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d2/de8/group__core__array.html#ga0002cf8b418479f4cb49a75442baee2f">OpenCV: bitwise_not</seealso>
public class InvertImageOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "InvertImage";
    /// <inheritdoc/>
    public string Category  => "Color & Channels";
    /// <inheritdoc/>
    public string Icon      => "invert";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", ConnectableType = typeof(Mat) },
        ..RoiParameters.Schema,
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;

        var result = RoiParameters.ApplyImageFilter(image, parameters,
            src => { var r = new Mat(); Cv2.BitwiseNot(src, r); return r; });

        var outputs = new Dictionary<string, object?> { ["Image"] = result };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
