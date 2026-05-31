# Waviness

Measures the waviness of trench lines in an image by running a thinning pipeline and fitting a best-fit line segment to each skeleton cluster.

## What it does

1. Runs `thinning.ipl` — a fixed IPLab flow that converts the input image to grayscale, blurs, thresholds, applies morphological opening, and thins the result to a 1-pixel-wide skeleton.
2. Clusters the skeleton pixels into line segments using connected components (8-connectivity). Any gap in the skeleton produces a separate segment — no direction assumptions.
3. Fits a PCA line (`Cv2.FitLine` L2) to each cluster — orientation-blind, minimizes perpendicular distances.
4. Scores each image as the mean perpendicular distance of skeleton pixels from their fitted line segments. Clusters shorter than 40 pixels are discarded as noise.
5. Saves a debug image (`results/<name>_debug.jpg`) with three layers: original image, green skeleton, red fitted segments, and the waviness score at the top left.

## Usage

```
Waviness <image-path|folder>
```

- Pass a single image file or a folder.
- When a folder is given, all images in it are processed (non-recursive). Files with `debug` in the name are skipped automatically.
- Up to 4 images are processed in parallel.

## Output

Progress is printed to stdout as each image starts:

```
path/to/image.bmp (1/10)
```

After all images finish, a summary table is printed:

```
Name           | # Lines | Waviness
---------------|---------|----------
image1.bmp     |      12 |   1.3421
image2.bmp     |       8 |   2.0017
```

Debug images are saved to a `results/` subfolder next to the source images.

## Example
 ![debug](samples/trenches_debug.jpg)

## Dependencies

- [IPLab.Core](../../IPLab.Core) — flow execution engine
- OpenCvSharp — image I/O, connected components, line fitting, and drawing
