using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

partial class MainForm
{
    void OnPictureBoxPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Écran psychédélique quand aucune image n'est chargée
        if (image == null)
        {
            if (psychoBitmap != null)
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(psychoBitmap, 0, 0, pictureBox.Width, pictureBox.Height);
            }
            return;
        }

        // Dessin de l'image avec zoom/pan
        if (displayBitmap != null)
        {
            var (ix, iy, iw, ih) = ZoomedRect();
            g.InterpolationMode = viewZoom >= 4f ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
            g.PixelOffsetMode = viewZoom >= 4f ? PixelOffsetMode.Half : PixelOffsetMode.Default;
            g.DrawImage(displayBitmap, ix, iy, iw, ih);
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }

        // Overlays LOCR
        if (rbLOCR.Checked) DrawLocrOverlays(g);

        // Surlignage de la ligne Hough la plus proche du curseur
        if (hoveredLine is { } hl)
        {
            var dp1 = ImageToDisplay(new Point2f(hl.P1.X, hl.P1.Y));
            var dp2 = ImageToDisplay(new Point2f(hl.P2.X, hl.P2.Y));
            if (dp1 != null && dp2 != null)
            {
                using var pen = new System.Drawing.Pen(Color.FromArgb(220, 255, 255, 0), 3);
                g.DrawLine(pen, dp1.Value, dp2.Value);
            }
        }

        // Dessine les 4 bords en coordonnées écran
        bool allSet = selectedBorders.All(b => b != null);
        (Point2f, Point2f)[]? borderSegs = allSet ? ComputeBorderSegments() : null;
        for (int i = 0; i < 4; i++)
        {
            if (selectedBorders[i] is not { } seg) continue;
            bool borderHovered = hoveredBorderIdx == i || draggingBorderIdx == i;
            using var borderPen = new System.Drawing.Pen(BorderWinColors[i], borderHovered ? 5f : 3f);
            if (borderSegs != null)
            {
                var (p1, p2) = borderSegs[i];
                var dp1 = ImageToDisplay(p1);
                var dp2 = ImageToDisplay(p2);
                if (dp1 != null && dp2 != null)
                    g.DrawLine(borderPen, dp1.Value, dp2.Value);
            }
            else
            {
                var dp1 = ImageToDisplay(new Point2f(seg.P1.X, seg.P1.Y));
                var dp2 = ImageToDisplay(new Point2f(seg.P2.X, seg.P2.Y));
                if (dp1 != null && dp2 != null)
                {
                    float dx = dp2.Value.X - dp1.Value.X, dy = dp2.Value.Y - dp1.Value.Y;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0.001f)
                    {
                        float ext = pictureBox.Width + pictureBox.Height;
                        g.DrawLine(borderPen,
                            dp1.Value.X - dx / len * ext, dp1.Value.Y - dy / len * ext,
                            dp2.Value.X + dx / len * ext, dp2.Value.Y + dy / len * ext);
                    }
                }
            }
        }

        // En mode parallel : surligne le bord de référence
        if (parallelMode && hoveredParallelRefIdx >= 0 && borderSegs != null)
        {
            var (rp1, rp2) = borderSegs[hoveredParallelRefIdx];
            var rdp1 = ImageToDisplay(rp1);
            var rdp2 = ImageToDisplay(rp2);
            if (rdp1 != null && rdp2 != null)
            {
                using var refPen = new System.Drawing.Pen(Color.FromArgb(220, 255, 255, 255), 5);
                g.DrawLine(refPen, rdp1.Value, rdp2.Value);
            }
        }

        // Panneaux "DONE" pour chaque photo déjà extraite
        if (doneCenters.Count > 0)
        {
            float dw = 28f * 1.6f * 2f * viewZoom, dh = 28f * 1.6f * viewZoom;
            using var dbg = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
            using var dfg = new SolidBrush(Color.White);
            using var df  = new Font("Segoe UI", Math.Max(1f, 28f * 0.42f * viewZoom), FontStyle.Bold);
            var dsf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            foreach (var dlc in doneCenters)
            {
                var ddc = ImageToDisplay(dlc);
                if (ddc == null) continue;
                var dr = new RectangleF(ddc.Value.X - dw / 2, ddc.Value.Y - dh / 2, dw, dh);
                g.FillRectangle(dbg, dr);
                g.DrawString("DONE", df, dfg, dr, dsf);
            }
        }

        if (!allSet) return;
        var corners = ComputeCorners();

        // Handles de coin en mode refine
        if (refineMode)
        {
            const float hr = 10f;
            using var hFill = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            using var hBorder = new System.Drawing.Pen(Color.FromArgb(180, 30, 30, 30), 1.5f);
            foreach (var corner in corners)
            {
                var dh = ImageToDisplay(corner);
                if (dh == null) continue;
                g.FillEllipse(hFill,   dh.Value.X - hr, dh.Value.Y - hr, hr * 2, hr * 2);
                g.DrawEllipse(hBorder, dh.Value.X - hr, dh.Value.Y - hr, hr * 2, hr * 2);
            }
        }

        var center = new Point2f(corners.Average(c => c.X), corners.Average(c => c.Y));
        var dc = ImageToDisplay(center);
        if (dc == null) return;

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        float r = 28f * viewZoom;
        string[] dirs = { "↑", "→", "↓", "←" };
        var items = new[]
        {
            (Text: "↺", X: dc.Value.X - r * 2.4f),
            (Text: dirs[extractOrientation], X: dc.Value.X),
            (Text: "↻", X: dc.Value.X + r * 2.4f),
        };

        using var bgBrush = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        using var fgBrush = new SolidBrush(Color.White);
        using var font    = new Font("Segoe UI", r * 0.85f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        foreach (var item in items)
        {
            var rect = new RectangleF(item.X - r, dc.Value.Y - r, r * 2, r * 2);
            g.FillEllipse(bgBrush, rect);
            g.DrawString(item.Text, font, fgBrush, rect, sf);
        }

        hitRotLeft  = new RectangleF(items[0].X - r, dc.Value.Y - r, r * 2, r * 2);
        hitRotRight = new RectangleF(items[2].X - r, dc.Value.Y - r, r * 2, r * 2);

        DrawCornerLoupe(g, corners, borderSegs);

        // Label "UP" près du bord dans la direction de la flèche
        if (!orientationTouched) return;
        float ulSide = r * 1.6f;
        var upTl = corners.OrderBy(p => p.X + p.Y).First();
        var upBr = corners.OrderBy(p => p.X + p.Y).Last();
        var upTr = corners.OrderBy(p => p.Y - p.X).First();
        var upBl = corners.OrderBy(p => p.Y - p.X).Last();
        (Point2f upA, Point2f upB) = extractOrientation switch
        {
            0 => (upTl, upTr),
            1 => (upTr, upBr),
            2 => (upBr, upBl),
            _ => (upBl, upTl),
        };
        var upMid = new Point2f((upA.X + upB.X) / 2, (upA.Y + upB.Y) / 2);
        var upMidDisp = ImageToDisplay(upMid);
        if (upMidDisp == null) return;
        float undx = dc.Value.X - upMidDisp.Value.X;
        float undy = dc.Value.Y - upMidDisp.Value.Y;
        float undlen = (float)Math.Sqrt(undx * undx + undy * undy);
        float ulcx = upMidDisp.Value.X, ulcy = upMidDisp.Value.Y;
        if (undlen > 0.001f)
        {
            ulcx += undx / undlen * (ulSide / 2 + 4);
            ulcy += undy / undlen * (ulSide / 2 + 4);
        }
        var upState = g.Save();
        g.TranslateTransform(ulcx, ulcy);
        g.RotateTransform(extractOrientation * 90f);
        var upLabelRect = new RectangleF(-ulSide / 2, -ulSide / 2, ulSide, ulSide);
        using var upFont = new Font("Segoe UI", r * 0.42f, FontStyle.Bold);
        g.FillRectangle(bgBrush, upLabelRect);
        g.DrawString("UP", upFont, fgBrush, upLabelRect, sf);
        g.Restore(upState);
    }

    void DrawCornerLoupe(Graphics g, Point2f[] corners, (Point2f, Point2f)[]? borderSegs)
    {
        if (draggingCornerIdx < 0) return;
        var bmp = rawBitmap ?? displayBitmap;
        if (bmp == null) return;
        var dragged = corners[draggingCornerIdx];

        const int loupeSize = 180;
        const int margin    = 10;
        int lx = margin, ly = pictureBox.Height - margin - loupeSize;
        var dstRect = new Rectangle(lx, ly, loupeSize, loupeSize);

        // srcSize adapté au zoom courant (×2 = moitié du zoom = plus de contexte)
        int srcSize = Math.Clamp((int)(loupeSize * 2 / Math.Max(1f, viewZoom)), 60, Math.Min(bmp.Width, bmp.Height) - 1);
        float zoom  = (float)loupeSize / srcSize;

        // Centre sur la position souris (pas sur le coin calculé) pour suivi direct
        float srcX0 = loupeCenterImg.X - srcSize / 2f;
        float srcY0 = loupeCenterImg.Y - srcSize / 2f;
        int srcX = Math.Clamp((int)MathF.Round(srcX0), 0, bmp.Width  - srcSize);
        int srcY = Math.Clamp((int)MathF.Round(srcY0), 0, bmp.Height - srcSize);
        var srcRect = new Rectangle(srcX, srcY, srcSize, srcSize);

        // Décalage pour que le coin reste toujours au centre de la loupe
        float dstLeft = lx + (srcX - srcX0) * zoom;
        float dstTop  = ly + (srcY - srcY0) * zoom;

        g.SetClip(dstRect);

        // Image originale (sans edges)
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(bmp, new RectangleF(dstLeft, dstTop, loupeSize, loupeSize), srcRect, GraphicsUnit.Pixel);
        g.InterpolationMode = InterpolationMode.Bilinear;

        // Bords superposés
        if (borderSegs != null)
        {
            PointF ToL(Point2f p) => new(dstLeft + (p.X - srcX) * zoom, dstTop + (p.Y - srcY) * zoom);
            for (int i = 0; i < 4; i++)
            {
                var (bp1, bp2) = borderSegs[i];
                using var bp = new System.Drawing.Pen(BorderWinColors[i], 1.5f);
                g.DrawLine(bp, ToL(bp1), ToL(bp2));
            }
        }

        g.ResetClip();

        // Réticule fixe au centre de la loupe
        float rx = lx + loupeSize / 2f;
        float ry = ly + loupeSize / 2f;
        using var crossPen = new System.Drawing.Pen(Color.White, 1.5f);
        g.DrawLine(crossPen, rx - 10, ry, rx + 10, ry);
        g.DrawLine(crossPen, rx, ry - 10, rx, ry + 10);

        // Cadre
        using var framePen = new System.Drawing.Pen(Color.FromArgb(200, 200, 200, 200), 2f);
        g.DrawRectangle(framePen, dstRect);
    }

    static Bitmap GeneratePsycho(int w, int h)
    {
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                bmp.PixelFormat);
        var row = new int[w];
        double cx = w / 2.0, cy = h / 2.0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double v = Math.Sin(x / 8.0)
                         + Math.Sin(y / 6.0)
                         + Math.Sin((x + y) / 11.0)
                         + Math.Sin(Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / 7.0);
                float hue = (float)(((v + 4.0) / 8.0) * 360.0) % 360f;
                if (hue < 0) hue += 360f;
                row[x] = PsychoHsl(hue, 1f, 0.5f);
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), w);
        }
        bmp.UnlockBits(data);
        return bmp;
    }

    static int PsychoHsl(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs(h / 60f % 2f - 1f));
        float m = l - c / 2f;
        float r, g, b;
        if      (h < 60f)  { r = c; g = x; b = 0; }
        else if (h < 120f) { r = x; g = c; b = 0; }
        else if (h < 180f) { r = 0; g = c; b = x; }
        else if (h < 240f) { r = 0; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0; b = c; }
        else               { r = c; g = 0; b = x; }
        return (255 << 24) | ((int)((r + m) * 255) << 16) | ((int)((g + m) * 255) << 8) | (int)((b + m) * 255);
    }
}
