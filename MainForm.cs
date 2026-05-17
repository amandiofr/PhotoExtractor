using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;

partial class MainForm : Form
{
    Mat? image;
    Mat? edges;
    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    // UI – vue principale
    PictureBox pictureBox;
    TrackBar sliderLow, sliderHigh, sliderBlur, sliderMinLen, sliderGap;
    Label labelLow, labelHigh, labelBlur, labelMinLen, labelGap, labelStatus;
    CheckBox checkEdges;
    CheckBox checkLines;
    RadioButton rbHough, rbLOCR;

    // Sélection manuelle des 4 bords
    Button[] btnBorders = new Button[4];
    Button btnRefine;
    Button btnParallel;
    Button btnExtractManual;
    Button btnOpen;
    Button btnClear;

    // Onglets
    List<ImageState> tabs = new();
    int activeTabIdx = -1;
    Panel tabPanel = null!;
    bool refineMode;
    bool parallelMode;
    List<Point2f> doneCenters = new();
    List<CheckBox> locrStepChecks = new();
    List<Label> locrStepLabels = new();
    List<Label> locrStepTimings = new();
    List<WhiteRun> locrStep1Runs = new();
    List<Step2Segment> locrStep2Segs = new();
    List<Step2Segment> locrStep3Segs = new();
    List<Step2Segment> locrStep4Segs = new();
    int extractOrientation;
    bool orientationTouched;
    RectangleF hitRotLeft, hitRotRight;
    LineSegmentPoint?[] selectedBorders = new LineSegmentPoint?[4];
    int activeBorderIdx = -1;

    static readonly Color[] BorderWinColors =
        { Color.OrangeRed, Color.LimeGreen, Color.DodgerBlue, Color.Yellow };

    // ── DWM (dark title bar + rounded corners) ──────────────────────────────
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); // dark title bar
        v = 2;     DwmSetWindowAttribute(Handle, 33, ref v, 4); // rounded corners
    }

    static Color Lighten(Color c, int amt) => Color.FromArgb(
        Math.Min(255, c.R + amt), Math.Min(255, c.G + amt), Math.Min(255, c.B + amt));
    static Color Darken(Color c, int amt) => Color.FromArgb(
        Math.Max(0, c.R - amt), Math.Max(0, c.G - amt), Math.Max(0, c.B - amt));

    // Lignes Hough détectées
    LineSegmentPoint[]? houghLines;
    LineSegmentPoint? hoveredLine;
    int hoveredParallelRefIdx = -1;

    // Grab de bord
    int hoveredBorderIdx = -1;
    int draggingBorderIdx = -1;
    bool dragMoved;

    // Drag de coin (mode refine)
    int draggingCornerIdx = -1;
    int dragCornerBorderA, dragCornerBorderB;
    Point2f dragCornerFixedA, dragCornerFixedB;
    Point2f dragCornerStartMouse, dragCornerStartPos;
    Point2f loupeCenterImg;

    // Zoom / pan
    Bitmap? displayBitmap;
    Bitmap? rawBitmap;
    float viewZoom = 1.0f;
    PointF viewPan = PointF.Empty;
    bool panDragging;
    System.Drawing.Point panDragStart;
    PointF panAtDragStart;

    string? loadedImagePath;
    Bitmap? psychoBitmap;

    public MainForm()
    {
        Text = "Photo Extractor";
        Width = 1100; Height = 750;
        MinimumSize = new System.Drawing.Size(980, 420);

        // ── PictureBox ──────────────────────────────────────────────────────
        pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Normal,
            BackColor = Color.Black
        };
        pictureBox.MouseClick += OnImageClick;
        pictureBox.MouseMove += OnImageMouseMove;
        pictureBox.MouseDown += OnImageMouseDown;
        pictureBox.MouseUp   += OnImageMouseUp;
        pictureBox.MouseWheel += OnPictureBoxMouseWheel;
        pictureBox.MouseEnter += (s, e) => pictureBox.Focus();
        pictureBox.Resize += (s, e) => pictureBox.Invalidate();
        pictureBox.Paint += OnPictureBoxPaint;
        Controls.Add(pictureBox);

        // ── Panneau de contrôles ────────────────────────────────────────────
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        // Sous-panneau switches + sliders, centré horizontalement
        int slW = 130, slH = 50, lblH = 18, slGap = 8, chkW = 72, chkH = 22, rbW = 76, stepCbSize = 18, stepLblW = 16, stepRowGap = 3, stepTimingW = 48;
        int stepsAreaW = stepLblW + 4 + stepCbSize + 4 + stepTimingW; // = 90
        int cpW = chkW + 8 + rbW + 12 + 5 * slW + 4 * slGap + 8 + stepsAreaW;
        var cp = new Panel { Top = 2, Width = cpW, Height = 92, BackColor = Color.FromArgb(30, 30, 30) };
        panel.Controls.Add(cp);
        panel.Resize += (s, e) => cp.Left = Math.Max(0, (panel.Width - cpW) / 2);

        // Colonne 1 : Edges / Lines (centrées verticalement)
        int chkTop = (90 - (2 * chkH + 4)) / 2;
        checkEdges = new CheckBox { Text = "Edges", ForeColor = Color.White, Font = new Font("Segoe UI", 9f),
            Left = 0, Top = chkTop, Width = chkW, Height = chkH, AutoSize = false };
        checkLines = new CheckBox { Text = "Lines", ForeColor = Color.White, Font = new Font("Segoe UI", 9f),
            Left = 0, Top = chkTop + chkH + 4, Width = chkW, Height = chkH, AutoSize = false };
        checkEdges.CheckedChanged += (s, e) => UpdateDisplay();
        checkLines.CheckedChanged += (s, e) => UpdateDisplay();
        cp.Controls.Add(checkEdges);
        cp.Controls.Add(checkLines);

        // Colonne 2 : Hough / LOCR (centrées verticalement)
        int rbH = 18, rbX = chkW + 8;
        int rbTop = (90 - (2 * rbH + 4)) / 2;
        rbHough = new RadioButton { Text = "Hough", ForeColor = Color.White, Font = new Font("Segoe UI", 9f),
            Left = rbX, Top = rbTop, Width = rbW, Height = rbH, Checked = true, AutoSize = false };
        rbLOCR  = new RadioButton { Text = "LOCR",  ForeColor = Color.White, Font = new Font("Segoe UI", 9f),
            Left = rbX, Top = rbTop + rbH + 4, Width = rbW, Height = rbH, AutoSize = false };
        rbHough.CheckedChanged += (s, e) => { if (rbHough.Checked) UpdateDetectorMode(); };
        rbLOCR.CheckedChanged  += (s, e) => { if (rbLOCR.Checked)  UpdateDetectorMode(); };
        cp.Controls.Add(rbHough);
        cp.Controls.Add(rbLOCR);

        // Sliders : même largeur, même nombre de ticks, label centré sur la graduation
        int sx = chkW + 8 + rbW + 12, lt = 8, st = lt + lblH + 2;
        (TrackBar, Label) Sl(string name, int min, int max, int val)
        {
            var l = new Label { Text = $"{name}: {val}", ForeColor = Color.White, Font = new Font("Segoe UI", 9f),
                Left = sx, Top = lt, Width = slW, Height = lblH,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
            var t = new TrackBar { Minimum = min, Maximum = max, Value = val,
                Left = sx, Top = st, Width = slW, Height = slH,
                TickFrequency = Math.Max(1, (max - min) / 10) };
            cp.Controls.Add(l); cp.Controls.Add(t);
            sx += slW + slGap;
            return (t, l);
        }

        (sliderBlur,   labelBlur)   = Sl("Blur",   1,   21,  3);
        (sliderLow,    labelLow)    = Sl("Low",    0,  255, 30);
        (sliderHigh,   labelHigh)   = Sl("High",   0,  255, 100);
        (sliderMinLen, labelMinLen) = Sl("MinLen", 5,  300, 20);
        (sliderGap,    labelGap)    = Sl("Gap",    1,   80, 15);

        sliderBlur.ValueChanged   += (s, e) => { int v = sliderBlur.Value; if (v % 2 == 0) { sliderBlur.Value = v + 1; return; } UpdateLabel(labelBlur, "Blur", v); UpdateEdges(); };
        sliderLow.ValueChanged    += (s, e) => { UpdateLabel(labelLow,    "Low",    sliderLow.Value);    UpdateEdges(); };
        sliderHigh.ValueChanged   += (s, e) => { UpdateLabel(labelHigh,   "High",   sliderHigh.Value);   UpdateEdges(); };
        sliderMinLen.ValueChanged += (s, e) => { UpdateLabel(labelMinLen, "MinLen", sliderMinLen.Value); UpdateEdges(); };
        sliderGap.ValueChanged    += (s, e) => { UpdateLabel(labelGap,    "Gap",    sliderGap.Value);    UpdateEdges(); };

        // Cases LOCR étapes, layout vertical avec numéro à gauche
        Color[] stepColors = { Color.FromArgb(255, 140, 0), Color.FromArgb(0, 210, 255), Color.FromArgb(60, 120, 255), Color.FromArgb(220, 50, 50) };
        int stepsStartX = cpW - stepsAreaW;
        int totalStepsH = 4 * stepCbSize + 3 * stepRowGap;
        int stepsTopY = (cp.Height - totalStepsH) / 2;
        for (int s = 0; s < 4; s++)
        {
            int rowY = stepsTopY + s * (stepCbSize + stepRowGap);
            var lbl = new Label { Text = $"{s + 1}", ForeColor = stepColors[s],
                Font = new Font("Segoe UI", 8f),
                Left = stepsStartX, Top = rowY, Width = stepLblW, Height = stepCbSize,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Visible = false };
            var cb = new CheckBox { Left = stepsStartX + stepLblW + 4, Top = rowY,
                Width = stepCbSize, Height = stepCbSize, AutoSize = false, Visible = false,
                CheckAlign = ContentAlignment.MiddleCenter };
            cb.CheckedChanged += (_, _) => pictureBox.Invalidate();
            var timing = new Label { Text = "", ForeColor = Color.FromArgb(140, 140, 140),
                Font = new Font("Segoe UI", 7.5f),
                Left = stepsStartX + stepLblW + 4 + stepCbSize + 4, Top = rowY,
                Width = stepTimingW, Height = stepCbSize,
                AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Visible = false };
            cp.Controls.Add(lbl);
            cp.Controls.Add(cb);
            cp.Controls.Add(timing);
            locrStepLabels.Add(lbl);
            locrStepChecks.Add(cb);
            locrStepTimings.Add(timing);
        }

        // ── Boutons ─────────────────────────────────────────────────────────
        Button MakeBtn(string text, int w, Color? bg = null)
        {
            var col = bg ?? Color.FromArgb(58, 58, 58);
            var b = new Button
            {
                Text = text, Top = 97, Width = w, Height = 28,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = col,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Lighten(col, 22);
            b.FlatAppearance.MouseDownBackColor = Darken(col, 12);
            return b;
        }

        btnOpen = MakeBtn("Open", 80);
        btnOpen.Click += OnOpen;
        panel.Controls.Add(btnOpen);

        btnClear = MakeBtn("Clear", 80);
        btnClear.Click += (s, e) => ClearBorders();
        panel.Controls.Add(btnClear);

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            btnBorders[i] = MakeBtn($"Border {i + 1}", 80);
            btnBorders[i].ForeColor = BorderWinColors[i];
            btnBorders[i].Click += (s, e) => SetActiveMode(activeBorderIdx == idx ? -1 : idx);
            panel.Controls.Add(btnBorders[i]);
        }

        btnRefine = MakeBtn("Refine", 80);
        btnRefine.Enabled = false;
        btnRefine.Click += (s, e) => SetActiveMode(refineMode ? -1 : 4);
        panel.Controls.Add(btnRefine);

        btnParallel = MakeBtn("Parallel", 85);
        btnParallel.Enabled = false;
        btnParallel.Click += (s, e) => SetActiveMode(parallelMode ? -1 : 5);
        panel.Controls.Add(btnParallel);

        btnExtractManual = MakeBtn("Extract", 110, Color.FromArgb(0, 120, 200));
        btnExtractManual.Enabled = false;
        btnExtractManual.Click += OnExtractManual;
        panel.Controls.Add(btnExtractManual);

        panel.Resize += (s, e) => RepositionButtons(panel);
        RepositionButtons(panel);

        Controls.Add(panel);

        // ── Barre de statut ─────────────────────────────────────────────────
        labelStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            Text = "Open an image to start."
        };
        Controls.Add(labelStatus);

        // ── Tab panel ────────────────────────────────────────────────────────
        tabPanel = new Panel
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = Color.FromArgb(24, 24, 24), Visible = false
        };
        typeof(Panel).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(tabPanel, true);
        Controls.Add(tabPanel);

        // ── Drag & drop ──────────────────────────────────────────────────────
        AllowDrop = true;
        DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop  += OnDragDrop;

        FormClosing += (s, e) => SaveConfig();
        LoadConfig();
        SetActiveMode(0);
    }

    void UpdateLabel(Label lbl, string name, int val) =>
        lbl.Text = $"{name}: {val}";

    void UpdateDetectorMode()
    {
        bool isLocr = rbLOCR.Checked;
        sliderMinLen.Visible = !isLocr;
        labelMinLen.Visible  = !isLocr;
        sliderGap.Visible    = !isLocr;
        labelGap.Visible     = !isLocr;
        foreach (var cb  in locrStepChecks)  cb.Visible     = isLocr;
        foreach (var lbl in locrStepLabels)  lbl.Visible    = isLocr;
        foreach (var t   in locrStepTimings) t.Visible      = isLocr;
        UpdateEdges();
    }

    void RepositionButtons(Panel panel)
    {
        btnOpen.Left = 10;
        btnExtractManual.Left = panel.Width - 10 - btnExtractManual.Width;
        Control[] center = new Control[] { btnClear, btnBorders[0], btnBorders[1], btnBorders[2], btnBorders[3], btnRefine, btnParallel };
        int[] gaps = new int[] { 18, 6, 6, 6, 18, 6 };
        int groupW = center.Sum(b => b.Width) + gaps.Sum();
        int minGap = gaps[0]; // 18 px — même espace qu'entre Clear et Border 1
        int startX = btnOpen.Right + Math.Max(minGap, (btnExtractManual.Left - btnOpen.Right - groupW) / 2);
        int cx = startX;
        for (int i = 0; i < center.Length; i++) { center[i].Left = cx; cx += center[i].Width + (i < gaps.Length ? gaps[i] : 0); }
    }
}
