# IPLab Operators

<!-- STRUCTURE: This file has two parts separated by ---
     1. TOC at the top — one bullet per operator, no details.
     2. Detailed sections below the --- divider — one ## heading per operator.
     New operators: add a bullet to the TOC AND a ## section below. Never put detail content in the TOC block. -->

## I/O
- [LoadImage](#loadimage) — load a color image from disk
- [SaveImage](#saveimage) — save an image to disk

## Color & Channels
- [ConvertToGrayscale](#converttograyscale) — convert BGR image to single-channel grayscale
- [SplitChannels](#splitchannels) — split BGR image into separate R, G, B channel images
- [InvertImage](#invertimage) — invert all pixel values (bitwise NOT)

## Filters
- [Threshold](#threshold) — apply binary threshold to a single-channel image (fixed, Otsu, Triangle, or Adaptive)
- [HistogramEqualization](#histogramequalization) — equalize pixel intensity distribution (EqualizeHist or CLAHE)
- [GaussianBlur](#gaussianblur) — smooth an image with a Gaussian kernel
- [Morphology](#morphology) — morphological operations (erode, dilate, open, close, gradient, top-hat, black-hat)
- [Thinning](#thinning) — skeletonize a binary image via iterative thinning (Zhang-Suen or Guo-Hall)
- [Bitwise](#bitwise) — pixel-wise AND, OR, or XOR of two images

## Visualization
- [DrawHistogram](#drawhistogram) — render a single-channel image's intensity histogram as an image

## Detection
- [DetectCircles](#detectcircles) — detect circles using Hough Gradient transform
- [DetectSimpleBlobs](#detectsimpleblobs) — detect circular blobs using SimpleBlobDetector
- [ConnectedComponents](#connectedcomponents) — label connected regions; outputs Count, Stats (Mat), Centroids (Mat), LabelImage
- [FindContours](#findcontours) — find contours in a binary image with built-in filter/repair
- [FindStripeEdges](#findstripeedges) — find N strongest 1D edges along a rotated stripe (caliper-style)
- [DetectSegment](#findedgeline) — fit a sub-pixel line to a single prominent edge within a rotated ROI
- [TemplateMatch](#templatematch) — find every occurrence of a selected visual pattern

## Flow
- [LoopStart](#loopstart) — control how a flat loop body executes (Discrete / Serial / Parallel)
- [LoopEnd](#loopend) — collect up to four loop body values

## Scripting
- [CSharpScript](#csharpscipt) — run a user-written C# snippet loaded from a `.cs` file

---

## LoadImage

Loads a color image from disk. Supports a list of images; the active one is determined by `ActiveIndex`. The inspector shows a horizontal thumbnail strip for all listed images — clicking a thumbnail switches the active image and re-runs the flow. Thumbnails are decoded at 120 px wide to limit memory use.

| Parameter   | Type       | Connectable | Description                                    |
|-------------|------------|-------------|------------------------------------------------|
| FilePaths   | StringList | No          | Ordered list of image file paths               |
| ActiveIndex | Int        | No          | Zero-based index of the currently active image |

| Output Port | Type   |
|-------------|--------|
| Image       | Mat    |
| FilePath    | string |

---

## SaveImage

Saves an image to disk. No output port — side effect only.

| Parameter | Type   | Connectable | Description           |
|-----------|--------|-------------|-----------------------|
| Image     | Object | Yes         | Image Mat to save     |
| FilePath  | String | No          | Destination file path |

---

## ConvertToGrayscale

Converts a BGR color image to a single-channel grayscale image.

| Parameter | Type   | Connectable | Description                                      |
|-----------|--------|-------------|--------------------------------------------------|
| Image     | Object | Yes         | Input BGR Mat                                    |
| Method    | Enum   | No          | `Luminance` (default) — weighted BGR2GRAY (0.299R + 0.587G + 0.114B); `HsvValue` — max(R,G,B), equal brightness for all pure hues |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## SplitChannels

Splits a BGR color image into three separate single-channel images.

| Parameter | Type   | Connectable | Description   |
|-----------|--------|-------------|---------------|
| Image     | Object | Yes         | Input BGR Mat |

| Output Port | Type | Description            |
|-------------|------|------------------------|
| Red         | Mat  | Red channel (BGR[2])   |
| Green       | Mat  | Green channel (BGR[1]) |
| Blue        | Mat  | Blue channel (BGR[0])  |

---

## InvertImage

Supports [ROI](#roi). Inverts all pixel values in the image using a bitwise NOT (`Cv2.BitwiseNot`). Works on any channel count (grayscale or color). Useful as a pre-processing step when blobs are dark on a bright background and the downstream detector expects light on dark.

| Parameter | Type   | Connectable | Description   |
|-----------|--------|-------------|---------------|
| Image     | Object | Yes         | Input Mat     |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## Threshold

Applies a threshold to a single-channel image. Supports [ROI](#roi).

| Parameter       | Type   | Connectable | Description                                                                                                                                      |
|-----------------|--------|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| Image           | Object | Yes         | Single-channel input Mat                                                                                                                         |
| Method          | Enum   | No          | `Fixed` (default) — use `Thresh` value; `Otsu` — auto threshold (bimodal histograms); `Triangle` — triangle algorithm (unimodal histograms); `Adaptive` — per-pixel local threshold (`Cv2.AdaptiveThreshold`) |
| Output Type     | Enum   | No          | `Binary` (default), `BinaryInv`, `Trunc`, `ToZero`, `ToZeroInv`. When Method is `Adaptive` only `Binary` and `BinaryInv` apply.                 |
| Thresh          | Double | No          | Threshold value (default 128); used only when Method is `Fixed`                                                                                  |
| Adaptive Method | Enum   | No          | `MeanC` (default) — local mean; `GaussianC` — Gaussian-weighted mean. Used only when Method is `Adaptive`.                                      |
| Block Size      | Int    | No          | Neighborhood size for local threshold computation (default 11, must be odd ≥ 3). Used only when Method is `Adaptive`.                           |
| C               | Double | No          | Constant subtracted from the local mean (default 2). Positive values raise the bar; negative lower it. Used only when Method is `Adaptive`.      |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## HistogramEqualization

Redistributes pixel intensity values to improve contrast before thresholding. Operates on 8-bit single-channel images.

- **Equalize** (`Cv2.EqualizeHist`) — global equalization; stretches the full histogram to cover 0–255. Best when illumination is globally poor.
- **CLAHE** (`Cv2.CreateCLAHE`) — Contrast Limited Adaptive Histogram Equalization; equalizes small local tiles independently, then clips the redistribution at `ClipLimit` to avoid amplifying noise. Better for images with uneven illumination across the frame.

| Parameter     | Type   | Connectable | Description                                                                                           |
|---------------|--------|-------------|-------------------------------------------------------------------------------------------------------|
| Image         | Object | Yes         | 8-bit single-channel input Mat                                                                        |
| Method        | Enum   | No          | `Equalize` (default) — global equalization; `CLAHE` — tile-based adaptive equalization               |
| Clip Limit    | Double | No          | CLAHE only. Maximum slope of the histogram redistribution (default 2.0). Higher = more contrast, more noise amplification. |
| Tile Grid Size | Int   | No          | CLAHE only. Image is divided into N×N tiles for local equalization (default 8).                      |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## GaussianBlur

Smooths an image using a Gaussian kernel (`Cv2.GaussianBlur`). Works on any channel count. Use before thresholding to reduce noise and suppress spurious regions that cause extra skeleton branches during thinning.

Supports [ROI](#roi).

| Parameter   | Type   | Connectable | Description                                                                                     |
|-------------|--------|-------------|-------------------------------------------------------------------------------------------------|
| Image       | Object | Yes         | Input Mat (any channel count)                                                                   |
| Kernel Size | Int    | No          | Side length of the Gaussian kernel in pixels (default 5, always rounded up to nearest odd)      |
| Sigma       | Double | No          | Standard deviation in X and Y (default 0 — auto-computed from kernel size)                      |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## Morphology

Applies a morphological operation to an image using `Cv2.MorphologyEx`. Works on any single- or multi-channel image. Common uses: erode/dilate to shrink or expand bright regions; open to remove small bright specks; close to fill small dark holes; gradient for edge outlines.

Supports [ROI](#roi).

| Parameter    | Type   | Connectable | Description                                                                                        |
|--------------|--------|-------------|----------------------------------------------------------------------------------------------------|
| Image        | Object | Yes         | Input Mat (any channel count)                                                                      |
| Operation    | Enum   | No          | `Erode` (default), `Dilate`, `Open`, `Close`, `Gradient`, `TopHat`, `BlackHat`                    |
| Kernel Shape | Enum   | No          | `Rect` (default) — filled rectangle; `Ellipse` — filled ellipse; `Cross` — plus-sign shape        |
| Kernel Size  | Int    | No          | Side length of the structuring element in pixels (default 3, must be odd for symmetric anchor)     |
| Iterations   | Int    | No          | Number of times the operation is applied (default 1); each pass adds one more ring of erosion/dilation |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## Thinning

Skeletonizes a binary single-channel image using iterative thinning (`CvXImgProc.Thinning`). Reduces foreground blobs to single-pixel-wide skeletons while preserving connectivity. Input must be an 8-bit single-channel image with pixel values 0 or 255.

Supports [ROI](#roi).

| Parameter    | Type   | Connectable | Description                                                                                   |
|--------------|--------|-------------|-----------------------------------------------------------------------------------------------|
| Image        | Object | Yes         | Binary single-channel input Mat (8-bit, values 0/255)                                         |
| ThinningType | Enum   | No          | `ZhangSuen` (default) — Zhang-Suen algorithm; `GuoHall` — Guo-Hall algorithm (slightly faster) |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## Bitwise

Applies a pixel-wise bitwise operation to two images (`Cv2.BitwiseAnd`, `Cv2.BitwiseOr`, `Cv2.BitwiseXor`). Both inputs must have the same dimensions, depth, and channel count. Supports integer depths (`CV_8U`, `CV_16U`, `CV_32S`); float images are not supported by OpenCV bitwise functions. Supports grayscale and color inputs — for color images the operation is applied per channel and the result is merged back into a color image.

| Parameter | Type   | Connectable | Description                  |
|-----------|--------|-------------|------------------------------|
| Image A   | Object | Yes         | First input Mat              |
| Image B   | Object | Yes         | Second input Mat             |
| Operation | Enum   | No          | `And` (default), `Or`, `Xor` |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## DrawHistogram

Computes the intensity histogram of a single-channel image using `Cv2.CalcHist` and renders it as a BGR image. Each of the 256 bins is drawn as a filled vertical bar scaled to the output height. Useful for visually checking the intensity distribution before or after `HistogramEqualization` or `Threshold`.

| Parameter | Type   | Connectable | Description                                                             |
|-----------|--------|-------------|-------------------------------------------------------------------------|
| Image     | Object | Yes         | Single-channel 8-bit input Mat                                          |
| Height    | Int    | No          | Output image height in pixels (default 200)                             |
| Width     | Int    | No          | Output image width in pixels — bins are scaled to fill it (default 512) |
| Color     | Enum   | No          | Bar color: `Green` (default), `White`, `Cyan`, `Red`, `Yellow`         |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## DetectCircles

Detects circles in a single-channel image using the Hough Gradient transform (`Cv2.HoughCircles`).

Supports [ROI](#roi). When an ROI is set, detection runs only within that region; returned circle coordinates are automatically translated to full-image space.

| Parameter | Type   | Connectable | Description                                        |
|-----------|--------|-------------|----------------------------------------------------|
| Image     | Object | Yes         | Single-channel input Mat                           |
| MinDist   | Double | No          | Minimum distance between detected circle centers   |
| Param1    | Double | No          | Canny edge detector upper threshold                |
| Param2    | Double | No          | Accumulator threshold — lower values detect more circles |
| MinRadius | Int    | No          | Minimum circle radius in pixels                    |
| MaxRadius | Int    | No          | Maximum circle radius in pixels                    |

| Output Port | Type            |
|-------------|-----------------|
| Circles     | CircleSegment[] |

---

## DetectSimpleBlobs

Detects circular blobs in a single-channel image using `SimpleBlobDetector`.

Supports [ROI](#roi). When an ROI is set, detection runs only within that region; returned blob coordinates are automatically translated to full-image space.

| Parameter           | Type   | Connectable | Description                                                       |
|---------------------|--------|-------------|-------------------------------------------------------------------|
| Image               | Object | Yes         | Single-channel input Mat                                          |
| Polarity            | Enum   | No          | `Light on Dark` (default) or `Dark on Light`                      |
| MinCircularity      | Double | No          | Minimum circularity score (0–1); higher = rounder                 |
| MinArea             | Double | No          | Minimum blob area in pixels²                                      |
| MaxArea             | Double | No          | Maximum blob area in pixels²                                      |
| MinDistBetweenBlobs | Double | No          | Minimum distance between blob centers in pixels                   |
| MinThreshold        | Double | No          | Lower bound of the internal threshold sweep                       |
| MaxThreshold        | Double | No          | Upper bound of the internal threshold sweep                       |
| ThresholdStep       | Double | No          | Step size between threshold levels (default 10)                   |
| MinRepeatability    | Int    | No          | How many threshold levels a blob must appear in to be kept (default 2) |

| Output Port | Type       |
|-------------|------------|
| Blobs       | KeyPoint[] |

---

## ConnectedComponents

Labels connected regions in a binary (thresholded) single-channel image using `Cv2.ConnectedComponentsWithStats`.

Supports [ROI](#roi). When an ROI is set, labeling runs only within that region; all returned coordinates are in full-image space, and the label image shows component colors inside the ROI on a black background. `Count` is the raw OpenCV label count (background label 0 included); actual components = Count − 1. `Stats` and `Centroids` rows follow OpenCV layout: row 0 = background, rows 1..Count−1 = components.

| Parameter        | Type   | Connectable | Description                                                    |
|------------------|--------|-------------|----------------------------------------------------------------|
| Image            | Object | Yes         | Binary single-channel input Mat (e.g. output of Threshold)     |
| Connectivity     | Enum   | No          | `8` (default) — 8-connected; `4` — 4-connected                 |
| OutputLabelImage | Bool   | No          | When `true` (default), produce the colored label image. Set to `false` for offline/batch use to skip the image build. |

| Output Port | Type   | Description                                                                                          |
|-------------|--------|------------------------------------------------------------------------------------------------------|
| Count       | int    | Total label count including background; component count = Count − 1. 0 only when the ROI is entirely outside the image (all other outputs also empty). |
| Labels      | Mat    | `H × W` int32 matrix — each pixel holds its label index: 0 = background, 1..Count−1 = components   |
| Stats       | Mat    | `Count × 5` int32 matrix — row 0 = background, rows 1..Count−1 = components; cols: LEFT, TOP, WIDTH, HEIGHT, AREA; all in full-image coords |
| Centroids   | Mat    | `Count × 2` float64 matrix — row 0 = background, rows 1..Count−1 = components; cols: cx, cy; all in full-image coords |
| LabelImage  | Mat    | BGR image with each component painted a distinct Jet-colormap color; background is black             |

---

## FindContours

Finds contours in a binary (thresholded) single-channel image using `Cv2.FindContours`.

Supports [ROI](#roi). When an ROI is set, contour detection runs only within that region; all returned point coordinates are automatically translated to full-image space. Each contour is a polygon — an ordered array of points tracing one connected boundary. Includes built-in filtering/repair to remove degenerate contours before they reach downstream operators. Visualised as yellow polygon overlays in the Inspector.

Raw output commonly contains degenerate contours (zero area, self-intersecting rings) that render as visual artifacts in the Inspector — use `Filter` or `Fix` to clean them up.

| Parameter | Type   | Connectable | Description                                                                                  |
|-----------|--------|-------------|----------------------------------------------------------------------------------------------|
| Image     | Object | Yes         | Binary single-channel input Mat (e.g. output of Threshold)                                   |
| Mode      | Enum   | No          | `List` (default) — all contours flat; `External` — outermost only; `CComp` — two-level hierarchy; `Tree` — full hierarchy |
| Method    | Enum   | No          | `Simple` (default) — compress collinear segments; `None` — all points; `TC89L1` / `TC89KCOS` — Teh-Chin approximation |
| Filter    | Enum   | No          | `Fix` (default) — repair invalid rings via GeometryFixer, splitting self-intersecting rings into valid sub-polygons; `Filter` — drop invalid contours; `None` — raw output, no filtering |
| MinArea   | Double | No          | Minimum contour area in px² (default 1.0); contours below this are always dropped (ignored when Filter = None) |

| Output Port | Type      | Description                                        |
|-------------|-----------|----------------------------------------------------|
| Contours    | Point[][] | Array of contours, each an ordered array of points |

---

## FindStripeEdges

Finds the N most prominent sub-pixel edges along a rotated stripe ROI by sampling a 1D profile via bilinear interpolation, applying a box-difference derivative, and locating peaks with parabolic sub-pixel refinement.

Supports [ROI](#roi). All returned coordinates are in full-image space.

| Parameter       | Type   | Connectable | Description |
|-----------------|--------|-------------|-------------|
| Image           | Object | Yes (Mat)   | Single-channel (grayscale) input Mat |
| Center X        | Double | No          | X coordinate of the stripe center in image pixels |
| Center Y        | Double | No          | Y coordinate of the stripe center in image pixels |
| Length          | Double | No          | Length of the stripe along the search axis in pixels |
| Width           | Double | No          | Height of the stripe perpendicular to the axis; wider stripes average over more rows, reducing noise |
| Angle (°)       | Double | No          | Rotation of the search axis in degrees; 0 = horizontal left-to-right |
| Filter Size     | Int    | No          | Width of the box-difference derivative kernel (min 2, clamped to at most Length/2); larger = smoother profile, less noise sensitivity |
| Threshold       | Enum   | No          | `Manual` (default) — use Threshold Value; `Auto` — Otsu on the gradient response distribution |
| Threshold Value | Double | No          | Minimum gradient response to consider as an edge candidate; shown only when Threshold = Manual (default 10) |
| Polarity        | Enum   | No          | `Both` (default) — any transition; `DarkToBright` — rising edges only; `BrightToDark` — falling edges only |
| Max Edges       | Int    | No          | Maximum number of edges to return (default 1) |

| Output Port | Type            | Description |
|-------------|-----------------|-------------|
| Points      | Point2f[]       | Edge center points in full-image coordinates |
| Score       | double[]        | Absolute gradient response at each edge; parallel to Points |
| Lines       | LineSegment2f[] | One line per edge, perpendicular to the stripe axis, spanning the full stripe Width |
| Polarity    | string[]        | Per-edge polarity: `"DarkToBright"` or `"BrightToDark"`; parallel to Points |

All four output arrays share the same index and are always the same length (≤ Max Edges).

---

## DetectSegment

Fits a sub-pixel line segment to a single prominent edge within a rotated ROI. The ROI is divided into `StripeCount` stripes along its height axis; each stripe calls `FindStripeEdgesOperator` internally to locate one edge candidate. Iterative outlier rejection in the ROI's (s, t) coordinate system fits a line through the surviving positions. Endpoint extent is then refined by splitting the area around the fitted segment into two half-ROIs on opposite perpendicular sides and pooling the strongest edge responses; the two most prominent determine the final segment endpoints.

Supports [ROI](#roi).

| Parameter       | Type   | Connectable | Description |
|-----------------|--------|-------------|-------------|
| Image           | Object | Yes (Mat)   | Single-channel (grayscale) input Mat |
| Center X        | Double | Yes         | X coordinate of the ROI center in image pixels |
| Center Y        | Double | Yes         | Y coordinate of the ROI center in image pixels |
| Width           | Double | Yes         | ROI extent along the scan direction (perpendicular to the edge) |
| Height          | Double | Yes         | ROI extent along the edge direction |
| Angle (°)       | Double | Yes         | ROI rotation; 0 = edge runs vertically, scan direction is horizontal |
| Stripe Count    | Int    | No          | Number of stripes to split the ROI into; min 2 (default 5) |
| Stripe Width    | Int    | No          | Height of each stripe in pixels (default 10) |
| Filter Size     | Int    | No          | Box-difference derivative kernel width; min 2 (default 5) |
| Threshold       | Enum   | No          | `Manual` (default) or `Auto` (Otsu on the gradient distribution) |
| Threshold Value | Double | No          | Minimum gradient response; shown when Threshold = Manual (default 10) |
| Polarity        | Enum   | No          | `DarkToBright` (default) — rising edges only; `BrightToDark` — falling edges only |
| Edge Select     | Enum   | No          | Which edge to pick per stripe: `Strongest` (default), `First`, `Last` |
| Min Score       | Double | No          | Minimum fraction [0–1] of stripes that must agree for Found = true (default 0.5) |

| Output Port | Type          | Description |
|-------------|---------------|-------------|
| Line        | LineSegment2f | Fitted edge segment with sub-pixel float endpoints in full-image coordinates |
| Points      | Point2f[]     | Per-stripe inlier edge positions in full-image coordinates _(populated only when internal debug mode is on)_ |
| Score       | double[]      | Gradient response strength at each inlier stripe; parallel to Points _(populated only when internal debug mode is on)_ |
| Found       | bool          | True when inlier fraction ≥ Min Score |
| Contours    | Point2f[][]   | The two endpoint-search half-ROIs as separate four-point polygons _(populated only when internal debug mode is on)_ |

---

## TemplateMatch

Finds every occurrence of a selected visual pattern in an image. Use **Edit Template...** in the
operator settings to draw a box around an example of the pattern. You can also mask rectangular
areas that should be ignored, which is useful when part of the pattern changes between images.

Each result includes the match location, size, and confidence score. **Minimum Score** controls how
closely a region must resemble the template. **Maximum Overlap** removes duplicate boxes around the
same occurrence. An optional ROI limits the search to part of the image.

The current version searches at the template's original size and angle. It does not find rotated or
scaled versions of the pattern. The input and template channels must match each other.

| Parameter         | Type   | Connectable | Description |
|-------------------|--------|-------------|-------------|
| Image             | Object | Yes (Mat)   | Image in which to search for the template |
| Template          | String | No          | Saved template selected or created with **Edit Template...** |
| Minimum Score     | Double | No          | Required match confidence from 0 to 1; higher values are stricter (default 0.8) |
| Maximum Matches   | Int    | No          | Maximum number of results; 0 means unlimited (default 100) |
| Maximum Overlap   | Double | No          | How much two result boxes may overlap before the weaker one is removed (default 0.3) |
| ROI Center X/Y    | Double | Yes         | Center of the optional axis-aligned search ROI |
| ROI Width/Height  | Double | Yes         | Search ROI size; zero disables the ROI |

| Output Port | Type   | Description |
|-------------|--------|-------------|
| Matches     | Mat    | One row per result: X, Y, Width, Height, and Score; strongest matches first |
| Rectangles  | Rect[] | Match boxes in full-image coordinates, displayed in the inspector |

The template editor opens on the operator's current input image. Run the flow first, then select the
template region, add or delete mask rectangles as needed, and save it. The saved template path is
assigned to the operator automatically.

---

## LoopStart

Marks the start of a flat loop body. `Source` defines the collection being iterated; `Count` is
derived from it. `Mode` controls execution:

- **Discrete** — runs the body exactly once for the user-set `Index`. Use this for interactive
  single-item debugging.
- **Serial** — runs all iterations 0..Count-1 in order. Default.
- **Parallel** — runs all iterations concurrently.

In all modes, `Index` also controls which iteration's results the inspector displays for body
operators. `LoopStart` does not expose the selected item directly; downstream operators consume the
original source plus `Index` through ordinary upstream wiring.

`Source` must be a `Mat` or a non-string `IEnumerable`. For a `Mat`, count is `Rows`. For a
collection, count is the number of items.

| Parameter | Type   | Connectable | Description |
|-----------|--------|-------------|-------------|
| Source    | Object | Yes         | Mat or non-string collection that defines the loop count |
| Index     | Int    | No          | Iteration to display in the inspector; the only iteration that runs in Discrete mode |
| Mode      | Enum   | No          | `Discrete` / `Serial` / `Parallel`; default `Serial` |

| Output Port | Type |
|-------------|------|
| Index       | int  |
| Count       | int  |

---

## LoopEnd

Marks the end of a flat loop body. `LoopEnd` itself is a stateless pass-through — the runtime
accumulates its per-iteration values. After all iterations complete, downstream operators receive
`Out1`–`Out4` as `object?[]` arrays indexed by loop iteration.

`Index` should be wired from the paired `LoopStart.Index`; this identifies which start/end nodes
belong together.

| Parameter | Type   | Connectable | Description |
|-----------|--------|-------------|-------------|
| Index     | Object | Yes (int)   | Index from the paired LoopStart |
| In 1      | Object | Yes         | First value to collect |
| In 2      | Object | Yes         | Second value to collect |
| In 3      | Object | Yes         | Third value to collect |
| In 4      | Object | Yes         | Fourth value to collect |

| Output Port | Type   |
|-------------|--------|
| Out1        | object |
| Out2        | object |
| Out3        | object |
| Out4        | object |

---

## ROI

Operators that support ROI expose four extra connectable Int parameters: **ROI X**, **ROI Y**, **ROI Width**, **ROI Height**. When Width and Height are both 0 (the default) the operator runs on the full image.

**Image-output operators** (Morphology, Threshold, InvertImage): the effect is confined to the ROI rectangle; pixels outside it are copied unchanged from the input.

**Detection operators** (DetectCircles, ConnectedComponents, FindContours, DetectSimpleBlobs): detection runs only within the ROI region, and all returned coordinates are expressed in full-image space.

In both cases, if the rectangle lies entirely outside the image bounds after clamping, image-output operators return the input unchanged and detection operators return an empty result.

All four parameters are connectable, so they can be wired to outputs of upstream operators (e.g. a detected bounding box driving the ROI of a downstream filter). Operators that support ROI also expose **RoiX**, **RoiY**, **RoiW**, **RoiH** as output ports, so their ROI values can be forwarded to downstream operators.

---

## CSharpScript

Executes a user-written C# snippet loaded from a `.cs` file on disk. The file is compiled with Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`) at first use and cached; if the file's modification date changes the script is recompiled automatically on the next pipeline run.

The script body is plain top-level C# (no namespace or class declaration). The following variables are pre-defined:

- `In1`, `In2`, `In3`, `In4` — (`object?`) — values from wired upstream ports; `null` when not connected.
- `Image` — (`Mat?`) — assign a Mat to send it to the primary image output port.
- `Out1`, `Out2`, `Out3`, `Out4` — (`object?`) — assign any value to send it to the corresponding output port.

Auto-imported namespaces: `System`, `System.Linq`, `System.Collections.Generic`, `OpenCvSharp`, `OpenCvSharp.Features2D`, `IPLab.Core.Models`.

**Security note:** scripts run with full trust inside the IPLab process. There is no sandboxing. Only load scripts from sources you trust.

**Example — image filter:**
```csharp
var src = (Mat)In1;
var dst = new Mat();
Cv2.GaussianBlur(src, dst, new Size(5, 5), 0);
Image = dst;
```

**Example — filter array output and pass image through:**
```csharp
var circles = (CircleSegment[])In2;
var big     = circles.Where(c => c.Radius > 20).ToArray();
Image = (Mat)In1;   // pass image through unchanged
Out1  = big;        // filtered circle array
Out2  = big.Length; // scalar count
```

**Debugging in-script:** use `Console.WriteLine`, `Cv2.ImWrite`, or `Cv2.ImShow` / `Cv2.WaitKey` directly in the script. A standalone debug runner project can be scaffolded via the "Scaffold debug project" button in the operator settings.

Does not support ROI.

| Parameter   | Type   | Connectable | Description                               |
|-------------|--------|-------------|-------------------------------------------|
| Script Path | String | No          | Absolute path to the `.cs` script file   |
| In1         | Object | Yes         | First wired input (wildcard type)         |
| In2         | Object | Yes         | Second wired input (wildcard type)        |
| In3         | Object | Yes         | Third wired input (wildcard type)         |
| In4         | Object | Yes         | Fourth wired input (wildcard type)        |

| Output Port | Type   |
|-------------|--------|
| Image       | Mat    |
| Out1        | Object |
| Out2        | Object |
| Out3        | Object |
| Out4        | Object |
