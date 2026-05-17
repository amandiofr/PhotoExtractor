using OpenCvSharp;
using System.Drawing;

partial class MainForm
{
    LineSegmentPoint[] DetectLinesLOCR()
    {
        if (edges == null) return Array.Empty<LineSegmentPoint>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        locrStep1Runs = ScanHorizontalRuns(edges);
        SetStepTiming(0, sw);
        locrStep2Segs = ComputeStep2(locrStep1Runs);
        SetStepTiming(1, sw);
        locrStep3Segs = ComputeStep3(locrStep2Segs);
        SetStepTiming(2, sw);
        locrStep4Segs = ComputeStep4(locrStep3Segs);
        SetStepTiming(3, sw);
        return Array.Empty<LineSegmentPoint>();
    }

    void SetStepTiming(int step, System.Diagnostics.Stopwatch sw)
    {
        if (locrStepTimings.Count > step)
            locrStepTimings[step].Text = $"{sw.ElapsedMilliseconds} ms";
        sw.Restart();
    }

    List<Step2Segment> ComputeStep4(List<Step2Segment> segs)
    {
        int n = segs.Count;
        if (n < 2) return new List<Step2Segment>(segs);

        // Précalcul : direction normalisée, centre, longueur
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

        // Tri par Y du haut pour court-circuit spatial sur le gap
        float[] topY = new float[n], botY = new float[n];
        for (int i = 0; i < n; i++)
        {
            topY[i] = MathF.Min(segs[i].P1.Y, segs[i].P2.Y);
            botY[i] = MathF.Max(segs[i].P1.Y, segs[i].P2.Y);
        }
        float maxGapAll = segs.Max(s => s.AvgWidth) * 20f;
        int[] order = Enumerable.Range(0, n).OrderBy(i => topY[i]).ToArray();

        // Union-Find
        int[] parent = Enumerable.Range(0, n).ToArray();
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int ii = 0; ii < n; ii++)
        {
            int i = order[ii];
            for (int jj = ii + 1; jj < n; jj++)
            {
                int j = order[jj];
                if (topY[j] > botY[i] + maxGapAll) break; // tous les j suivants sont encore plus loin
                // 1. Similarité de largeur (facteur 2)
                if (MathF.Min(segs[i].AvgWidth, segs[j].AvgWidth) * 2f <
                    MathF.Max(segs[i].AvgWidth, segs[j].AvgWidth)) continue;

                // 2. Similarité d'orientation
                if (MathF.Abs(dir[i].x * dir[j].x + dir[i].y * dir[j].y) < cosMaxAngle) continue;

                // 3. Colinéarité : perp des extrémités les plus proches vers la ligne de l'autre segment
                // Court-circuit : si la distance centre-à-centre dépasse maxPerp + demi-longueurs, impossible
                float maxPerp = (segs[i].AvgWidth + segs[j].AvgWidth) / 2f * 4f;
                float ecx = cen[j].x - cen[i].x, ecy = cen[j].y - cen[i].y;
                float cenDist2 = ecx * ecx + ecy * ecy;
                float maxReach = maxPerp + (len[i] + len[j]) / 2f;
                if (cenDist2 > maxReach * maxReach) continue;

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

                // 4. Gap le long de la direction
                float tj1 = (segs[j].P1.X - segs[i].P1.X) * dir[i].x + (segs[j].P1.Y - segs[i].P1.Y) * dir[i].y;
                float tj2 = (segs[j].P2.X - segs[i].P1.X) * dir[i].x + (segs[j].P2.Y - segs[i].P1.Y) * dir[i].y;
                float jLo = MathF.Min(tj1, tj2), jHi = MathF.Max(tj1, tj2);
                float gap = MathF.Max(0, MathF.Max(-jHi, jLo - len[i]));
                float maxGap = (segs[i].AvgWidth + segs[j].AvgWidth) / 2f * 20f;
                if (gap > maxGap) continue;

                Union(i, j);
            }
        }

        // Regroupement
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!groups.TryGetValue(root, out var g)) groups[root] = g = new();
            g.Add(i);
        }

        // Fusion de chaque groupe
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
        var lens4 = result.Select(s =>
            MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y)))
            .OrderBy(l => l).ToList();
        float median4 = lens4[lens4.Count / 2];
        return result.Where(s =>
            MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y)) > 2f * median4)
            .ToList();
    }

    List<Step2Segment> ComputeStep3(List<Step2Segment> segs)
    {
        var result = new List<Step2Segment>();
        foreach (var seg in segs)
        {
            float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 2f * seg.AvgWidth) continue;
            if (MathF.Abs(dy) < 2f * MathF.Abs(dx)) continue;
            result.Add(seg);
        }
        if (result.Count < 2) return result;
        var lengths = result.Select(s =>
            MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y)))
            .OrderBy(l => l).ToList();
        float median = lengths[lengths.Count / 2];
        return result.Where(s =>
            MathF.Sqrt((s.P2.X-s.P1.X)*(s.P2.X-s.P1.X) + (s.P2.Y-s.P1.Y)*(s.P2.Y-s.P1.Y)) > 1.5f * median)
            .ToList();
    }

    List<Step2Segment> ComputeStep2(List<WhiteRun> runs)
    {
        int n = runs.Count;
        if (n == 0) return new();

        // Union-Find
        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        // Tableaux plats pour accès sans double indirection
        var midX  = new float[n];
        var width = new float[n];
        for (int i = 0; i < n; i++) { midX[i] = runs[i].MidX; width[i] = runs[i].Width; }

        // Groupe les runs de lignes adjacentes dont les étendues X se chevauchent
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
                // Avance lo : b dont le bord droit est < bord gauche de a ne peut plus jamais correspondre
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

        // Regroupe par racine — ignore immédiatement les runs isolés (groupe de taille 1)
        var groupSize = new Dictionary<int, int>(n / 4);
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            groupSize.TryGetValue(root, out int sz);
            groupSize[root] = sz + 1;
        }
        var groups = new Dictionary<int, List<int>>(groupSize.Count);
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (groupSize[root] < 2) continue;
            if (!groups.TryGetValue(root, out var g)) groups[root] = g = new List<int>(groupSize[root]);
            g.Add(i);
        }

        var result = new List<Step2Segment>(groups.Count * 2);

        foreach (var g in groups.Values)
        {
            // Tri par Y (puis X à Y égal)
            g.Sort((ia, ib) => runs[ia].Y != runs[ib].Y
                ? runs[ia].Y.CompareTo(runs[ib].Y)
                : midX[ia].CompareTo(midX[ib]));

            float groupAvgW = 0;
            for (int k = 0; k < g.Count; k++) groupAvgW += width[g[k]];
            groupAvgW /= g.Count;
            float eps = groupAvgW; // tolérance RDP : 1× largeur moyenne du groupe

            // Régression linéaire sur g[from..to] → un Step2Segment
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
                    p1 = new PointF(mx, minY + 0.5f); p2 = new PointF(mx, maxY + 0.5f);
                }
                else
                {
                    float a = (cnt * sxy - sy * sx) / det;
                    float b = (sx - a * sy) / cnt;
                    p1 = new PointF(a * (minY + 0.5f) + b, minY + 0.5f);
                    p2 = new PointF(a * (maxY + 0.5f) + b, maxY + 0.5f);
                }
                result.Add(new Step2Segment(p1, p2, avgW));
            }

            // Ramer-Douglas-Peucker : découpe si déviation perpendiculaire max > eps
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
        }

        return result;
    }

    // Remplace le coin haut-gauche de l'image edges par un patron de test :
    // ligne 0 et colonne 0 : successions de n pixels blancs + n pixels noirs (n=1..10)
    // intérieur : blanc si la colonne ET la ligne sont blanches dans le patron
    static void DrawTestPattern(Mat edges)
    {
        const int N = 10;
        // Pattern : W(1)B(1) W(2)B(2) ... W(N)B(N)
        // Taille totale = 2 * sum(n, 1..N) = N*(N+1)
        int totalSize = N * (N + 1); // = 110
        if (edges.Rows < totalSize || edges.Cols < totalSize) return;

        var pat = new byte[totalSize];
        int pos = 0;
        for (int n = 1; n <= N; n++)
        {
            for (int i = 0; i < n; i++) pat[pos++] = 255;
            for (int i = 0; i < n; i++) pat[pos++] = 0;
        }

        for (int x = 0; x < totalSize; x++) edges.At<byte>(0, x) = pat[x]; // ligne 0
        for (int y = 0; y < totalSize; y++) edges.At<byte>(y, 0) = pat[y]; // colonne 0

        for (int y = 1; y < totalSize; y++)
            for (int x = 1; x < totalSize; x++)
                edges.At<byte>(y, x) = (pat[x] == 255 && pat[y] == 255) ? (byte)255 : (byte)0;
    }

    internal void DrawLocrOverlays(Graphics g)
    {
        if (image == null) return;
        var (ox, oy, ow, oh) = ZoomedRect();
        float scX = ow / image.Width, scY = oh / image.Height;

        // Étape 1 : runs horizontaux (rectangle orange + point central)
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

        // Étape 2 : segments groupés (rectangle cyan + ligne médiane)
        if (locrStepChecks.Count > 1 && locrStepChecks[1].Checked && locrStep2Segs.Count > 0)
        {
            using var pen = new System.Drawing.Pen(Color.FromArgb(200, 0, 210, 255), 1f);
            foreach (var seg in locrStep2Segs)
            {
                float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float nx, ny;
                if (len < 0.001f) { nx = seg.AvgWidth / 2f; ny = 0f; }
                else { nx = -dy / len * seg.AvgWidth / 2f; ny = dx / len * seg.AvgWidth / 2f; }
                PointF S(float ix, float iy) => new(ox + ix * scX, oy + iy * scY);
                var pts = new PointF[]
                {
                    S(seg.P1.X + nx, seg.P1.Y + ny), S(seg.P1.X - nx, seg.P1.Y - ny),
                    S(seg.P2.X - nx, seg.P2.Y - ny), S(seg.P2.X + nx, seg.P2.Y + ny),
                };
                g.DrawPolygon(pen, pts);
                g.DrawLine(pen, S(seg.P1.X, seg.P1.Y), S(seg.P2.X, seg.P2.Y));
            }
        }

        // Étape 3 : segments filtrés (vertical, longueur >= 2× largeur)
        if (locrStepChecks.Count > 2 && locrStepChecks[2].Checked && locrStep3Segs.Count > 0)
        {
            using var pen = new System.Drawing.Pen(Color.FromArgb(220, 60, 120, 255), 1f);
            foreach (var seg in locrStep3Segs)
            {
                float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float nx, ny;
                if (len < 0.001f) { nx = seg.AvgWidth / 2f; ny = 0f; }
                else { nx = -dy / len * seg.AvgWidth / 2f; ny = dx / len * seg.AvgWidth / 2f; }
                PointF S(float ix, float iy) => new(ox + ix * scX, oy + iy * scY);
                var pts = new PointF[]
                {
                    S(seg.P1.X + nx, seg.P1.Y + ny), S(seg.P1.X - nx, seg.P1.Y - ny),
                    S(seg.P2.X - nx, seg.P2.Y - ny), S(seg.P2.X + nx, seg.P2.Y + ny),
                };
                g.DrawPolygon(pen, pts);
                g.DrawLine(pen, S(seg.P1.X, seg.P1.Y), S(seg.P2.X, seg.P2.Y));
            }
        }

        // Étape 4 : segments recollés (rouge)
        if (locrStepChecks.Count > 3 && locrStepChecks[3].Checked && locrStep4Segs.Count > 0)
        {
            using var pen = new System.Drawing.Pen(Color.FromArgb(220, 220, 50, 50), 1f);
            foreach (var seg in locrStep4Segs)
            {
                float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                float nx, ny;
                if (len < 0.001f) { nx = seg.AvgWidth / 2f; ny = 0f; }
                else { nx = -dy / len * seg.AvgWidth / 2f; ny = dx / len * seg.AvgWidth / 2f; }
                PointF S(float ix, float iy) => new(ox + ix * scX, oy + iy * scY);
                var pts = new PointF[]
                {
                    S(seg.P1.X + nx, seg.P1.Y + ny), S(seg.P1.X - nx, seg.P1.Y - ny),
                    S(seg.P2.X - nx, seg.P2.Y - ny), S(seg.P2.X + nx, seg.P2.Y + ny),
                };
                g.DrawPolygon(pen, pts);
                g.DrawLine(pen, S(seg.P1.X, seg.P1.Y), S(seg.P2.X, seg.P2.Y));
            }
        }

    }

    // Scan horizontal : pour chaque ligne de l'image edges (binaire),
    // retourne tous les runs blancs avec leur ligne, centre X et largeur.
    List<WhiteRun> ScanHorizontalRuns(Mat edges)
    {
        int rows = edges.Rows, cols = edges.Cols;
        int step = (int)edges.Step();
        var data = new byte[rows * step];
        System.Runtime.InteropServices.Marshal.Copy(edges.Data, data, 0, data.Length);

        // Scan parallèle : chaque ligne est indépendante
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

        // Fusion dans l'ordre (préserve le tri Y croissant)
        var runs = new List<WhiteRun>(rows * 8);
        for (int y = 0; y < rows; y++)
            if (perRow[y] != null) runs.AddRange(perRow[y]);
        return runs;
    }
}
