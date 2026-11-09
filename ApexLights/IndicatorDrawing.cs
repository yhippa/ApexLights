using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

internal readonly struct StripLayout
{
    public const double SideTileWidth = 48, SideTileHeight = 35, FlagTileHeight = 36, CueGap = 6;
    private readonly double lensPadding;
    private readonly Settings s;
    private readonly int firstCount;
    private readonly double sideMargin;
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
        double shellH = Math.Max(LensHeight + 16, Math.Max(opt.ShowProximity ? SideTileHeight * opt.SideScale + 11 : 0,
            opt.ShowFlags ? FlagTileHeight * opt.FlagScale + 10 : 0));
        double podH = opt.ShowGear ? Math.Max(opt.GearSize * 1.35 + 6, Math.Min(54, LensHeight + 36)) : 0;
        double height = Math.Max(shellH, podH + (opt.ShowGear ? 2 : 0));
        double padding = (height - LensHeight) / 2;
        lensPadding = padding;
        sideMargin = opt.ShowProximity ? Math.Max(6, (height - SideTileHeight * opt.SideScale) / 2) : 0;
        // Reserve equal space outside the pill so a right-hand flag cannot shift the instrument off center.
        double statusSpace = opt.ShowFlags ? FlagWidth + CueGap : 0;
        double side = opt.ShowProximity ? SideTileWidth * opt.SideScale : 0;
        double x = statusSpace + sideMargin + side;
        Rect Rail(int count, double left) => count == 0 ? Rect.Empty :
            new Rect(left, 0, 2 * padding + count * settings.LensSize + (count - 1) * settings.Gap, height);
        FirstRail = Rail(firstCount, x);
        if (!FirstRail.IsEmpty) x = FirstRail.Right;
        GearPodBounds = Rect.Empty;
        if (opt.ShowGear)
        {
            GearPodBounds = new Rect(x, (height - podH) / 2, Math.Max(opt.GearSize * 1.6, podH * 1.04), podH);
            x = GearPodBounds.Right;
        }
        SecondRail = Rail(s.Count - firstCount, x);
        if (!SecondRail.IsEmpty) x = SecondRail.Right;
        double pillRight = x + side + sideMargin;
        Size = new(pillRight + statusSpace, height);
        ShellBounds = new Rect(statusSpace + .6, .6, pillRight - statusSpace - 1.2, height - 1.2);
        FlagX = pillRight + CueGap;
    }

    public double LensX(int index) => index < firstCount
        ? FirstRail.X + lensPadding + index * (s.LensSize + s.Gap)
        : SecondRail.X + lensPadding + (index - firstCount) * (s.LensSize + s.Gap);
    public double SideX(bool left) => left ? ShellBounds.Left - .6 + sideMargin :
        ShellBounds.Right + .6 - sideMargin - SideTileWidth * s.Indicators.SideScale;
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
        if (settings.DarkHousing)
        {
            // A hairline separates the gear from the adjacent bank inside the same continuous surface.
            double inset = pod.Height * .22;
            if (!layout.FirstRail.IsEmpty)
                dc.DrawLine(HudArt.Hairline, new Point(pod.Left, pod.Top + inset), new Point(pod.Left, pod.Bottom - inset));
            if (!layout.SecondRail.IsEmpty)
                dc.DrawLine(HudArt.Hairline, new Point(pod.Right, pod.Top + inset), new Point(pod.Right, pod.Bottom - inset));
        }
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
                options.GearSize, Brushes.White, dpi, HudArt.GearKeyline);
        }
        pod.Inflate(-4, -4);
        gearText.DrawCentered(dc, pod);
    }
}
