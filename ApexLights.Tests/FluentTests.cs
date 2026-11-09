using System;
using System.IO;
using ApexLights.Core;

namespace ApexLights.Tests;

internal static partial class Program
{
    private static void FluentSettings() => InTemp(path =>
    {
        File.WriteAllText(path, """{"Scale":1.45,"X":-843,"Y":182,"Indicators":{"ShowFlags":true,"SideScale":1.2}}""");
        var store=new SettingsStore(path);
        var config=store.Load();
        Assert(config.Theme==SettingsTheme.Dark && config.Backdrop==PreviewBackdrop.Dark &&
            !config.Indicators.FloatingFlag && !config.Indicators.PeripheralBands && config.X==-843 &&
            config.Scale==1.45 && config.Indicators.ShowFlags,"upgrade preserves profile");
        config.Theme=SettingsTheme.Light;config.Backdrop=PreviewBackdrop.Light;
        config.Indicators.PeripheralBands=true;config.Indicators.FloatingFlag=true;
        config.Indicators.CueOpacity=.6;config.Indicators.SideInset=.22;config.Indicators.FlagElevation=.3;
        Assert(store.Save(config),"save appearance");
        var value=store.Load();
        Assert(value.Theme==config.Theme && value.Backdrop==config.Backdrop && value.Indicators.PeripheralBands &&
            value.Indicators.FloatingFlag && value.Indicators.CueOpacity==.6 && value.Indicators.SideInset==.22 &&
            value.Indicators.FlagElevation==.3 && value.X==config.X && value.Scale==config.Scale,"round trip");
        value.Theme=(SettingsTheme)7;value.Backdrop=(PreviewBackdrop)999;value.Indicators.CueOpacity=double.NaN;
        value.Normalize();
        Assert(value.Theme==SettingsTheme.Dark && value.Backdrop==PreviewBackdrop.Dark && value.Indicators.CueOpacity==.78,"normalize");
    });

    private static void RemoteCues()
    {
        var p=new IndicatorPolicy();
        var o=new IndicatorSettings { PeripheralBands=true, FloatingFlag=true, AlertSeconds=2 };
        var both=Signal(RaceFlag.Black,SpotterState.Both);
        var on=p.Evaluate(both,o,new(12,true,true),0);
        Assert(p.FloatingFlag==RaceFlag.Black && on.Left==1 && on.Right==1 && on.Accent==0,"independent floating and peripheral alerts");
        var held=p.Evaluate(both,o,default,2);
        Assert(p.FloatingFlag==0 && held.Flag==RaceFlag.Black && held.Left==1,"forward flag expires with persistent strip status");
        p.Evaluate(both,o,default,40);
        Assert(p.FloatingFlag==0,"no repetition");
        p.Evaluate(Signal(0),o,default,41);
        p.Evaluate(Signal(RaceFlag.Blue),o,default,42);
        Assert(p.FloatingFlag==RaceFlag.Blue,"next edge");
        p.Evaluate(null,o,default,43);
        Assert(p.FloatingFlag==0,"clear on disconnect");
        p.Evaluate(Signal(0),o,default,44);p.Evaluate(Signal(RaceFlag.Checkered),o,default,45);
        Assert(p.FloatingFlag==0,"no routine or celebratory forward alert");
    }

    private static void ForwardView()
    {
        foreach (double dpi in new[]{1,1.25,1.5,2})
        foreach (int width in new[]{1920,3840,5120})
        {
            const double height = 1200;
            var left = CuePosition.Within(CuePlace.Left,width,height,150*dpi,156*dpi,.08,.22);
            var right = CuePosition.Within(CuePlace.Right,width,height,150*dpi,156*dpi,.08,.22);
            Assert(Math.Abs(width/2d-left.X - 672)<.01 && Math.Abs(right.X+150*dpi-width/2d - 672)<.01,
                "extra monitor width should not push the cues outward");
            var preview = CuePosition.Within(CuePlace.Left,width*.2,76,150*dpi*.2,56,.08,.22,width/height);
            Assert(Math.Abs(preview.X-left.X*.2)<.01,"preview uses the monitor aspect");
            var near = CuePosition.Within(CuePlace.Left,width,height,150*dpi,156*dpi,.35,.22);
            Assert(near.X>left.X && near.X+150*dpi<=width/2d,"inset brings the cue closer without crossing center");
        }
    }

    private static void CueCoordinates()
    {
        foreach (var (width,height) in new[]{(1280,720),(1920,1080),(3440,1440),(5760,1080)})
        foreach (double dpi in new[]{1,1.25,1.5,2})
        {
            double w=88*dpi,h=188*dpi;
            var left=CuePosition.Within(CuePlace.Left,width,height,w,h,.08,.22);
            var right=CuePosition.Within(CuePlace.Right,width,height,w,h,.08,.22);
            var flag=CuePosition.Within(CuePlace.Flag,width,height,216*dpi,54*dpi,.08,.22);
            Assert(Math.Abs(left.X+right.X+w-width)<.01 && left.Y==right.Y,"symmetric directional cues");
            Assert(left.X>=0 && right.X+w<=width && left.Y>=0 && left.Y+h<=height,"inside resolution");
            Assert(Math.Abs(flag.X+108*dpi-width*.5)<.01 && Math.Abs(flag.Y+27*dpi-height*.22)<.01,"forward flag anchor");
        }
        Assert(CuePosition.Within(CuePlace.Flag,50,30,210,60,.08,.22)==(0d,0d),"tiny preview clamped");
    }
}
