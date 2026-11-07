using System;

namespace ApexLights.Core;

public sealed record Thresholds(double First, double Last, double Shift, double Blink)
{
    public static Thresholds Default { get; } = new(5200, 7200, 7400, 7650);
    public bool Valid => First is >= 100 and < 50000 && Last > First && Last <= 50000 &&
        Shift >= First && Shift <= 50000 && Blink >= First && Blink <= 50000;

    public static Thresholds Resolve(double first, double last, double shift, double blink, double redline)
    {
        static bool Usable(double value) => value is >= 100 and <= 50000;
        double ceiling = Usable(redline) ? redline : Usable(blink) ? blink : Usable(shift) ? shift :
            Usable(last) ? last : Default.Blink;
        if (!Usable(first) || first >= 50000) first = Math.Max(100, Math.Round(ceiling * 0.72));
        if (!Usable(last) || last <= first) last = Math.Min(50000, Math.Max(first + 1, Math.Round(ceiling * 0.95)));
        if (!Usable(shift) || shift < first) shift = last;
        if (!Usable(blink) || blink < first) blink = Math.Max(last, shift);
        return new(first, last, shift, blink);
    }
}

public enum FlashTrigger { Shift, Blink, Off }
public enum Progression { LeftToRight, RightToLeft, OutsideIn }
public enum LensShape { Blade, Round, Square }
public enum SourceMode { Automatic, Demo, Live }

public readonly record struct LightFrame(int Lit, bool Flashing, bool BrightPhase);
public sealed class ShiftLights
{
    private double? flashStart;

    public LightFrame Evaluate(double rpm, Thresholds points, int count, int offset, FlashTrigger trigger, int hz, double seconds)
    {
        // Shift the entire RPM curve together so adjustment preserves its spacing.
        double adjusted = rpm - offset;
        int lit = 0;
        if (double.IsFinite(rpm) && rpm > 0 && adjusted >= points.First)
        {
            double fraction = Math.Clamp((adjusted - points.First) / (points.Last - points.First), 0, 1);
            lit = 1 + (int)Math.Floor(fraction * (count - 1) + 1e-10);
        }
        bool flash = lit > 0 && trigger != FlashTrigger.Off &&
            adjusted >= (trigger == FlashTrigger.Shift ? points.Shift : points.Blink);
        if (!flash) { flashStart = null; return new(lit, false, true); }
        flashStart ??= seconds;
        bool bright = (int)(Math.Max(0, seconds - flashStart.Value) * hz * 2) % 2 == 0;
        return new(count, true, bright);
    }

    // Outside-in consumes alternating lenses at each end, including odd-sized strips.
    public static int Rank(int index, int count, Progression direction) => direction switch
    {
        Progression.RightToLeft => count - 1 - index,
        Progression.OutsideIn => Math.Min(index * 2, (count - 1 - index) * 2 + 1),
        _ => index
    };

    public static int ColorZone(int rank, int count) =>
        rank < (count + 2) / 3 ? 0 : rank < (2 * count + 2) / 3 ? 1 : rank < count - 2 ? 2 : 3;
}
