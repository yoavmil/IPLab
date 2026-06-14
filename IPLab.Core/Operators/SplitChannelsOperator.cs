using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Splits a BGR image into its Red, Green, and Blue channel images (each as a single-channel grayscale Mat).</summary>
/// <seealso href="https://docs.opencv.org/4.x/d2/de8/group__core__array.html#ga0547c7fed86152d7e9d0096029c8518a">OpenCV: split</seealso>
public class SplitChannelsOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "SplitChannels";
    /// <inheritdoc/>
    public string Category  => "Color & Channels";
    /// <inheritdoc/>
    public string Icon      => "split";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image", Label = "Image", ConnectableType = typeof(Mat) }
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts =>
    [
        new() { Name = "Red",   DataType = typeof(Mat), IsDisplayImage = true },
        new() { Name = "Green", DataType = typeof(Mat), IsDisplayImage = true },
        new() { Name = "Blue",  DataType = typeof(Mat), IsDisplayImage = true },
    ];

    /// <inheritdoc/>
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
