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
        hoveredLine = null; hoveredBorderIdx = -1; hoveredParallelRefIdx = -1;
        draggingBorderIdx = -1; dragMoved = false;
        viewZoom = 1f; viewPan = PointF.Empty;
        bool allSet = selectedBorders.All(b => b != null);
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
        RefreshTabPanel();
    }

    void CloseAllExcept(int keepIdx)
    {
        var keep = tabs[keepIdx];
        tabs.Clear();
        tabs.Add(keep);
        activeTabIdx = 0;
        LoadStateFromTab(keep);
        RefreshTabPanel();
    }

    void CloseAllTabs()
    {
        tabs.Clear();
        image = null; edges = null; houghLines = null;
        for (int i = 0; i < 4; i++) selectedBorders[i] = null;
        doneCenters.Clear(); loadedImagePath = null; activeTabIdx = -1;
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
            UpdateDisplay();
        }
        else
        {
            activeTabIdx = Math.Clamp(idx, 0, tabs.Count - 1);
            LoadStateFromTab(tabs[activeTabIdx]);
        }
        RefreshTabPanel();
    }

    void RefreshTabPanel()
    {
        tabPanel.Controls.Clear();
        if (tabs.Count <= 1) { tabPanel.Visible = false; return; }
        tabPanel.Visible = true;
        int tw = 160, gap = 2;
        int tx = 4;
        int ph = tabPanel.Height;
        for (int i = 0; i < tabs.Count; i++)
        {
            int idx = i;
            bool active = i == activeTabIdx;
            int tabTop = active ? 2 : 5;
            int tabH   = ph - tabTop;
            var tabBg  = active ? Color.FromArgb(52, 52, 52) : Color.FromArgb(36, 36, 36);
            var panelBg = tabPanel.BackColor;

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
                bool isActive = idx == activeTabIdx;
                var bg = isActive ? Color.FromArgb(52, 52, 52) : Color.FromArgb(36, 36, 36);
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

            var lbl = new Label
            {
                Text = Path.GetFileName(tabs[i].ImagePath ?? "Image"),
                Left = 10, Top = 0, Width = tw - 30, Height = tabH,
                ForeColor = active ? Color.White : Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var ctx = new ContextMenuStrip();
            ctx.Items.Add("Close this",   null, (s, e) => CloseTab(idx));
            ctx.Items.Add("Close others", null, (s, e) => CloseAllExcept(idx));
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("Close all",    null, (s, e) => CloseAllTabs());

            lbl.Click  += (s, e) => SwitchToTab(idx);
            tab.Click  += (s, e) => SwitchToTab(idx);
            lbl.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ctx.Show(lbl, e.Location); };
            tab.MouseUp  += (s, e) => { if (e.Button == MouseButtons.Right) ctx.Show(tab, e.Location); };

            var close = new Button
            {
                Text = "×", Left = tw - 24, Top = (tabH - 18) / 2, Width = 18, Height = 18,
                FlatStyle = FlatStyle.Flat,
                ForeColor = active ? Color.White : Color.FromArgb(150, 150, 150),
                BackColor = Color.Transparent,
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
    }
}
