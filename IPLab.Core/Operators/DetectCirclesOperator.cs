using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

/// <summary>Detects circles using Hough Circle Transform and outputs them back-projected to original-image coordinates, with optional ROI support.</summary>
/// <seealso href="https://docs.opencv.org/4.x/dd/d1a/group__imgproc__feature.html#ga47849c3be0d0406ad3ca45db65a25d2d">OpenCV: HoughCircles</seealso>
public class DetectCirclesOperator : IOperatorType
{
    /// <inheritdoc/>
    public string TypeName  => "DetectCircles";
    /// <inheritdoc/>
    public string Category  => "Detection";
    /// <inheritdoc/>
    public string Icon      => "circle";
    /// <inheritdoc/>
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",     Label = "Image",              ConnectableType = typeof(Mat) },
        new() { Name = "MinDist",   Label = "Min Distance",       Type = ParameterType.Double },
        new() { Name = "Param1",    Label = "Canny Threshold",    Type = ParameterType.Double },
        new() { Name = "Param2",    Label = "Accumulator Thresh", Type = ParameterType.Double },
        new() { Name = "MinRadius", Label = "Min Radius",         Type = ParameterType.Int,},
        new() { Name = "MaxRadius", Label = "Max Radius",         Type = ParameterType.Int,},
        ..RoiParameters.Schema,
    ];
    /// <inheritdoc/>
    public IReadOnlyList<OutputPortDescriptor> OutputPorts => [new() { Name = "Circles", DataType = typeof(CircleSegment[]) }, ..RoiParameters.OutputPorts];

    /// <inheritdoc/>
    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        var minDist   = Convert.ToDouble(parameters["MinDist"]);
        var param1    = Convert.ToDouble(parameters["Param1"]);
        var param2    = Convert.ToDouble(parameters["Param2"]);
        var minRadius = Convert.ToInt32(parameters["MinRadius"]);
        var maxRadius = Convert.ToInt32(parameters["MaxRadius"]);

        var roi = RoiParameters.Extract(parameters);
        using var warped = roi is { Angle: not 0.0 } ? RoiParameters.WarpForRoi(image, roi) : null;
        var effectiveImg = warped ?? image;

        var roiRect = roi is not null ? (Rect?)RoiParameters.Clamp(roi, effectiveImg.Width, effectiveImg.Height) : null;
        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?> { ["Circles"] = Array.Empty<CircleSegment>() };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        var transform = roiRect.HasValue ? RoiParameters.BuildTransform(roi!, rect) : null;
        using var crop = roiRect.HasValue ? new Mat(effectiveImg, rect) : null;
        var       src  = crop ?? effectiveImg;

        var circles = Cv2.HoughCircles(src, HoughModes.Gradient, dp: 1, minDist,
                                        param1: param1, param2: param2,
                                        minRadius: minRadius, maxRadius: maxRadius);

        if (transform is { } t)
            circles = [.. circles.Select(c => RoiParameters.BackProject(c, t))];

        var outputs = new Dictionary<string, object?> { ["Circles"] = circles };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
