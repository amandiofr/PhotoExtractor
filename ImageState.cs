using OpenCvSharp;
using System.Drawing;

record struct WhiteRun(int Y, float MidX, int Width);
record struct Step2Segment(PointF P1, PointF P2, float AvgWidth);

class ImageState
{
    public Mat? Image;
    public Mat? Edges;
    public LineSegmentPoint[]? HoughLines;
    public LineSegmentPoint?[] SelectedBorders = new LineSegmentPoint?[4];
    public List<Point2f> DoneCenters = new();
    public string? ImagePath;
    public int ExtractOrientation;
    public bool OrientationTouched;
    public List<WhiteRun> LocrStep1Runs = new();
    public List<Step2Segment> LocrStep2Segs = new();
    public List<Step2Segment> LocrStep3Segs = new();
    public List<Step2Segment> LocrStep4Segs = new();
}
