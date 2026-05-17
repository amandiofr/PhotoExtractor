using System.Windows.Forms;
using System.Drawing;
using OpenCvSharp;

class ValidationForm : Form
{
    public Mat? CroppedImage { get; private set; }
    readonly Mat photo;

    public ValidationForm(Mat photo, int index, int total)
    {
        this.photo = photo;
        Text = $"Validate photo {index} / {total}";
        Width = Math.Min(photo.Width + 40, 900);
        Height = Math.Min(photo.Height + 120, 700);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var pb = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
            Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(photo)
        };
        Controls.Add(pb);

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(30, 30, 30) };

        var btnOk = new Button { Text = "✔ Accept", Left = 20, Top = 10, Width = 120, Height = 30,
            BackColor = Color.FromArgb(0, 150, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.Click += (s, e) => { DialogResult = DialogResult.Yes; Close(); };

        var btnFix = new Button { Text = "✎ Crop", Left = 160, Top = 10, Width = 120, Height = 30,
            BackColor = Color.FromArgb(180, 120, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnFix.Click += (s, e) => { DialogResult = DialogResult.Retry; Close(); };

        var btnNo = new Button { Text = "✘ Reject", Left = 300, Top = 10, Width = 120, Height = 30,
            BackColor = Color.FromArgb(180, 40, 40), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnNo.Click += (s, e) => { DialogResult = DialogResult.No; Close(); };

        btnPanel.Controls.AddRange(new Control[] { btnOk, btnFix, btnNo });
        Controls.Add(btnPanel);
    }
}
