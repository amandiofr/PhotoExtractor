using OpenCvSharp;
using System.Drawing;

partial class MainForm
{
    LineSegmentPoint[] DetectLinesLOCR()
    {
        if (edges == null) return Array.Empty<LineSegmentPoint>();
        locrStep1Runs = ScanHorizontalRuns(edges);
        locrStep2Segs = ComputeStep2(locrStep1Runs);
        locrStep3Segs = ComputeStep3(locrStep2Segs);
        locrStep4Segs = ComputeStep4(locrStep3Segs);
        return Array.Empty<LineSegmentPoint>();
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

        // Union-Find
        int[] parent = Enumerable.Range(0, n).ToArray();
        int Find(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                // 1. Similarité de largeur (facteur 2)
                if (MathF.Min(segs[i].AvgWidth, segs[j].AvgWidth) * 2f <
                    MathF.Max(segs[i].AvgWidth, segs[j].AvgWidth)) continue;

                // 2. Similarité d'orientation
                if (MathF.Abs(dir[i].x * dir[j].x + dir[i].y * dir[j].y) < cosMaxAngle) continue;

                // 3. Colinéarité : perp des extrémités les plus proches vers la ligne de l'autre segment
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
                float maxPerp = (segs[i].AvgWidth + segs[j].AvgWidth) / 2f * 4f;
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

        // Groupe les runs de lignes adjacentes dont les étendues X se chevauchent
        var byRow = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            int y = runs[i].Y;
            if (!byRow.TryGetValue(y, out var list)) byRow[y] = list = new();
            list.Add(i);
        }
        foreach (var (y, row) in byRow)
        {
            if (!byRow.TryGetValue(y + 1, out var nextRow)) continue;
            foreach (int a in row)
                foreach (int b in nextRow)
                    if (Math.Abs(runs[a].MidX - runs[b].MidX) <= (runs[a].Width + runs[b].Width) / 2f
                        && Math.Min(runs[a].Width, runs[b].Width) * 2 >= Math.Max(runs[a].Width, runs[b].Width))
                        Union(a, b);
        }

        // Regroupe par racine
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
            if (g.Count < 2) continue; // ignorer les runs isolés

            // Régression linéaire : MidX = a*Y + b
            float ng = g.Count;
            float sy = 0, sx = 0, syy = 0, sxy = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            float sumW = 0;
            foreach (int i in g)
            {
                var r = runs[i];
                float fy = r.Y;
                sy += fy; sx += r.MidX; syy += fy * fy; sxy += r.MidX * fy;
                if (fy < minY) minY = fy;
                if (fy > maxY) maxY = fy;
                sumW += r.Width;
            }
            float avgW = sumW / ng;
            float det = ng * syy - sy * sy;

            PointF p1, p2;
            if (Math.Abs(det) < 1f)
            {
                float mx = sx / ng;
                p1 = new PointF(mx, minY + 0.5f);
                p2 = new PointF(mx, maxY + 0.5f);
            }
            else
            {
                float a = (ng * sxy - sy * sx) / det;
                float b = (sx - a * sy) / ng;
                p1 = new PointF(a * (minY + 0.5f) + b, minY + 0.5f);
                p2 = new PointF(a * (maxY + 0.5f) + b, maxY + 0.5f);
            }
            result.Add(new Step2Segment(p1, p2, avgW));
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
            using var circlePen = new System.Drawing.Pen(Color.FromArgb(80, 60, 120, 255), 1f);
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

                // Cercles de proximité step 4 : rayon = AvgWidth * 5 (même que maxGap)
                float rx = seg.AvgWidth * 20f * scX, ry = seg.AvgWidth * 20f * scY;
                foreach (var ep in new[] { seg.P1, seg.P2 })
                {
                    float cx = ox + ep.X * scX, cy = oy + ep.Y * scY;
                    g.DrawEllipse(circlePen, cx - rx, cy - ry, rx * 2, ry * 2);
                }
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

        // Debug : 2 segments step3 les plus proches de la souris + critères step4
        if (locrStepChecks.Count > 3 && locrStepChecks[2].Checked && locrStepChecks[3].Checked
            && locrStep3Segs.Count >= 2)
        {
            var mp = pictureBox.PointToClient(Cursor.Position);

            float DistPtToSeg(Step2Segment s)
            {
                float ax = ox + s.P1.X * scX, ay = oy + s.P1.Y * scY;
                float bx = ox + s.P2.X * scX, by = oy + s.P2.Y * scY;
                float ddx = bx - ax, ddy = by - ay, lsq = ddx * ddx + ddy * ddy;
                if (lsq < 1f) return MathF.Sqrt((mp.X - ax) * (mp.X - ax) + (mp.Y - ay) * (mp.Y - ay));
                float t = Math.Clamp(((mp.X - ax) * ddx + (mp.Y - ay) * ddy) / lsq, 0f, 1f);
                return MathF.Sqrt((mp.X - ax - t * ddx) * (mp.X - ax - t * ddx) + (mp.Y - ay - t * ddy) * (mp.Y - ay - t * ddy));
            }

            var nearest = locrStep3Segs
                .Select((s, idx) => (s, idx, d: DistPtToSeg(s)))
                .OrderBy(t => t.d).Take(2)
                .OrderBy(t => t.idx).ToList();
            var sA = nearest[0].s; var sB = nearest[1].s;

            // Surlignage blanc
            using var hpen = new System.Drawing.Pen(Color.White, 2f);
            void DrawHL(Step2Segment s)
            {
                float ddx = s.P2.X - s.P1.X, ddy = s.P2.Y - s.P1.Y;
                float l = MathF.Sqrt(ddx * ddx + ddy * ddy);
                float nx = l > 0 ? -ddy / l * s.AvgWidth / 2f : s.AvgWidth / 2f;
                float ny = l > 0 ?  ddx / l * s.AvgWidth / 2f : 0f;
                PointF Sc(float ix, float iy) => new(ox + ix * scX, oy + iy * scY);
                g.DrawPolygon(hpen, new[] {
                    Sc(s.P1.X + nx, s.P1.Y + ny), Sc(s.P1.X - nx, s.P1.Y - ny),
                    Sc(s.P2.X - nx, s.P2.Y - ny), Sc(s.P2.X + nx, s.P2.Y + ny)
                });
            }
            DrawHL(sA); DrawHL(sB);

            // Calcul des critères
            float dxA = sA.P2.X - sA.P1.X, dyA = sA.P2.Y - sA.P1.Y, lA = MathF.Sqrt(dxA * dxA + dyA * dyA);
            float dxB = sB.P2.X - sB.P1.X, dyB = sB.P2.Y - sB.P1.Y;
            float lB = MathF.Sqrt(dxB * dxB + dyB * dyB);
            var dirA = lA > 0 ? (dxA / lA, dyA / lA) : (0f, 1f);
            var dirB = lB > 0 ? (dxB / lB, dyB / lB) : (0f, 1f);

            float wMin = MathF.Min(sA.AvgWidth, sB.AvgWidth);
            float wMax = MathF.Max(sA.AvgWidth, sB.AvgWidth);
            bool c1 = wMin * 2f >= wMax;

            float dotAB = MathF.Abs(dirA.Item1 * dirB.Item1 + dirA.Item2 * dirB.Item2);
            bool c2 = dotAB >= 0.9945f;

            var depA = sA.P1; var depB = sB.P1;
            float minD2db = float.MaxValue;
            foreach (var a in new[] { sA.P1, sA.P2 })
                foreach (var b in new[] { sB.P1, sB.P2 })
                {
                    float d2db = (a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y);
                    if (d2db < minD2db) { minD2db = d2db; depA = a; depB = b; }
                }
            float cenAx = (sA.P1.X + sA.P2.X) / 2f, cenAy = (sA.P1.Y + sA.P2.Y) / 2f;
            float cenBx = (sB.P1.X + sB.P2.X) / 2f, cenBy = (sB.P1.Y + sB.P2.Y) / 2f;
            float avgWDB = (sA.AvgWidth + sB.AvgWidth) / 2f;
            float perpAtoB = MathF.Abs(-dirA.Item2 * (depB.X - cenAx) + dirA.Item1 * (depB.Y - cenAy));
            float perpBtoA = MathF.Abs(-dirB.Item2 * (depA.X - cenBx) + dirB.Item1 * (depA.Y - cenBy));
            float maxPerp = avgWDB * 4f;
            bool c3 = perpAtoB <= maxPerp && perpBtoA <= maxPerp;

            float tj1 = (sB.P1.X - sA.P1.X) * dirA.Item1 + (sB.P1.Y - sA.P1.Y) * dirA.Item2;
            float tj2 = (sB.P2.X - sA.P1.X) * dirA.Item1 + (sB.P2.Y - sA.P1.Y) * dirA.Item2;
            float jLo = MathF.Min(tj1, tj2), jHi = MathF.Max(tj1, tj2);
            float gap = MathF.Max(0f, MathF.Max(-jHi, jLo - lA));
            float maxGap = avgWDB * 20f;
            bool c4 = gap <= maxGap;

            var lines = new[] {
                $"1. Largeur   {wMin:F1}/{wMax:F1} (max 2×)      {(c1 ? "✓" : "✗")}",
                $"2. Direction cos={dotAB:F4} (≥0.9945)      {(c2 ? "✓" : "✗")}",
                $"3. Perp A→B  {perpAtoB/avgWDB:F2}/{maxPerp/avgWDB:F1}× larg  {(perpAtoB <= maxPerp ? "✓" : "✗")}",
                $"   Perp B→A  {perpBtoA/avgWDB:F2}/{maxPerp/avgWDB:F1}× larg  {(perpBtoA <= maxPerp ? "✓" : "✗")}",
                $"4. Gap       {gap/avgWDB:F1}/{maxGap/avgWDB:F1}× larg          {(c4 ? "✓" : "✗")}",
            };

            using var font = new Font("Consolas", 9f);
            float lh = font.Height + 2;
            float pw = lines.Max(l => g.MeasureString(l, font).Width) + 10;
            float ph = lines.Length * lh + 8;
            float ppx = MathF.Min(mp.X + 14, pictureBox.Width  - pw - 4);
            float ppy = MathF.Min(mp.Y + 14, pictureBox.Height - ph - 4);
            using var bgBrush = new SolidBrush(Color.FromArgb(210, 20, 20, 20));
            g.FillRectangle(bgBrush, ppx, ppy, pw, ph);
            bool[] lineOk = { c1, c2, perpAtoB <= maxPerp, perpBtoA <= maxPerp, c4 };
            for (int ki = 0; ki < lines.Length; ki++)
            {
                using var br = new SolidBrush(lineOk[ki] ? Color.LightGreen : Color.OrangeRed);
                g.DrawString(lines[ki], font, br, ppx + 5, ppy + 4 + ki * lh);
            }
        }
    }

    // Scan horizontal : pour chaque ligne de l'image edges (binaire),
    // retourne tous les runs blancs avec leur ligne, centre X et largeur.
    List<WhiteRun> ScanHorizontalRuns(Mat edges)
    {
        var runs = new List<WhiteRun>();
        int rows = edges.Rows, cols = edges.Cols;
        for (int y = 0; y < rows; y++)
        {
            int x = 0;
            while (x < cols)
            {
                if (edges.At<byte>(y, x) == 255)
                {
                    int start = x;
                    while (x < cols && edges.At<byte>(y, x) == 255) x++;
                    int width = x - start;
                    runs.Add(new WhiteRun(y, start + width / 2f, width));
                }
                else x++;
            }
        }
        return runs;
    }
}
