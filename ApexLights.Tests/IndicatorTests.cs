using System.IO;
using ApexLights.Core;

namespace ApexLights.Tests;

internal static partial class Program
{
    private static RaceSignal Signal(RaceFlag flags, SpotterState side = SpotterState.Clear, int number = 1) =>
        new(4, flags, side, new SessionKey(1, 100, number, "gt"));
    private static IndicatorSettings Enabled() => new() { ShowFlags = true, ShowProximity = true, ShowGear = true };

    private static void Priority()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        var shift = new LightFrame(12, true, false);
        var flags = RaceFlag.Red | RaceFlag.Black | RaceFlag.WavingYellow | RaceFlag.Blue;
        IndicatorFrame At(RaceFlag set) => policy.Evaluate(Signal(set, SpotterState.Both), options, shift, 0);
        var frame = At(flags);
        Assert(frame == new IndicatorFrame("4", RaceFlag.Red, 1, 1, 0), "full shift and simultaneous alerts");
        Assert(At(flags & ~RaceFlag.Red).Flag == RaceFlag.Black, "black over caution");
        Assert(At(RaceFlag.Blue | RaceFlag.WavingYellow).Flag == RaceFlag.WavingYellow, "local caution over blue");
        Assert(At(RaceFlag.Blue).Flag == RaceFlag.Blue, "next active flag");
    }

    private static void Transitions()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        options.AlertSeconds = 2;
        IndicatorFrame At(RaceFlag flags, double time) => policy.Evaluate(Signal(flags), options, default, time);
        Assert(At(RaceFlag.Yellow, 1).Accent == 4 && At(RaceFlag.Yellow, 2).Accent == 0, "single entry animation");
        Assert(At(RaceFlag.Yellow, 3).Flag == 0 && At(RaceFlag.Yellow, 10).Flag == 0, "no timer refresh on repeated telemetry");
        At(0, 11);
        Assert(At(RaceFlag.Yellow, 12).Flag == RaceFlag.Yellow, "new rising edge");
        policy.Dismiss();
        Assert(At(RaceFlag.Yellow | RaceFlag.Blue, 12.1).Flag == RaceFlag.Blue, "dismiss promotes active flag");
        Assert(At(RaceFlag.Yellow, 12.2).Flag == 0, "dismissal is latched");
        At(0, 13);
        options.FlagLifetime = FlagLifetime.UntilClear;
        At(RaceFlag.Black, 14);
        Assert(At(RaceFlag.Black, 50).Flag == RaceFlag.Black && At(0, 51).Flag == 0, "until-clear mode");
        options.AnimateEntry = false;
        Assert(At(RaceFlag.Green, 52).Accent == 0, "animation disabled");
    }

    private static void Proximity()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        (int, int)[] sides = [(0,0), (0,0), (1,0), (0,1), (1,1), (2,0), (0,2)];
        for (int i = 0; i < sides.Length; i++)
        {
            var frame = policy.Evaluate(Signal(0, (SpotterState)i), options, default, i);
            Assert((frame.Left, frame.Right) == sides[i], $"spotter state {i}");
        }
        var clear = policy.Evaluate(Signal(0, SpotterState.Clear), options, default, 7);
        Assert(clear.Left == 0 && clear.Right == 0, "clear is immediate");
        options.ShowProximity = false;
        Assert(policy.Evaluate(Signal(0, SpotterState.Both), options, default, 8) == new IndicatorFrame("4"), "proximity disabled");
    }

    private static void SessionChanges()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        var signal = Signal(RaceFlag.Red | RaceFlag.White, SpotterState.Left);
        policy.Evaluate(signal, options, default, 0);
        policy.Dismiss();
        Assert(policy.Evaluate(signal, options, default, .1).Flag == RaceFlag.White, "dismissed session");
        var next = signal with { Session = signal.Session with { Number = 2 } };
        Assert(policy.Evaluate(next, options, default, .2).Flag == RaceFlag.Red, "new session restarts alert");
        Assert(policy.Evaluate(null, options, default, .3) == new IndicatorFrame(""), "disconnect clears all fields");
        options.EnabledFlags &= ~RaceFlag.Red;
        Assert(policy.Evaluate(next, options, default, .4).Flag == RaceFlag.White, "filter survives reconnect");
        options.ShowFlags = false;
        Assert(policy.Evaluate(next, options, default, .5).Flag == 0, "disabled flags");
    }

    private static void Upgrade() => InTemp(path =>
    {
        File.WriteAllText(path, """{"X":-1400,"Y":233,"Count":10,"Scale":1.36,"Offset":75,"Cars":{"id-146":{"First":5000,"Last":7000,"Shift":6750,"Blink":7300}}}""");
        var store = new SettingsStore(path);
        var old = store.Load();
        Assert(old.X == -1400 && old.Y == 233 && old.Count == 10 && old.Scale == 1.36 && old.Offset == 75 &&
            old.Cars["id-146"] == Points && !old.Indicators.ShowGear && !old.Indicators.ShowFlags &&
            !old.Indicators.ShowProximity, "existing settings upgrade");
        var options = old.Indicators;
        options.ShowGear = options.ShowFlags = options.ShowProximity = true;
        options.EnabledFlags = RaceFlag.Red | RaceFlag.WavingYellow;
        options.SideStyle = SideStyle.Lamps;
        options.GearFont = "Bahnschrift";
        options.GearPosition = GearPosition.Center;
        options.AlertSeconds = 8;
        Assert(store.Save(old), "save upgrade");
        options = store.Load().Indicators;
        Assert(options.ShowGear && options.ShowFlags && options.ShowProximity && options.SideStyle == SideStyle.Lamps &&
            options.GearFont == "Bahnschrift" && options.GearPosition == GearPosition.Center && options.AlertSeconds == 8 &&
            options.EnabledFlags == (RaceFlag.Red | RaceFlag.WavingYellow), "new settings round trip");
    });
}
