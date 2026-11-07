namespace ApexLights.Core;

public enum DemoScenario { Normal, ApproachShift, SideBySide, LocalYellow, Repair, Competing, ReturnToNormal }
public readonly record struct DemoDrive(double Rpm, bool Sweep);

// Ephemeral simulator inputs. A reconnect or a new session re-arms active alerts.
public sealed class DemoSignals
{
    public int Gear { get; set; } = 3;
    public RaceFlag Flags { get; set; }
    public SpotterState Spotter { get; set; } = SpotterState.Clear;
    public bool Connected { get; private set; } = true;
    private int sessionNumber = 1;

    public RaceSignal? Read() => Connected
        ? new RaceSignal(Gear, Flags, Spotter, new SessionKey(0, 0, sessionNumber, "demo")) : null;

    public void SetConnection(bool connected)
    {
        if (!Connected && connected) NewSession();
        Connected = connected;
    }
    public void NewSession() => sessionNumber++;

    public DemoDrive Apply(DemoScenario scenario, Thresholds points, int offset, FlashTrigger trigger)
    {
        // Each press stages a fresh occurrence, including repeat presses of the same scenario.
        Connected = true;
        NewSession();
        (Gear, Flags, Spotter) = scenario switch
        {
            DemoScenario.Normal => (4, RaceFlag.Green, SpotterState.Clear),
            DemoScenario.ApproachShift => (4, RaceFlag.None, SpotterState.Clear),
            DemoScenario.SideBySide => (3, RaceFlag.None, SpotterState.Left),
            DemoScenario.LocalYellow => (3, RaceFlag.WavingYellow, SpotterState.Clear),
            DemoScenario.Repair => (2, RaceFlag.Repair, SpotterState.Clear),
            DemoScenario.Competing => (5, RaceFlag.Black | RaceFlag.Repair | RaceFlag.WavingYellow, SpotterState.Both),
            _ => (4, RaceFlag.None, SpotterState.Clear)
        };
        double shift = trigger == FlashTrigger.Blink ? points.Blink : points.Shift;
        double rpm = scenario switch
        {
            DemoScenario.ApproachShift => shift - (points.Last - points.First) * .08,
            DemoScenario.Competing => shift,
            DemoScenario.Repair => points.First * .91,
            _ => points.First + (points.Last - points.First) * .55
        };
        return new(System.Math.Max(0, rpm + offset), scenario == DemoScenario.ApproachShift);
    }
}
