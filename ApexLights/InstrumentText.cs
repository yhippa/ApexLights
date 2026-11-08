using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ApexLights;

// Use the drawn glyphs for alignment. Italic overhangs and font line spacing make the
// advance width and line height a poor measure of the visible center of a digit.
internal sealed class InstrumentText
{
    private readonly FormattedText text;
    private readonly Rect ink;

    public InstrumentText(string value, Typeface face, double size, Brush color, double dpi)
    {
        text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, color, dpi);
        ink = text.BuildGeometry(new Point()).Bounds;
        if (ink.IsEmpty) ink = new Rect(0, 0, text.Width, text.Height);
    }

    public bool Matches(string value, double dpi) => text.Text == value && text.PixelsPerDip == dpi;

    public void DrawCentered(DrawingContext dc, Rect area, InstrumentText? caption = null)
    {
        const double gap = 3;
        double width = Math.Max(ink.Width, caption?.ink.Width ?? 0);
        double height = ink.Height + (caption == null ? 0 : gap + caption.ink.Height);
        if (width <= 0 || height <= 0) return;
        double cx = area.X + area.Width / 2, cy = area.Y + area.Height / 2;
        double fit = Math.Min(area.Width / width, area.Height / height);
        if (fit < 1) dc.PushTransform(new ScaleTransform(fit, fit, cx, cy));
        double top = cy - height / 2;
        DrawAt(dc, cx, top);
        caption?.DrawAt(dc, cx, top + ink.Height + gap);
        if (fit < 1) dc.Pop();
    }

    private void DrawAt(DrawingContext dc, double centerX, double inkTop) =>
        dc.DrawText(text, new Point(centerX - ink.Left - ink.Width / 2, inkTop - ink.Top));
}
