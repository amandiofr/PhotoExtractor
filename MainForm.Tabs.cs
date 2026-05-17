using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

partial class MainForm
{
    void AddImageAsTab(string path)
    {
        var mat = Cv2.ImRead(path);
        if (mat.Empty()) return;
        if (activeTabIdx >= 0 && activeTabIdx < tabs.Count) SaveStateToActiveTab();
        if (tabs.Count == 1 && tabs[0].Image == null) tabs.RemoveAt(0);
        var state = new ImageState { Image = mat, ImagePath = path };
        tabs.Add(state);
        activeTabIdx = tabs.Count - 1;
        LoadStateFromTab(state);
        UpdateEdges();
        EnsureActiveTabVisible();
        RefreshTabPanel();
    }

    void SaveStateToActiveTab()
    {
        if (activeTabIdx < 0 || activeTabIdx >= tabs.Count) return;
        var t = tabs[activeTabIdx];
        t.Image = image; t.Edges = edges; t.HoughLines = houghLines;
        t.SelectedBorders = (LineSegmentPoint?[])selectedBorders.Clone();
        t.DoneCenters = new List<Point2f>(doneCenters);
        t.ImagePath = loadedImagePath;
        t.ExtractOrientation = extractOrientation;
        t.OrientationTouched = orientationTouched;
        t.LocrStep1Runs = new List<WhiteRun>(locrStep1Runs);
        t.LocrStep2Segs = new List<Step2Segment>(locrStep2Segs);
        t.LocrStep3Segs = new List<Step2Segment>(locrStep3Segs);
        t.LocrStep4Segs = new List<Step2Segment>(locrStep4Segs);
        for (int i = 0; i < Math.Min(locrStepTimings.Count, 4); i++) t.LocrStepTimings[i] = locrStepTimings[i].Text;
    }

    void LoadStateFromTab(ImageState t)
    {
        image = t.Image; edges = t.Edges; houghLines = t.HoughLines;
        selectedBorders = (LineSegmentPoint?[])t.SelectedBorders.Clone();
        doneCenters = new List<Point2f>(t.DoneCenters);
        loadedImagePath = t.ImagePath;
        extractOrientation = t.ExtractOrientation;
        orientationTouched = t.OrientationTouched;
        locrStep1Runs = new List<WhiteRun>(t.LocrStep1Runs);
        locrStep2Segs = new List<Step2Segment>(t.LocrStep2Segs);
        locrStep3Segs = new List<Step2Segment>(t.LocrStep3Segs);
        locrStep4Segs = new List<Step2Segment>(t.LocrStep4Segs);
        for (int i = 0; i < Math.Min(locrStepTimings.Count, 4); i++) locrStepTimings[i].Text = t.LocrStepTimings[i] ?? "";
        hoveredLine = null; hoveredBorderIdx = -1; hoveredParallelRefIdx = -1;
        draggingBorderIdx = -1; dragMoved = false;
        viewZoom = 1f; viewPan = PointF.Empty;
        bool allSet = selectedBorders.All(b => b != null);
        btnClear.Enabled = true;
        foreach (var b in btnBorders) b.Enabled = true;
        btnExtractManual.Enabled = allSet;
        btnRefine.Enabled = allSet;
        btnParallel.Enabled = allSet;
        SetActiveMode(-1);
        UpdateDisplay();
    }

    void SwitchToTab(int idx)
    {
        if (idx == activeTabIdx || idx < 0 || idx >= tabs.Count) return;
        SaveStateToActiveTab();
        activeTabIdx = idx;
        LoadStateFromTab(tabs[idx]);
        EnsureActiveTabVisible();
        RefreshTabPanel();
    }

    void CloseAllExcept(int keepIdx)
    {
        var keep = tabs[keepIdx];
        tabs.Clear();
        tabs.Add(keep);
        activeTabIdx = 0;
        tabScrollOffset = 0;
        LoadStateFromTab(keep);
        RefreshTabPanel();
    }

    void CloseAllTabs()
    {
        tabs.Clear();
        image = null; edges = null; houghLines = null;
        for (int i = 0; i < 4; i++) selectedBorders[i] = null;
        doneCenters.Clear(); loadedImagePath = null; activeTabIdx = -1;
        tabScrollOffset = 0;
        DisableImageButtons();
        UpdateDisplay();
        RefreshTabPanel();
    }

    void CloseTab(int idx)
    {
        if (idx < 0 || idx >= tabs.Count) return;
        tabs.RemoveAt(idx);
        if (tabs.Count == 0)
        {
            image = null; edges = null; houghLines = null;
            for (int i = 0; i < 4; i++) selectedBorders[i] = null;
            doneCenters.Clear(); loadedImagePath = null; activeTabIdx = -1;
            tabScrollOffset = 0;
            DisableImageButtons();
            UpdateDisplay();
        }
        else
        {
            activeTabIdx = Math.Clamp(idx, 0, tabs.Count - 1);
            LoadStateFromTab(tabs[activeTabIdx]);
            EnsureActiveTabVisible();
        }
        RefreshTabPanel();
    }

    // Appelé uniquement quand l'onglet actif change — pas lors du scroll manuel
    void EnsureActiveTabVisible()
    {
        if (activeTabIdx < 0 || tabs.Count <= 1) return;
        int tw = 160, gap = 2, arrowW = 22, arrowMargin = 4;
        int availW = tabPanel.Width - 2 * (arrowW + arrowMargin + 4);
        int visibleCount = Math.Max(1, (availW + gap) / (tw + gap));
        if (activeTabIdx < tabScrollOffset)
            tabScrollOffset = activeTabIdx;
        else if (activeTabIdx > tabScrollOffset + visibleCount - 1)
            tabScrollOffset = activeTabIdx - visibleCount + 1;
        tabScrollOffset = Math.Clamp(tabScrollOffset, 0, Math.Max(0, tabs.Count - visibleCount));
    }

    void RefreshTabPanel()
    {
        tabPanel.Controls.Clear();
        if (tabs.Count <= 1) { tabPanel.Visible = false; tabScrollOffset = 0; return; }
        tabPanel.Visible = true;

        int arrowW = 22, tw = 160, gap = 2, arrowMargin = 4;
        int ph = tabPanel.Height;
        int tabsStartX = arrowMargin + arrowW + 4;
        int tabsEndX   = tabPanel.Width - arrowMargin - arrowW - 4;
        int availW     = tabsEndX - tabsStartX;
        int visibleCount = Math.Max(1, (availW + gap) / (tw + gap));

        tabScrollOffset = Math.Clamp(tabScrollOffset, 0, Math.Max(0, tabs.Count - visibleCount));

        bool canLeft  = tabScrollOffset > 0;
        bool canRight = tabScrollOffset + visibleCount < tabs.Count;

        Color arrowBg = tabPanel.BackColor;

        // ── Onglets (ajoutés en premier → sous les flèches en z-order) ──
        int tx = tabsStartX;
        var panelBg = tabPanel.BackColor;

        for (int i = tabScrollOffset; i < tabs.Count; i++)
        {
            if (tx + tw > tabsEndX) break;

            int idx = i;
            bool active = i == activeTabIdx;
            int tabTop = active ? 2 : 5;
            int tabH   = ph - tabTop;
            var tabBg  = active ? Color.FromArgb(52, 52, 52) : Color.FromArgb(36, 36, 36);

            var tab = new Panel
            {
                Left = tx, Top = tabTop, Width = tw, Height = tabH,
                BackColor = tabBg, Cursor = Cursors.Hand
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(tab, true);

            tab.Paint += (ps, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(panelBg);
                var bg = idx == activeTabIdx ? Color.FromArgb(52, 52, 52) : Color.FromArgb(36, 36, 36);
                int rad = 7;
                var path = new GraphicsPath();
                path.AddLine(0, tab.Height + 1, 0, rad);
                path.AddArc(0, 0, rad * 2, rad * 2, 180, 90);
                path.AddLine(rad, 0, tab.Width - rad, 0);
                path.AddArc(tab.Width - rad * 2, 0, rad * 2, rad * 2, 270, 90);
                path.AddLine(tab.Width, rad, tab.Width, tab.Height + 1);
                using var brush = new SolidBrush(bg);
                g.FillPath(brush, path);
            };

            var lbl = new SmoothLabel
            {
                Text = $"{idx + 1} · {Path.GetFileName(tabs[i].ImagePath ?? "Image")}",
                Left = 10, Top = 0, Width = tw - 30, Height = tabH,
                ForeColor = active ? Color.White : Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = tabBg
            };

            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Close this",   null, (s, e) => CloseTab(idx));
            ctx.Items.Add("Close others", null, (s, e) => CloseAllExcept(idx));
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Close all",    null, (s, e) => CloseAllTabs());

            lbl.Click   += (s, e) => SwitchToTab(idx);
            tab.Click   += (s, e) => SwitchToTab(idx);
            lbl.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ctx.Show(lbl, e.Location); };
            tab.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ctx.Show(tab, e.Location); };

            var close = new SmoothButton
            {
                Text = "×", Left = tw - 24, Top = (tabH - 18) / 2, Width = 18, Height = 18,
                FlatStyle = FlatStyle.Flat,
                ForeColor = active ? Color.White : Color.FromArgb(150, 150, 150),
                BackColor = tabBg,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            close.Click += (s, e) => CloseTab(idx);

            tab.Controls.Add(lbl);
            tab.Controls.Add(close);
            tabPanel.Controls.Add(tab);
            tx += tw + gap;
        }

        // ── Flèches (ajoutées après → par-dessus les onglets en z-order) ──
        void AddArrow(string text, int left, bool enabled, Action onClick)
        {
            var btn = new SmoothButton
            {
                Text = text, Left = left, Top = 4, Width = arrowW, Height = ph - 8,
                FlatStyle = FlatStyle.Flat,
                ForeColor = enabled ? Color.FromArgb(180, 180, 180) : Color.FromArgb(55, 55, 55),
                BackColor = arrowBg,
                Font = new Font("Segoe UI", 10f),
                Enabled = enabled, Cursor = enabled ? Cursors.Hand : Cursors.Default
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = enabled ? Color.FromArgb(48, 48, 48) : arrowBg;
            if (enabled) btn.Click += (s, e) => onClick();
            tabPanel.Controls.Add(btn);
        }

        AddArrow("◂", arrowMargin, canLeft,  () => { tabScrollOffset--; RefreshTabPanel(); });
        AddArrow("▸", tabPanel.Width - arrowMargin - arrowW, canRight, () => { tabScrollOffset++; RefreshTabPanel(); });
    }
}
