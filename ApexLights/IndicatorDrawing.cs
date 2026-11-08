using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

internal readonly struct StripLayout
{
    private readonly Settings s;
    private readonly int gearIndex;
    private readonly double gearWidth;
    private readonly double sideWidth;
    public const double SideTileWidth = 46, SideTileHeight = 36;
    public const double FlagTileHeight = 38, CueGap = 6;
    public double FlagWidth { get; }
    public double FlagX => Size.Width - 9 - sideWidth - FlagWidth;
    public double SideX(bool left) => left ? 9 : Size.Width - 9 - sideWidth + CueGap;
    public double LensHeight { get; }
    public Size Size { get; }

    public StripLayout(Settings settings)
    {
        s = settings;
        var options = s.Indicators;
        sideWidth = options.ShowProximity ? SideTileWidth * options.SideScale + CueGap : 0;
        FlagWidth = options.ShowFlags ? (options.FlagStyle == FlagStyle.Labeled ? 138 : 48) * options.FlagScale : 0;
        gearIndex = options.GearPosition switch { GearPosition.Left => 0, GearPosition.Center => (s.Count + 1) / 2, _ => s.Count };
        gearWidth = options.ShowGear ? options.GearSize * 1.65 + s.Gap : 0;
        LensHeight = s.LensSize * (s.Shape == LensShape.Blade ? .72 : 1);
        double cueHeight = Math.Max(options.ShowFlags ? FlagTileHeight * options.FlagScale + 10 : 0,
            options.ShowProximity ? SideTileHeight * options.SideScale + 10 : 0);
        Size = new(18 + s.Count * s.LensSize + (s.Count - 1) * s.Gap + gearWidth +
            (options.ShowFlags ? FlagWidth + CueGap : 0) + 2 * sideWidth,
            Math.Max(cueHeight, Math.Max(16 + LensHeight, options.ShowGear ? options.GearSize * 1.45 + 4 : 0)));
    }

    public double LensX(int i) => 9 + sideWidth + i * (s.LensSize + s.Gap) + (i >= gearIndex ? gearWidth : 0);
    public double GearCenter => 9 + sideWidth + gearIndex * (s.LensSize + s.Gap) + (gearWidth - s.Gap) / 2;
}

public sealed partial class LedStrip
{
    private IndicatorFrame indicators;
    private FormattedText? gearText;
    private bool compactGear;

    public void DisplayIndicators(IndicatorFrame next)
    {
        if (indicators == next) return;
        indicators = next;
        InvalidateVisual();
    }

    private void DrawIndicators(DrawingContext dc, StripLayout layout)
    {
        if (settings.Indicators.ShowProximity)
        {
            DrawSide(dc, layout, true, indicators.Left);
            DrawSide(dc, layout, false, indicators.Right);
        }
        if (settings.Indicators.ShowFlags) DrawFlag(dc, layout);
        if (!settings.Indicators.ShowGear || string.IsNullOrEmpty(indicators.Gear)) return;
        var options = settings.Indicators;
        double podWidth = options.GearSize * 1.65;
        dc.DrawRoundedRectangle(GearPod, WellEdge,
            new Rect(layout.GearCenter - podWidth / 2, 2, podWidth, layout.Size.Height - 4), 5, 5);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double scale = Math.Min(ActualWidth / layout.Size.Width, ActualHeight / layout.Size.Height);
        bool compact = options.GearSize * scale * dpi < 20;
        if (gearText == null || gearText.Text != indicators.Gear || gearText.PixelsPerDip != dpi || compactGear != compact)
        {
            compactGear = compact;
            gearText = new FormattedText(indicators.Gear, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily(options.GearFont), FontStyles.Normal,
                    compact ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal),
                options.GearSize, Brushes.WhiteSmoke, dpi);
        }
        dc.DrawText(gearText, new Point(layout.GearCenter - gearText.Width / 2, (layout.Size.Height - gearText.Height) / 2));
    }
}
