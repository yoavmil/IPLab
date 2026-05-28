# Waviness

Measures the waviness of trench lines in an image by running a thinning pipeline and fitting a regression line to each skeleton cluster.

## What it does

1. Runs `thinning.ipl` — a fixed IPLab flow that converts the input image to grayscale, blurs, thresholds, applies morphological opening, and thins the result to a 1-pixel-wide skeleton.
2. Clusters the skeleton pixels into individual lines by scanning column by column.
3. Fits a least-squares regression line to each cluster.
4. Reports mean absolute deviation of skeleton pixels from their regression line as the waviness score.
5. Saves a debug image (`<input>_debug.jpg`) with three layers: original image, green skeleton, red regression lines.

## Usage

```
Waviness <image-path>
```

## Output

- Per-operator status printed to stdout.
- `Lines found: N` — number of line clusters detected.
- `Waviness <score>` — mean absolute pixel deviation from the regression lines.
- `<input>_debug.jpg` saved next to the input image.

## Example
 ![debug](samples/trenches_debug.jpg)

## Dependencies

- [IPLab.Core](../../IPLab.Core) — flow execution engine
- [MathNet.Numerics](https://numerics.mathdotnet.com/) — `Fit.Line` for least-squares regression
- OpenCvSharp — image I/O and drawing
