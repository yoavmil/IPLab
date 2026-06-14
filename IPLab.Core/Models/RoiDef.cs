namespace IPLab.Core.Models;

/// <summary>Defines a region of interest by its center, size, and optional rotation.</summary>
/// <param name="CX">X coordinate of the ROI center in image pixels.</param>
/// <param name="CY">Y coordinate of the ROI center in image pixels.</param>
/// <param name="Width">Width of the ROI in pixels (axis-aligned before rotation).</param>
/// <param name="Height">Height of the ROI in pixels (axis-aligned before rotation).</param>
/// <param name="Angle">Rotation of the ROI in degrees. Zero means axis-aligned.</param>
public record RoiDef(double CX, double CY, double Width, double Height, double Angle = 0.0);
