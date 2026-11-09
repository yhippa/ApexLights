using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ApexLights.Core;

namespace ApexLights;

// All gradients and vector artwork are created once. HUD materials have no desktop-theme dependencies.
internal static class HudArt
{
    internal static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
    private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    internal static Brush Solid(string hex) => Frozen(new SolidColorBrush(ColorOf(hex)));
    internal static Brush Gradient(string top, string middle, string bottom)
    {
        var brush = new LinearGradientBrush { StartPoint = new(.5, 0), EndPoint = new(.5, 1) };
        brush.GradientStops.Add(new(ColorOf(top), 0));
        brush.GradientStops.Add(new(ColorOf(middle), .45));
        brush.GradientStops.Add(new(ColorOf(bottom), 1));
        return Frozen(brush);
    }

    internal static readonly Brush Shell = Gradient("#7659758F", "#6A152332", "#81354759");
    internal static readonly Pen ShellEdge = Frozen(new Pen(Gradient("#DADEEDFB", "#7058758A", "#BBC3DAEF"), 1));
    internal static readonly Brush Rail = Gradient("#9510212D", "#AB101B23", "#7423313D");
    internal static readonly Pen Hairline = Frozen(new Pen(Gradient("#907D94A8", "#3663798B", "#78879CAC"), .75));
    internal static readonly Pen GearKeyline = Frozen(new Pen(Solid("#D918232E"), 1.8) { LineJoin = PenLineJoin.Round });
    internal static readonly Brush RestingMark = Gradient("#ADDEE8F2", "#9B96A6B7", "#AAB6C3D0");
    internal static readonly Brush Amber = Gradient("#FFFFEA69", "#FFFFCE00", "#FFFFB900");
    internal static readonly Pen AmberRim = Frozen(new Pen(Solid("#EEFFF099"), 1.1));

    private static readonly Geometry Cloth = Frozen(Geometry.Parse(
        "M23,22 C38,9 49,9 64,17 C80,25 94,26 108,17 L99,52 C83,62 70,59 54,52 C40,46 30,49 16,58 Z"));
    private static readonly Pen Keyline = Frozen(new Pen(Solid("#CA12202B"), 2.6) { LineJoin=PenLineJoin.Round });
    private static readonly Brush Fold = Frozen(new LinearGradientBrush(new GradientStopCollection {
        new(Colors.Transparent,0), new(ColorOf("#38FFFFFF"),.22), new(ColorOf("#08000000"),.4),
        new(ColorOf("#46080D15"),.67), new(ColorOf("#20FFFFFF"),1)
    }, 14));

    private static readonly Dictionary<RaceFlag, Brush> Flags = CreateFlags();
    private static readonly Brush[,] Wings = CreateWings();

    internal static Brush Flag(RaceFlag flag) => Flags[flag];
    internal static Brush Wing(bool left, int count) => Wings[left ? 0 : 1, count - 1];

    private static Brush Symbol(Rect viewBox, Action<DrawingContext> paint)
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open()) paint(dc);
        return Frozen(new DrawingBrush(drawing) {
            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = viewBox, Stretch = Stretch.Uniform
        });
    }

    private static Brush Halo(Color color, byte centerAlpha)
    {
        color.A = centerAlpha;
        var brush = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B));
        return Frozen(brush);
    }

    private static Dictionary<RaceFlag, Brush> CreateFlags()
    {
        var flags = new Dictionary<RaceFlag, Brush>();
        foreach (var flag in Enum.GetValues<RaceFlag>())
        {
            if (flag == RaceFlag.None) continue;
            flags.Add(flag, Symbol(new Rect(0,0,116,82), dc => PaintFlag(dc,flag)));
        }
        return flags;
    }

    private static void PaintFlag(DrawingContext dc, RaceFlag flag)
    {
        var (light, body, shadow) = flag switch
        {
            RaceFlag.Green => ("#9FFF77","#19EE31","#008D23"),
            RaceFlag.Blue => ("#77C6FF","#2585FF","#123CA7"),
            RaceFlag.Red => ("#FF9888","#FF3427","#B30D1E"),
            RaceFlag.Black or RaceFlag.Repair => ("#C6D2DE","#323942","#080C12"),
            RaceFlag.White or RaceFlag.Checkered => ("#FFFFFF","#F1F5F9","#9DAAB7"),
            _ => ("#FFEF64","#FFDC06","#DDA600")
        };
        var rim = Frozen(new Pen(Solid(light), 1.15) { LineJoin = PenLineJoin.Round });
        var fabric = Gradient(light, body, shadow);
        dc.DrawEllipse(Halo(ColorOf(body), 84), null, new Point(61,39), 55,37);
        // Layer two thin emissive edges into the cached artwork in place of a blur effect.
        dc.PushOpacity(.16);
        dc.DrawGeometry(null, new Pen(fabric, 7) { LineJoin=PenLineJoin.Round }, Cloth);
        dc.Pop();
        dc.DrawGeometry(null, Keyline, Cloth);
        dc.PushOpacity(.93);
        dc.DrawGeometry(fabric, rim, Cloth);
        dc.PushClip(Cloth);
        if (flag == RaceFlag.Checkered) PaintChecks(dc);
        if (flag == RaceFlag.Repair)
            dc.DrawEllipse(Gradient("#FFCC67","#FFA329","#E2800B"), null, new Point(60,34), 12,12);
        dc.DrawGeometry(Fold, null, Cloth);
        dc.Pop();
        dc.Pop();

        var pole = new Pen(fabric, 3.6) { StartLineCap=PenLineCap.Round, EndLineCap=PenLineCap.Round };
        var poleEdge = new Pen(Keyline.Brush, 5.4) { StartLineCap=PenLineCap.Round, EndLineCap=PenLineCap.Round };
        dc.DrawLine(poleEdge, new Point(21,20), new Point(8,75));
        dc.DrawLine(pole, new Point(21,20), new Point(8,75));
        dc.DrawEllipse(fabric, rim, new Point(22,14), 4.2,4.2);
        if (flag is RaceFlag.WavingYellow or RaceFlag.WavingCaution)
        {
            dc.DrawGeometry(null, rim, Geometry.Parse("M53,61 Q67,69 80,65 M63,69 Q72,73 81,70"));
        }
    }

    private static void PaintChecks(DrawingContext dc)
    {
        // Bend the checker rows with the fabric and slant their columns with its hoist.
        Point Corner(int col, int row)
        {
            double x = 14 + col * 18;
            return new Point(x - row * 2, 8 + row * 12 + 5 * Math.Sin((x - 62) / 18));
        }
        for (int row = -1; row < 6; row++)
        for (int col = 0; col < 7; col++)
        {
            if (((row + col) & 1) != 0) continue;
            var cell = new StreamGeometry();
            using (var g = cell.Open())
            {
                g.BeginFigure(Corner(col,row), true, true);
                g.LineTo(Corner(col+1,row),true,false);
                g.LineTo(Corner(col+1,row+1),true,false);
                g.LineTo(Corner(col,row+1),true,false);
            }
            dc.DrawGeometry(Brushes.Black,null,cell);
        }
    }

    private static Brush[,] CreateWings()
    {
        var result = new Brush[2,2];
        var shape = Geometry.Parse("M0,7 L96,28 Q111,31 119,44 L137,69 Q143,78 137,87 L119,111 Q111,124 96,128 L0,150 Z");
        var chevron = Geometry.Parse("M1,1 Q0,0 3,0 H17 Q20,0 22,3 L41,29 Q44,32 41,35 L22,61 Q20,64 17,64 H3 Q0,64 2,61 L22,32 Z");
        var taper = new LinearGradientBrush(ColorOf("#00151D26"), ColorOf("#B75D421D"), 0);
        var lip = new Pen(new LinearGradientBrush(Colors.Transparent, ColorOf("#EFFFDF83"), 0), 1.3);
        var glow = Halo(ColorOf("#FFB500"),148);
        var spill = new Pen(Solid("#44FFD11F"), 8) { LineJoin=PenLineJoin.Round };
        for (int side=0; side<2; side++)
        for (int n=1; n<=2; n++)
        {
            result[side,n-1] = Symbol(new Rect(0,0,150,156), dc => {
                if (side==1) dc.PushTransform(new MatrixTransform(-1,0,0,1,150,0));
                dc.DrawGeometry(taper,lip,shape);
                dc.DrawEllipse(glow,null,new Point(87,78),59,52);
                for (int i=0; i<n; i++)
                {
                    dc.PushTransform(new TranslateTransform((n==1 ? 65 : 46) + i*33,46));
                    dc.DrawGeometry(null,spill,chevron);
                    dc.DrawGeometry(null,Keyline,chevron);
                    dc.DrawGeometry(Amber,AmberRim,chevron);
                    dc.Pop();
                }
                if (side==1) dc.Pop();
            });
        }
        return result;
    }
}
