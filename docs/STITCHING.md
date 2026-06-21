# Image Stitching Roadmap

Goal: stitch images from 4 cameras into a single undistorted, geometrically aligned composite.

## Pipeline Overview

```
Camera images (×4)
  → Distortion rectification    ✅ DistortionCalibrationOperator + UndistortOperator
  → Fiducial detection          ✅ TemplateMatchOperator
  → Fiducial line analysis      ✅ DetectSegmentOperator
  → Per-fiducial payload decode 🔲 CSharpScriptOperator (via LoopStart/LoopEnd)
  → Homography / stitching      🔲 TBD
```

## Completed Steps

### 1. Distortion rectification
Each camera's barrel/pincushion distortion is corrected using a checkerboard calibration image.
- `DistortionCalibrationOperator` detects inner corners via saddle filter, infers the grid, and writes a `CalibrationData` JSON file.
- `UndistortOperator` reads that file and bilinearly resamples each input image to a rectilinear output.

### 2. Fiducial detection
Fiducial markers (ArUco-style or bespoke; printed on the checkerboard or separate target) are located with `TemplateMatchOperator`.
- Produces `Rect[]` bounding boxes in (undistorted) image coordinates.

## Pending Steps

### 3. Line detection operator
`DetectSegmentOperator` detects a sub-pixel edge segment within a rotated ROI. It divides the ROI into stripes, calls `FindStripeEdgesOperator` per stripe to find one edge candidate each, rejects outliers iteratively, fits a line, and refines the endpoints via perpendicular-split extent ROIs. The operator is currently in active development on the `feature/find-edge-line` branch.

### 4. Loop operator ✅ Available
Iterate over the detected fiducial `Rect[]` and run per-fiducial analysis (line detection + payload decode) for each element.
Use the `LoopStart` → body → `LoopEnd` pattern already implemented:
- Wire `TemplateMatch.Rectangles` → `LoopStart.Source`.
- Choose `Mode`: `Serial` (one at a time), `Parallel` (all concurrent), or `Discrete` (single iteration for debugging).
- Body operators receive `LoopStart.Index` and wire to the upstream `Rect[]` to index into it.
- `LoopEnd` collects body outputs into `Out1[]`–`Out4[]` arrays for downstream use.
See `docs/loop plan.md` for full architecture and slice status.

### 5. Fiducial payload decode
A `CSharpScriptOperator` (see TODO.md) that reads the line-detection result for each fiducial and decodes the encoded value (encoding scheme TBD).

### 6. Homography estimation and stitching
Use the decoded fiducial world-coordinates (known from the checkerboard layout) and their image-space positions to compute per-camera homographies.
Warp and blend the four rectified images into a single mosaic.
Operator(s) TBD.

## Open Questions

- What is the fiducial encoding scheme (binary stripe pattern, dot code, etc.)?
- Are fiducials printed on the checkerboard itself or on a separate target overlay?
- What is the desired output format of the stitch (pixel scale, coordinate origin, blending method)?
- Does stitching happen inside IPLab (new operators) or in a post-processing script?
