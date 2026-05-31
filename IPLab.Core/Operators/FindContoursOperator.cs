using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Utilities;
using OpenCvSharp;
using System.Linq;

namespace IPLab.Core.Operators;

public class FindContoursOperator : IOperatorType
{
    public string TypeName  => "FindContours";
    public string Category  => "Detection";
    public string Icon      => "contour";

    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",   Label = "Image",    Type = ParameterType.Object, IsConnectable = true },
        new() { Name = "Mode",    Label = "Mode",     Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "List",
                Options = ["External", "List", "CComp", "Tree"] },
        new() { Name = "Method",  Label = "Method",   Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Simple",
                Options = ["None", "Simple", "TC89L1", "TC89KCOS"] },
        new() { Name = "Filter",  Label = "Filter",   Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "Fix",
                Options = ["None", "Filter", "Fix"] },
        new() { Name = "MinArea", Label = "Min Area", Type = ParameterType.Double, IsConnectable = false,
                DefaultValue = 1.0,
                ShowWhenParam = "Filter", ShowWhenValues = ["Filter", "Fix"] },
        ..RoiParameters.Schema,
    ];

    public IReadOnlyList<string> OutputPorts => ["Contours", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image   = (Mat)parameters["Image"]!;
        var mode    = (string?)parameters.GetValueOrDefault("Mode")   ?? "List";
        var method  = (string?)parameters.GetValueOrDefault("Method") ?? "Simple";
        var filter  = (string?)parameters.GetValueOrDefault("Filter") ?? "Fix";
        var minArea = parameters.GetValueOrDefault("MinArea") is double d ? d : 1.0;

        var retrievalMode = mode switch
        {
            "External" => RetrievalModes.External,
            "CComp"    => RetrievalModes.CComp,
            "Tree"     => RetrievalModes.Tree,
            _          => RetrievalModes.List
        };

        var approxMethod = method switch
        {
            "None"     => ContourApproximationModes.ApproxNone,
            "TC89L1"   => ContourApproximationModes.ApproxTC89L1,
            "TC89KCOS" => ContourApproximationModes.ApproxTC89KCOS,
            _          => ContourApproximationModes.ApproxSimple
        };

        var roiRect = RoiParameters.Clamp(parameters, image.Width, image.Height);
        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?> { ["Contours"] = Array.Empty<Point[]>() };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        using var crop = roiRect.HasValue ? new Mat(image, rect) : null;
        var       src  = crop ?? image;

        Cv2.FindContours(src, out Point[][] contours, out _, retrievalMode, approxMethod);

        Point[][] output = filter switch
        {
            "Filter" => contours.Where(c => ContourValidator.IsValid(c, minArea)).ToArray(),
            "Fix"    => contours
                            .SelectMany(c => ContourValidator.ToValidRings(c, minArea))
                            .Select(ring => ring.Select(p => new Point((int)p.X, (int)p.Y)).ToArray())
                            .Where(c => c.Length >= 3)
                            .ToArray(),
            _        => contours
        };

        // Translate crop-local coordinates back to full-image coordinates.
        if (roiRect.HasValue)
            output = [.. output.Select(c => c.Select(p => new Point(p.X + rect.X, p.Y + rect.Y)).ToArray())];

        var outputs = new Dictionary<string, object?> { ["Contours"] = output };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
