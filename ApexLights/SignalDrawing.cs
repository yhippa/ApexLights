using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip
{
    private static readonly Brush SideColor = Solid("#FFD37B");
    private static readonly Pen SidePen = Freeze(new Pen(SideColor, 2.3));
    private static readonly Pen FlagEdge = Freeze(new Pen(Solid("#D9E0DE"), .9));
    private static readonly Brush FlagYellow = Solid("#FFD355"), FlagRed = Solid("#FD5143"),
        FlagBlue = Solid("#479BFF"), FlagGreen = Solid("#43EC86");
    private static readonly Geometry Banner = Freeze(Geometry.Parse("M0,0 Q4,-2 8,0 T16,0 L16,11 Q12,13 8,11 T0,11 Z"));
    private FormattedText? flagLabel;

    private void DrawSide(DrawingContext dc, double tip, double mid, int direction, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double x = tip - direction * i * 7;
            if (settings.Indicators.SideStyle == SideStyle.Lamps)
                dc.DrawRoundedRectangle(SideColor, null, new Rect(x - 2, mid - 6, 4, 12), 1.5, 1.5);
            else
            {
                var end = new Point(x, mid);
                dc.DrawLine(SidePen, new Point(x - direction * 4, mid - 5), end);
                dc.DrawLine(SidePen, end, new Point(x - direction * 4, mid + 5));
            }
        }
    }

    private static (Brush Fill, string Label) FlagArt(RaceFlag flag) => flag switch
    {
        RaceFlag.Green => (FlagGreen, "GREEN"), RaceFlag.Red => (FlagRed, "RED"),
        RaceFlag.Black => (Brushes.Black, "BLACK"), RaceFlag.Repair => (Brushes.Black, "REPAIR"),
        RaceFlag.Blue => (FlagBlue, "BLUE"), RaceFlag.White => (Brushes.WhiteSmoke, "WHITE"),
        RaceFlag.Checkered => (Brushes.WhiteSmoke, "FINISH"),
        RaceFlag.WavingYellow => (FlagYellow, "WAVING"), RaceFlag.WavingCaution => (FlagYellow, "FCY WAVE"),
        RaceFlag.Caution => (FlagYellow, "FCY"), _ => (FlagYellow, "YELLOW")
    };

    private void DrawFlag(DrawingContext dc, StripLayout layout)
    {
        var (fill, label) = FlagArt(indicators.Flag);
        dc.PushTransform(new TranslateTransform(layout.FlagX + 5, layout.Size.Height / 2 - 6));
        dc.DrawLine(FlagEdge, new Point(0,-1), new Point(0,17));
        dc.DrawGeometry(fill, FlagEdge, Banner);
        if (indicators.Flag == RaceFlag.Checkered)
        {
            dc.PushClip(Banner);
            for (int y = -1; y < 3; y++)
                for (int x = 0; x < 4; x++)
                    if (((x + y) & 1) == 0) dc.DrawRectangle(Brushes.Black, null, new Rect(x * 4, y * 4, 4, 4));
            dc.Pop();
        }
        if (indicators.Flag == RaceFlag.Repair) dc.DrawEllipse(Brushes.OrangeRed, null, new Point(8,5.5), 3.4, 3.4);
        if (indicators.Flag is RaceFlag.WavingYellow or RaceFlag.WavingCaution)
        {
            dc.DrawLine(SidePen, new Point(5,14), new Point(13,14));
            dc.DrawLine(SidePen, new Point(8,17), new Point(15,17));
        }
        if (settings.Indicators.FlagStyle == FlagStyle.Labeled)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (flagLabel == null || flagLabel.Text != label || flagLabel.PixelsPerDip != dpi)
                flagLabel = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Consolas"), 11, Brushes.WhiteSmoke, dpi);
            dc.DrawText(flagLabel, new Point(22, 6 - flagLabel.Height / 2));
        }
        if (indicators.Accent > 0)
        {
            dc.PushOpacity(indicators.Accent * .18);
            dc.DrawRoundedRectangle(null, FlagEdge, new Rect(-3, -4, layout.FlagWidth - 4, 25), 3, 3);
            dc.Pop();
        }
        dc.Pop();
    }
}
