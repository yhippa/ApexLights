using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexLights.Core;

public sealed class Settings
{
    public IndicatorSettings Indicators { get; set; } = new();
    public int Count { get; set; } = 12;
    public string[] Colors { get; set; } = DefaultColors();
    public static string[] DefaultColors() => ["#43EC86", "#FFCF46", "#FF513D", "#479BFF"];
    public Progression Direction { get; set; }
    public LensShape Shape { get; set; }
    public double Brightness { get; set; } = 0.92;
    public double Opacity { get; set; } = 1;
    public double LensSize { get; set; } = 25;
    public double Gap { get; set; } = 5;
    public double Scale { get; set; } = 1;
    public bool DarkHousing { get; set; } = true;
    public bool StartMinimized { get; set; }
    public SourceMode Source { get; set; }
    public FlashTrigger Flash { get; set; }
    public int Hz { get; set; } = 4;
    public int Offset { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public Thresholds Demo { get; set; } = Thresholds.Default;
    public Dictionary<string, Thresholds> Cars { get; set; } = new();

    public void Normalize()
    {
        static double Limit(double n, double low, double high, double fallback) =>
            double.IsFinite(n) ? Math.Clamp(n, low, high) : fallback;
        Indicators ??= new();
        Indicators.Normalize();
        Count = Math.Clamp(Count, 10, 12);
        Gap = Limit(Gap, 1, 14, 5);
        LensSize = Limit(LensSize, 12, 44, 25);
        Scale = Limit(Scale, 0.5, 2.5, 1);
        Brightness = Limit(Brightness, 0.1, 1, 0.92);
        Opacity = Limit(Opacity, 0.15, 1, 1);
        Hz = Math.Clamp(Hz, 1, 10);
        Offset = Math.Clamp(Offset, -1500, 1500);
        if (!Enum.IsDefined(Direction)) Direction = Progression.LeftToRight;
        if (!Enum.IsDefined(Shape)) Shape = LensShape.Blade;
        if (!Enum.IsDefined(Flash)) Flash = FlashTrigger.Shift;
        if (!Enum.IsDefined(Source)) Source = SourceMode.Automatic;
        string[] palette = DefaultColors();
        for (int i = 0; i < 4; i++)
            if (Colors is { } colors && colors.Length > i && colors[i] is { Length: 7 } hex &&
                hex[0] == '#' && hex.Skip(1).All(Uri.IsHexDigit)) palette[i] = hex;
        Colors = palette;
        if (Demo?.Valid != true) Demo = Thresholds.Default;
        Cars = (Cars ?? new()).Where(p => p.Key.Length is > 0 and < 150 && p.Value?.Valid == true)
            .ToDictionary(p => p.Key, p => p.Value);
    }
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string? Warning { get; private set; }

    public Settings Load()
    {
        try
        {
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("Settings file too large.");
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new();
            settings.Normalize();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Warning = "Could not read settings. Defaults are active.";
            return new();
        }
    }

    public bool Save(Settings settings)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Json));
            File.Move(temporary, path, true);
            Warning = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Warning = $"Could not save settings. {e.Message}";
            return false;
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
