using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip : FrameworkElement
{
    private Settings settings = new();
    private LightFrame frame;
    private readonly Brush[] lens = new Brush[4], halo = new Brush[4];
    private static readonly Brush Housing = Gradient("#252B2D", "#101416");
    private static readonly Brush Unlit = Gradient("#333C3F", "#1B2023");
    private static readonly Brush Socket = Solid("#090C0D");
    private static readonly Pen Rim = Freeze(new Pen(Solid("#495052"), 0.8));
    private static readonly Pen Glint = Freeze(new Pen(Solid("#70FFFFFF"), 0.8));
    private static readonly Pen Rail = Freeze(new Pen(Solid("#3B4245"), 0.8));

    public LedStrip() { IsHitTestVisible = false; Configure(settings); }
    private static T Freeze<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static Brush Solid(string hex) => Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));
    private static Brush Gradient(string a, string b) =>
        Freeze(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 90));

    public static Size Footprint(Settings s) => new StripLayout(s).Size;

    public void Configure(Settings s)
    {
        settings = s;
        gearText = null;
        for (int i = 0; i < 4; i++)
        {
            Color c = (Color)ColorConverter.ConvertFromString(s.Colors[i]);
            Color Tone(double factor, double white) => Color.FromRgb(
                (byte)Math.Clamp(c.R * factor + white, 0, 255),
                (byte)Math.Clamp(c.G * factor + white, 0, 255),
                (byte)Math.Clamp(c.B * factor + white, 0, 255));
            lens[i] = Freeze(new LinearGradientBrush(Tone(s.Brightness * 0.95, 28 * s.Brightness),
                Tone(s.Brightness * 0.7, 0), 90));
            halo[i] = Freeze(new SolidColorBrush(Color.FromArgb((byte)(38 * s.Brightness), c.R, c.G, c.B)));
        }
        InvalidateVisual();
    }

    public void Display(LightFrame next)
    {
        if (frame == next) return;
        frame = next;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var layout = new StripLayout(settings);
        Size design = layout.Size;
        double scale = Math.Min(ActualWidth / design.Width, ActualHeight / design.Height);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - design.Width * scale) / 2,
            (ActualHeight - design.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (settings.DarkHousing)
        {
            dc.DrawRoundedRectangle(Housing, Rim, new Rect(1, 1, design.Width - 2, design.Height - 2), 7, 7);
            dc.DrawLine(Rail, new Point(10, 3), new Point(design.Width - 10, 3));
        }
        DrawIndicators(dc, layout);
        double height = layout.LensHeight;
        double radius = settings.Shape == LensShape.Round ? height / 2 : settings.Shape == LensShape.Square ? 1 : 3;
        for (int i = 0; i < settings.Count; i++)
        {
            var rect = new Rect(layout.LensX(i), (design.Height - height) / 2, settings.LensSize, height);
            int rank = ShiftLights.Rank(i, settings.Count, settings.Direction);
            int zone = ShiftLights.ColorZone(rank, settings.Count);
            bool on = rank < frame.Lit && frame.BrightPhase;
            var bezel = rect;
            bezel.Inflate(1.2, 1.2);
            dc.DrawRoundedRectangle(Socket, null, bezel, radius + 1, radius + 1);
            dc.DrawRoundedRectangle(on ? lens[zone] : Unlit, null, rect, radius, radius);
            if (on)
            {
                var glow = rect;
                glow.Inflate(2, 2);
                dc.DrawRoundedRectangle(halo[zone], null, glow, radius + 2, radius + 2);
                double inset = settings.Shape == LensShape.Round ? rect.Width * 0.30 : 3;
                dc.DrawLine(Glint, new Point(rect.Left + inset, rect.Top + 2),
                    new Point(rect.Right - inset, rect.Top + 2));
            }
        }
        dc.Pop();
        dc.Pop();
    }
}
