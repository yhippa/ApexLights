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
    private Brush unlit = Brushes.Transparent;
    private static readonly Brush Housing = Solid("#101416");
    private static readonly Pen Rim = Freeze(new Pen(Solid("#303638"), .8));

    public LedStrip() { IsHitTestVisible = false; Configure(settings); }
    private static T Freeze<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static Brush Solid(string hex) => Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));
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
            lens[i] = Freeze(new SolidColorBrush(Color.FromRgb((byte)(c.R * s.Brightness),
                (byte)(c.G * s.Brightness), (byte)(c.B * s.Brightness))));
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
            dc.DrawRoundedRectangle(on ? lens[zone] : unlit, null, rect, radius, radius);
        }
        dc.Pop();
        dc.Pop();
    }
}
