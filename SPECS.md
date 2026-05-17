# PhotoExtractor — Functional Specifications

## Overview

Windows application (WinForms / .NET 10) for manually extracting photographs glued onto scanned album pages. The user outlines each photo with 4 borders; the application corrects the perspective and exports a straightened JPEG.

---

## Loading Images

- **Open button** (left of the control panel) or **Ctrl+O**: opens an image file
- **Drag & drop** of a single file: replaces the current image without creating a new tab
- **Drag & drop of N files**: creates N tabs (one per image)
- When no image is loaded: a full-screen psychedelic graphic is displayed (HSL plasma)

---

## Tabs (multi-image)

- Visible only when 2+ images are loaded
- Each tab shows the file name + a **×** close button
- **Right-click** on a tab: context menu → *Close this / Close others / Close all*
- Each tab's state is independent (selected borders, DONE markers, orientation…)
- The JSON config saves all open tab paths and the active tab index; on startup, missing files are filtered out before tabs are recreated

---

## Edge Detection

Adjustable parameters via sliders (all saved in config):

| Slider | Role |
|--------|------|
| Blur | Gaussian blur applied before Canny |
| Low / High | Canny thresholds |
| MinLen | Minimum segment length for HoughLinesP |
| Gap | Maximum gap for joining two collinear segments |

- **Edges switch**: blend of dilated Canny (75 %) + original image (25 %)
- **Lines switch**: displays all detected Hough segments in magenta

---

## Image Navigation

- **Scroll wheel**: zoom centered on the cursor (×1.25 / ×0.8 per notch, clamped to ×0.5–×10)
- **Middle-click + drag**: pan; the image cannot be moved fully out of view (50 px margin enforced)
- **Ctrl+0**: resets zoom and pan to fit-to-window
- Zoom and pan are reset automatically when loading a new image or switching tabs

---

## Selecting the 4 Borders

### Hover

- In normal/refine mode: the Hough segment nearest the cursor is highlighted in yellow
- In parallel mode: the **reference border** (the one that will be copied) is highlighted in thick white

### Selection Buttons

| Button | Behaviour |
|--------|-----------|
| **Border 1–4** | Activates selection mode for that border; clicking a Hough line assigns it |
| **Refine** | Click near an existing border → replaces it with the currently hovered Hough line |
| **Parallel** | Click near a border → snaps it parallel to the "lone" border among the other three |
| **Clear** | Clears all 4 borders |

- The 6 Border/Refine/Parallel buttons are **mutually exclusive** via `SetActiveMode()`
- The active button is visually highlighted (thicker coloured border)
- Refine and Parallel are disabled until all 4 borders are defined

### Grab / Drag a Border

- In **all** modes, hovering over a selected border switches the cursor to `SizeAll` and thickens the border to 5 px
- Dragging moves the border while preserving its angle

### Border Display

- **Fewer than 4 borders defined**: each border is drawn as a line extended to the edges of the image
- **All 4 borders defined**: each border is drawn as the segment between its two intersection corners with the adjacent borders

---

## Extraction Orientation

Three clickable elements appear at the centre of the defined quadrilateral:

```
↺   [↑]   ↻
```

- **↺ / ↻**: rotates the extraction orientation (0° → 90° → 180° → 270°)
- **[↑/→/↓/←]**: shows which side will be at the top of the extracted image
- An **UP** label appears near the "top" edge of the quadrilateral **only after** ↺ or ↻ has been used; it rotates with the orientation
- The UP label and orientation are reset after each extraction

---

## Extraction

- **Extract button** (right of the control panel), enabled only when all 4 borders are defined
- Processing: `WarpPerspective` (perspective correction) + rotation according to the chosen orientation
- Output: JPEG at quality 95, auto-named `stem_NN_Extractored.jpg`
- After extraction: a semi-transparent **DONE** panel appears at the centre of the extracted area and **persists** until:
  - a new image is loaded, **or**
  - a new quadrilateral is defined that encloses that centre (signals a re-extraction attempt of the same area)

---

## Configuration (JSON)

Saved automatically on each image load, restored on startup:

- Window position and size
- All slider values
- Edges / Lines switch states
- Open image paths + active tab index

---

## Out of Scope

- No automatic photo detection — everything is manual
- No editing of the extracted image
- No folder or batch processing
