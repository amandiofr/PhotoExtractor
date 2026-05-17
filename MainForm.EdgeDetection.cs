using OpenCvSharp;
using System.Drawing;

partial class MainForm
{
    void UpdateEdges()
    {
        if (image == null) return;
        int blur = sliderBlur.Value % 2 == 1 ? sliderBlur.Value : sliderBlur.Value + 1;
        Mat gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Mat blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(blur, blur), 0);
        edges = new Mat();
        Cv2.Canny(blurred, edges, sliderLow.Value, sliderHigh.Value);
        if (rbLOCR.Checked) DrawTestPattern(edges);
        houghLines = rbLOCR.Checked
            ? DetectLinesLOCR()
            : Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold: 40,
                              minLineLength: sliderMinLen.Value, maxLineGap: sliderGap.Value);
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (image == null)
        {
            displayBitmap = null;
            rawBitmap?.Dispose(); rawBitmap = null;
            pictureBox.Invalidate();
            return;
        }

        Mat display;
        if (checkEdges.Checked && edges != null)
        {
            Mat edgesForDisplay;
            if (rbLOCR.Checked)
            {
                edgesForDisplay = edges;
            }
            else
            {
                var thick = new Mat();
                var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                Cv2.Dilate(edges, thick, kernel, iterations: 1);
                edgesForDisplay = thick;
            }
            Mat edgesBGR = new Mat();
            Cv2.CvtColor(edgesForDisplay, edgesBGR, ColorConversionCodes.GRAY2BGR);
            if (!rbLOCR.Checked) edgesForDisplay.Dispose();
            display = new Mat();
            Cv2.AddWeighted(edgesBGR, 0.75, image, 0.25, 0, display);
            edgesBGR.Dispose();
        }
        else
        {
            display = image.Clone();
        }

        if (checkLines.Checked && houghLines != null)
        {
            foreach (var seg in houghLines)
                Cv2.Line(display, seg.P1, seg.P2, new Scalar(220, 0, 220), 2);
        }

        rawBitmap?.Dispose();
        rawBitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
        displayBitmap?.Dispose();
        displayBitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(display);
        display.Dispose();
        pictureBox.Invalidate();
    }
}
