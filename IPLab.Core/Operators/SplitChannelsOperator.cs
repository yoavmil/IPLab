using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class SplitChannelsOperator : IOperatorType
{
    public string TypeName => "SplitChannels";
    public string Icon => "split";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", Type = ParameterType.Object, IsConnectable = true }
    ];
    public IReadOnlyList<string> OutputPorts => ["Red", "Green", "Blue"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image = (Mat)parameters["Image"]!;
        Cv2.Split(image, out Mat[] channels); // OpenCV channel order: BGR
        return new Dictionary<string, object?>
        {
            ["Blue"]  = channels[0],
            ["Green"] = channels[1],
            ["Red"]   = channels[2]
        };
    }
}
