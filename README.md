# PhotoExtractor

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/amandiofr)

A Windows desktop tool for digitising old photo albums. Point it at a page captured with a flatbed scanner, a camera, or a smartphone — then extract each photograph individually with perspective correction and full manual control.

## Why PhotoExtractor?

Most tools assume photos are straight rectangles. Album photos rarely are: they're glued at an angle, overlapping, shot from the side. Fully automatic tools either miss them or produce distorted crops. Photoshop works but is slow when you have hundreds to process.

PhotoExtractor sits in between: edge detection finds the lines for you, you confirm which ones define each photo, and the tool does the geometry. One clean JPEG per photo, no distortion, no manual cropping in a pixel editor.

It is designed for a specific use case — digitising a physical album page by page — and optimised for doing that quickly and reliably, whether your source is a flatbed scanner, a DSLR shot overhead, or a smartphone photo.

---

## Features

- **Manual border selection** using detected lines — click the line that matches each edge
- **Two line detectors** — classic Hough or LOCR (custom segment detector optimised for album scans), switchable on the fly
- **Perspective correction** via `WarpPerspective`
- **Zoom & pan** — scroll wheel to zoom (centered on cursor), middle- or right-click to pan, Ctrl+0 to reset
- **Grab & drag borders** — hover any selected border and drag it to fine-tune its position
- **Refine mode** — replace a border with a better detected line, or drag it freely; corner handles let you move any of the four intersection points directly
- **Corner handle loupe** — a magnifier appears in the bottom-left corner when dragging a handle; movement is 3× slower than the mouse for sub-pixel precision
- **Parallel mode** — snap a border parallel to its opposite
- **Extraction orientation** — rotate the output 0°/90°/180°/270° before exporting
- **DONE markers** — extracted areas are marked so you don't lose track across a busy page
- **Multi-image tabs** — drag & drop several files to open them side by side
- **Persistent config** — window layout, slider values, and open files are restored on next launch

---

## Requirements

- Windows 10/11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download)
- [OpenCvSharp4](https://github.com/shimat/opencvsharp) (restored automatically via NuGet)

---

## Getting Started

```
git clone <repo>
cd PhotoExtractor
dotnet run
```

Open an image with the **Open** button or drag & drop a file onto the window.

---

## Workflow

1. Choose a line detector (**Hough** or **LOCR**) and tune the sliders until useful lines appear (enable **Lines** to see them)
2. Click **Border 1** then click the detected line that matches the first edge of the photo — repeat for borders 2–4
3. Use **Refine** to adjust borders: drag a border directly, click a better line, or drag one of the four corner handles for precise corner placement
4. Use **Parallel** to snap a border parallel to its opposite when needed
5. If the photo is rotated, click **↺** or **↻** to set the correct up direction
6. Click **Extract** — the file is saved next to the source image as `filename_NN_Extractored.jpg`

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open image |
| Ctrl+R | Reset zoom & pan |
| Scroll wheel | Zoom in / out |
| Middle- or right-click drag | Pan |

---

## Output

Files are written to the same folder as the source image:

```
my_album_page_01_Extractored.jpg
my_album_page_02_Extractored.jpg
...
```

Quality is fixed at JPEG 95.
