using OpenCvSharp;

namespace IPLab.Core.Spatial;

/// <summary>A line segment with sub-pixel floating-point endpoints.</summary>
/// <param name="P1">First endpoint in image coordinates.</param>
/// <param name="P2">Second endpoint in image coordinates.</param>
public readonly record struct LineSegment2f(Point2f P1, Point2f P2);
