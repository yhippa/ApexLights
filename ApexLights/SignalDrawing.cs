using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip
{
    private static readonly Brush Chalk = HudArt.Solid("#F6F9FF");
    private static readonly Brush Operational = HudArt.Solid("#A4E6E5");
    private static readonly Pen GaugePen = HudArt.Frozen(new Pen(Operational, 2.4));
    private static readonly Geometry Gauge = HudArt.Frozen(Geometry.Parse("M2,20 A12,12 0 0 1 26,20 M14,20 L21,11 M3,20 L7,20 M21,20 L25,20"));
    private static readonly Geometry Chevron = HudArt.Frozen(Geometry.Parse("M8,0 H16 L8,12 L16,24 H8 L0,12 Z"));
    private static readonly Typeface FlagTypeface = new(new FontFamily("Segoe UI Variable, Segoe UI"), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);
    private const double IconLane = 46;
    private InstrumentText? flagLabel, waveCaption;

    private void DrawSide(DrawingContext dc, StripLayout layout, bool left, int count)
    {
        bool active = count != 0;
        if (!active && !settings.DarkHousing) return;
        double scale = settings.Indicators.SideScale;
        double width = StripLayout.SideTileWidth, height = StripLayout.SideTileHeight;
        dc.PushTransform(new TranslateTransform(layout.SideX(left), (layout.Size.Height - height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var tile = new Rect(0, 0, width, height);
        if (active)
            dc.DrawRoundedRectangle(HudArt.Rail, HudArt.Hairline, tile, 8, 8);
        if (settings.Indicators.SideStyle == SideStyle.Lamps)
        {
            int lamps = active ? count : 2;
            double lampWidth = (width - 10 - (lamps - 1) * 4) / lamps;
            for (int i = 0; i < lamps; i++)
                dc.DrawRoundedRectangle(active ? HudArt.Amber : HudArt.RestingMark, null, new Rect(5 + i * (lampWidth + 4), 4, lampWidth, height - 8), 3, 3);
        }
        else
        {
            if (!left) dc.PushTransform(new MatrixTransform(-1, 0, 0, 1, width, 0));
            int marks = active ? count : 2;
            double start = (width - (marks == 2 ? 36 : 16)) / 2;
            for (int i = 0; i < marks; i++)
            {
                dc.PushTransform(new TranslateTransform(start + i * 20, (height - 24) / 2));
                dc.DrawGeometry(active ? HudArt.Amber : HudArt.RestingMark, null, Chevron);
                dc.Pop();
            }
            if (!left) dc.Pop();
        }
        dc.Pop();
        dc.Pop();
    }

    private static string StatusLabel(RaceFlag flag) => flag switch
    {
        RaceFlag.Green => "GREEN", RaceFlag.Red => "RED", RaceFlag.Black => "BLACK",
        RaceFlag.Repair => "REPAIR", RaceFlag.Blue => "BLUE", RaceFlag.White => "WHITE",
        RaceFlag.Checkered => "FINISH", RaceFlag.Caution or RaceFlag.WavingCaution => "CAUTION",
        _ => "YELLOW"
    };

    private void DrawFlag(DrawingContext dc, StripLayout layout)
    {
        var flag = indicators.Flag;
        string label = indicators.Limiter ? "LIMITER" : StatusLabel(flag);
        double scale = settings.Indicators.FlagScale;
        bool labeled = settings.Indicators.FlagStyle == FlagStyle.Labeled;
        double width = layout.FlagWidth / scale;
        var tile = new Rect(0, 0, width, StripLayout.FlagTileHeight);
        dc.PushTransform(new TranslateTransform(layout.FlagX, (layout.Size.Height - tile.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (flag != RaceFlag.None || indicators.Limiter)
        {
            dc.DrawRoundedRectangle(HudArt.Rail, HudArt.Hairline, tile, 8, 8);
            double lane = labeled ? IconLane : width;
            if (indicators.Limiter)
            {
                dc.PushTransform(new TranslateTransform((lane - 28) / 2, (tile.Height - 25) / 2));
                dc.DrawGeometry(null, GaugePen, Gauge);
                dc.Pop();
            }
            else dc.DrawRectangle(HudArt.Flag(flag), null, new Rect(2, 1, lane - 4, tile.Height - 2));
            if (labeled) DrawStatusLabel(dc, label, flag is RaceFlag.WavingYellow or RaceFlag.WavingCaution, tile);
            if (indicators.Accent > 0)
            {
                dc.PushOpacity(indicators.Accent * .22);
                dc.DrawRoundedRectangle(null, HudArt.ShellEdge, tile, 8, 8);
                dc.Pop();
            }
        }
        dc.Pop();
        dc.Pop();
    }

    private void DrawStatusLabel(DrawingContext dc, string label, bool waving, Rect tile)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (flagLabel == null || !flagLabel.Matches(label, dpi))
            flagLabel = new InstrumentText(label, FlagTypeface, 18, Chalk, dpi);
        if (waving && (waveCaption == null || !waveCaption.Matches("WAVING", dpi)))
            waveCaption = new InstrumentText("WAVING", FlagTypeface, 10, HudArt.Amber, dpi);
        var area = new Rect(IconLane + 8, 5, tile.Width - IconLane - 16, tile.Height - 10);
        flagLabel.DrawCentered(dc, area, waving ? waveCaption : null);
    }
}
