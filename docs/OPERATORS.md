# IPLab Operators

## I/O
- [LoadImage](#loadimage) — load a color image from disk
- [SaveImage](#saveimage) — save an image to disk

## Color & Channels
- [ConvertToGrayscale](#converttograyscale) — convert BGR image to single-channel grayscale
- [SplitChannels](#splitchannels) — split BGR image into separate R, G, B channel images
- [InvertImage](#invertimage) — invert all pixel values (bitwise NOT)

---

## InvertImage

Inverts all pixel values in the image using a bitwise NOT (`Cv2.BitwiseNot`). Works on any channel count (grayscale or color). Useful as a pre-processing step when blobs are dark on a bright background and the downstream detector expects light on dark.

| Parameter | Type   | Connectable | Description   |
|-----------|--------|-------------|---------------|
| Image     | Object | Yes         | Input Mat     |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## Filters
- [Threshold](#threshold) — apply binary threshold to a single-channel image

## Detection
- [DetectCircles](#detectcircles) — detect circles using Hough Gradient transform
- [DetectSimpleBlobs](#detectsimpleblobs) — detect circular blobs using SimpleBlobDetector
- [ConnectedComponents](#connectedcomponents) — label connected regions and return per-component stats
- [FindContours](#findcontours) — find contours in a binary image with built-in filter/repair

---

## LoadImage

Loads a color image from disk.

| Parameter | Type   | Connectable | Description        |
|-----------|--------|-------------|--------------------|
| FilePath  | String | No          | Path to image file |

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

## Threshold

Applies a binary threshold to a single-channel image. Pixels above `Thresh` are set to `MaxVal`; all others are set to 0.

| Parameter | Type   | Connectable | Description                        |
|-----------|--------|-------------|------------------------------------|
| Image     | Object | Yes         | Single-channel input Mat           |
| Thresh    | Double | No          | Threshold value (default 128)      |
| MaxVal    | Double | No          | Value assigned to passing pixels (default 255) |

| Output Port | Type |
|-------------|------|
| Image       | Mat  |

---

## DetectCircles

Detects circles in a single-channel image using the Hough Gradient transform (`Cv2.HoughCircles`).

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

| Parameter          | Type   | Connectable | Description                                      |
|--------------------|--------|-------------|--------------------------------------------------|
| Image              | Object | Yes         | Single-channel input Mat                         |
| Polarity           | Enum   | No          | `Light on Dark` (default) or `Dark on Light`     |
| MinCircularity     | Double | No          | Minimum circularity score (0–1); higher = rounder |
| MinArea            | Double | No          | Minimum blob area in pixels²                     |
| MaxArea            | Double | No          | Maximum blob area in pixels²                     |
| MinDistBetweenBlobs| Double | No          | Minimum distance between blob centers in pixels  |
| MinThreshold       | Double | No          | Lower bound of the internal threshold sweep      |
| MaxThreshold       | Double | No          | Upper bound of the internal threshold sweep      |
| ThresholdStep      | Double | No          | Step size between threshold levels (default 10)  |
| MinRepeatability   | Int    | No          | How many threshold levels a blob must appear in to be kept (default 2) |

| Output Port | Type       |
|-------------|------------|
| Blobs       | KeyPoint[] |

---

## ConnectedComponents

Labels connected regions in a binary (thresholded) single-channel image using `Cv2.ConnectedComponentsWithStats`. Returns one `ConnectedComponentInfo` per region (background label 0 is excluded). Visualised as orange bounding-box rectangles with centroid cross-marks in the Inspector.

| Parameter        | Type   | Connectable | Description                                                    |
|------------------|--------|-------------|----------------------------------------------------------------|
| Image            | Object | Yes         | Binary single-channel input Mat (e.g. output of Threshold)     |
| Connectivity     | Enum   | No          | `8` (default) — 8-connected; `4` — 4-connected                 |
| OutputLabelImage | Bool   | No          | When `true` (default), produce the colored label image. Set to `false` for offline/batch use to skip the image build. |

| Output Port | Type                      | Description |
|-------------|---------------------------|-------------|
| Components  | ConnectedComponentInfo[]  | Per-component stats (label, area, bounding box, centroid) |
| LabelImage  | Mat                       | BGR image with each component painted a distinct color; background is black |

`ConnectedComponentInfo` fields: `Label` (int), `Area` (int, pixels), `BoundingBox` (OpenCvSharp.Rect), `Centroid` (Point2f).

---

## FindContours

Finds contours in a binary (thresholded) single-channel image using `Cv2.FindContours`. Each contour is a polygon — an ordered array of points tracing one connected boundary. Includes built-in filtering/repair to remove degenerate contours before they reach downstream operators. Visualised as yellow polygon overlays in the Inspector.

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
