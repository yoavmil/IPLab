# IPLab Operators

## I/O
- [LoadImage](#loadimage) — load a color image from disk
- [SaveImage](#saveimage) — save an image to disk

## Color & Channels
- [ConvertToGrayscale](#converttograyscale) — convert BGR image to single-channel grayscale
- [SplitChannels](#splitchannels) — split BGR image into separate R, G, B channel images
- [InvertImage](#invertimage) — invert all pixel values (bitwise NOT)

## Filters
- [Threshold](#threshold) — apply binary threshold to a single-channel image
- [GaussianBlur](#gaussianblur) — smooth an image with a Gaussian kernel
- [Morphology](#morphology) — morphological operations (erode, dilate, open, close, gradient, top-hat, black-hat)
- [Thinning](#thinning) — skeletonize a binary image via iterative thinning (Zhang-Suen or Guo-Hall)

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

## Detection
- [DetectCircles](#detectcircles) — detect circles using Hough Gradient transform
- [DetectSimpleBlobs](#detectsimpleblobs) — detect circular blobs using SimpleBlobDetector
- [ConnectedComponents](#connectedcomponents) — label connected regions and return per-component stats
- [FindContours](#findcontours) — find contours in a binary image with built-in filter/repair

---

## LoadImage

Loads a color image from disk. Supports a list of images; the active one is determined by `ActiveIndex`. The inspector shows a horizontal thumbnail strip for all listed images — clicking a thumbnail switches the active image and re-runs the flow. Thumbnails are decoded at 120 px wide to limit memory use.

| Parameter   | Type       | Connectable | Description                                    |
|-------------|------------|-------------|------------------------------------------------|
| FilePaths   | StringList | No          | Ordered list of image file paths               |
| ActiveIndex | Int        | No          | Zero-based index of the currently active image |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

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

Applies a threshold to a single-channel image using `Cv2.Threshold`. Supports [ROI](#roi).

| Parameter | Type   | Connectable | Description                                                                                                                                      |
|-----------|--------|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| Image     | Object | Yes         | Single-channel input Mat                                                                                                                         |
| Method    | Enum   | No          | `Fixed` (default) — use `Thresh` value; `Otsu` — auto-compute optimal threshold from histogram (best for bimodal images, ignores `Thresh`); `Triangle` — triangle algorithm (best for unimodal histograms, ignores `Thresh`) |
| Output Type | Enum   | No        | `Binary` (default) — above→255, below→0; `BinaryInv` — inverted binary; `Trunc` — above→Thresh, below unchanged; `ToZero` — above unchanged, below→0; `ToZeroInv` — above→0, below unchanged |
| Thresh      | Double | No        | Threshold value (default 128); ignored when Method is `Otsu` or `Triangle`                                                                     |

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

Supports [ROI](#roi). When an ROI is set, labeling runs only within that region; all returned coordinates (bounding boxes, centroids) are in full-image space, and the label image shows component colors inside the ROI on a black background. Returns one `ConnectedComponentInfo` per region (background label 0 is excluded). Visualised as orange bounding-box rectangles with centroid cross-marks in the Inspector.

| Parameter        | Type   | Connectable | Description                                                    |
|------------------|--------|-------------|----------------------------------------------------------------|
| Image            | Object | Yes         | Binary single-channel input Mat (e.g. output of Threshold)     |
| Connectivity     | Enum   | No          | `8` (default) — 8-connected; `4` — 4-connected                 |
| OutputLabelImage | Bool   | No          | When `true` (default), produce the colored label image. Set to `false` for offline/batch use to skip the image build. |

| Output Port | Type                     | Description                                                              |
|-------------|--------------------------|--------------------------------------------------------------------------|
| Components  | ConnectedComponentInfo[] | Per-component stats (label, area, bounding box, centroid)                |
| LabelImage  | Mat                      | BGR image with each component painted a distinct color; background is black |

`ConnectedComponentInfo` fields: `Label` (int), `Area` (int, pixels), `BoundingBox` (OpenCvSharp.Rect), `Centroid` (Point2f).

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

## ROI

Operators that support ROI expose four extra connectable Int parameters: **ROI X**, **ROI Y**, **ROI Width**, **ROI Height**. When Width and Height are both 0 (the default) the operator runs on the full image.

**Image-output operators** (Morphology, Threshold, InvertImage): the effect is confined to the ROI rectangle; pixels outside it are copied unchanged from the input.

**Detection operators** (DetectCircles, ConnectedComponents, FindContours, DetectSimpleBlobs): detection runs only within the ROI region, and all returned coordinates are expressed in full-image space.

In both cases, if the rectangle lies entirely outside the image bounds after clamping, image-output operators return the input unchanged and detection operators return an empty result.

All four parameters are connectable, so they can be wired to outputs of upstream operators (e.g. a detected bounding box driving the ROI of a downstream filter). Operators that support ROI also expose **RoiX**, **RoiY**, **RoiW**, **RoiH** as output ports, so their ROI values can be forwarded to downstream operators.
