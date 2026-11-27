# PhotoExtractor: technical notes

Back to the README: [click here](../README.md).

## Technologies

- C# / WinForms, .NET 9 (`net9.0-windows`)
- [OpenCvSharp4](https://github.com/shimat/opencvsharp) for image processing, restored automatically via NuGet

> Keep `OpenCvSharp4.Extensions` on a version compatible with .NET 9 (currently `4.10.0.20241108`). Version 4.13 pulls in `System.Drawing.Common` 10.x, whose DLLs override the WindowsDesktop 9 runtime and make WinForms crash at startup (`TypeLoadException` on `System.Private.Windows.Core.OsVersion`).

## How it works

- **Line detection**: two detectors, switchable on the fly:
  - **Hough**: classic OpenCV Hough line transform;
  - **LOCR**: a custom segment detector optimised for album scans.
- **Border selection**: the user picks one detected line per edge; the four corners are the intersections of those lines. Refine mode lets each border or corner be moved by hand (corner drags move at 1/3 of the mouse speed, with a loupe).
- **Extraction**: the quadrilateral is straightened with a perspective transform (`WarpPerspective`), then rotated 0°/90°/180°/270° according to the chosen orientation, and saved as JPEG quality 95 next to the source file (`<name>_NN_Extractored.jpg`).
- **Persistence**: window layout, slider values, open files and tab scroll positions are saved in the app config and restored at next launch.

Full functional specification: [click here](../SPECS.md).

## Building from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```
git clone https://github.com/amandiofr/PhotoExtractor.git
cd PhotoExtractor
dotnet run
```

## Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`) in addition to the .NET 9 SDK.

```
.\installer\build-installer.ps1 -Version 1.1.1
```

This publishes a self-contained build (no separate .NET Runtime needed on the target machine) and packages it into `publish-installer\PhotoExtractor-Setup-<version>.exe`. Upload that file to a new GitHub release marked **Latest**: the download links on the README and on amandiofr.github.io point to `releases/latest`.
