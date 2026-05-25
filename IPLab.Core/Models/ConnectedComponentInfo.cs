using OpenCvSharp;

namespace IPLab.Core.Models;

public record ConnectedComponentInfo(int Label, int Area, Rect BoundingBox, Point2f Centroid);
