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

    private static void DemoCycle()
    {
        var demo = new DemoSignals { Gear = -1, Flags = RaceFlag.Blue | RaceFlag.Black, Spotter = SpotterState.TwoRight };
        var policy = new IndicatorPolicy();
        var options = Enabled();
        IndicatorFrame At(double t) => policy.Evaluate(demo.Read(), options, new(12, true, true), t);
        var oldSession = demo.Read()!.Value.Session;
        Assert(At(0) == new IndicatorFrame("R", RaceFlag.Black, 0, 2), "shared demo and live policy");
        demo.SetConnection(false);
        Assert(At(1) == new IndicatorFrame(""), "disconnect clears all demo indicators");
        demo.Gear = 0;
        demo.SetConnection(true);
        Assert(At(2).Gear == "N" && At(2).Flag == RaceFlag.Black && demo.Read()!.Value.Session != oldSession,
            "reconnect rearms staged flags");
        policy.Dismiss();
        Assert(At(3).Flag == RaceFlag.Blue, "second simultaneous alert");
        demo.NewSession();
        Assert(At(4).Flag == RaceFlag.Black, "session turnover with a persistent signal");
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
        options.FlagScale = 1.5;
        options.SideScale = .8;
        Assert(store.Save(old), "save upgrade");
        options = store.Load().Indicators;
        Assert(options.ShowGear && options.ShowFlags && options.ShowProximity && options.SideStyle == SideStyle.Lamps &&
            options.GearFont == "Bahnschrift" && options.GearPosition == GearPosition.Center && options.AlertSeconds == 8 &&
            options.EnabledFlags == (RaceFlag.Red | RaceFlag.WavingYellow) && options.FlagScale == 1.5 &&
            options.SideScale == .8, "new settings round trip");
    });

    private static void PendingCritical()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        options.AlertSeconds = 2;
        IndicatorFrame At(RaceFlag flags, double t) => policy.Evaluate(Signal(flags), options, default, t);
        var all = RaceFlag.Red | RaceFlag.Black | RaceFlag.Repair | RaceFlag.Blue;
        Assert(At(all, 0).Flag == RaceFlag.Red, "red first");
        Assert(At(all, 2).Flag == RaceFlag.Black && At(all, 3.9).Flag == RaceFlag.Black, "black gets full slot");
        Assert(At(all & ~RaceFlag.Repair, 4).Flag == 0, "cleared queued repair and expired blue stay hidden");
        Assert(At(all, 4.1).Flag == RaceFlag.Repair, "new repair edge");
        Assert(At(RaceFlag.Red, 4.2).Flag == 0, "held red never restarts");
    }

    private static void QuietNotice()
    {
        var policy = new IndicatorPolicy();
        var options = Enabled();
        options.FlagLifetime = FlagLifetime.UntilClear;
        IndicatorFrame At(RaceFlag flag, double t, int session = 1) =>
            policy.Evaluate(Signal(flag, SpotterState.Both, session), options, default, t);
        Assert(At(RaceFlag.Green, 0).Accent == 0 && !At(RaceFlag.Green, 1.49).NoticeSettled, "quiet entry");
        Assert(At(RaceFlag.Green, 1.5).NoticeSettled && At(RaceFlag.Green, 100).NoticeSettled, "persistent compact notice");
        Assert(At(RaceFlag.Blue | RaceFlag.Green, 101).Flag == RaceFlag.Blue, "caution preempts notice");
        Assert(At(RaceFlag.Green, 102).NoticeSettled, "return without new entry");
        Assert(!At(RaceFlag.Green, 103, 2).NoticeSettled && At(0, 104, 2).Flag == 0, "new session then clear");
    }

    private static void ScenarioInputs()
    {
        var demo = new DemoSignals();
        var options = Enabled();
        var policy = new IndicatorPolicy();
        DemoDrive Stage(DemoScenario scene) => demo.Apply(scene, Points, 125, FlashTrigger.Blink);
        demo.SetConnection(false);
        Stage(DemoScenario.Normal);
        Assert(demo.Connected && demo.Gear == 4 && demo.Flags == RaceFlag.Green, "normal driving reconnects");
        var drive = Stage(DemoScenario.ApproachShift);
        Assert(drive.Sweep && drive.Rpm < Points.Blink + 125 && drive.Rpm > Points.First + 125, "approach uses selected cue");
        Stage(DemoScenario.SideBySide);
        Assert(demo.Spotter == SpotterState.Left && demo.Flags == 0, "side by side");
        Stage(DemoScenario.LocalYellow);
        Assert(demo.Spotter == SpotterState.Clear && demo.Flags == RaceFlag.WavingYellow, "local yellow");
        Stage(DemoScenario.Repair);
        Assert(demo.Flags == RaceFlag.Repair && demo.Gear == 2, "repair");
        drive = Stage(DemoScenario.Competing);
        var lights = new ShiftLights().Evaluate(drive.Rpm, Points, 12, 125, FlashTrigger.Blink, 4, 10);
        var frame = policy.Evaluate(demo.Read(), options, lights, 10);
        Assert(lights == new LightFrame(12, true, true) && frame == new IndicatorFrame("5", RaceFlag.Black, 1, 1),
            "all LEDs flash independently of black flag and bilateral overlap");
        policy.Dismiss();
        Assert(policy.Evaluate(demo.Read(), options, lights, 11).Flag == RaceFlag.Repair, "next critical");
        Stage(DemoScenario.Competing);
        Assert(policy.Evaluate(demo.Read(), options, lights, 12).Flag == RaceFlag.Black, "repeat starts a fresh occurrence");
        Stage(DemoScenario.ReturnToNormal);
        Assert(policy.Evaluate(demo.Read(), options, default, 13) == new IndicatorFrame("4"), "return clears all warnings");
    }

    private static void ScenarioSettings() => InTemp(path =>
    {
        File.WriteAllText(path, """{"Scale":1.3,"X":-1020,"Y":45,"Indicators":{"ShowGear":true,"EnabledFlags":"Yellow","SideScale":1.4}}""");
        var store = new SettingsStore(path);
        var saved = store.Load();
        var preview = saved.PreviewAllIndicators();
        Assert(preview.Indicators.ShowProximity && preview.Indicators.ShowFlags &&
            preview.Indicators.EnabledFlags == IndicatorPolicy.Supported && preview.Indicators.SideScale == 1.4, "preview all");
        Assert(saved.Indicators.EnabledFlags == RaceFlag.Yellow && !saved.Indicators.ShowFlags &&
            !saved.Indicators.ShowProximity && saved.X == -1020 && saved.Scale == 1.3 && saved.InactiveBrightness == .22,
            "saved demo and overlay preferences retained");
        saved.InactiveBrightness = .4;
        Assert(store.Save(saved) && store.Load().InactiveBrightness == .4, "inactive brightness round trip");
        saved.InactiveBrightness = double.PositiveInfinity;
        saved.Normalize();
        Assert(saved.InactiveBrightness == .22, "invalid brightness repaired");
    });

    private static void CueSizeUpgrade() => InTemp(path =>
    {
        File.WriteAllText(path, """{"Count":12,"X":-125,"Scale":1.15,"Indicators":{"ShowFlags":true,"ShowProximity":true}}""");
        var options = new SettingsStore(path).Load();
        Assert(options.Scale == 1.15 && options.X == -125 && options.Indicators.ShowFlags &&
            options.Indicators.ShowProximity && options.Indicators.FlagScale == 1 && options.Indicators.SideScale == 1,
            "1.1 cue size defaults");
        options.Indicators.FlagScale = double.NaN;
        options.Indicators.SideScale = 100;
        options.Normalize();
        Assert(options.Indicators.FlagScale == 1 && options.Indicators.SideScale == 1.75, "size bounds");
    });
}
