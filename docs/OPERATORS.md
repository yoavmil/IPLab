# IPLab Operators

## I/O
- [LoadImage](#loadimage) — load a color image from disk
- [SaveImage](#saveimage) — save an image to disk

## Color & Channels
- [ConvertToGrayscale](#converttograyscale) — convert BGR image to single-channel grayscale
- [SplitChannels](#splitchannels) — split BGR image into separate R, G, B channel images

## Detection
- [DetectCircles](#detectcircles) — detect circles using Hough Gradient transform

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

| Parameter | Type   | Connectable | Description    |
|-----------|--------|-------------|----------------|
| Image     | Object | Yes         | Input BGR Mat  |

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
