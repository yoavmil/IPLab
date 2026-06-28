using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPLab.Core.Models;

/// <summary>
/// Corner-correspondence calibration data produced by <see cref="Operators.DistortionCalibrationOperator"/>
/// and consumed by <see cref="Operators.UndistortOperator"/> to build a dense bilinear warp map.
/// The <see cref="Corners"/> list contains the complete grid (detected + gap-filled +
/// boundary-extended) so <see cref="Operators.UndistortOperator"/> needs no preprocessing.
/// </summary>
public class CalibrationData
{
    /// <summary>Width of the image used during calibration, in pixels.</summary>
    public int ImageWidth { get; set; }
    /// <summary>Height of the image used during calibration, in pixels.</summary>
    public int ImageHeight { get; set; }
    /// <summary>Normalised horizontal anchor [0,1]: maps to the output pixel column <c>AnchorX × W</c>.</summary>
    public double AnchorX { get; set; }
    /// <summary>Normalised vertical anchor [0,1]: maps to the output pixel row <c>AnchorY × H</c>.</summary>
    public double AnchorY { get; set; }
    /// <summary>Angle of the checkerboard I-axis relative to image horizontal, in degrees clockwise.</summary>
    public double RotationAngleDeg { get; set; }
    /// <summary>Median pixel distance between adjacent grid corners (filled grid). Used directly by <see cref="Operators.UndistortOperator"/>.</summary>
    public float Pitch { get; set; }
    /// <summary>Mean Col index of the originally detected corners. Anchors the pixel↔grid coordinate mapping.</summary>
    public float MeanCol { get; set; }
    /// <summary>Mean Row index of the originally detected corners. Anchors the pixel↔grid coordinate mapping.</summary>
    public float MeanRow { get; set; }
    /// <summary>Physical scale in mm per pixel, computed from <c>SquareSizeMm / Pitch</c> at calibration time. Null when the square size was not provided.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MmPerPixel { get; set; }
    /// <summary>Complete grid corners (detected + gap-filled + boundary-extended) ready for bilinear warp.</summary>
    public List<CornerRecord> Corners { get; set; } = [];
}

/// <summary>One checkerboard corner: its integer grid position and sub-pixel image location.</summary>
public class CornerRecord
{
    /// <summary>Grid column index (I axis, increasing rightward).</summary>
    public int Col { get; set; }
    /// <summary>Grid row index (J axis, increasing upward).</summary>
    public int Row { get; set; }
    /// <summary>Sub-pixel image X coordinate.</summary>
    public double ImgX { get; set; }
    /// <summary>Sub-pixel image Y coordinate.</summary>
    public double ImgY { get; set; }
}

/// <summary>Serialisation helpers for <see cref="CalibrationData"/>.</summary>
internal static class CalibrationHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Save(CalibrationData data, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
    }

    public static CalibrationData Load(string path) =>
        JsonSerializer.Deserialize<CalibrationData>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Failed to parse calibration file: {path}");
}
