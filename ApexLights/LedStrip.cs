using System;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class LedStrip : FrameworkElement
{
    private Settings settings = new();
    private LightFrame frame;
    private readonly Brush[] lens = new Brush[4];
    private readonly Brush[] bloom = new Brush[4];
    private Brush unlit = Brushes.Transparent;
    private static readonly Brush Housing = Finish("#35383B", "#191B1D", "#090A0C");
    private static readonly Pen Rim = Freeze(new Pen(Finish("#7A7C80", "#34373A", "#121315"), 1.1));
    private static readonly Pen RecessEdge = Freeze(new Pen(Finish("#060709", "#313438", "#55585B"), 1));
    private static readonly Brush Recess = Finish("#080A0B", "#0C0F10", "#131617");

    public LedStrip() { IsHitTestVisible = false; Configure(settings); }
    private static T Freeze<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static Brush Solid(string hex) => Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));
    private static Brush Finish(string top, string middle, string bottom)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(.5, 0), EndPoint = new Point(.5, 1) };
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(top), 0));
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(middle), .45));
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(bottom), 1));
        return Freeze(brush);
    }
    public static Size Footprint(Settings s) => new StripLayout(s).Size;

    public void Configure(Settings s)
    {
        settings = s;
        gearText = null;
        byte idle = (byte)(13 + 64 * s.InactiveBrightness);
        unlit = s.InactiveBrightness == 0 ? Brushes.Transparent :
            Freeze(new SolidColorBrush(Color.FromRgb(idle, (byte)(idle + 3), (byte)(idle + 4))));
        for (int i = 0; i < 4; i++)
        {
            Color c = (Color)ColorConverter.ConvertFromString(s.Colors[i]);
            Color face = Color.FromRgb((byte)(c.R * s.Brightness), (byte)(c.G * s.Brightness), (byte)(c.B * s.Brightness));
            byte Top(byte value) => (byte)Math.Min(255, value * 1.10 + 9 * s.Brightness);
            lens[i] = Freeze(new LinearGradientBrush(Color.FromRgb(Top(face.R), Top(face.G), Top(face.B)), face, 90));
            var light = new RadialGradientBrush();
            light.GradientStops.Add(new(Color.FromArgb(145, face.R, face.G, face.B), 0));
            light.GradientStops.Add(new(Color.FromArgb(90, face.R, face.G, face.B), .56));
            light.GradientStops.Add(new(Color.FromArgb(0, face.R, face.G, face.B), 1));
            bloom[i] = Freeze(light);
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
            var shell = layout.ShellBounds;
            double corner = shell.Height * .42;
            dc.DrawRoundedRectangle(Housing, Rim, shell, corner, corner);
            if (!layout.FirstRail.IsEmpty) dc.DrawRoundedRectangle(Recess, RecessEdge, layout.FirstRail, 5, 5);
            if (!layout.SecondRail.IsEmpty) dc.DrawRoundedRectangle(Recess, RecessEdge, layout.SecondRail, 5, 5);
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
            if (on)
            {
                var light = rect;
                light.Inflate(Math.Min(8, height * .5), Math.Min(8, height * .5));
                dc.DrawEllipse(bloom[zone], null, new Point(light.X + light.Width / 2, light.Y + light.Height / 2),
                    light.Width / 2, light.Height / 2);
            }
            dc.DrawRoundedRectangle(on ? lens[zone] : unlit, null, rect, radius, radius);
        }
        dc.Pop();
        dc.Pop();
    }
}
