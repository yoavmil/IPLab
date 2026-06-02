using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ConvertToGrayscaleOperator : IOperatorType
{
    public string TypeName  => "ConvertToGrayscale";
    public string Category  => "Color & Channels";
    public string Icon      => "palette";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",  ConnectableType = typeof(Mat) },
        new() { Name = "Method", Label = "Method", Type = ParameterType.Enum,
                DefaultValue = "Luminance", Options = ["Luminance", "HsvValue"] }
    ];
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Image", DataType = typeof(Mat), IsDisplayImage = true }];

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
            try     { return channels[2]; }
            finally { channels[0].Dispose(); channels[1].Dispose(); }
        }

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
