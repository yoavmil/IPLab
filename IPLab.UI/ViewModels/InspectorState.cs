using IPLab.Core.Models;
using OpenCvSharp;
using System.Windows.Media.Imaging;

namespace IPLab.UI.ViewModels;

public sealed record InspectorState(
    BitmapSource?          Image    = null,
    CircleSegment[]?       Circles  = null,
    KeyPoint[]?            Blobs    = null,
    OpenCvSharp.Point[][]? Contours = null,
    RoiDef?                Roi      = null,
    LineSegmentPoint[]?    Lines    = null,
    Point2f[]?             Crosses  = null,
    Rect[]?                Rectangles = null);
