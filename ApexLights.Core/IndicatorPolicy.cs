using System;

namespace ApexLights.Core;

public enum FlagProminence { Informational, Celebration, Operational, Caution, Critical }

public sealed class IndicatorPolicy
{
    public const RaceFlag Supported = RaceFlag.Red | RaceFlag.Black | RaceFlag.Repair | RaceFlag.WavingYellow |
        RaceFlag.Yellow | RaceFlag.Caution | RaceFlag.WavingCaution | RaceFlag.Blue | RaceFlag.Checkered |
        RaceFlag.White | RaceFlag.Green;

    // One exclusive status slot. None in this table identifies the separate EngineWarnings input.
    private static readonly RaceFlag[] Order = [RaceFlag.Red, RaceFlag.Black, RaceFlag.Repair,
        RaceFlag.WavingYellow, RaceFlag.WavingCaution, RaceFlag.Yellow, RaceFlag.Caution,
        RaceFlag.Blue, RaceFlag.None, RaceFlag.Checkered, RaceFlag.White, RaceFlag.Green];

    public static FlagProminence Prominence(RaceFlag flag) => flag switch
    {
        RaceFlag.Red or RaceFlag.Black or RaceFlag.Repair => FlagProminence.Critical,
        RaceFlag.Blue or RaceFlag.Yellow or RaceFlag.WavingYellow or RaceFlag.Caution or RaceFlag.WavingCaution => FlagProminence.Caution,
        RaceFlag.None => FlagProminence.Operational,
        RaceFlag.Checkered => FlagProminence.Celebration,
        _ => FlagProminence.Informational
    };

    private struct Alert
    {
        private bool observed, previouslyRaised;
        public bool Visible;
        public double Began;

        public void Update(bool raised, bool edgeEvent, bool timed, double now, double duration)
        {
            // A first sample describes the current state; it cannot establish a start or finish crossing.
            if (raised && !previouslyRaised && (observed || !edgeEvent))
            {
                Visible = true;
                Began = now;
            }
            if ((!edgeEvent && !raised) || (timed && now - Began >= duration)) Visible = false;
            previouslyRaised = raised;
            observed = true;
        }
    }

    private readonly Alert[] alerts = new Alert[Order.Length];
    private SessionKey? session;
    private int displayed = -1;

    public void Reset()
    {
        session = null;
        Array.Clear(alerts);
        displayed = -1;
    }

    public void Dismiss()
    {
        if (displayed >= 0) alerts[displayed].Visible = false;
    }

    public IndicatorFrame Evaluate(RaceSignal? input, IndicatorSettings options, LightFrame lights, double seconds)
    {
        if (input is not { } signal)
        {
            if (session.HasValue) Reset();
            return new("");
        }
        if (session != signal.Session) { Reset(); session = signal.Session; }
        displayed = -1;
        for (int i = 0; i < Order.Length; i++)
        {
            RaceFlag flag = Order[i];
            bool limiter = flag == RaceFlag.None;
            bool enabled = limiter ? options.IncludeLimiter : signal.FlagsAvailable && (flag & options.EnabledFlags) != 0;
            if (!options.ShowFlags || !enabled) { alerts[i] = default; continue; }

            bool edgeEvent = flag is RaceFlag.Green or RaceFlag.Checkered;
            bool timed = edgeEvent || (flag == RaceFlag.White && options.FlagLifetime == FlagLifetime.Timed);
            bool raised = limiter ? signal.PitLimiter : (signal.Flags & flag) != 0;
            alerts[i].Update(raised, edgeEvent, timed, seconds, options.AlertSeconds);
            if (displayed < 0 && alerts[i].Visible) displayed = i;
        }

        var (left, right) = options.ShowProximity ? Sides(signal.Spotter) : (0, 0);
        RaceFlag selected = displayed < 0 ? RaceFlag.None : Order[displayed];
        int accent = 0;
        if (displayed >= 0 && options.AnimateEntry && !lights.Flashing &&
            Prominence(selected) is FlagProminence.Critical or FlagProminence.Caution)
        {
            double age = seconds - alerts[displayed].Began;
            if (age < .5) accent = Math.Clamp(4 - (int)(age * 8), 0, 4);
        }
        return new(options.ShowGear ? GearDisplay.Format(signal.Gear) : "", selected, left, right, accent,
            displayed >= 0 && selected == RaceFlag.None);
    }

    private static (int, int) Sides(SpotterState state) => state switch
    {
        SpotterState.Left => (1, 0), SpotterState.Right => (0, 1), SpotterState.Both => (1, 1),
        SpotterState.TwoLeft => (2, 0), SpotterState.TwoRight => (0, 2), _ => (0, 0)
    };
}
