using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using OpenCvSharp;

namespace IPLab.Core.Operators;

public class ConnectedComponentsOperator : IOperatorType
{
    public string TypeName  => "ConnectedComponents";
    public string Category  => "Detection";
    public string Icon      => "component";
    public IReadOnlyList<ParameterDescriptor> ParameterSchema =>
    [
        new() { Name = "Image",        Label = "Image",        Type = ParameterType.Object, IsConnectable = true },
        new() { Name = "Connectivity", Label = "Connectivity", Type = ParameterType.Enum,   IsConnectable = false,
                DefaultValue = "8", Options = ["4", "8"] }
    ];
    public IReadOnlyList<string> OutputPorts => ["Components"];

    public object? Execute(IReadOnlyDictionary<string, object?> parameters)
    {
        var image        = (Mat)parameters["Image"]!;
        var connectivity = (string?)parameters.GetValueOrDefault("Connectivity") ?? "8";

        var conn = connectivity == "4" ? PixelConnectivity.Connectivity4 : PixelConnectivity.Connectivity8;

        using var labels    = new Mat();
        using var stats     = new Mat();
        using var centroids = new Mat();

        int count = Cv2.ConnectedComponentsWithStats(image, labels, stats, centroids, conn);

        // Label 0 is the background; start from 1.
        var result = new ConnectedComponentInfo[count - 1];
        for (int i = 1; i < count; i++)
        {
            int left   = stats.At<int>(i, 0);
            int top    = stats.At<int>(i, 1);
            int width  = stats.At<int>(i, 2);
            int height = stats.At<int>(i, 3);
            int area   = stats.At<int>(i, 4);
            float cx   = (float)centroids.At<double>(i, 0);
            float cy   = (float)centroids.At<double>(i, 1);

            result[i - 1] = new ConnectedComponentInfo(
                Label:       i,
                Area:        area,
                BoundingBox: new Rect(left, top, width, height),
                Centroid:    new Point2f(cx, cy));
        }

        return result;
    }
}
