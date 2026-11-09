using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip : FrameworkElement
{
    private Settings settings = new();
    private StripLayout layout;
    private LightFrame frame;
    private readonly Brush[] lens = new Brush[4];
    private readonly Brush[] bloom = new Brush[4];
    private Brush unlit = Brushes.Transparent;
    public LedStrip() { IsHitTestVisible = false; Configure(settings); }
    public static Size Footprint(Settings s) => new StripLayout(s).Size;

    public void Configure(Settings s)
    {
        settings = s;
        layout = new StripLayout(s);
        gearText = null;
        byte idle = (byte)(13 + 64 * s.InactiveBrightness);
        unlit = s.InactiveBrightness == 0 ? Brushes.Transparent :
            HudArt.Frozen(new SolidColorBrush(Color.FromArgb(164, idle, (byte)(idle + 6), (byte)(idle + 11))));
        for (int i = 0; i < 4; i++)
        {
            Color c = (Color)ColorConverter.ConvertFromString(s.Colors[i]);
            Color face = Color.FromRgb((byte)(c.R * s.Brightness), (byte)(c.G * s.Brightness), (byte)(c.B * s.Brightness));
            byte Top(byte value) => (byte)Math.Min(255, value * 1.10 + 9 * s.Brightness);
            var highlight = Color.FromRgb(Top(face.R), Top(face.G), Top(face.B));
            var bevel = new LinearGradientBrush { StartPoint = new(.5, 0), EndPoint = new(.5, 1) };
            bevel.GradientStops.Add(new(Color.FromRgb((byte)(highlight.R + (255-highlight.R)*.36*s.Brightness),
                (byte)(highlight.G + (255-highlight.G)*.36*s.Brightness), (byte)(highlight.B + (255-highlight.B)*.36*s.Brightness)), 0));
            bevel.GradientStops.Add(new(highlight, .15));
            bevel.GradientStops.Add(new(face, 1));
            lens[i] = HudArt.Frozen(bevel);
            var light = new RadialGradientBrush();
            light.GradientStops.Add(new(Color.FromArgb(145, face.R, face.G, face.B), 0));
            light.GradientStops.Add(new(Color.FromArgb(90, face.R, face.G, face.B), .56));
            light.GradientStops.Add(new(Color.FromArgb(0, face.R, face.G, face.B), 1));
            bloom[i] = HudArt.Frozen(light);
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
        Size design = layout.Size;
        double scale = Math.Min(ActualWidth / design.Width, ActualHeight / design.Height);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - design.Width * scale) / 2,
            (ActualHeight - design.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (settings.DarkHousing)
        {
            var shell = layout.ShellBounds;
            double corner = shell.Height / 2;
            dc.DrawRoundedRectangle(HudArt.Shell, HudArt.ShellEdge, shell, corner, corner);
        }
        DrawIndicators(dc, layout);
        double height = layout.LensHeight;
        double radius = settings.Shape == LensShape.Round ? height / 2 : settings.Shape == LensShape.Square ? 1 : Math.Min(5, height * .24);
        for (int i = 0; i < settings.Count; i++)
        {
            var rect = new Rect(layout.LensX(i), (design.Height - height) / 2, settings.LensSize, height);
            int rank = ShiftLights.Rank(i, settings.Count, settings.Direction);
            int zone = ShiftLights.ColorZone(rank, settings.Count);
            bool on = rank < frame.Lit && frame.BrightPhase;
            if (on)
            {
                var light = rect;
                light.Inflate(Math.Min(12, height * .68), Math.Min(9, height * .52));
                dc.DrawEllipse(bloom[zone], null, new Point(light.X + light.Width / 2, light.Y + light.Height / 2),
                    light.Width / 2, light.Height / 2);
            }
            dc.DrawRoundedRectangle(on ? lens[zone] : unlit, null, rect, radius, radius);
        }
        dc.Pop();
        dc.Pop();
    }
}
