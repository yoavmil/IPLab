using System.Text.Json;

namespace IPLab.Core.Models;

/// <summary>Camera intrinsic matrix and distortion coefficients consumed by the Undistort operator.</summary>
public class UndistortCalibrationData
{
    /// <summary>Width of the calibrated image in pixels.</summary>
    public int ImageWidth { get; set; }
    /// <summary>Height of the calibrated image in pixels.</summary>
    public int ImageHeight { get; set; }
    /// <summary>Three-by-three camera matrix stored row-major as a jagged array.</summary>
    public double[][] CameraMatrix { get; set; } = [];
    /// <summary>OpenCV distortion coefficients, typically k1, k2, p1, p2, and k3.</summary>
    public double[] DistCoeffs { get; set; } = [];
    /// <summary>RMS reprojection error in pixels, when supplied by the calibration source.</summary>
    public double ReprojectionError { get; set; }
    /// <summary>Optional ISO 8601 UTC timestamp written by the calibration source.</summary>
    public string? CreatedAt { get; set; }
}

internal static class UndistortCalibrationFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static UndistortCalibrationData Load(string path) =>
        JsonSerializer.Deserialize<UndistortCalibrationData>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Failed to parse calibration file: {path}");

    public static double[,] ToRectangular(double[][] values)
    {
        if (values.Length == 0 || values.Any(row => row.Length != values[0].Length))
            throw new InvalidDataException("Calibration camera matrix must be a non-empty rectangular array.");

        int rows = values.Length;
        int columns = values[0].Length;
        var result = new double[rows, columns];
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
            result[row, column] = values[row][column];
        return result;
    }
}
