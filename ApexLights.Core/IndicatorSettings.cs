using System;
using System.Globalization;
using System.Linq;

namespace ApexLights.Core;

public enum GearPosition { Left, Center, Right }

public enum FlagLifetime { Timed, UntilClear }
public enum FlagStyle { Symbol, Labeled }
public enum SideStyle { Chevrons, Lamps }

public sealed class IndicatorSettings
{
    public bool ShowFlags { get; set; }
    public bool ShowProximity { get; set; }
    public FlagLifetime FlagLifetime { get; set; }
    public FlagStyle FlagStyle { get; set; } = FlagStyle.Labeled;
    public SideStyle SideStyle { get; set; }
    public double FlagScale { get; set; } = 1;
    public double SideScale { get; set; } = 1;
    public double AlertSeconds { get; set; } = 4;
    public bool AnimateEntry { get; set; } = true;
    public RaceFlag EnabledFlags { get; set; } = IndicatorPolicy.Supported;
    public bool ShowGear { get; set; }
    public GearPosition GearPosition { get; set; } = GearPosition.Right;
    public double GearSize { get; set; } = 24;
    public string GearFont { get; set; } = "Consolas";

    public void Normalize()
    {
        if (!Enum.IsDefined(FlagLifetime)) FlagLifetime = FlagLifetime.Timed;
        if (!Enum.IsDefined(FlagStyle)) FlagStyle = FlagStyle.Labeled;
        if (!Enum.IsDefined(SideStyle)) SideStyle = SideStyle.Chevrons;
        FlagScale = NormalizeScale(FlagScale);
        SideScale = NormalizeScale(SideScale);
        AlertSeconds = double.IsFinite(AlertSeconds) ? Math.Clamp(AlertSeconds, 1, 20) : 4;
        EnabledFlags &= IndicatorPolicy.Supported;
        if (!Enum.IsDefined(GearPosition)) GearPosition = GearPosition.Right;
        GearSize = double.IsFinite(GearSize) ? Math.Clamp(GearSize, 14, 40) : 24;
        if (string.IsNullOrWhiteSpace(GearFont) || GearFont.Length > 100) GearFont = "Consolas";
    }

    private static double NormalizeScale(double value) => double.IsFinite(value) ? Math.Clamp(value, .75, 1.75) : 1;

    internal IndicatorSettings PreviewAll()
    {
        var copy = (IndicatorSettings)MemberwiseClone();
        copy.ShowGear = copy.ShowFlags = copy.ShowProximity = true;
        copy.EnabledFlags = IndicatorPolicy.Supported;
        return copy;
    }
}

public static class GearDisplay
{
    private static readonly string[] Forward = Enumerable.Range(1, 30)
        .Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
    public static string Format(int? gear) => gear switch
    {
        -1 => "R", 0 => "N", >= 1 and <= 30 => Forward[gear.Value - 1], _ => ""
    };
}

public readonly record struct IndicatorFrame(string Gear, RaceFlag Flag = RaceFlag.None,
    int Left = 0, int Right = 0, int Accent = 0, bool NoticeSettled = false);
