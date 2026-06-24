using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.Core.Utilities;
using OpenCvSharp;
using System.Linq;

namespace IPLab.Core.Operators;

/// <summary>Finds contours in a binary image and outputs them as arrays of points in original-image coordinates, with optional ROI and contour filtering support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html#gadf1ad6a0b82947fa1fe3c3d497f260e0">OpenCV: findContours</seealso>
/// <seealso href="https://github.com/yoavmil/IPLab/blob/master/docs/OPERATORS.md#findcontours">Operator reference</seealso>
public class FindContoursOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "FindContours";
    /// <inheritdoc/>
    public string Category  => "Detection";
    /// <inheritdoc/>
    public string Icon      => "contour";

    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",   Label = "Image",    ConnectableType = typeof(Mat) },
        new() { Name = "Mode",    Label = "Mode",     Type = ParameterType.Enum,
                DefaultValue = "List",
                Options = ["External", "List", "CComp", "Tree"] },
        new() { Name = "Method",  Label = "Method",   Type = ParameterType.Enum,
                DefaultValue = "Simple",
                Options = ["None", "Simple", "TC89L1", "TC89KCOS"] },
        new() { Name = "Filter",  Label = "Filter",   Type = ParameterType.Enum,
                DefaultValue = "Fix",
                Options = ["None", "Filter", "Fix"] },
        new() { Name = "MinArea", Label = "Min Area", Type = ParameterType.Double,
                DefaultValue = 1.0,
                ShowWhenParam = "Filter", ShowWhenValues = ["Filter", "Fix"] },
        ..RoiParameters.Schema,
    ];

    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Contours", DataType = typeof(Point[][]) }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Execute(IReadOnlyDictionary<string, object?> parameters)
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

        var roi = RoiParameters.Extract(parameters);
        using var warped = roi is { Angle: not 0.0 } ? RoiParameters.WarpForRoi(image, roi) : null;
        var effectiveImg = warped ?? image;

        var roiRect = roi is not null ? (Rect?)RoiParameters.Clamp(roi, effectiveImg.Width, effectiveImg.Height) : null;
        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?> { ["Contours"] = Array.Empty<Point[]>() };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        var transform = roiRect.HasValue ? RoiParameters.BuildTransform(roi!, rect) : null;
        using var crop = roiRect.HasValue ? new Mat(effectiveImg, rect) : null;
        var       src  = crop ?? effectiveImg;

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

        if (transform is { } t)
            output = [.. output.Select(c => RoiParameters.BackProject(c, t))];

        var outputs = new Dictionary<string, object?> { ["Contours"] = output };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
