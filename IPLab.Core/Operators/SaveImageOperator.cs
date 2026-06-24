using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Writes the input image to a file. Has no output ports.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d4/da8/group__imgcodecs.html#gabbc7ef1aa2edfaa87772f1202d67e0ce">OpenCV: imwrite</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#saveimage">Operator reference</seealso>
public class SaveImageOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "SaveImage";
    /// <inheritdoc/>
    public string Category  => "I/O";
    /// <inheritdoc/>
    public string Icon      => "save";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",    Label = "Image",     ConnectableType = typeof(Mat) },
        new() { Name = "FilePath", Label = "File Path", Type = ParameterType.String }
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image    = (Mat)parameters["Image"]!;
        var filePath = (string)parameters["FilePath"]!;
        Cv2.ImWrite(filePath, image);
        return new Dictionary<string, object?>();
    }
}
