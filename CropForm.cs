using System.Windows.Forms;
using System.Drawing;
using OpenCvSharp;

class CropForm : Form
{
    public Mat? CroppedImage { get; private set; }
    readonly Mat source;
    readonly PictureBox pb;

    System.Drawing.Point dragStart, dragEnd;
    bool dragging;
    Rectangle selection;

    public CropForm(Mat source)
    {
        this.source = source;
        Text = "Manual crop – drag to select";
        Width = Math.Min(source.Width + 40, 1000);
        Height = Math.Min(source.Height + 120, 750);
        StartPosition = FormStartPosition.CenterParent;

        pb = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
            Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(source)
        };
        pb.MouseDown += (s, e) => { dragging = true; dragStart = dragEnd = e.Location; };
        pb.MouseMove += (s, e) => { if (!dragging) return; dragEnd = e.Location; pb.Invalidate(); };
        pb.MouseUp += (s, e) => { dragging = false; dragEnd = e.Location; UpdateSelection(); };
        pb.Paint += OnPaint;
        Controls.Add(pb);

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(30, 30, 30) };

        var btnOk = new Button { Text = "✔ Confirm", Left = 20, Top = 10, Width = 120, Height = 30,
            BackColor = Color.FromArgb(0, 150, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.Click += (s, e) =>
        {
            if (selection.Width > 0 && selection.Height > 0)
            {
                var imgRect = GetImageRect();
                float scaleX = (float)source.Width / imgRect.Width;
                float scaleY = (float)source.Height / imgRect.Height;
                int rx = (int)((selection.X - imgRect.X) * scaleX);
                int ry = (int)((selection.Y - imgRect.Y) * scaleY);
                int rw = (int)(selection.Width * scaleX);
                int rh = (int)(selection.Height * scaleY);
                rx = Math.Max(0, rx); ry = Math.Max(0, ry);
                rw = Math.Min(rw, source.Width - rx);
                rh = Math.Min(rh, source.Height - ry);
                if (rw > 0 && rh > 0)
                    CroppedImage = new Mat(source, new OpenCvSharp.Rect(rx, ry, rw, rh));
            }
            DialogResult = DialogResult.OK; Close();
        };

        var btnCancel = new Button { Text = "Cancel", Left = 160, Top = 10, Width = 100, Height = 30,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        btnPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });
        Controls.Add(btnPanel);
    }

    void UpdateSelection()
    {
        int x = Math.Min(dragStart.X, dragEnd.X);
        int y = Math.Min(dragStart.Y, dragEnd.Y);
        int w = Math.Abs(dragEnd.X - dragStart.X);
        int h = Math.Abs(dragEnd.Y - dragStart.Y);
        selection = new Rectangle(x, y, w, h);
        pb.Invalidate();
    }

    void OnPaint(object? sender, PaintEventArgs e)
    {
        if (selection.Width <= 0 || selection.Height <= 0) return;
        using var pen = new System.Drawing.Pen(Color.Yellow, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        e.Graphics.DrawRectangle(pen, selection);
        using var brush = new SolidBrush(Color.FromArgb(40, 255, 255, 0));
        e.Graphics.FillRectangle(brush, selection);
    }

    Rectangle GetImageRect()
    {
        if (pb.Image == null) return pb.ClientRectangle;
        float imgRatio = (float)pb.Image.Width / pb.Image.Height;
        float boxRatio = (float)pb.Width / pb.Height;
        int w, h, x, y;
        if (imgRatio > boxRatio) { w = pb.Width; h = (int)(pb.Width / imgRatio); x = 0; y = (pb.Height - h) / 2; }
        else { h = pb.Height; w = (int)(pb.Height * imgRatio); y = 0; x = (pb.Width - w) / 2; }
        return new Rectangle(x, y, w, h);
    }
}
