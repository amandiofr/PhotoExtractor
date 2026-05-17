using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;
using System.Text.Json;

partial class MainForm
{
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O)) { OnOpen(null, EventArgs.Empty); return true; }
        if (keyData == (Keys.Control | Keys.R) ||
            keyData == (Keys.Control | Keys.D0) ||
            keyData == (Keys.Control | Keys.Shift | Keys.D0) ||
            keyData == (Keys.Control | Keys.NumPad0))
        { viewZoom = 1f; viewPan = PointF.Empty; pictureBox.Invalidate(); return true; }
        if (keyData == Keys.F11 && image != null)
        {
            if (viewZoom < 140f) { viewZoom = 140f; viewPan = PointF.Empty; ClampPan(); }
            else                 { viewZoom = 1f;   viewPan = PointF.Empty; }
            pictureBox.Invalidate();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        var files = ((string[]?)e.Data?.GetData(DataFormats.FileDrop))
            ?.Where(f => Path.GetExtension(f).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif")
            .ToArray();
        if (files == null || files.Length == 0) return;
        if (files.Length == 1) { LoadImage(files[0]); return; }
        foreach (var f in files) AddImageAsTab(f);
    }

    void OnOpen(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose an album page",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tiff"
        };
        if (dialog.ShowDialog() == DialogResult.OK) LoadImage(dialog.FileName);
    }

    void LoadImage(string path)
    {
        var mat = Cv2.ImRead(path);
        if (mat.Empty()) return;
        image = mat;
        edges = null; houghLines = null;
        loadedImagePath = path;
        viewZoom = 1f; viewPan = PointF.Empty;
        doneCenters.Clear();
        extractOrientation = 0; orientationTouched = false;
        for (int i = 0; i < 4; i++) selectedBorders[i] = null;
        btnExtractManual.Enabled = false;
        btnRefine.Enabled = false;
        btnParallel.Enabled = false;
        SetActiveMode(0);
        btnClear.Enabled = true;
        foreach (var b in btnBorders) b.Enabled = true;
        labelStatus.Text = $"Image loaded: {Path.GetFileName(path)} ({image.Width}×{image.Height})";
        if (tabs.Count == 0) { tabs.Add(new ImageState()); activeTabIdx = 0; }
        else if (activeTabIdx < 0) activeTabIdx = 0;
        SaveStateToActiveTab();
        UpdateEdges();
        SaveConfig(path);
        RefreshTabPanel();
    }

    void LoadConfig()
    {
        if (!File.Exists(configPath)) { Width = 1100; Height = 750; return; }
        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath))!;
            StartPosition = FormStartPosition.Manual;
            Left = cfg.WindowLeft; Top = cfg.WindowTop;
            Width = cfg.WindowWidth; Height = cfg.WindowHeight;
            sliderBlur.Value = Math.Clamp(cfg.Blur, sliderBlur.Minimum, sliderBlur.Maximum);
            sliderLow.Value  = Math.Clamp(cfg.Low,  sliderLow.Minimum,  sliderLow.Maximum);
            sliderHigh.Value = Math.Clamp(cfg.High, sliderHigh.Minimum, sliderHigh.Maximum);
            checkEdges.Checked = cfg.ShowEdges;
            checkLines.Checked = cfg.ShowLines;
            sliderMinLen.Value = Math.Clamp(cfg.MinLen, sliderMinLen.Minimum, sliderMinLen.Maximum);
            sliderGap.Value    = Math.Clamp(cfg.MaxGap, sliderGap.Minimum,    sliderGap.Maximum);
            rbLOCR.Checked = cfg.UseLOCR;
            if (cfg.LocrStepChecked != null)
                for (int i = 0; i < Math.Min(locrStepChecks.Count, cfg.LocrStepChecked.Length); i++)
                    locrStepChecks[i].Checked = cfg.LocrStepChecked[i];
            var validPaths = (cfg.TabPaths ?? new[] { cfg.ImagePath })
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .ToArray();
            if (validPaths.Length > 1)
            {
                foreach (var p in validPaths) AddImageAsTab(p);
                int target = Math.Clamp(cfg.ActiveTab, 0, tabs.Count - 1);
                if (target != activeTabIdx) SwitchToTab(target);
                tabScrollOffset = cfg.TabScrollOffset;
                RefreshTabPanel();
            }
            else if (validPaths.Length == 1) LoadImage(validPaths[0]);

            if (cfg.ViewZoom > 1f)
            {
                viewZoom = cfg.ViewZoom;
                viewPan  = new PointF(cfg.ViewPanX, cfg.ViewPanY);
                ClampPan();
                pictureBox.Invalidate();
            }
            showSettings = cfg.ShowSettings;
        }
        catch { Width = 1100; Height = 750; }
    }

    void SaveConfig(string? imagePath = null)
    {
        SaveStateToActiveTab();
        string[] tabPaths = tabs.Count > 1
            ? tabs.Select(t => t.ImagePath ?? "").Where(p => p != "").ToArray()
            : Array.Empty<string>();
        string singlePath = imagePath
            ?? (tabs.Count > 0 ? (tabs[activeTabIdx >= 0 ? activeTabIdx : 0].ImagePath ?? "") : "")
            ?? (File.Exists(configPath) ? (JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath))?.ImagePath ?? "") : "");
        int activeTab = tabs.Count > 1 ? Math.Max(0, activeTabIdx) : 0;
        var cfg = new AppConfig(singlePath, Left, Top, Width, Height,
                                sliderBlur.Value, sliderLow.Value, sliderHigh.Value,
                                checkEdges.Checked, checkLines.Checked,
                                sliderMinLen.Value, sliderGap.Value,
                                tabPaths, activeTab, rbLOCR.Checked,
                                locrStepChecks.Select(cb => cb.Checked).ToArray(),
                                viewZoom, viewPan.X, viewPan.Y,
                                showSettings, tabScrollOffset);
        File.WriteAllText(configPath,
            JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    }
}
