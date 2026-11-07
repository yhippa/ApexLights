using System;
using System.Globalization;
using System.Linq;

namespace ApexLights.Core;

public enum GearPosition { Left, Center, Right }

public sealed class IndicatorSettings
{
    public bool ShowGear { get; set; }
    public GearPosition GearPosition { get; set; } = GearPosition.Right;
    public double GearSize { get; set; } = 24;
    public string GearFont { get; set; } = "Consolas";

    public void Normalize()
    {
        if (!Enum.IsDefined(GearPosition)) GearPosition = GearPosition.Right;
        GearSize = double.IsFinite(GearSize) ? Math.Clamp(GearSize, 14, 40) : 24;
        if (string.IsNullOrWhiteSpace(GearFont) || GearFont.Length > 100) GearFont = "Consolas";
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

public readonly record struct IndicatorFrame(string Gear);
