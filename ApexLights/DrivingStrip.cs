using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

internal sealed class DrivingStrip : FrameworkElement
{
    internal const double SegmentHeight = 8;
    private const double GearGap = 2;
    private Settings settings = new();
    private readonly Brush[] colors = new Brush[4];
    private Brush inactive = Brushes.Transparent;
    private LightFrame frame;
    private string gear = "";
    private InstrumentText? glyph;

    private static double Gap(Settings s) => s.Gap * .4;
    private static double GearHeight(Settings s) => s.Indicators.GearSize * .55;
    internal static Size Footprint(Settings s) => new(
        s.Count * s.LensSize + (s.Count - 1) * Gap(s),
        SegmentHeight + (s.Indicators.ShowGear ? GearGap + GearHeight(s) : 0));

    public void Configure(Settings s)
    {
        settings = s;
        glyph = null;
        for (int i = 0; i < colors.Length; i++)
        {
            var c = (Color)ColorConverter.ConvertFromString(s.Colors[i]);
            colors[i] = HudArt.Frozen(new SolidColorBrush(Color.FromRgb(
                (byte)(c.R * s.Brightness), (byte)(c.G * s.Brightness), (byte)(c.B * s.Brightness))));
        }
        byte alpha = (byte)Math.Round(255 * Math.Sqrt(s.InactiveBrightness));
        inactive = HudArt.Frozen(new SolidColorBrush(Color.FromArgb(alpha, 46, 52, 58)));
        InvalidateVisual();
    }

    public void Display(LightFrame next)
    {
        if (next == frame) return;
        frame = next;
        InvalidateVisual();
    }

    public void DisplayIndicators(IndicatorFrame next)
    {
        if (next.Gear == gear) return;
        gear = next.Gear;
        glyph = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Footprint(settings);
        double scale = Math.Min(ActualWidth / size.Width, ActualHeight / size.Height);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - size.Width * scale) / 2,
            (ActualHeight - size.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        for (int i = 0; i < settings.Count; i++)
        {
            int rank = ShiftLights.Rank(i, settings.Count, settings.Direction);
            var fill = rank < frame.Lit && frame.BrightPhase ? colors[ShiftLights.ColorZone(rank, settings.Count)] : inactive;
            dc.DrawRectangle(fill, null, new Rect(i * (settings.LensSize + Gap(settings)), 0, settings.LensSize, SegmentHeight));
        }
        if (settings.Indicators.ShowGear && !string.IsNullOrEmpty(gear))
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (glyph == null || !glyph.Matches(gear, dpi))
                glyph = new InstrumentText(gear, new Typeface(new FontFamily(settings.Indicators.GearFont),
                    FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), GearHeight(settings), Brushes.White, dpi, HudArt.GearKeyline);
            glyph.DrawCentered(dc, new Rect(0, SegmentHeight + GearGap, size.Width, GearHeight(settings)));
        }
        dc.Pop();
        dc.Pop();
    }
}
