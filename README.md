# PhotoExtractor

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/amandiofr)

A Windows desktop tool for manually extracting photographs from scanned album pages. Define the four edges of a photo, correct the perspective, and export a clean JPEG — one photo at a time, with full control.

---

## Features

- **Manual border selection** using detected Hough lines — click the line that matches each edge
- **Perspective correction** via `WarpPerspective`
- **Zoom & pan** — scroll wheel to zoom (centered on cursor), middle-click to pan, Ctrl+0 to reset
- **Grab & drag borders** — hover any selected border and drag it to fine-tune its position
- **Refine mode** — replace a border with a better Hough line without restarting
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

1. Tune the **Blur / Low / High / MinLen / Gap** sliders until useful edge lines appear (enable **Lines** to see them)
2. Click **Border 1** then click the Hough line that matches the first edge of the photo — repeat for borders 2–4
3. Use **Refine** or **Parallel** to adjust any border that isn't quite right; drag borders directly if needed
4. If the photo is rotated, click **↺** or **↻** to set the correct up direction
5. Click **Extract** — the file is saved next to the source image as `filename_NN_Extractored.jpg`

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open image |
| Ctrl+R | Reset zoom & pan |
| Scroll wheel | Zoom in / out |
| Middle-click drag | Pan |

---

## Output

Files are written to the same folder as the source image:

```
my_album_page_01_Extractored.jpg
my_album_page_02_Extractored.jpg
...
```

Quality is fixed at JPEG 95.
