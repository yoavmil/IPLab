using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class DetectCirclesOperator : IOperatorType
{
    public string TypeName  => "DetectCircles";
    public string Category  => "Detection";
    public string Icon      => "circle";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",     Label = "Image",              Type = ParameterType.Object, IsConnectable = true  },
        new() { Name = "MinDist",   Label = "Min Distance",       Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "Param1",    Label = "Canny Threshold",    Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "Param2",    Label = "Accumulator Thresh", Type = ParameterType.Double, IsConnectable = false },
        new() { Name = "MinRadius", Label = "Min Radius",         Type = ParameterType.Int,    IsConnectable = false },
        new() { Name = "MaxRadius", Label = "Max Radius",         Type = ParameterType.Int,    IsConnectable = false },
        ..RoiParameters.Schema,
    ];
    public IReadOnlyList<string> OutputPorts => ["Circles", ..RoiParameters.OutputPorts];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image     = (Mat)parameters["Image"]!;
        var minDist   = Convert.ToDouble(parameters["MinDist"]);
        var param1    = Convert.ToDouble(parameters["Param1"]);
        var param2    = Convert.ToDouble(parameters["Param2"]);
        var minRadius = Convert.ToInt32(parameters["MinRadius"]);
        var maxRadius = Convert.ToInt32(parameters["MaxRadius"]);
        var roiRect = RoiParameters.Clamp(parameters, image.Width, image.Height);
        if (roiRect is { Width: <= 0 } or { Height: <= 0 })
        {
            var empty = new Dictionary<string, object?> { ["Circles"] = Array.Empty<CircleSegment>() };
            RoiParameters.AddToOutputs(empty, parameters);
            return empty;
        }

        var rect      = roiRect ?? default;
        using var crop = roiRect.HasValue ? new Mat(image, rect) : null;
        var       src  = crop ?? image;

        var circles = Cv2.HoughCircles(src, HoughModes.Gradient, dp: 1, minDist,
                                        param1: param1, param2: param2,
                                        minRadius: minRadius, maxRadius: maxRadius);

        // Translate crop-local coordinates back to full-image coordinates.
        if (roiRect.HasValue)
            circles = [.. circles.Select(c => new CircleSegment(
                new Point2f(c.Center.X + rect.X, c.Center.Y + rect.Y), c.Radius))];

        var outputs = new Dictionary<string, object?> { ["Circles"] = circles };
        RoiParameters.AddToOutputs(outputs, parameters);
        return outputs;
    }
}
