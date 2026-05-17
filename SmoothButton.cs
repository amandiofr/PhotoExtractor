using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using CA = System.Drawing.ContentAlignment;

class SmoothButton : Button
{
    bool _hovered, _pressed;

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
        g.Clear(bg);

        int bs = FlatAppearance.BorderSize;
        if (bs > 0)
        {
            using var pen = new Pen(FlatAppearance.BorderColor, bs);
            g.DrawRectangle(pen, bs / 2f, bs / 2f, Width - bs, Height - bs);
        }

        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        Color fg = Enabled ? ForeColor : Color.FromArgb(110, ForeColor.R, ForeColor.G, ForeColor.B);
        using var brush = new SolidBrush(fg);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), sf);
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
