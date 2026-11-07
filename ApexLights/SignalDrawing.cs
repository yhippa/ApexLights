using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip
{
    private static readonly Brush Amber = Solid("#FFBE46"), Ink = Solid("#10171A"), Chalk = Solid("#F4F7F2");
    private static readonly Brush StatusWell = Solid("#202629");
    private static readonly Brush FlagYellow = Solid("#FFCF3C"), FlagRed = Solid("#FF6254"),
        FlagBlue = Solid("#73B9FF"), FlagGreen = Solid("#59E695"), Orange = Solid("#FF9048");
    private static readonly Pen DarkEdge = Freeze(new Pen(Ink, 1.8)), LightEdge = Freeze(new Pen(Chalk, 2));
    private static readonly Geometry Chevron = Freeze(Geometry.Parse("M8,0 H16 L8,12 L16,24 H8 L0,12 Z"));
    private static readonly Geometry Banner = Freeze(Geometry.Parse("M0,0 Q7,-3 14,0 T28,0 V18 Q21,21 14,18 T0,18 Z"));
    private static readonly Typeface FlagTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private FormattedText? flagLabel;

    private void DrawSide(DrawingContext dc, StripLayout layout, bool left, int count)
    {
        if (count == 0) return;
        double scale = settings.Indicators.SideScale;
        double width = StripLayout.SideTileWidth, height = StripLayout.SideTileHeight;
        dc.PushTransform(new TranslateTransform(layout.SideX(left), (layout.Size.Height - height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var tile = new Rect(0, 0, width, height);
        if (settings.Indicators.SideStyle == SideStyle.Lamps)
        {
            dc.DrawRoundedRectangle(StatusWell, null, tile, 4, 4);
            double lampWidth = count == 2 ? 16 : 36;
            for (int i = 0; i < count; i++)
                dc.DrawRoundedRectangle(Amber, null, new Rect(5 + i * 20, 4, lampWidth, height - 8), 3, 3);
        }
        else
        {
            dc.DrawRoundedRectangle(Amber, null, tile, 4, 4);
            if (!left) dc.PushTransform(new MatrixTransform(-1, 0, 0, 1, width, 0));
            double start = (width - (count == 2 ? 36 : 16)) / 2;
            for (int i = 0; i < count; i++)
            {
                dc.PushTransform(new TranslateTransform(start + i * 20, (height - 24) / 2));
                dc.DrawGeometry(Ink, null, Chevron);
                dc.Pop();
            }
            if (!left) dc.Pop();
        }
        dc.Pop();
        dc.Pop();
    }

    private static (Brush Tile, string Label) FlagArt(RaceFlag flag) => flag switch
    {
        RaceFlag.Green => (FlagGreen, "GREEN"), RaceFlag.Red => (FlagRed, "RED"),
        RaceFlag.Black => (Chalk, "BLACK"), RaceFlag.Repair => (Orange, "REPAIR"),
        RaceFlag.Blue => (FlagBlue, "BLUE"), RaceFlag.White => (Chalk, "WHITE"),
        RaceFlag.Checkered => (Chalk, "FINISH"),
        RaceFlag.WavingYellow => (FlagYellow, "WAVING"), RaceFlag.WavingCaution => (FlagYellow, "FCY WAVE"),
        RaceFlag.Caution => (FlagYellow, "CAUTION"), _ => (FlagYellow, "YELLOW")
    };

    private void DrawFlag(DrawingContext dc, StripLayout layout)
    {
        var flag = indicators.Flag;
        var (fill, label) = FlagArt(flag);
        double scale = settings.Indicators.FlagScale;
        var prominence = IndicatorPolicy.Prominence(flag);
        bool notice = prominence == FlagProminence.Informational;
        bool critical = prominence == FlagProminence.Critical;
        bool labeled = settings.Indicators.FlagStyle == FlagStyle.Labeled && !indicators.NoticeSettled;
        double width = layout.FlagWidth / scale;
        double faceWidth = indicators.NoticeSettled ? Math.Min(46, width) : width;
        var tile = new Rect(0, 0, faceWidth, StripLayout.FlagTileHeight);
        dc.PushTransform(new TranslateTransform(layout.FlagX, (layout.Size.Height - tile.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawRoundedRectangle(critical ? fill : StatusWell, null, tile, 4, 4);
        if (prominence == FlagProminence.Caution)
            dc.DrawRoundedRectangle(fill, null, new Rect(0, 0, labeled ? 46 : faceWidth, tile.Height), 4, 4);
        dc.PushTransform(new TranslateTransform(labeled ? 9 : (faceWidth - 28) / 2, 8));
        Pen edge = notice ? LightEdge : DarkEdge;
        dc.DrawLine(edge, new Point(0, -1), new Point(0, 25));
        Brush fabric = notice ? fill : Ink;
        dc.DrawGeometry(fabric, edge, Banner);
        if (flag == RaceFlag.Checkered)
        {
            dc.PushClip(Banner);
            for (int y = -1; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    if (((x + y) & 1) == 0) dc.DrawRectangle(Ink, null, new Rect(x * 7, y * 6, 7, 6));
            dc.Pop();
        }
        if (flag == RaceFlag.Repair) dc.DrawEllipse(Orange, null, new Point(14, 9), 5, 5);
        if (flag is RaceFlag.WavingYellow or RaceFlag.WavingCaution)
        {
            dc.DrawLine(DarkEdge, new Point(8, 23), new Point(20, 23));
            dc.DrawLine(DarkEdge, new Point(15, 26), new Point(25, 26));
        }
        dc.Pop();
        if (labeled)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (flagLabel == null || flagLabel.Text != label || flagLabel.PixelsPerDip != dpi)
                flagLabel = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    FlagTypeface, 18, critical ? Ink : (notice ? fill : Chalk), dpi);
            dc.DrawText(flagLabel, new Point(critical ? 45 : 50, (tile.Height - flagLabel.Height) / 2));
        }
        if (indicators.Accent > 0)
        {
            dc.PushOpacity(indicators.Accent * .22);
            dc.DrawRoundedRectangle(null, LightEdge, tile, 5, 5);
            dc.Pop();
        }
        dc.Pop();
        dc.Pop();
    }
}
