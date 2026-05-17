using OpenCvSharp;
using System.Drawing;

partial class MainForm
{
    static bool IsInsideQuad(Point2f p, Point2f[] quad)
    {
        int? sign = null;
        for (int i = 0; i < quad.Length; i++)
        {
            var a = quad[i];
            var b = quad[(i + 1) % quad.Length];
            int s = Math.Sign((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));
            if (s == 0) continue;
            if (sign == null) sign = s;
            else if (s != sign) return false;
        }
        return true;
    }

    static float LineAngle(LineSegmentPoint seg)
    {
        float dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
        float a = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
        return ((a % 180) + 180) % 180;
    }

    static float AngleDiff(float a, float b)
    {
        float d = Math.Abs(a - b) % 180;
        return Math.Min(d, 180 - d);
    }

    static Point2f LineIntersect(LineSegmentPoint a, LineSegmentPoint b)
    {
        float x1 = a.P1.X, y1 = a.P1.Y, x2 = a.P2.X, y2 = a.P2.Y;
        float x3 = b.P1.X, y3 = b.P1.Y, x4 = b.P2.X, y4 = b.P2.Y;
        float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 0.001f) return new Point2f((x1 + x3) / 2, (y1 + y3) / 2);
        float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        return new Point2f(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
    }

    static float Dist(Point2f a, Point2f b) =>
        (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    static float DistPointToLine(Point2f p, Point2f a, Point2f b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return Dist(p, a);
        return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / len;
    }

    static float DistPointToSegment(Point2f p, Point2f a, Point2f b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return Dist(p, a);
        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0f, 1f);
        return Dist(p, new Point2f(a.X + t * dx, a.Y + t * dy));
    }

    static float DistPtSegF(System.Drawing.Point p, System.Drawing.PointF a, System.Drawing.PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0f, 1f);
        float nx = a.X + t * dx - p.X, ny = a.Y + t * dy - p.Y;
        return (float)Math.Sqrt(nx * nx + ny * ny);
    }

    static float DistPtLineF(System.Drawing.Point p, System.Drawing.PointF a, System.Drawing.PointF b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / len;
    }
}
