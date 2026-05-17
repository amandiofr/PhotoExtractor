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

        // Fond cinéma quand aucune image n'est chargée
        if (image == null)
        {
            int pw = Math.Max(1, pictureBox.Width), ph = Math.Max(1, pictureBox.Height);
            if (cinemaBg == null || (!settingsAnimating && (cinemaBg.Width != pw || cinemaBg.Height != ph)))
            {
                cinemaBg?.Dispose();
                cinemaBg = GenerateCinemaBg(pw, ph);
            }
            g.DrawImage(cinemaBg, 0, 0, pw, ph);
            DrawCinemaTitle(g, pw, ph);
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

    static FontFamily VintageFontFamily()
    {
        foreach (var name in new[] { "Harrington", "Copperplate Gothic Bold", "Garamond", "Palatino Linotype", "Georgia" })
            try { return new FontFamily(name); } catch { }
        return FontFamily.GenericSerif;
    }

    void DrawCinemaTitle(Graphics g, int pw, int ph)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        var ff = VintageFontFamily();

        const string title = "PHOTOEXTRACTOR";
        float size = Math.Max(14f, ph / 9f);
        using var font = new Font(ff, size, FontStyle.Bold, GraphicsUnit.Pixel);

        // Mesure précise (GenericTypographic évite les marges GDI parasites)
        var sz  = g.MeasureString(title, font, PointF.Empty, StringFormat.GenericTypographic);
        float tx = (pw - sz.Width)  / 2f;
        float ty = ph / 2f + ph * 0.06f;   // légèrement sous le centre (icône au-dessus)

        // ── Icône de la form (zoomée, dégradée) ───────────────────────────────
        if (Icon != null)
        {
            int iconSize = (int)(Math.Min(pw, ph) * 0.20f);
            using var iconBmp = Icon.ToBitmap();
            var attr = new System.Drawing.Imaging.ImageAttributes();
            attr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.28f });
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(iconBmp,
                new Rectangle((pw - iconSize) / 2, (int)(ty - iconSize - size * 0.9f), iconSize, iconSize),
                0, 0, iconBmp.Width, iconBmp.Height, GraphicsUnit.Pixel, attr);
            g.InterpolationMode = InterpolationMode.Bilinear;
        }

        // ── Lignes décoratives encadrant le titre ─────────────────────────────
        float gap   = size * 0.35f;
        float lineW = sz.Width * 1.08f;
        float lx    = (pw - lineW) / 2f;
        using var lp1 = new System.Drawing.Pen(Color.FromArgb(55, 220, 205, 170), 1f);
        using var lp2 = new System.Drawing.Pen(Color.FromArgb(26, 220, 205, 170), 1f);
        // au-dessus
        g.DrawLine(lp1, lx, ty - gap,        lx + lineW, ty - gap);
        g.DrawLine(lp2, lx, ty - gap - 4f,   lx + lineW, ty - gap - 4f);
        // en dessous
        g.DrawLine(lp1, lx, ty + sz.Height + gap,      lx + lineW, ty + sz.Height + gap);
        g.DrawLine(lp2, lx, ty + sz.Height + gap + 4f, lx + lineW, ty + sz.Height + gap + 4f);

        // ── Texte principal ───────────────────────────────────────────────────
        using var textBrush = new SolidBrush(Color.FromArgb(62, 230, 215, 185));
        g.DrawString(title, font, textBrush, tx, ty, StringFormat.GenericTypographic);
    }

    static Bitmap GenerateCinemaBg(int w, int h)
    {
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                bmp.PixelFormat);
        var row = new int[w];
        var rng = new Random(7);
        double cx = w / 2.0, cy = h / 2.0;
        double maxDist = Math.Sqrt(cx * cx + cy * cy);

        // Poussières épars
        var dust = new HashSet<(int, int)>();
        int dustCount = Math.Max(1, w * h / 3500);
        while (dust.Count < dustCount) dust.Add((rng.Next(w), rng.Next(h)));

        // Griffure verticale douce
        int scratchX = (int)(cx + rng.Next(-w / 5, w / 5));

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double dx = x - cx, dy = y - cy;
                double distSq = (dx * dx + dy * dy) / (maxDist * maxDist);

                // Faisceau projecteur + vignette
                double beam = Math.Exp(-distSq * 2.2);
                double vig  = 1.0 - distSq * 0.88;

                // Variation basse fréquence lisse (pas de blocs)
                double wave = Math.Sin(x / (w * 0.18)) * Math.Cos(y / (h * 0.14)) * 5.0
                            + Math.Sin(x / (w * 0.07) + 1.1) * 3.0;

                // Grain fin uniquement (per-pixel, faible amplitude)
                int fine = rng.Next(0, 11);

                double base_ = (5 + fine + wave) * vig + beam * 28;

                double warmth = beam * 0.38;
                int r = Math.Clamp((int)(base_ * (1.0 + warmth * 0.18)), 0, 255);
                int g = Math.Clamp((int)(base_ * (1.0 + warmth * 0.06)), 0, 255);
                int b = Math.Clamp((int)(base_ * (1.0 - warmth * 0.12)), 0, 255);

                // Griffure gaussienne très subtile
                double sd = x - scratchX;
                double scratch = Math.Exp(-sd * sd / 6.0) * 6;
                r = Math.Clamp(r + (int)scratch, 0, 255);
                g = Math.Clamp(g + (int)(scratch * 0.9), 0, 255);

                // Poussière
                if (dust.Contains((x, y)))
                { r = Math.Clamp(r + 55, 0, 255); g = Math.Clamp(g + 50, 0, 255); b = Math.Clamp(b + 38, 0, 255); }

                row[x] = (255 << 24) | (r << 16) | (g << 8) | b;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), w);
        }
        bmp.UnlockBits(data);
        return bmp;
    }
}
