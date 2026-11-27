using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using CA = System.Drawing.ContentAlignment;

class SmoothButton : Button
{
    bool _hovered, _pressed;

    /// <summary>Rayon des coins, en pixels (réduit automatiquement sur les petits boutons).</summary>
    [System.ComponentModel.DefaultValue(6f)]
    public float Radius { get; set; } = 6f;

    public SmoothButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true;  Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e)    { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color over(Color c) => c.A == 0 ? BackColor : c;
        Color bg = !Enabled             ? BackColor :
                   _pressed && _hovered ? over(FlatAppearance.MouseDownBackColor) :
                   _hovered             ? over(FlatAppearance.MouseOverBackColor) : BackColor;
        // Coins arrondis : le fond du parent remplit les coins, puis le bouton
        // est dessiné en rectangle arrondi lissé.
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float radius = Math.Min(Radius, Math.Min(Width, Height) / 4f);

        int bs = FlatAppearance.BorderSize;
        using (var fill = new SolidBrush(bg))
        using (var path = RoundedRect(new RectangleF(0, 0, Width, Height), radius))
            g.FillPath(fill, path);
        if (bs > 0)
        {
            using var pen = new Pen(FlatAppearance.BorderColor, bs);
            var r = new RectangleF(bs / 2f, bs / 2f, Width - bs, Height - bs);
            using var path = RoundedRect(r, Math.Max(0, radius - bs / 2f));
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = SmoothingMode.Default;

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color fg = Enabled ? ForeColor : Color.FromArgb(110, ForeColor.R, ForeColor.G, ForeColor.B);
        using var brush = new SolidBrush(fg);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), sf);
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(r); return path; }
        float d = radius * 2;
        path.AddArc(r.Left,      r.Top,        d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top,        d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        path.AddArc(r.Left,      r.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }
}

class SmoothCheckBox : CheckBox
{
    public SmoothCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
    }

    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        var state = !Enabled      ? CheckBoxState.UncheckedDisabled :
                    Checked       ? CheckBoxState.CheckedNormal      :
                                    CheckBoxState.UncheckedNormal;
        var glyphSize = CheckBoxRenderer.GetGlyphSize(g, state);
        var glyphPt   = new Point(0, (Height - glyphSize.Height) / 2);
        CheckBoxRenderer.DrawCheckBox(g, glyphPt, state);

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color fg = Enabled ? ForeColor : Color.FromArgb(110, ForeColor.R, ForeColor.G, ForeColor.B);
        using var brush = new SolidBrush(fg);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, brush,
            new RectangleF(glyphSize.Width + 3, 0, Width - glyphSize.Width - 3, Height), sf);
    }
}

class SmoothRadioButton : RadioButton
{
    public SmoothRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
    }

    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        var state = !Enabled ? RadioButtonState.UncheckedDisabled :
                    Checked  ? RadioButtonState.CheckedNormal      :
                               RadioButtonState.UncheckedNormal;
        var glyphSize = RadioButtonRenderer.GetGlyphSize(g, state);
        var glyphPt   = new Point(0, (Height - glyphSize.Height) / 2);
        RadioButtonRenderer.DrawRadioButton(g, glyphPt, state);

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color fg = Enabled ? ForeColor : Color.FromArgb(110, ForeColor.R, ForeColor.G, ForeColor.B);
        using var brush = new SolidBrush(fg);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, brush,
            new RectangleF(glyphSize.Width + 3, 0, Width - glyphSize.Width - 3, Height), sf);
    }
}

class SmoothLabel : Label
{
    public SmoothLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
    }

    protected override void OnTextChanged(EventArgs e)    { Invalidate(); base.OnTextChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color fg = Enabled ? ForeColor : Color.FromArgb(110, ForeColor.R, ForeColor.G, ForeColor.B);
        using var brush = new SolidBrush(fg);
        StringAlignment ha = TextAlign switch {
            CA.TopLeft   or CA.MiddleLeft   or CA.BottomLeft   => StringAlignment.Near,
            CA.TopRight  or CA.MiddleRight  or CA.BottomRight  => StringAlignment.Far,
            _ => StringAlignment.Center
        };
        StringAlignment va = TextAlign switch {
            CA.TopLeft   or CA.TopCenter   or CA.TopRight    => StringAlignment.Near,
            CA.BottomLeft or CA.BottomCenter or CA.BottomRight => StringAlignment.Far,
            _ => StringAlignment.Center
        };
        var sf = new StringFormat { Alignment = ha, LineAlignment = va, FormatFlags = StringFormatFlags.NoWrap };
        var rect = new RectangleF(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
        g.DrawString(Text, Font, brush, rect, sf);
    }
}
