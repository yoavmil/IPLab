using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Converts a colour image to a single-channel grayscale image using luminance weighting or the HSV value channel.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d8/d01/group__imgproc__color__conversions.html#ga397ae87e1288a81d2363b61574eb8cab">OpenCV: cvtColor</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#converttograyscale">Operator reference</seealso>
public class ConvertToGrayscaleOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "ConvertToGrayscale";
    /// <inheritdoc/>
    public string Category  => "Color & Channels";
    /// <inheritdoc/>
    public string Icon      => "palette";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",  ConnectableType = typeof(Mat) },
        new() { Name = "Method", Label = "Method", Type = ParameterType.Enum,
                DefaultValue = "Luminance", Options = ["Luminance", "HsvValue"] }
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        parameters.TryGetValue("Method", out var methodObj);
        var method = (string?)methodObj ?? "Luminance";

        if (method == "HsvValue")
        {
            using var hsv = new Mat();
            Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
            var channels = Cv2.Split(hsv);
            try     { return new Dictionary<string, object?> { ["Image"] = channels[2] }; }
            finally { channels[0].Dispose(); channels[1].Dispose(); }
        }

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return new Dictionary<string, object?> { ["Image"] = gray };
    }
}
