using OpenCvSharp;
using System.Windows.Forms;
using System.Drawing;

partial class MainForm
{
    void OnPictureBoxMouseWheel(object? sender, MouseEventArgs e)
    {
        if (image == null || displayBitmap == null) return;
        float factor = e.Delta > 0 ? 1.25f : 0.8f;
        float newZoom = Math.Clamp(viewZoom * factor, 0.5f, 140f);

        // Garde le point image sous le curseur fixe
        var (ox, oy, w, h) = ZoomedRect();
        float imgX = (e.X - ox) * image.Width / w;
        float imgY = (e.Y - oy) * image.Height / h;

        viewZoom = newZoom;
        var (bx, by, bw, bh) = BaseRect();
        float nw = bw * viewZoom, nh = bh * viewZoom;
        viewPan = new PointF(e.X - imgX * nw / image.Width - (bx + (bw - nw) / 2f),
                             e.Y - imgY * nh / image.Height - (by + (bh - nh) / 2f));
        ClampPan();
        pictureBox.Invalidate();
    }

    (int bx, int by, int bw, int bh) BaseRect()
    {
        float imgRatio = (float)image!.Width / image.Height;
        float boxRatio = (float)pictureBox.Width / pictureBox.Height;
        int bw, bh, bx, by;
        if (imgRatio > boxRatio) { bw = pictureBox.Width; bh = (int)(pictureBox.Width / imgRatio); bx = 0; by = (pictureBox.Height - bh) / 2; }
        else { bh = pictureBox.Height; bw = (int)(pictureBox.Height * imgRatio); by = 0; bx = (pictureBox.Width - bw) / 2; }
        return (bx, by, bw, bh);
    }

    (float ox, float oy, float w, float h) ZoomedRect()
    {
        var (bx, by, bw, bh) = BaseRect();
        float zw = bw * viewZoom, zh = bh * viewZoom;
        return (bx + (bw - zw) / 2f + viewPan.X, by + (bh - zh) / 2f + viewPan.Y, zw, zh);
    }

    void ClampPan()
    {
        if (image == null) return;
        var (bx, by, bw, bh) = BaseRect();
        float zw = bw * viewZoom, zh = bh * viewZoom;
        float ox0 = bx + (bw - zw) / 2f;
        float oy0 = by + (bh - zh) / 2f;
        float m = 50f;
        viewPan = new PointF(
            Math.Clamp(viewPan.X, m - ox0 - zw, pictureBox.Width  - m - ox0),
            Math.Clamp(viewPan.Y, m - oy0 - zh, pictureBox.Height - m - oy0));
    }

    System.Drawing.PointF? ImageToDisplay(Point2f pt)
    {
        if (image == null || displayBitmap == null) return null;
        var (ox, oy, w, h) = ZoomedRect();
        return new System.Drawing.PointF(ox + pt.X * w / image.Width, oy + pt.Y * h / image.Height);
    }

    Point2f? DisplayToImage(System.Drawing.Point pt)
    {
        if (image == null || displayBitmap == null) return null;
        var (ox, oy, w, h) = ZoomedRect();
        float ix = (pt.X - ox) * image.Width / w;
        float iy = (pt.Y - oy) * image.Height / h;
        if (ix < 0 || iy < 0 || ix >= image.Width || iy >= image.Height) return null;
        return new Point2f(ix, iy);
    }
}
