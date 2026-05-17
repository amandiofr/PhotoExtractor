using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;

partial class MainForm
{
    // mode : -1=aucun  0-3=bord actif  4=refine  5=parallel
    void SetActiveMode(int mode)
    {
        activeBorderIdx = (mode is >= 0 and <= 3) ? mode : -1;
        refineMode      = mode == 4;
        parallelMode    = mode == 5;

        for (int i = 0; i < 4; i++)
        {
            bool active = i == activeBorderIdx;
            btnBorders[i].BackColor = active ? Color.FromArgb(80, 80, 80) : Color.FromArgb(58, 58, 58);
            btnBorders[i].FlatAppearance.BorderColor = active ? BorderWinColors[i] : btnBorders[i].BackColor;
            btnBorders[i].FlatAppearance.BorderSize = active ? 2 : 0;
        }
        btnRefine.BackColor                    = refineMode   ? Color.FromArgb(100, 70, 0) : Color.FromArgb(58, 58, 58);
        btnRefine.FlatAppearance.BorderColor   = refineMode   ? Color.FromArgb(255, 160, 0) : btnRefine.BackColor;
        btnRefine.FlatAppearance.BorderSize    = refineMode   ? 2 : 0;
        btnParallel.BackColor                  = parallelMode ? Color.FromArgb(0, 80, 100)  : Color.FromArgb(58, 58, 58);
        btnParallel.FlatAppearance.BorderColor = parallelMode ? Color.FromArgb(0, 200, 255) : btnParallel.BackColor;
        btnParallel.FlatAppearance.BorderSize  = parallelMode ? 2 : 0;

        labelStatus.Text = mode switch
        {
            >= 0 and <= 3 => $"Click on border {mode + 1} in the image.",
            4 => "Refine mode: click near a border to adjust it.",
            5 => "Parallel mode: click near a border to snap it parallel to its opposite.",
            _ => "Selection cleared."
        };
    }

    void OnImageMouseMove(object? sender, MouseEventArgs e)
    {
        if (panDragging)
        {
            viewPan = new PointF(panAtDragStart.X + e.X - panDragStart.X, panAtDragStart.Y + e.Y - panDragStart.Y);
            ClampPan();
            pictureBox.Invalidate();
            return;
        }
        if (image == null)
        {
            if (hoveredLine != null || hoveredParallelRefIdx >= 0 || hoveredBorderIdx >= 0)
            { hoveredLine = null; hoveredParallelRefIdx = -1; hoveredBorderIdx = -1; pictureBox.Invalidate(); }
            return;
        }
        var imgPt = DisplayToImage(e.Location);
        if (imgPt == null)
        {
            if (hoveredLine != null || hoveredParallelRefIdx >= 0 || hoveredBorderIdx >= 0)
            { hoveredLine = null; hoveredParallelRefIdx = -1; hoveredBorderIdx = -1; pictureBox.Invalidate(); }
            return;
        }
        var p = new Point2f(imgPt.Value.X, imgPt.Value.Y);

        // Drag de coin en cours
        if (draggingCornerIdx >= 0)
        {
            var scaledP = new Point2f(
                dragCornerStartPos.X + (p.X - dragCornerStartMouse.X) / 3f,
                dragCornerStartPos.Y + (p.Y - dragCornerStartMouse.Y) / 3f);
            loupeCenterImg = scaledP;
            selectedBorders[dragCornerBorderA] = LineThroughPoints(scaledP, dragCornerFixedA);
            selectedBorders[dragCornerBorderB] = LineThroughPoints(scaledP, dragCornerFixedB);
            dragMoved = true;
            CleanDoneInsideCurrentQuad();
            pictureBox.Invalidate();
            return;
        }

        // Drag en cours : déplacer le bord en gardant son angle
        if (draggingBorderIdx >= 0)
        {
            var b = selectedBorders[draggingBorderIdx]!.Value;
            float dx = b.P2.X - b.P1.X, dy = b.P2.Y - b.P1.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f)
            {
                float ext = (image!.Width + image.Height) / 2f;
                selectedBorders[draggingBorderIdx] = new LineSegmentPoint(
                    new OpenCvSharp.Point((int)(p.X - dx / len * ext), (int)(p.Y - dy / len * ext)),
                    new OpenCvSharp.Point((int)(p.X + dx / len * ext), (int)(p.Y + dy / len * ext)));
                dragMoved = true;
                CleanDoneInsideCurrentQuad();
                pictureBox.Invalidate();
            }
            return;
        }

        // En mode refine, annuler le hover de bord si le curseur est sur un handle de coin
        if (refineMode && selectedBorders.All(b => b != null))
        {
            const float hr = 10f;
            var cs = ComputeCorners();
            bool nearHandle = cs.Any(c => { var d = ImageToDisplay(c); if (d == null) return false; float ddx = e.X - d.Value.X, ddy = e.Y - d.Value.Y; return ddx * ddx + ddy * ddy <= hr * hr; });
            if (nearHandle)
            {
                if (hoveredBorderIdx >= 0) { hoveredBorderIdx = -1; pictureBox.Cursor = Cursors.Default; pictureBox.Invalidate(); }
                return;
            }
        }

        // Hover sur un bord sélectionné (tous modes)
        {
            int newHovBorder = -1;
            float threshold = 8f;
            bool allSet = selectedBorders.All(b => b != null);
            var segs = allSet ? ComputeBorderSegments() : null;
            for (int i = 0; i < 4; i++)
            {
                if (selectedBorders[i] == null) continue;
                float dist;
                if (segs != null)
                {
                    var sp1 = ImageToDisplay(segs[i].Item1);
                    var sp2 = ImageToDisplay(segs[i].Item2);
                    if (sp1 == null || sp2 == null) continue;
                    dist = DistPtSegF(e.Location, sp1.Value, sp2.Value);
                }
                else
                {
                    var b = selectedBorders[i]!.Value;
                    var sp1 = ImageToDisplay(new Point2f(b.P1.X, b.P1.Y));
                    var sp2 = ImageToDisplay(new Point2f(b.P2.X, b.P2.Y));
                    if (sp1 == null || sp2 == null) continue;
                    dist = DistPtLineF(e.Location, sp1.Value, sp2.Value);
                }
                if (dist < threshold) { threshold = dist; newHovBorder = i; }
            }
            if (newHovBorder != hoveredBorderIdx)
            {
                hoveredBorderIdx = newHovBorder;
                pictureBox.Cursor = newHovBorder >= 0 ? Cursors.SizeAll : Cursors.Default;
                pictureBox.Invalidate();
            }
            if (hoveredBorderIdx >= 0) return; // pas de hover Hough si on est sur un bord
        }

        // Hover Hough – mode parallel
        if (parallelMode && selectedBorders.All(b => b != null))
        {
            int nearestIdx = -1;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (selectedBorders[i] is not { } b) continue;
                float d = DistPointToLine(p, new Point2f(b.P1.X, b.P1.Y), new Point2f(b.P2.X, b.P2.Y));
                if (d < nearestDist) { nearestDist = d; nearestIdx = i; }
            }
            int refIdx = nearestIdx >= 0 ? FindParallelRefIdx(nearestIdx) : -1;
            if (refIdx != hoveredParallelRefIdx || hoveredLine != null)
            {
                hoveredLine = null;
                hoveredParallelRefIdx = refIdx;
                pictureBox.Invalidate();
            }
            return;
        }

        // Hover Hough – lignes
        hoveredParallelRefIdx = -1;
        if (houghLines == null || houghLines.Length == 0)
        {
            if (hoveredLine != null) { hoveredLine = null; pictureBox.Invalidate(); }
            return;
        }
        float bestDist = float.MaxValue;
        LineSegmentPoint bestSeg = default;
        foreach (var seg in houghLines)
        {
            float d = DistPointToSegment(p, new Point2f(seg.P1.X, seg.P1.Y), new Point2f(seg.P2.X, seg.P2.Y));
            if (d < bestDist) { bestDist = d; bestSeg = seg; }
        }
        LineSegmentPoint? newHover = bestSeg;
        if (newHover?.P1 != hoveredLine?.P1 || newHover?.P2 != hoveredLine?.P2)
        {
            hoveredLine = newHover;
            pictureBox.Invalidate();
        }
    }

    void OnImageMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            panDragging = true; panDragStart = e.Location; panAtDragStart = viewPan;
            pictureBox.Cursor = Cursors.Hand;
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        // En mode refine, priorité aux handles de coin
        if (refineMode && selectedBorders.All(b => b != null))
        {
            var corners = ComputeCorners();
            const float hr = 10f;
            for (int i = 0; i < 4; i++)
            {
                var dc = ImageToDisplay(corners[i]);
                if (dc == null) continue;
                float dx = e.X - dc.Value.X, dy = e.Y - dc.Value.Y;
                if (dx * dx + dy * dy <= hr * hr)
                {
                    draggingCornerIdx = i;
                    var (bA, fA, bB, fB) = CornerDragInfo(i, corners);
                    dragCornerBorderA = bA; dragCornerFixedA = fA;
                    dragCornerBorderB = bB; dragCornerFixedB = fB;
                    dragCornerStartPos = corners[i];
                    var spt = DisplayToImage(e.Location);
                    dragCornerStartMouse = spt != null ? new Point2f(spt.Value.X, spt.Value.Y) : corners[i];
                    loupeCenterImg = corners[i];
                    dragMoved = false;
                    pictureBox.Cursor = Cursors.SizeAll;
                    pictureBox.Invalidate();
                    return;
                }
            }
        }

        if (hoveredBorderIdx < 0) return;
        draggingBorderIdx = hoveredBorderIdx;
        dragMoved = false;
        pictureBox.Cursor = Cursors.SizeAll;
    }

    void OnImageMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            panDragging = false;
            pictureBox.Cursor = hoveredBorderIdx >= 0 ? Cursors.SizeAll : Cursors.Default;
            return;
        }
        if (draggingCornerIdx >= 0)
        {
            draggingCornerIdx = -1;
            pictureBox.Cursor = Cursors.Default;
            return;
        }
        if (draggingBorderIdx < 0) return;
        draggingBorderIdx = -1;
        pictureBox.Cursor = hoveredBorderIdx >= 0 ? Cursors.SizeAll : Cursors.Default;
    }

    void OnImageClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (dragMoved) { dragMoved = false; return; }
        if (hitRotLeft.Contains(e.Location))  { extractOrientation = (extractOrientation + 3) % 4; orientationTouched = true; pictureBox.Invalidate(); return; }
        if (hitRotRight.Contains(e.Location)) { extractOrientation = (extractOrientation + 1) % 4; orientationTouched = true; pictureBox.Invalidate(); return; }

        if (image == null || houghLines == null || houghLines.Length == 0) return;
        var imgPt = DisplayToImage(e.Location);
        if (imgPt == null) return;
        var p = new Point2f(imgPt.Value.X, imgPt.Value.Y);

        if (parallelMode)
        {
            ParallelizeNearBorder(p);
            return;
        }

        if (refineMode)
        {
            RefineNearestBorder(p);
            return;
        }

        if (activeBorderIdx < 0) return;
        if (hoveredLine == null) return;

        bool wereAllSet = selectedBorders.All(b => b != null);
        selectedBorders[activeBorderIdx] = hoveredLine.Value;
        bool allSet = selectedBorders.All(b => b != null);
        CleanDoneInsideCurrentQuad();

        if (wereAllSet)
        {
            labelStatus.Text = $"Border {activeBorderIdx + 1} updated — click again to adjust.";
        }
        else
        {
            if (allSet)
            {
                SetActiveMode(4);
            }
            else
            {
                int next = -1;
                for (int i = 1; i <= 4; i++)
                {
                    int candidate = (activeBorderIdx + i) % 4;
                    if (selectedBorders[candidate] == null) { next = candidate; break; }
                }
                SetActiveMode(next);
            }
            btnExtractManual.Enabled = allSet;
            btnRefine.Enabled = allSet;
            btnParallel.Enabled = allSet;
        }

        UpdateDisplay();
    }

    int FindParallelRefIdx(int clickedIdx)
    {
        var others = Enumerable.Range(0, 4).Where(i => i != clickedIdx && selectedBorders[i] != null).ToArray();
        foreach (int i in others)
        {
            bool hasPair = others.Where(j => j != i)
                                 .Any(j => AngleDiff(LineAngle(selectedBorders[i]!.Value),
                                                     LineAngle(selectedBorders[j]!.Value)) < 20f);
            if (!hasPair) return i;
        }
        float clickedAngle = LineAngle(selectedBorders[clickedIdx]!.Value);
        int best = -1;
        float bestDiff = float.MaxValue;
        foreach (int i in others)
        {
            float diff = AngleDiff(clickedAngle, LineAngle(selectedBorders[i]!.Value));
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    void ParallelizeNearBorder(Point2f p)
    {
        int clickedIdx = -1;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            if (selectedBorders[i] is not { } b) continue;
            float d = DistPointToLine(p, new Point2f(b.P1.X, b.P1.Y), new Point2f(b.P2.X, b.P2.Y));
            if (d < nearestDist) { nearestDist = d; clickedIdx = i; }
        }
        if (clickedIdx < 0) return;

        int oppositeIdx = FindParallelRefIdx(clickedIdx);
        if (oppositeIdx < 0) return;

        var opp = selectedBorders[oppositeIdx]!.Value;
        float dx = opp.P2.X - opp.P1.X, dy = opp.P2.Y - opp.P1.Y;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;
        float ext = (image!.Width + image.Height) / 2f;
        selectedBorders[clickedIdx] = new LineSegmentPoint(
            new OpenCvSharp.Point((int)(p.X - dx / len * ext), (int)(p.Y - dy / len * ext)),
            new OpenCvSharp.Point((int)(p.X + dx / len * ext), (int)(p.Y + dy / len * ext)));
        CleanDoneInsideCurrentQuad();
        UpdateDisplay();
    }

    void RefineNearestBorder(Point2f p)
    {
        if (hoveredLine == null) return;

        int nearestIdx = -1;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            if (selectedBorders[i] is not { } b) continue;
            float d = DistPointToLine(p, new Point2f(b.P1.X, b.P1.Y), new Point2f(b.P2.X, b.P2.Y));
            if (d < nearestDist) { nearestDist = d; nearestIdx = i; }
        }
        if (nearestIdx < 0) return;

        selectedBorders[nearestIdx] = hoveredLine.Value;
        CleanDoneInsideCurrentQuad();
        UpdateDisplay();
    }

    // Retourne (borderA, coinFixeA, borderB, coinFixeB) pour le drag du coin k
    (int, Point2f, int, Point2f) CornerDragInfo(int k, Point2f[] corners)
    {
        var lines = selectedBorders.Select(b => b!.Value).ToArray();
        bool[] isH = lines.Select(l => Math.Abs(l.P2.X - l.P1.X) >= Math.Abs(l.P2.Y - l.P1.Y)).ToArray();
        var hIdx = Enumerable.Range(0, 4).Where(i =>  isH[i]).ToArray();
        var vIdx = Enumerable.Range(0, 4).Where(i => !isH[i]).ToArray();
        if (hIdx.Length == 2 && vIdx.Length == 2)
            return k switch
            {
                0 => (hIdx[0], corners[1], vIdx[0], corners[3]),
                1 => (hIdx[0], corners[0], vIdx[1], corners[2]),
                2 => (hIdx[1], corners[3], vIdx[1], corners[1]),
                3 => (hIdx[1], corners[2], vIdx[0], corners[0]),
                _ => throw new InvalidOperationException()
            };
        // Cas séquentiel : corner[k] = lines[k] ∩ lines[(k+1)%4]
        return (k, corners[(k + 3) % 4], (k + 1) % 4, corners[(k + 1) % 4]);
    }

    // Crée un LineSegmentPoint passant par p et q, étendu aux dimensions de l'image
    LineSegmentPoint LineThroughPoints(Point2f p, Point2f q)
    {
        float dx = q.X - p.X, dy = q.Y - p.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return selectedBorders[0]!.Value;
        float ext = (image!.Width + image.Height) / 2f;
        return new LineSegmentPoint(
            new OpenCvSharp.Point((int)(p.X - dx / len * ext), (int)(p.Y - dy / len * ext)),
            new OpenCvSharp.Point((int)(p.X + dx / len * ext), (int)(p.Y + dy / len * ext)));
    }

    void ClearBorders()
    {
        for (int i = 0; i < 4; i++) selectedBorders[i] = null;
        btnExtractManual.Enabled = false;
        btnRefine.Enabled = false;
        btnParallel.Enabled = false;
        SetActiveMode(0);
        UpdateDisplay();
    }

    void CleanDoneInsideCurrentQuad()
    {
        if (doneCenters.Count == 0 || !selectedBorders.All(b => b != null)) return;
        var quad = ComputeCorners();
        if (doneCenters.RemoveAll(p => IsInsideQuad(p, quad)) > 0)
            pictureBox.Invalidate();
    }
}
