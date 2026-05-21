using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class FindContoursOperator : IOperatorType
{
    public string TypeName  => "FindContours";
    public string Category  => "Detection";
    public string Icon      => "contour";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",  Label = "Image",  Type = ParameterType.Object, IsConnectable = true },
        new() { Name = "Mode",   Label = "Mode",   Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "External",
                Options = ["External", "List", "CComp", "Tree"] },
        new() { Name = "Method", Label = "Method", Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Simple",
                Options = ["None", "Simple", "TC89L1", "TC89KCOS"] }
    ];
    public IReadOnlyList<string> OutputPorts => ["Contours"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image  = (Mat)parameters["Image"]!;
        var mode   = (string?)parameters.GetValueOrDefault("Mode")   ?? "External";
        var method = (string?)parameters.GetValueOrDefault("Method") ?? "Simple";

        var retrievalMode = mode switch
        {
            "List"   => RetrievalModes.List,
            "CComp"  => RetrievalModes.CComp,
            "Tree"   => RetrievalModes.Tree,
            _        => RetrievalModes.External
        };

        var approxMethod = method switch
        {
            "None"     => ContourApproximationModes.ApproxNone,
            "TC89L1"   => ContourApproximationModes.ApproxTC89L1,
            "TC89KCOS" => ContourApproximationModes.ApproxTC89KCOS,
            _          => ContourApproximationModes.ApproxSimple
        };

        Cv2.FindContours(image, out Point[][] contours, out _, retrievalMode, approxMethod);
        return contours;
    }
}
