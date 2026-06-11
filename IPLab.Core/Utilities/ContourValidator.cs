using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using System;
using System.Collections.Generic;
using CvPoint    = OpenCvSharp.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace IPLab.Core.Utilities;

/// <summary>Validates and repairs OpenCV contours using NTS (NetTopologySuite) geometry operations.</summary>
public static class ContourValidator
{
    private static readonly GeometryFactory _factory =
        NtsGeometryServices.Instance.CreateGeometryFactory();

    /// <summary>
    /// Returns true if the contour has sufficient area and forms a valid NTS ring.
    /// </summary>
    public static bool IsValid(CvPoint[] contour, double minArea = 1.0)
    {
        if (contour == null || contour.Length < 3)
            return false;

        if (Math.Abs(OpenCvSharp.Cv2.ContourArea(contour)) < minArea)
            return false;

        if (OpenCvSharp.Cv2.ArcLength(contour, closed: true) < 1.0)
            return false;

        Coordinate[] ring = ToClosedRing(contour);
        if (ring.Length < 4)
            return false;

        try
        {
            LinearRing shell = _factory.CreateLinearRing(ring);
            return new NetTopologySuite.Operation.Valid.IsValidOp(shell).IsValid;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a contour to one or more valid exterior rings using GeometryFixer.
    /// Self-intersecting rings are split into valid sub-polygons.
    /// Returns empty if the contour is degenerate or unfixable.
    /// </summary>
    public static IEnumerable<IReadOnlyList<(double X, double Y)>> ToValidRings(
        CvPoint[] contour, double minArea = 1.0)
    {
        if (contour == null || contour.Length < 3)
            yield break;

        if (Math.Abs(OpenCvSharp.Cv2.ContourArea(contour)) < minArea)
            yield break;

        if (OpenCvSharp.Cv2.ArcLength(contour, closed: true) < 1.0)
            yield break;

        Coordinate[] ring = ToClosedRing(contour);
        if (ring.Length < 4)
            yield break;

        Geometry? repaired;
        try
        {
            LinearRing  shell   = _factory.CreateLinearRing(ring);
            NtsPolygon  polygon = _factory.CreatePolygon(shell);
            repaired = GeometryFixer.Fix(polygon);
        }
        catch
        {
            yield break;
        }

        if (repaired == null || repaired.IsEmpty)
            yield break;

        foreach (NtsPolygon poly in ExtractPolygons(repaired))
        {
            var pts = RingToPoints(poly.ExteriorRing);
            if (pts.Count >= 3)
                yield return pts;
        }
    }

    private static IEnumerable<NtsPolygon> ExtractPolygons(Geometry geom)
    {
        if (geom is NtsPolygon p)
        {
            yield return p;
        }
        else if (geom is GeometryCollection gc)
        {
            foreach (Geometry g in gc.Geometries)
                foreach (NtsPolygon sub in ExtractPolygons(g))
                    yield return sub;
        }
    }

    private static IReadOnlyList<(double X, double Y)> RingToPoints(LineString ring)
    {
        int n = ring.NumPoints - 1; // skip duplicate closing coordinate
        var pts = new List<(double, double)>(n);
        for (int i = 0; i < n; i++)
        {
            Coordinate c = ring.GetCoordinateN(i);
            pts.Add((c.X, c.Y));
        }
        return pts;
    }

    private static Coordinate[] ToClosedRing(CvPoint[] contour)
    {
        var coords = new List<Coordinate>(contour.Length + 1);
        foreach (CvPoint p in contour)
        {
            var c = new Coordinate(p.X, p.Y);
            if (coords.Count == 0 || coords[coords.Count - 1].X != c.X || coords[coords.Count - 1].Y != c.Y)
                coords.Add(c);
        }

        while (coords.Count > 1 &&
               coords[coords.Count - 1].X == coords[0].X &&
               coords[coords.Count - 1].Y == coords[0].Y)
            coords.RemoveAt(coords.Count - 1);

        if (coords.Count > 0)
            coords.Add(new Coordinate(coords[0]));

        return coords.ToArray();
    }
}
