using OpenCvSharp;
using System.Drawing;

partial class MainForm
{
    LineSegmentPoint[] DetectLinesLOCR()
    {
        if (edges == null) return Array.Empty<LineSegmentPoint>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        locrStep1Runs = Step1_ScanHorizontalRuns(edges);
        var hRuns     = Step1_ScanVerticalRuns(edges);
        SetStepTiming(0, sw);

        var vSegs2 = Step2_GroupRunsIntoSegments(locrStep1Runs, horizontal: false);
        var hSegs2 = Step2_GroupRunsIntoSegments(hRuns,         horizontal: true);
        locrStep2Segs = vSegs2.Concat(hSegs2).ToList();
        SetStepTiming(1, sw);

        var vSegs3 = Step3_FilterByOrientation(vSegs2, horizontal: false);
        var hSegs3 = Step3_FilterByOrientation(hSegs2, horizontal: true);
        locrStep3Segs = vSegs3.Concat(hSegs3).ToList();
        SetStepTiming(2, sw);

        var vSegs4 = Step4_MergeCollinearSegments(vSegs3, horizontal: false);
        var hSegs4 = Step4_MergeCollinearSegments(hSegs3, horizontal: true);
        locrStep4Segs = vSegs4.Concat(hSegs4).ToList();
        SetStepTiming(3, sw);

        return locrStep4Segs.Select(s => new LineSegmentPoint(
            new OpenCvSharp.Point((int)MathF.Round(s.P1.X), (int)MathF.Round(s.P1.Y)),
            new OpenCvSharp.Point((int)MathF.Round(s.P2.X), (int)MathF.Round(s.P2.Y))
        )).ToArray();
    }

    void SetStepTiming(int step, System.Diagnostics.Stopwatch sw)
    {
        if (locrStepTimings.Count > step)
            locrStepTimings[step].Text = $"{sw.ElapsedMilliseconds} ms";
        sw.Restart();
    }

    // ── Step 1 ───────────────────────────────────────────────────────────────

    // Scan horizontal → détecte les lignes verticales.
    // WhiteRun : Y = ligne, MidX = centre X du run, Width = largeur.
    List<WhiteRun> Step1_ScanHorizontalRuns(Mat edges)
    {
        int rows = edges.Rows, cols = edges.Cols;
        int step = (int)edges.Step();
        var data = new byte[rows * step];
        System.Runtime.InteropServices.Marshal.Copy(edges.Data, data, 0, data.Length);

        var perRow = new List<WhiteRun>[rows];
        System.Threading.Tasks.Parallel.For(0, rows, y =>
        {
            var row = new List<WhiteRun>();
            int rowBase = y * step, x = 0;
            while (x < cols)
            {
                if (data[rowBase + x] == 255)
                {
                    int start = x;
                    while (x < cols && data[rowBase + x] == 255) x++;
                    int width = x - start;
                    row.Add(new WhiteRun(y, start + width / 2f, width));
                }
                else x++;
            }
            perRow[y] = row;
        });

        var runs = new List<WhiteRun>(rows * 8);
        for (int y = 0; y < rows; y++)
            if (perRow[y] != null) runs.AddRange(perRow[y]);
        return runs;
    }

    // Scan vertical → détecte les lignes horizontales.
    // Transpose l'image pour réutiliser le scan horizontal avec bon cache locality.
    // WhiteRun résultant : Y = colonne (= X réel), MidX = centre Y du run, Width = hauteur.
    List<WhiteRun> Step1_ScanVerticalRuns(Mat edges)
    {
        using var transposed = new Mat();
        Cv2.Transpose(edges, transposed);
        return Step1_ScanHorizontalRuns(transposed);
    }

    // ── Step 2 ───────────────────────────────────────────────────────────────

    List<Step2Segment> Step2_GroupRunsIntoSegments(List<WhiteRun> runs, bool horizontal)
    {
        int n = runs.Count;
        if (n == 0) return new();

        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        var midX  = new float[n];
        var width = new float[n];
        for (int i = 0; i < n; i++) { midX[i] = runs[i].MidX; width[i] = runs[i].Width; }

        var byRow = new Dictionary<int, List<int>>(n / 4);
        for (int i = 0; i < n; i++)
        {
            int y = runs[i].Y;
            if (!byRow.TryGetValue(y, out var list)) byRow[y] = list = new List<int>(8);
            list.Add(i);
        }

        foreach (var (y, row) in byRow)
        {
            if (!byRow.TryGetValue(y + 1, out var nextRow)) continue;
            int nr = nextRow.Count;
            int lo = 0;
            foreach (int a in row)
            {
                float ax = midX[a], aw = width[a];
                float aLeft = ax - aw / 2f, aRight = ax + aw / 2f;
                while (lo < nr && midX[nextRow[lo]] + width[nextRow[lo]] / 2f < aLeft) lo++;
                for (int bi = lo; bi < nr; bi++)
                {
                    int b = nextRow[bi];
                    float bx = midX[b], bw = width[b];
                    if (bx - bw / 2f > aRight) break;
                    if (Math.Abs(ax - bx) <= (aw + bw) / 2f
                        && Math.Min(aw, bw) * 2 >= Math.Max(aw, bw))
                        Union(a, b);
                }
            }
        }

        var groupSize = new Dictionary<int, int>(n / 4);
        for (int i = 0; i < n; i++) { int root = Find(i); groupSize.TryGetValue(root, out int sz); groupSize[root] = sz + 1; }
        var groups = new Dictionary<int, List<int>>(groupSize.Count);
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (groupSize[root] < 2) continue;
            if (!groups.TryGetValue(root, out var g)) groups[root] = g = new List<int>(groupSize[root]);
            g.Add(i);
        }

        // Les runs sont ajoutés dans l'ordre i=0..n-1 = ordre Y croissant de Step1 → pas besoin de tri.
        // Les groupes sont indépendants → traitement en parallèle.
        var groupList = groups.Values.ToArray();
        var resultParts = new List<Step2Segment>[groupList.Length];

        System.Threading.Tasks.Parallel.For(0, groupList.Length, gi =>
        {
            var g = groupList[gi];
            var localResult = new List<Step2Segment>();

            float groupAvgW = 0;
            for (int k = 0; k < g.Count; k++) groupAvgW += width[g[k]];
            groupAvgW /= g.Count;
            float eps = groupAvgW;

            void Emit(int from, int to)
            {
                if (to <= from) return;
                int cnt = to - from + 1;
                float sy = 0, sx = 0, syy = 0, sxy = 0;
                float minY = float.MaxValue, maxY = float.MinValue, sumW = 0;
                for (int k = from; k <= to; k++)
                {
                    int idx = g[k];
                    float fy = runs[idx].Y, mx = midX[idx], w = width[idx];
                    sy += fy; sx += mx; syy += fy * fy; sxy += mx * fy;
                    if (fy < minY) minY = fy;
                    if (fy > maxY) maxY = fy;
                    sumW += w;
                }
                float avgW = sumW / cnt;
                float det = cnt * syy - sy * sy;
                PointF p1, p2;
                if (MathF.Abs(det) < 1f)
                {
                    float mx = sx / cnt;
                    p1 = horizontal ? new PointF(minY + 0.5f, mx) : new PointF(mx, minY + 0.5f);
                    p2 = horizontal ? new PointF(maxY + 0.5f, mx) : new PointF(mx, maxY + 0.5f);
                }
                else
                {
                    float a = (cnt * sxy - sy * sx) / det;
                    float b = (sx - a * sy) / cnt;
                    float rx1 = a * (minY + 0.5f) + b, rx2 = a * (maxY + 0.5f) + b;
                    p1 = horizontal ? new PointF(minY + 0.5f, rx1) : new PointF(rx1, minY + 0.5f);
                    p2 = horizontal ? new PointF(maxY + 0.5f, rx2) : new PointF(rx2, maxY + 0.5f);
                }
                localResult.Add(new Step2Segment(p1, p2, avgW));
            }

            void Split(int from, int to)
            {
                if (to - from < 2) { Emit(from, to); return; }
                float x0 = midX[g[from]], y0 = runs[g[from]].Y;
                float x1 = midX[g[to]],  y1 = runs[g[to]].Y;
                float dx = x1 - x0, dy = y1 - y0;
                float lineLen = MathF.Sqrt(dx * dx + dy * dy);
                float maxDist = 0; int maxK = from + 1;
                if (lineLen > 0.001f)
                {
                    for (int k = from + 1; k < to; k++)
                    {
                        float ex = midX[g[k]] - x0, ey = runs[g[k]].Y - y0;
                        float d = MathF.Abs(ex * dy - ey * dx) / lineLen;
                        if (d > maxDist) { maxDist = d; maxK = k; }
                    }
                }
                if (maxDist > eps) { Split(from, maxK); Split(maxK, to); }
                else Emit(from, to);
            }

            Split(0, g.Count - 1);
            resultParts[gi] = localResult;
        });

        var result = new List<Step2Segment>(groupList.Length * 2);
        foreach (var part in resultParts) result.AddRange(part);
        return result;
    }

    // ── Step 3 ───────────────────────────────────────────────────────────────

    List<Step2Segment> Step3_FilterByOrientation(List<Step2Segment> segs, bool horizontal)
    {
        var result = new List<Step2Segment>();
        foreach (var seg in segs)
        {
            float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 2f * seg.AvgWidth) continue;
            if (!horizontal && MathF.Abs(dy) < 2f * MathF.Abs(dx)) continue;
            if ( horizontal && MathF.Abs(dx) < 2f * MathF.Abs(dy)) continue;
            result.Add(seg);
        }
        if (result.Count < 2) return result;
        var withLen = result
            .Select(s => (s, len: MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y))))
            .OrderBy(x => x.len).ToList();
        float median = withLen[withLen.Count / 2].len;
        return withLen.Where(x => x.len > 1.5f * median).Select(x => x.s).ToList();
    }

    // ── Step 4 ───────────────────────────────────────────────────────────────

    List<Step2Segment> Step4_MergeCollinearSegments(List<Step2Segment> segs, bool horizontal)
    {
        int n = segs.Count;
        if (n < 2) return new List<Step2Segment>(segs);

        var dir = new (float x, float y)[n];
        var cen = new (float x, float y)[n];
        float[] len = new float[n];
        for (int i = 0; i < n; i++)
        {
            float dx = segs[i].P2.X - segs[i].P1.X, dy = segs[i].P2.Y - segs[i].P1.Y;
            len[i] = MathF.Sqrt(dx * dx + dy * dy);
            dir[i] = len[i] > 0 ? (dx / len[i], dy / len[i]) : (0f, 1f);
            cen[i] = ((segs[i].P1.X + segs[i].P2.X) / 2f, (segs[i].P1.Y + segs[i].P2.Y) / 2f);
        }

        const float cosMaxAngle = 0.9945f; // cos(6°)

        // Tri sur l'axe principal (Y pour vertical, X pour horizontal)
        float[] topY = new float[n], botY = new float[n];
        for (int i = 0; i < n; i++)
        {
            topY[i] = horizontal
                ? MathF.Min(segs[i].P1.X, segs[i].P2.X)
                : MathF.Min(segs[i].P1.Y, segs[i].P2.Y);
            botY[i] = horizontal
                ? MathF.Max(segs[i].P1.X, segs[i].P2.X)
                : MathF.Max(segs[i].P1.Y, segs[i].P2.Y);
        }
        int[] order = Enumerable.Range(0, n).OrderBy(i => topY[i]).ToArray();

        int[] parent = Enumerable.Range(0, n).ToArray();
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int ii = 0; ii < n; ii++)
        {
            int i = order[ii];
            float maxReachI = segs[i].AvgWidth * 30f;
            for (int jj = ii + 1; jj < n; jj++)
            {
                int j = order[jj];
                if (topY[j] > botY[i] + maxReachI) break;

                if (MathF.Min(segs[i].AvgWidth, segs[j].AvgWidth) * 2f <
                    MathF.Max(segs[i].AvgWidth, segs[j].AvgWidth)) continue;

                if (MathF.Abs(dir[i].x * dir[j].x + dir[i].y * dir[j].y) < cosMaxAngle) continue;

                float maxPerp = (segs[i].AvgWidth + segs[j].AvgWidth) / 2f * 4f;
                float ecx = cen[j].x - cen[i].x, ecy = cen[j].y - cen[i].y;
                float maxReach = maxPerp + (len[i] + len[j]) / 2f;
                if (ecx * ecx + ecy * ecy > maxReach * maxReach) continue;

                var epI = segs[i].P1; var epJ = segs[j].P1;
                float minD2ep = float.MaxValue;
                foreach (var a in new[] { segs[i].P1, segs[i].P2 })
                    foreach (var b in new[] { segs[j].P1, segs[j].P2 })
                    {
                        float d2ep = (a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y);
                        if (d2ep < minD2ep) { minD2ep = d2ep; epI = a; epJ = b; }
                    }
                float perpItoJ = MathF.Abs(-dir[i].y * (epJ.X - cen[i].x) + dir[i].x * (epJ.Y - cen[i].y));
                float perpJtoI = MathF.Abs(-dir[j].y * (epI.X - cen[j].x) + dir[j].x * (epI.Y - cen[j].y));
                if (perpItoJ > maxPerp || perpJtoI > maxPerp) continue;

                float tj1 = (segs[j].P1.X - segs[i].P1.X) * dir[i].x + (segs[j].P1.Y - segs[i].P1.Y) * dir[i].y;
                float tj2 = (segs[j].P2.X - segs[i].P1.X) * dir[i].x + (segs[j].P2.Y - segs[i].P1.Y) * dir[i].y;
                float jLo = MathF.Min(tj1, tj2), jHi = MathF.Max(tj1, tj2);
                float gap = MathF.Max(0, MathF.Max(-jHi, jLo - len[i]));
                float maxGap = (segs[i].AvgWidth + segs[j].AvgWidth) / 2f * 20f;
                if (gap > maxGap) continue;

                Union(i, j);
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!groups.TryGetValue(root, out var g)) groups[root] = g = new();
            g.Add(i);
        }

        var result = new List<Step2Segment>();
        foreach (var g in groups.Values)
        {
            if (g.Count == 1) { result.Add(segs[g[0]]); continue; }

            float sumL = g.Sum(k => len[k]);
            float mdx = g.Sum(k => dir[k].x * len[k]) / sumL;
            float mdy = g.Sum(k => dir[k].y * len[k]) / sumL;
            float mNorm = MathF.Sqrt(mdx * mdx + mdy * mdy);
            if (mNorm > 0) { mdx /= mNorm; mdy /= mNorm; }
            else { mdx = 0; mdy = 1; }
            if (mdy < 0) { mdx = -mdx; mdy = -mdy; }

            float rcx = g.Sum(k => cen[k].x * len[k]) / sumL;
            float rcy = g.Sum(k => cen[k].y * len[k]) / sumL;

            float tMin = float.MaxValue, tMax = float.MinValue;
            foreach (int k in g)
            {
                float t1 = (segs[k].P1.X - rcx) * mdx + (segs[k].P1.Y - rcy) * mdy;
                float t2 = (segs[k].P2.X - rcx) * mdx + (segs[k].P2.Y - rcy) * mdy;
                tMin = MathF.Min(tMin, MathF.Min(t1, t2));
                tMax = MathF.Max(tMax, MathF.Max(t1, t2));
            }
            float avgW = g.Sum(k => segs[k].AvgWidth * len[k]) / sumL;
            result.Add(new Step2Segment(
                new PointF(rcx + mdx * tMin, rcy + mdy * tMin),
                new PointF(rcx + mdx * tMax, rcy + mdy * tMax),
                avgW));
        }
        if (result.Count < 2) return result;
        var withLen = result
            .Select(s => (s, len: MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y))))
            .OrderBy(x => x.len).ToList();
        float median4 = withLen[withLen.Count / 2].len;
        return withLen.Where(x => x.len > 2f * median4).Select(x => x.s).ToList();
    }

    // ── Rendu ────────────────────────────────────────────────────────────────

    void DrawSegmentList(Graphics g, List<Step2Segment> segs, Color col, float ox, float oy, float scX, float scY)
    {
        using var pen = new System.Drawing.Pen(col, 1f);
        PointF S(float ix, float iy) => new(ox + ix * scX, oy + iy * scY);
        foreach (var seg in segs)
        {
            float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
            float slen = MathF.Sqrt(dx * dx + dy * dy);
            float nx, ny;
            if (slen < 0.001f) { nx = seg.AvgWidth / 2f; ny = 0f; }
            else { nx = -dy / slen * seg.AvgWidth / 2f; ny = dx / slen * seg.AvgWidth / 2f; }
            g.DrawPolygon(pen, new PointF[]
            {
                S(seg.P1.X + nx, seg.P1.Y + ny), S(seg.P1.X - nx, seg.P1.Y - ny),
                S(seg.P2.X - nx, seg.P2.Y - ny), S(seg.P2.X + nx, seg.P2.Y + ny),
            });
            g.DrawLine(pen, S(seg.P1.X, seg.P1.Y), S(seg.P2.X, seg.P2.Y));
        }
    }

    internal void DrawLocrOverlays(Graphics g)
    {
        if (image == null) return;
        var (ox, oy, ow, oh) = ZoomedRect();
        float scX = ow / image.Width, scY = oh / image.Height;

        if (locrStepChecks.Count > 0 && locrStepChecks[0].Checked && locrStep1Runs.Count > 0)
        {
            using var pen = new System.Drawing.Pen(Color.FromArgb(200, 255, 140, 0), 1f);
            using var dotBrush = new SolidBrush(Color.FromArgb(200, 255, 140, 0));
            float dotR = Math.Max(2f, scY * 0.3f);
            foreach (var run in locrStep1Runs)
            {
                float x1 = ox + (run.MidX - run.Width / 2f) * scX;
                float x2 = ox + (run.MidX + run.Width / 2f) * scX;
                float y1 = oy + run.Y * scY;
                float y2 = oy + (run.Y + 1) * scY;
                if (x2 < 0 || x1 > pictureBox.Width || y2 < 0 || y1 > pictureBox.Height) continue;
                g.DrawRectangle(pen, x1, y1, x2 - x1, y2 - y1);
                float cx = ox + run.MidX * scX;
                float cy = (y1 + y2) / 2f;
                g.FillEllipse(dotBrush, cx - dotR, cy - dotR, dotR * 2, dotR * 2);
            }
        }

        if (locrStepChecks.Count > 1 && locrStepChecks[1].Checked && locrStep2Segs.Count > 0)
            DrawSegmentList(g, locrStep2Segs, Color.FromArgb(200, 0, 210, 255), ox, oy, scX, scY);

        if (locrStepChecks.Count > 2 && locrStepChecks[2].Checked && locrStep3Segs.Count > 0)
            DrawSegmentList(g, locrStep3Segs, Color.FromArgb(220, 60, 120, 255), ox, oy, scX, scY);

        if (locrStepChecks.Count > 3 && locrStepChecks[3].Checked && locrStep4Segs.Count > 0)
            DrawSegmentList(g, locrStep4Segs, Color.FromArgb(220, 220, 50, 50), ox, oy, scX, scY);
    }

    static void DrawTestPattern(Mat edges)
    {
        const int N = 10;
        int totalSize = N * (N + 1);
        if (edges.Rows < totalSize || edges.Cols < totalSize) return;

        var pat = new byte[totalSize];
        int pos = 0;
        for (int n = 1; n <= N; n++)
        {
            for (int i = 0; i < n; i++) pat[pos++] = 255;
            for (int i = 0; i < n; i++) pat[pos++] = 0;
        }
        for (int x = 0; x < totalSize; x++) edges.At<byte>(0, x) = pat[x];
        for (int y = 0; y < totalSize; y++) edges.At<byte>(y, 0) = pat[y];
        for (int y = 1; y < totalSize; y++)
            for (int x = 1; x < totalSize; x++)
                edges.At<byte>(y, x) = (pat[x] == 255 && pat[y] == 255) ? (byte)255 : (byte)0;
    }
}
