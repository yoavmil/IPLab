using IPLab.Core.Models;
using IPLab.Core.Spatial;
using OpenCvSharp;
using System.Windows.Media.Imaging;

namespace IPLab.UI.ViewModels;

public sealed record InspectorState(
    BitmapSource?          Image    = null,
    CircleSegment[]?       Circles  = null,
    KeyPoint[]?            Blobs    = null,
    Point2f[][]?           Contours = null,
    RoiDef?                Roi      = null,
    LineSegment2f[]?       Lines      = null,
    Point2f[]?             Crosses    = null,
    Rect[]?                Rectangles = null);
