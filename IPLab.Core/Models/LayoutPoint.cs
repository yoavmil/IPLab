namespace IPLab.Core.Models;

/// <summary>A 2-D position in the visual flow editor's canvas coordinate space.</summary>
/// <param name="X">Horizontal position in canvas units.</param>
/// <param name="Y">Vertical position in canvas units.</param>
public readonly record struct LayoutPoint(double X, double Y);
