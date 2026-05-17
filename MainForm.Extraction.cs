using OpenCvSharp;
using System.Windows.Forms;

partial class MainForm
{
    Point2f[] ComputeCorners()
    {
        var lines = selectedBorders.Select(b => b!.Value).ToArray();
        bool[] isHoriz = lines.Select(l =>
            Math.Abs(l.P2.X - l.P1.X) >= Math.Abs(l.P2.Y - l.P1.Y)).ToArray();
        var horizLines = lines.Where((_, i) =>  isHoriz[i]).ToArray();
        var vertLines  = lines.Where((_, i) => !isHoriz[i]).ToArray();
        if (horizLines.Length == 2 && vertLines.Length == 2)
            return new[]
            {
                LineIntersect(horizLines[0], vertLines[0]),
                LineIntersect(horizLines[0], vertLines[1]),
                LineIntersect(horizLines[1], vertLines[1]),
                LineIntersect(horizLines[1], vertLines[0]),
            };
        return new[]
        {
            LineIntersect(lines[0], lines[1]),
            LineIntersect(lines[1], lines[2]),
            LineIntersect(lines[2], lines[3]),
            LineIntersect(lines[3], lines[0]),
        };
    }

    (Point2f, Point2f)[] ComputeBorderSegments()
    {
        var lines = selectedBorders.Select(b => b!.Value).ToArray();
        bool[] isHoriz = lines.Select(l =>
            Math.Abs(l.P2.X - l.P1.X) >= Math.Abs(l.P2.Y - l.P1.Y)).ToArray();
        var result = new (Point2f, Point2f)[4];

        var hIdx = Enumerable.Range(0, 4).Where(i =>  isHoriz[i]).ToArray();
        var vIdx = Enumerable.Range(0, 4).Where(i => !isHoriz[i]).ToArray();

        if (hIdx.Length == 2 && vIdx.Length == 2)
        {
            var c = new[]
            {
                LineIntersect(lines[hIdx[0]], lines[vIdx[0]]),
                LineIntersect(lines[hIdx[0]], lines[vIdx[1]]),
                LineIntersect(lines[hIdx[1]], lines[vIdx[1]]),
                LineIntersect(lines[hIdx[1]], lines[vIdx[0]]),
            };
            result[hIdx[0]] = (c[0], c[1]);
            result[hIdx[1]] = (c[3], c[2]);
            result[vIdx[0]] = (c[0], c[3]);
            result[vIdx[1]] = (c[1], c[2]);
        }
        else
        {
            var c = new[]
            {
                LineIntersect(lines[0], lines[1]),
                LineIntersect(lines[1], lines[2]),
                LineIntersect(lines[2], lines[3]),
                LineIntersect(lines[3], lines[0]),
            };
            result[0] = (c[3], c[0]);
            result[1] = (c[0], c[1]);
            result[2] = (c[1], c[2]);
            result[3] = (c[2], c[3]);
        }

        return result;
    }

    void OnExtractManual(object? sender, EventArgs e)
    {
        if (image == null || selectedBorders.Any(b => b == null)) return;
        var corners = ComputeCorners();
        Mat warped = WarpQuad(image, corners);

        if (extractOrientation != 0)
        {
            var rotFlag = extractOrientation switch
            {
                1 => RotateFlags.Rotate90Counterclockwise,
                2 => RotateFlags.Rotate180,
                _ => RotateFlags.Rotate90Clockwise
            };
            Mat rotated = new Mat();
            Cv2.Rotate(warped, rotated, rotFlag);
            warped.Dispose();
            warped = rotated;
        }

        string outPath = NextOutputPath(loadedImagePath!);
        var jpegParams = new ImageEncodingParam(ImwriteFlags.JpegQuality, 95);
        Cv2.ImWrite(outPath, warped, jpegParams);
        warped.Dispose();
        labelStatus.Text = $"Saved: {Path.GetFileName(outPath)}";

        doneCenters.Add(new Point2f(corners.Average(c => c.X), corners.Average(c => c.Y)));
        for (int i = 0; i < 4; i++) selectedBorders[i] = null;
        extractOrientation = 0;
        orientationTouched = false;
        btnExtractManual.Enabled = false;
        btnRefine.Enabled = false;
        btnParallel.Enabled = false;
        SetActiveMode(0);
        UpdateDisplay();
    }

    static Mat WarpQuad(Mat src, Point2f[] quad)
    {
        var sorted = quad.OrderBy(p => p.X + p.Y).ToArray();
        Point2f tl = sorted[0];
        Point2f br = sorted[3];
        Point2f tr = quad.OrderBy(p => p.Y - p.X).First();
        Point2f bl = quad.OrderBy(p => p.X - p.Y).First();

        float w = Math.Max(Dist(tl, tr), Dist(bl, br));
        float h = Math.Max(Dist(tl, bl), Dist(tr, br));

        var dst = new Point2f[]
        {
            new(0, 0), new(w - 1, 0),
            new(w - 1, h - 1), new(0, h - 1)
        };

        Mat M = Cv2.GetPerspectiveTransform(new[] { tl, tr, br, bl }, dst);
        Mat warped = new Mat();
        Cv2.WarpPerspective(src, warped, M, new OpenCvSharp.Size((int)w, (int)h));
        return warped;
    }

    static string NextOutputPath(string sourcePath)
    {
        string dir  = Path.GetDirectoryName(sourcePath)!;
        string stem = Path.GetFileNameWithoutExtension(sourcePath);
        for (int n = 1; n <= 99; n++)
        {
            string candidate = Path.Combine(dir, $"{stem}_{n:D2}_Extractored.jpg");
            if (!File.Exists(candidate)) return candidate;
        }
        return Path.Combine(dir, $"{stem}_{Guid.NewGuid():N}.jpg");
    }
}
