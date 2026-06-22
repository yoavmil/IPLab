using IPLab.Core.Models;
using IPLab.Core.Spatial;
using OpenCvSharp;
using System.Collections;
using System.Reflection;

namespace IPLab.UI.ViewModels;

internal static class DataNodeBuilder
{
    private const int BreadthCap = 500;
    private const int DepthCap   = 6;

    internal static DataNodeViewModel Build(string name, object? value, bool isImageMat = false, int depth = 0)
    {
        if (value is null)
            return Leaf(name, "(null)", "null");

        // Mat — special-cased before reflection to avoid touching native pixel data.
        if (value is Mat mat)
            return BuildMat(name, mat, isImageMat, depth);

        // Primitives and simple convertible types → leaf.
        if (IsSimple(value))
            return Leaf(name, FormatSimple(value), ShortTypeName(value.GetType()));

        // Known OpenCV / IPLab structs with friendly one-liners.
        if (TryFormatKnownStruct(value, out var friendly, out var typeName))
            return LazyNode(name, friendly!, typeName!, () => ReflectChildren(value, depth + 1));

        // String[] and other plain string arrays handled as IEnumerable below.
        // IEnumerable (excluding string).
        if (value is not string && value is IEnumerable enumerable)
            return BuildEnumerable(name, enumerable, value.GetType(), depth);

        // Fallback: reflect public properties and fields.
        return LazyNode(name, ShortTypeName(value.GetType()), ShortTypeName(value.GetType()),
            () => ReflectChildren(value, depth + 1));
    }

    // ── Mat ──────────────────────────────────────────────────────────────────

    private static DataNodeViewModel BuildMat(string name, Mat mat, bool isImageMat, int depth)
    {
        var summary = $"{mat.Width}×{mat.Height}, {mat.Channels()}ch, {DepthName(mat.Depth())}";

        // Image Mats and large/multi-channel data Mats → leaf summary only.
        if (isImageMat || mat.Rows > 256 || mat.Cols > 32)
            return Leaf(name, summary, "Mat");

        // Small data Mats (Stats, Centroids, Matches) → expandable table.
        var tableLabel = $"{mat.Rows} rows × {mat.Cols} cols, {DepthName(mat.Depth())}";
        return LazyNode(name, tableLabel, "Mat", () => BuildMatRows(mat));
    }

    private static IEnumerable<DataNodeViewModel> BuildMatRows(Mat mat)
    {
        using var f64 = new Mat();
        mat.ConvertTo(f64, MatType.CV_64F);

        int count = Math.Min(mat.Rows, BreadthCap);
        for (int r = 0; r < count; r++)
        {
            var cells = new double[mat.Cols];
            for (int c = 0; c < mat.Cols; c++)
                cells[c] = f64.At<double>(r, c);
            yield return Leaf($"[{r}]", string.Join(", ", cells.Select(v => v.ToString("G6"))), "row");
        }
        if (mat.Rows > BreadthCap)
            yield return Leaf($"…", $"{mat.Rows - BreadthCap} more rows", "");
    }

    private static string DepthName(int depth) => depth switch
    {
        0 => "8U", 1 => "8S", 2 => "16U", 3 => "16S",
        4 => "32S", 5 => "32F", 6 => "64F", _ => $"D{depth}"
    };

    // ── IEnumerable ──────────────────────────────────────────────────────────

    private static DataNodeViewModel BuildEnumerable(string name, IEnumerable enumerable, Type type, int depth)
    {
        // Materialise once to get the count cheaply — results here are already in memory.
        var items = enumerable.Cast<object?>().ToList();
        var label = $"{items.Count} items";

        return LazyNode(name, label, ShortTypeName(type),
            () => BuildEnumerableChildren(items, depth + 1));
    }

    private static IEnumerable<DataNodeViewModel> BuildEnumerableChildren(List<object?> items, int depth)
    {
        int count = Math.Min(items.Count, BreadthCap);
        for (int i = 0; i < count; i++)
            yield return Build($"[{i}]", items[i], depth: depth);
        if (items.Count > BreadthCap)
            yield return Leaf("…", $"{items.Count - BreadthCap} more items", "");
    }

    // ── Reflection fallback ───────────────────────────────────────────────────

    private static IEnumerable<DataNodeViewModel> ReflectChildren(object obj, int depth)
    {
        if (depth > DepthCap) yield break;

        var props  = obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0);
        var fields = obj.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance);

        int count = 0;
        foreach (var prop in props)
        {
            if (count++ >= BreadthCap) { yield return Leaf("…", "more members", ""); yield break; }
            object? val = null;
            try { val = prop.GetValue(obj); } catch { continue; }
            yield return Build(prop.Name, val, depth: depth);
        }
        foreach (var field in fields)
        {
            if (count++ >= BreadthCap) { yield return Leaf("…", "more members", ""); yield break; }
            object? val = null;
            try { val = field.GetValue(obj); } catch { continue; }
            yield return Build(field.Name, val, depth: depth);
        }
    }

    // ── Known struct formatting ───────────────────────────────────────────────

    private static bool TryFormatKnownStruct(object value, out string? text, out string? type)
    {
        switch (value)
        {
            case Point2f  p: text = $"({p.X:G6}, {p.Y:G6})";                    type = "Point2f";        return true;
            case Point    p: text = $"({p.X}, {p.Y})";                           type = "Point";          return true;
            case Point3f  p: text = $"({p.X:G6}, {p.Y:G6}, {p.Z:G6})";         type = "Point3f";        return true;
            case Size     s: text = $"{s.Width}×{s.Height}";                     type = "Size";           return true;
            case Size2f   s: text = $"{s.Width:G6}×{s.Height:G6}";              type = "Size2f";         return true;
            case Rect     r: text = $"({r.X}, {r.Y}) {r.Width}×{r.Height}";     type = "Rect";           return true;
            case Rect2f   r: text = $"({r.X:G6}, {r.Y:G6}) {r.Width:G6}×{r.Height:G6}"; type = "Rect2f"; return true;
            case CircleSegment c: text = $"c=({c.Center.X:G6}, {c.Center.Y:G6}) r={c.Radius:G6}"; type = "CircleSegment"; return true;
            case KeyPoint k: text = $"({k.Pt.X:G6}, {k.Pt.Y:G6}) r={k.Size / 2:G6}"; type = "KeyPoint"; return true;
            case LineSegment2f l: text = $"({l.P1.X:G6},{l.P1.Y:G6})→({l.P2.X:G6},{l.P2.Y:G6})"; type = "LineSegment2f"; return true;
            case LineSegmentPoint l: text = $"({l.P1.X},{l.P1.Y})→({l.P2.X},{l.P2.Y})"; type = "LineSegmentPoint"; return true;
            case RoiDef   r: text = $"cx={r.CX:G6} cy={r.CY:G6} w={r.Width:G6} h={r.Height:G6} a={r.Angle:G4}°"; type = "RoiDef"; return true;
            case Scalar   s: text = $"({s.Val0:G4}, {s.Val1:G4}, {s.Val2:G4}, {s.Val3:G4})"; type = "Scalar"; return true;
        }
        text = null; type = null; return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsSimple(object value) =>
        value is bool or byte or sbyte or short or ushort or int or uint
              or long or ulong or float or double or decimal or char or string
              or Enum;

    private static string FormatSimple(object value) => value switch
    {
        float  f => f.ToString("G6"),
        double d => d.ToString("G10"),
        _        => value.ToString() ?? "(null)"
    };

    private static string ShortTypeName(Type t)
    {
        if (t.IsArray)
            return ShortTypeName(t.GetElementType()!) + "[]";
        return t.Name;
    }

    private static DataNodeViewModel Leaf(string name, string value, string type) =>
        new(name, value, type);

    private static DataNodeViewModel LazyNode(string name, string value, string type,
        Func<IEnumerable<DataNodeViewModel>> factory) =>
        new(name, value, type, factory);
}
