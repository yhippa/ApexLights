using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

internal readonly struct StripLayout
{
    public const double SideTileWidth = 48, SideTileHeight = 35, FlagTileHeight = 36, CueGap = 6;
    private const double RailPadding = 8;
    private readonly Settings s;
    private readonly int firstCount;
    private readonly double margin;
    public Rect FirstRail { get; }
    public Rect SecondRail { get; }
    public Rect GearPodBounds { get; }
    public Rect ShellBounds { get; }
    public double FlagWidth { get; }
    public double FlagX { get; }
    public double LensHeight { get; }
    public Size Size { get; }

    public StripLayout(Settings settings)
    {
        s = settings;
        var opt = s.Indicators;
        LensHeight = s.LensSize * (s.Shape == LensShape.Blade ? .72 : 1);
        FlagWidth = opt.ShowFlags ? (opt.FlagStyle == FlagStyle.Labeled ? 152 : 48) * opt.FlagScale : 0;
        firstCount = !opt.ShowGear ? s.Count : opt.GearPosition switch
        {
            GearPosition.Left => 0, GearPosition.Center => (s.Count + 1) / 2, _ => s.Count
        };
        margin = opt.ShowFlags || opt.ShowProximity || opt.ShowGear ? 7 : 1;
        double shellH = Math.Max(LensHeight + 16, Math.Max(opt.ShowProximity ? SideTileHeight * opt.SideScale + 11 : 0,
            opt.ShowFlags ? FlagTileHeight * opt.FlagScale + 10 : 0));
        double podH = opt.ShowGear ? Math.Max(opt.GearSize * 1.35 + 6, Math.Min(54, LensHeight + 36)) : 0;
        double height = Math.Max(shellH, podH + (opt.ShowGear ? 2 : 0));
        double railH = Math.Max(LensHeight + 6, shellH - 10);
        double side = opt.ShowProximity ? SideTileWidth * opt.SideScale + CueGap : 0;
        double x = margin + side;
        Rect Rail(int count, double left) => count == 0 ? Rect.Empty :
            new Rect(left, (height - railH) / 2, 2 * RailPadding + count * settings.LensSize + (count - 1) * settings.Gap, railH);
        FirstRail = Rail(firstCount, x);
        if (!FirstRail.IsEmpty) x = FirstRail.Right;
        GearPodBounds = Rect.Empty;
        if (opt.ShowGear)
        {
            if (firstCount > 0) x += CueGap;
            GearPodBounds = new Rect(x, (height - podH) / 2, Math.Max(opt.GearSize * 1.6, podH * 1.04), podH);
            x = GearPodBounds.Right;
            if (firstCount < s.Count) x += CueGap;
        }
        SecondRail = Rail(s.Count - firstCount, x);
        if (!SecondRail.IsEmpty) x = SecondRail.Right;
        FlagX = x + CueGap;
        if (opt.ShowFlags) x = FlagX + FlagWidth;
        Size = new(x + side + margin, height);
        ShellBounds = new Rect(.6, (height - shellH) / 2 + .6, Size.Width - 1.2, shellH - 1.2);
    }

    public double LensX(int index) => index < firstCount
        ? FirstRail.X + RailPadding + index * (s.LensSize + s.Gap)
        : SecondRail.X + RailPadding + (index - firstCount) * (s.LensSize + s.Gap);
    public double SideX(bool left) => left ? margin : Size.Width - margin - SideTileWidth * s.Indicators.SideScale;
}

public sealed partial class LedStrip
{
    private IndicatorFrame indicators;
    private InstrumentText? gearText;
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
        if (!settings.Indicators.ShowGear) return;
        var options = settings.Indicators;
        Rect pod = layout.GearPodBounds;
        var face = pod;
        face.Inflate(-1.3, -1.3);
        double corner = Math.Min(13, pod.Height * .21);
        dc.DrawRoundedRectangle(GearPod, PodRim, face, corner, corner);
        if (string.IsNullOrEmpty(indicators.Gear)) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double scale = Math.Min(ActualWidth / layout.Size.Width, ActualHeight / layout.Size.Height);
        bool compact = options.GearSize * scale * dpi < 20;
        if (gearText == null || !gearText.Matches(indicators.Gear, dpi) || compactGear != compact)
        {
            compactGear = compact;
            gearText = new InstrumentText(indicators.Gear,
                new Typeface(new FontFamily(options.GearFont), FontStyles.Italic,
                    compact ? FontWeights.Black : FontWeights.ExtraBold, FontStretches.Normal),
                options.GearSize, Brushes.White, dpi);
        }
        pod.Inflate(-4, -4);
        gearText.DrawCentered(dc, pod);
    }
}
