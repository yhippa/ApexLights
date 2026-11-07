using System;

namespace ApexLights.Core;

public sealed class IndicatorPolicy
{
    public const RaceFlag Supported = RaceFlag.Red | RaceFlag.Black | RaceFlag.Repair | RaceFlag.WavingYellow |
        RaceFlag.Yellow | RaceFlag.Caution | RaceFlag.WavingCaution | RaceFlag.Blue | RaceFlag.Checkered |
        RaceFlag.White | RaceFlag.Green;

    // Only flags contend for the same region. Gear, proximity and the shift LEDs have independent slots.
    private static readonly RaceFlag[] Order = [RaceFlag.Red, RaceFlag.Black, RaceFlag.Repair,
        RaceFlag.WavingYellow, RaceFlag.WavingCaution, RaceFlag.Yellow, RaceFlag.Caution,
        RaceFlag.Blue, RaceFlag.Checkered, RaceFlag.White, RaceFlag.Green];
    private enum Phase { Clear, Visible, Dismissed, Expired }
    private struct Alert
    {
        public Phase Phase;
        public double Began;
        public void Update(bool raised, double time, IndicatorSettings options)
        {
            if (!raised) { Phase = Phase.Clear; return; }
            if (Phase == Phase.Clear) { Began = time; Phase = Phase.Visible; }
            if (Phase == Phase.Visible && options.FlagLifetime == FlagLifetime.Timed &&
                time - Began >= options.AlertSeconds) Phase = Phase.Expired;
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
        if (displayed >= 0) alerts[displayed].Phase = Phase.Dismissed;
    }

    public IndicatorFrame Evaluate(RaceSignal? input, IndicatorSettings options, LightFrame lights, double seconds)
    {
        if (input is not { } signal)
        {
            if (session.HasValue) Reset();
            return new("");
        }
        if (session != signal.Session) { Reset(); session = signal.Session; }
        RaceFlag enabled = options.ShowFlags ? signal.Flags & options.EnabledFlags : RaceFlag.None;
        displayed = -1;
        for (int i = 0; i < Order.Length; i++)
        {
            alerts[i].Update((enabled & Order[i]) != 0, seconds, options);
            if (displayed < 0 && alerts[i].Phase == Phase.Visible) displayed = i;
        }
        var (left, right) = options.ShowProximity ? Sides(signal.Spotter) : (0, 0);
        // A short fading outline on entry is the only flag animation. RPM flashing owns the blink cadence.
        int accent = 0;
        if (displayed >= 0 && options.AnimateEntry && !lights.Flashing)
            accent = Math.Clamp(4 - (int)((seconds - alerts[displayed].Began) * 8), 0, 4);
        return new(options.ShowGear ? GearDisplay.Format(signal.Gear) : "",
            displayed < 0 ? RaceFlag.None : Order[displayed], left, right, accent);
    }

    private static (int, int) Sides(SpotterState state) => state switch
    {
        SpotterState.Left => (1, 0), SpotterState.Right => (0, 1), SpotterState.Both => (1, 1),
        SpotterState.TwoLeft => (2, 0), SpotterState.TwoRight => (0, 2), _ => (0, 0)
    };
}
