# RControls

A WPF custom control library (.NET Framework 4.8) that provides an interactive image viewer with zoom/pan, programmatic shape overlays, and interactive drawing/selection.

## Overview

The library exposes a single top-level control — `ImageViewer` — that hosts an image and an overlay canvas. It is oriented toward machine-vision and inspection applications: coordinates follow the `(row, column)` convention (Y first, then X), stroke thickness auto-compensates for zoom level, and the image is rendered with `NearestNeighbor` scaling for pixel-perfect display.

---

## Namespace

```
RControls
```

---

## Main Control: `ImageViewer`

A WPF `Control` that composes an `ImageCanvas` (overlay layer) and a WPF `Image` inside a clipping `Border`.

### Dependency Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `SourceImage` | `ImageSource` | `null` | The image to display. Setting it for the first time triggers `FitToWindow()` automatically. |
| `OperationMode` | `OpMode` | `Move` | Current interaction mode (see `OpMode` enum). |
| `DrawShape` | `ShapeMode` | `None` | Active shape for Draw mode (see `ShapeMode` enum). |
| `IsSelectable` | `bool` | `true` | Enables/disables Select mode and shape dragging. Setting to `false` while in Select mode reverts to Move mode. |
| `IsDrawable` | `bool` | `true` | Enables/disables Draw mode. Setting to `false` while drawing reverts to Move mode. |
| `MaxImageWidth` | `double` | `6000` | Width of the internal canvas (should match or exceed the image width). |
| `MaxImageHeight` | `double` | `6000` | Height of the internal canvas (should match or exceed the image height). |

### Events

| Event | Signature | Description |
|---|---|---|
| `ImageClicked` | `Action<Point>` | Fires on left mouse-down anywhere on the canvas. `Point` is in canvas/image pixel coordinates. |

### Public Methods

#### Zoom / Pan

| Method | Description |
|---|---|
| `FitToWindow()` | Scales and centers the image to fit the control's visible area. |
| `FitToImage()` | Resets to 1:1 pixel scale (identity transform). |
| `ScaleImage(double ratio)` | Sets an explicit uniform scale (0 < ratio ≤ 16). |
| `HighlightImage(double row, double col)` | Resets to 1:1 scale and centers the viewport on the given image coordinate. |

#### Drawing (programmatic)

All draw methods place a named `ImageItem` on the overlay canvas. Coordinates are in image pixels using `(row, column)` order.

| Method | Description |
|---|---|
| `DrawDefect(row1, col1, row2, col2, name, color)` | Axis-aligned rectangle with a fixed 1 px stroke (not zoom-compensated). Intended for defect annotation boxes. |
| `DrawRectangle(row1, col1, row2, col2, name, color, bFilled)` | Axis-aligned rectangle; stroke compensates for zoom. |
| `DrawRectangle2(row, col, phi, length1, length2, name, color, bFilled)` | Centered rectangle rotated by `phi` degrees, with half-extents `length1` (horizontal) and `length2` (vertical). |
| `DrawLine(rowBegin, colBegin, rowEnd, colEnd, name, color, bFilled)` | Single line segment. |
| `DrawLine(double[] rows1, double[] cols1, double[] rows2, double[] cols2, name, color, bFilled)` | Batch of line segments (arrays must be the same length). |
| `DrawCircle(row, col, radius, name, color, bFilled)` | Circle centered at `(row, col)`. |
| `DrawEllipse(row, col, angle, ra, rb, name, color, bFilled)` | Ellipse with semi-axes `ra` and `rb`, rotated by `angle` degrees. |
| `DrawPolygon(List<Point> pts, name, color, bFilled)` | Closed polygon from a point list (in `(X=col, Y=row)` WPF `Point` coordinates). |
| `DrawAnyShape(List<Point> pts, name, color, bFilled)` | Open or closed freeform path. `bFilled` closes the figure when `true`. |
| `DrawCross(row, col, angle, name, length, color)` | Crosshair / cross marker of given `length`, rotated by `angle` degrees. |
| `DispText(text, pt, size, colSel)` | Places a `TextBlock` at position `pt`. `size` is font size; `colSel` selects color: 1=Red, 2=Green (default), 3=Blue, else Gray. |

#### Region Management

| Method | Description |
|---|---|
| `RemoveRegion(string name, ShapeMode shape)` | Removes items matching both `name` and `shape`. Pass `null`/empty name to match all names; pass `ShapeMode.None` to match all types. Passing both empty removes all items. |
| `GetRegions(string name, ShapeMode shape)` | Returns `List<ImageItem>` matching the filter (same wildcard rules as `RemoveRegion`). |

---

## Enums

### `OpMode`

| Value | Description |
|---|---|
| `Disable` | View-only; mouse events are ignored. |
| `Move` | Left-drag pans the image. Mouse wheel zooms. |
| `Select` | Left-drag draws a rubberband selection rectangle; selected items can be moved, resized, rotated, or deleted. |
| `Draw` | Left-click sets the anchor; drag previews the shape; right-click commits. After commit the mode reverts to `Select`. |

### `ShapeMode`

| Value | Description |
|---|---|
| `None` | No shape active. |
| `Rectangle` | Axis-aligned rectangle. |
| `Line` | Straight line segment. |
| `Circle` | Circle (uniform radius from anchor). |
| `Ellipse` | Ellipse. |
| `Polygon` | Closed polygon (click to add vertices). |
| `Any` | Freeform open/closed path. |
| `Cross` | Two-line crosshair marker. |

---

## Interaction

### Zoom & Pan (mouse)

- **Scroll wheel** — zooms in/out centered on the cursor position. Scale range: ~0.01× to 32×. Stroke thickness of all overlay shapes is updated in real time to remain visually constant.
- **Left-drag** in `Move` mode — pans the canvas.

### Draw mode (user interaction)

1. Right-click → Draw → choose a shape type.
2. Left-click on the canvas sets the start point; drag to preview (rubberband).
3. Right-click commits the shape (minimum 5-pixel drag required).
4. The mode switches back to `Select` automatically after commit.

### Select mode

- Left-drag draws a rubberband rectangle; any item fully inside is selected.
- **Shift** or **Ctrl** + click on an item toggles its individual selection.
- **Delete** key removes all currently selected items.

### Selected item handles

When an item is selected a `ResizeRotateAdorner` appears with:

- **8 resize handles** (N, S, E, W, NE, NW, SE, SW) — drag to resize. Respects the item's current rotation. Circle constrains width = height. Polygon and freeform (`Any`) shapes are **not resizable**.
- **Rotate handle** (right side, arrow) — drag to rotate. Circle, Polygon, and Any shapes are **not rotatable**.
- **Move** — drag anywhere on the item (cursor `SizeAll`).
- **Size overlay** — while resizing, red dimension labels show current width/height in image pixels.

---

## Right-Click Context Menu

The built-in context menu (defined in `Themes/Generic.xaml`) exposes:

| Menu item | Action |
|---|---|
| View | `OpMode.Disable` |
| Move | `OpMode.Move` |
| Select | `OpMode.Select` (disabled when `IsSelectable = false`) |
| Draw → Rectangle / Line / Circle / Ellipse / Polygon / Freeform | Sets Draw mode + shape (disabled when `IsDrawable = false`) |
| Fit Image | 1:1 pixel scale |
| Fit Window | Scale to fit control |
| Save Image | Saves `SourceImage` as BMP or TIFF (file dialog) |
| Save Window | Snapshots the entire control as PNG (file dialog) |

---

## Routed Commands (`RControls.Interactivity.ControlCommands`)

These can be bound from outside (e.g. toolbar buttons) to the `ImageViewer`:

| Command | Effect |
|---|---|
| `ControlCommands.ViewMode` | Switch to Disable mode |
| `ControlCommands.MoveMode` | Switch to Move mode |
| `ControlCommands.SelectMode` | Switch to Select mode |
| `ControlCommands.DrawMode` | Switch to Draw mode |
| `ControlCommands.DrawRectangle` | Draw mode + Rectangle shape |
| `ControlCommands.DrawLine` | Draw mode + Line shape |
| `ControlCommands.DrawCircle` | Draw mode + Circle shape |
| `ControlCommands.DrawEllipse` | Draw mode + Ellipse shape |
| `ControlCommands.DrawPolygon` | Draw mode + Polygon shape |
| `ControlCommands.DrawAny` | Draw mode + Freeform shape |
| `ControlCommands.FitWindow` | Fit to window |
| `ControlCommands.FitImage` | Fit to 1:1 |
| `ControlCommands.SaveImage` | Save source image |
| `ControlCommands.SaveWindow` | Save control snapshot |

---

## `ImageItem`

The `ContentControl` that wraps each overlay shape on the canvas.

| Member | Description |
|---|---|
| `ItemName` | String tag set when the item is drawn. Used for `RemoveRegion`/`GetRegions` lookup. |
| `ItemType` | `ShapeMode` value identifying the shape kind. |
| `Ratio` | Scale ratio recorded at draw time (used internally by the adorner system). |
| `IsSelected` (DP) | `true` when the item is selected. Toggling this shows/hides the resize-rotate adorner. |
| `MoveThumbTemplate` (attached DP) | Allows shapes to specify a custom move-thumb template. |

---

## Value Converters

| Converter | Location | Description |
|---|---|---|
| `OperModeBoolConverter` | `RControls.Converts` | Converts `OpMode` ↔ `bool` given an `OpMode` string parameter. Used to drive menu `IsChecked`. |
| `ShapeModeBoolConverter` | `RControls.Converts` | Converts `ShapeMode` ↔ `bool`. Used to drive draw-submenu `IsChecked`. |
| `DoubleFormatConverter` | `RControls.Adorners` | Rounds a `double` to an integer. Used to display W/H labels without decimals. |

---

## Internal Architecture

```
ImageViewer (Control)
│  SourceImage, OperationMode, DrawShape, IsSelectable, IsDrawable
│  Public draw API delegates to ImageCanvas
│
└─ Border (PART_Border)  — clips content; hosts context menu
   └─ ImageCanvas (Canvas, PART_MainCanvas)
      │  Mouse pan/zoom transform via MatrixTransform
      │  RubberbandAdorner — drawn during select/draw drag
      │
      ├─ Image (PART_MainImage)  — NearestNeighbor, Stretch=None
      └─ ImageItem* (ContentControl, N items)
         │  ItemName, ItemType, IsSelected
         │
         ├─ MoveThumb           — transparent overlay; drag to move
         ├─ ContentPresenter    — the actual WPF Shape
         └─ ImageItemDecorator  — shows/hides ResizeRotateAdorner
            └─ ResizeRotateAdorner
               └─ ResizeRotateChrome
                  ├─ ResizeThumb ×8  (edges + corners)
                  └─ RotateThumb ×1  (right side)
```

---

## Usage Example (XAML + C#)

```xml
<rc:ImageViewer x:Name="viewer"
                SourceImage="{Binding MyImage}"
                OperationMode="Move"
                MaxImageWidth="4096"
                MaxImageHeight="3000"
                Background="Black" />
```

```csharp
// Load an image
viewer.SourceImage = new BitmapImage(new Uri("image.bmp", UriKind.Relative));

// Pan/zoom helpers
viewer.FitToWindow();
viewer.HighlightImage(row: 512, col: 256);

// Programmatic overlays
viewer.DrawRectangle(100, 200, 300, 500, "roi1", Brushes.Lime, false);
viewer.DrawCircle(400, 400, 50, "center", Brushes.Red, false);
viewer.DrawCross(400, 400, 0, "mark", 30, Brushes.Yellow);
viewer.DrawLine(0, 0, 100, 200, "edge", Brushes.Cyan, true);
viewer.DispText("OK", new Point(50, 50), size: 40, colSel: 2);

// Batch lines
viewer.DrawLine(
    new[] { 10.0, 20.0 }, new[] { 10.0, 20.0 },
    new[] { 100.0, 200.0 }, new[] { 100.0, 200.0 },
    "lines", Brushes.White, true);

// Query / remove
var rects = viewer.GetRegions("roi1", ShapeMode.Rectangle);
viewer.RemoveRegion("roi1", ShapeMode.None);   // remove all named "roi1"
viewer.RemoveRegion(null, ShapeMode.Circle);   // remove all circles
viewer.RemoveRegion(null, ShapeMode.None);     // clear everything

// React to user clicks
viewer.ImageClicked += pt => Console.WriteLine($"Clicked at col={pt.X} row={pt.Y}");
```

---

## Notes & Caveats

- **Coordinate order**: all `Draw*` methods take `row` (Y) before `column` (X), matching the HALCON / machine-vision convention. The `ImageClicked` event returns a standard WPF `Point` where `.X` = column and `.Y` = row.
- **Stroke thickness**: programmatic shapes use `2.0 / scaleRatio`; interactively drawn shapes use `1.0 / scaleRatio`. Both stay visually constant as the user zooms.
- **`ImageCanvas0.cs`** is a partial class of `ImageCanvas` containing an updated implementation of `DrawLine` (batch overload) and a slightly different `GetBounds` helper — the active (non-partial) file used at runtime is determined by the compiler combining both partial declarations.
- **Polygon / Any shapes** cannot be resized or rotated via the adorner handles; only moved.
- **Circle** shape maintains a 1:1 aspect ratio during resize.
- **`bFilled` parameter** in the draw methods is accepted but the fill is not currently applied to the shapes (stroke-only rendering).
- The library has no external NuGet dependencies — only standard WPF assemblies (`PresentationCore`, `PresentationFramework`, `WindowsBase`).
