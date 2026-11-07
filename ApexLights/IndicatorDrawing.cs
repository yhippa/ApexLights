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
    public double FlagWidth { get; }
    public double FlagX => Size.Width - 9 - sideWidth - FlagWidth;
    public double LensHeight { get; }
    public Size Size { get; }

    public StripLayout(Settings settings)
    {
        s = settings;
        var options = s.Indicators;
        sideWidth = options.ShowProximity ? 19 : 0;
        FlagWidth = options.ShowFlags ? (options.FlagStyle == FlagStyle.Labeled ? 84 : 28) : 0;
        gearIndex = options.GearPosition switch { GearPosition.Left => 0, GearPosition.Center => (s.Count + 1) / 2, _ => s.Count };
        gearWidth = options.ShowGear ? options.GearSize * 1.65 + s.Gap : 0;
        LensHeight = s.LensSize * (s.Shape == LensShape.Blade ? .72 : 1);
        Size = new(18 + s.Count * s.LensSize + (s.Count - 1) * s.Gap + gearWidth + FlagWidth + 2 * sideWidth,
            Math.Max(16 + LensHeight, options.ShowGear ? options.GearSize * 1.45 + 4 : 0));
    }

    public double LensX(int i) => 9 + sideWidth + i * (s.LensSize + s.Gap) + (i >= gearIndex ? gearWidth : 0);
    public double GearCenter => 9 + sideWidth + gearIndex * (s.LensSize + s.Gap) + (gearWidth - s.Gap) / 2;
}

public sealed partial class LedStrip
{
    private IndicatorFrame indicators;
    private FormattedText? gearText;

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
            DrawSide(dc, 10, layout.Size.Height / 2, -1, indicators.Left);
            DrawSide(dc, layout.Size.Width - 10, layout.Size.Height / 2, 1, indicators.Right);
        }
        if (indicators.Flag != RaceFlag.None && settings.Indicators.ShowFlags) DrawFlag(dc, layout);
        if (!settings.Indicators.ShowGear || string.IsNullOrEmpty(indicators.Gear)) return;
        var options = settings.Indicators;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (gearText == null || gearText.Text != indicators.Gear || gearText.PixelsPerDip != dpi)
            gearText = new FormattedText(indicators.Gear, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily(options.GearFont), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                options.GearSize, Brushes.WhiteSmoke, dpi);
        dc.DrawText(gearText, new Point(layout.GearCenter - gearText.Width / 2, (layout.Size.Height - gearText.Height) / 2));
    }
}
