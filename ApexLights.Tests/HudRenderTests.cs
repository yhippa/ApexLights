using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ApexLights.Core;

namespace ApexLights.Tests;

internal static partial class Program
{
    private static byte[] Pixels(Visual visual, int width, int height, double dpi)
    {
        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var data = new byte[width * height * 4];
        bitmap.CopyPixels(data, width * 4, 0);
        return data;
    }

    private static void GearInkBounds()
    {
        var options = new Settings();
        options.ApplyCockpitStyle();
        foreach (var position in Enum.GetValues<GearPosition>())
        foreach (var (scale, dpi) in new[] { (.5,96d), (1,144d), (2.5,192d) })
        {
            options.Indicators.GearPosition = position;
            var layout = new StripLayout(options);
            var pod = layout.GearPodBounds;
            for (int lens = 0; lens < options.Count; lens++)
            {
                double x = layout.LensX(lens);
                Assert(x + options.LensSize <= pod.Left || x >= pod.Right, "lens crosses gear pod");
            }
            var strip = new LedStrip();
            strip.Configure(options);
            var size = new Size(layout.Size.Width * scale, layout.Size.Height * scale);
            strip.Measure(size);
            strip.Arrange(new Rect(size));
            double factor = scale * dpi / 96;
            int width = (int)Math.Ceiling(size.Width * dpi / 96);
            int height = (int)Math.Ceiling(size.Height * dpi / 96);
            var empty = Pixels(strip,width,height,dpi);
            for (int gear = -1; gear <= 30; gear++)
            {
                strip.DisplayIndicators(new IndicatorFrame(GearDisplay.Format(gear)));
                strip.Measure(size);
                strip.Arrange(new Rect(size));
                var ink = Pixels(strip,width,height,dpi);
                int left=width, right=-1, top=height, bottom=-1;
                // The gear is the only varying input. Compare all pixels to catch spills as well as clipped glyphs.
                for (int y=0; y<height; y++)
                for (int x=0; x<width; x++)
                {
                    int offset=(y*width+x)*4;
                    if (Math.Abs(ink[offset]-empty[offset]) < 16) continue;
                    left=Math.Min(left,x); right=Math.Max(right,x);
                    top=Math.Min(top,y); bottom=Math.Max(bottom,y);
                }
                Assert(right > left && bottom > top, $"missing gear {gear}");
                Assert(left >= pod.Left*factor && right < pod.Right*factor && top >= pod.Top*factor &&
                    bottom < pod.Bottom*factor, $"gear {gear} escaped {position} pod");
                Assert(Math.Abs(left+right+1-(pod.Left+pod.Right)*factor)<4 &&
                    Math.Abs(top+bottom+1-(pod.Top+pod.Bottom)*factor)<4, $"gear {gear} off center");
            }
        }
    }

    private static void CueSilhouettes()
    {
        byte[] Paint(Brush art)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(art,null,new Rect(0,0,128,96));
            var pixels = Pixels(visual,128,96,96);
            int visible=0, clear=0;
            for (int i=3;i<pixels.Length;i+=4)
            {
                if (pixels[i] > 160) visible++;
                if (pixels[i] == 0) clear++;
            }
            Assert(visible > 400 && clear > 1500, "symbol needs both a strong silhouette and transparent negative space");
            return pixels;
        }

        var images = new Dictionary<RaceFlag,byte[]>();
        foreach (var flag in Enum.GetValues<RaceFlag>())
            if (flag != RaceFlag.None) images.Add(flag,Paint(HudArt.Flag(flag)));
        foreach (var (first,second) in new[] { (RaceFlag.White,RaceFlag.Checkered), (RaceFlag.Black,RaceFlag.Repair),
            (RaceFlag.Blue,RaceFlag.Green), (RaceFlag.Yellow,RaceFlag.WavingYellow), (RaceFlag.Caution,RaceFlag.WavingCaution) })
            Assert(!images[first].AsSpan().SequenceEqual(images[second]), "distinct flag detail");
        foreach (bool left in new[]{true,false})
            Assert(!Paint(HudArt.Wing(left,1)).AsSpan().SequenceEqual(Paint(HudArt.Wing(left,2))), "single and double chevrons");
    }
}
