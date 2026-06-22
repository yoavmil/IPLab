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

    internal static DataNodeViewModel Build(
        string name, object? value,
        bool isImageMat = false, int depth = 0,
        string path = "", ISet<string>? expandedPaths = null)
    {
        DataNodeViewModel node;

        if (value is null)
        {
            node = Leaf(name, "(null)", "null", path, expandedPaths);
        }
        else if (value is Mat mat)
        {
            node = BuildMat(name, mat, isImageMat, depth, path, expandedPaths);
        }
        else if (IsSimple(value))
        {
            node = Leaf(name, FormatSimple(value), ShortTypeName(value.GetType()), path, expandedPaths);
        }
        else if (TryFormatKnownStruct(value, out var friendly, out var typeName))
        {
            node = LazyNode(name, friendly!, typeName!, path, expandedPaths,
                () => ReflectChildren(value, depth + 1, path, expandedPaths));
        }
        else if (value is not string && value is IEnumerable enumerable)
        {
            node = BuildEnumerable(name, enumerable, value.GetType(), depth, path, expandedPaths);
        }
        else
        {
            node = LazyNode(name, ShortTypeName(value.GetType()), ShortTypeName(value.GetType()),
                path, expandedPaths,
                () => ReflectChildren(value, depth + 1, path, expandedPaths));
        }

        return node;
    }

    // ── Mat ──────────────────────────────────────────────────────────────────

    private static DataNodeViewModel BuildMat(
        string name, Mat mat, bool isImageMat, int depth,
        string path, ISet<string>? expandedPaths)
    {
        var summary = $"{mat.Width}×{mat.Height}, {mat.Channels()}ch, {DepthName(mat.Depth())}";

        if (isImageMat || mat.Rows > 256 || mat.Cols > 32)
            return Leaf(name, summary, "Mat", path, expandedPaths);

        var tableLabel = $"{mat.Rows} rows × {mat.Cols} cols, {DepthName(mat.Depth())}";
        return LazyNode(name, tableLabel, "Mat", path, expandedPaths,
            () => BuildMatRows(mat, path, expandedPaths));
    }

    private static IEnumerable<DataNodeViewModel> BuildMatRows(
        Mat mat, string parentPath, ISet<string>? expandedPaths)
    {
        using var f64 = new Mat();
        mat.ConvertTo(f64, MatType.CV_64F);

        int count = Math.Min(mat.Rows, BreadthCap);
        for (int r = 0; r < count; r++)
        {
            var cells = new double[mat.Cols];
            for (int c = 0; c < mat.Cols; c++)
                cells[c] = f64.At<double>(r, c);
            yield return Leaf($"[{r}]", string.Join(", ", cells.Select(v => v.ToString("G6"))),
                "row", $"{parentPath}/[{r}]", expandedPaths);
        }
        if (mat.Rows > BreadthCap)
            yield return Leaf("…", $"{mat.Rows - BreadthCap} more rows", "", $"{parentPath}/…", expandedPaths);
    }

    private static string DepthName(int depth) => depth switch
    {
        0 => "8U", 1 => "8S", 2 => "16U", 3 => "16S",
        4 => "32S", 5 => "32F", 6 => "64F", _ => $"D{depth}"
    };

    // ── IEnumerable ──────────────────────────────────────────────────────────

    private static DataNodeViewModel BuildEnumerable(
        string name, IEnumerable enumerable, Type type, int depth,
        string path, ISet<string>? expandedPaths)
    {
        var items = enumerable.Cast<object?>().ToList();
        return LazyNode(name, $"{items.Count} items", ShortTypeName(type), path, expandedPaths,
            () => BuildEnumerableChildren(items, depth + 1, path, expandedPaths));
    }

    private static IEnumerable<DataNodeViewModel> BuildEnumerableChildren(
        List<object?> items, int depth, string parentPath, ISet<string>? expandedPaths)
    {
        int count = Math.Min(items.Count, BreadthCap);
        for (int i = 0; i < count; i++)
            yield return Build($"[{i}]", items[i], depth: depth,
                path: $"{parentPath}/[{i}]", expandedPaths: expandedPaths);
        if (items.Count > BreadthCap)
            yield return Leaf("…", $"{items.Count - BreadthCap} more items", "",
                $"{parentPath}/…", expandedPaths);
    }

    // ── Reflection fallback ───────────────────────────────────────────────────

    private static IEnumerable<DataNodeViewModel> ReflectChildren(
        object obj, int depth, string parentPath, ISet<string>? expandedPaths)
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
            if (count++ >= BreadthCap) { yield return Leaf("…", "more members", "", $"{parentPath}/…", expandedPaths); yield break; }
            object? val = null;
            try { val = prop.GetValue(obj); } catch { continue; }
            yield return Build(prop.Name, val, depth: depth,
                path: $"{parentPath}/{prop.Name}", expandedPaths: expandedPaths);
        }
        foreach (var field in fields)
        {
            if (count++ >= BreadthCap) { yield return Leaf("…", "more members", "", $"{parentPath}/…", expandedPaths); yield break; }
            object? val = null;
            try { val = field.GetValue(obj); } catch { continue; }
            yield return Build(field.Name, val, depth: depth,
                path: $"{parentPath}/{field.Name}", expandedPaths: expandedPaths);
        }
    }

    // ── Known struct formatting ───────────────────────────────────────────────

    private static bool TryFormatKnownStruct(object value, out string? text, out string? type)
    {
        switch (value)
        {
            case Point2f  p: text = $"({p.X:G6}, {p.Y:G6})";                                             type = "Point2f";         return true;
            case Point    p: text = $"({p.X}, {p.Y})";                                                    type = "Point";           return true;
            case Point3f  p: text = $"({p.X:G6}, {p.Y:G6}, {p.Z:G6})";                                  type = "Point3f";         return true;
            case Size     s: text = $"{s.Width}×{s.Height}";                                              type = "Size";            return true;
            case Size2f   s: text = $"{s.Width:G6}×{s.Height:G6}";                                       type = "Size2f";          return true;
            case Rect     r: text = $"({r.X}, {r.Y}) {r.Width}×{r.Height}";                              type = "Rect";            return true;
            case Rect2f   r: text = $"({r.X:G6}, {r.Y:G6}) {r.Width:G6}×{r.Height:G6}";                type = "Rect2f";          return true;
            case CircleSegment c: text = $"c=({c.Center.X:G6}, {c.Center.Y:G6}) r={c.Radius:G6}";       type = "CircleSegment";   return true;
            case KeyPoint k: text = $"({k.Pt.X:G6}, {k.Pt.Y:G6}) r={k.Size / 2:G6}";                   type = "KeyPoint";        return true;
            case LineSegment2f   l: text = $"({l.P1.X:G6},{l.P1.Y:G6})→({l.P2.X:G6},{l.P2.Y:G6})";    type = "LineSegment2f";   return true;
            case LineSegmentPoint l: text = $"({l.P1.X},{l.P1.Y})→({l.P2.X},{l.P2.Y})";                type = "LineSegmentPoint"; return true;
            case RoiDef   r: text = $"cx={r.CX:G6} cy={r.CY:G6} w={r.Width:G6} h={r.Height:G6} a={r.Angle:G4}°"; type = "RoiDef"; return true;
            case Scalar   s: text = $"({s.Val0:G4}, {s.Val1:G4}, {s.Val2:G4}, {s.Val3:G4})";            type = "Scalar";          return true;
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

    private static DataNodeViewModel Leaf(string name, string value, string type,
        string path = "", ISet<string>? expandedPaths = null) =>
        new(name, value, type, path: path, expandedPaths: expandedPaths);

    private static DataNodeViewModel LazyNode(string name, string value, string type,
        string path, ISet<string>? expandedPaths,
        Func<IEnumerable<DataNodeViewModel>> factory) =>
        new(name, value, type, factory, path, expandedPaths);
}
