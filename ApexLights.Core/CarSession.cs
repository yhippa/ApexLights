using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ApexLights.Core;

public sealed record CarSession(string Key, string Name, Thresholds Points)
{
    // Only the small DriverInfo subset emitted by iRacing is needed here.
    public static CarSession Parse(string yaml)
    {
        var info = new Dictionary<string, string>();
        var drivers = new Dictionary<int, Dictionary<string, string>>();
        Dictionary<string, string>? driver = null;
        bool inInfo = false, inDrivers = false;
        int rootIndent = -1;
        using var reader = new StringReader(yaml.TrimEnd('\0'));
        while (reader.ReadLine() is { } line)
        {
            string body = line.TrimStart();
            if (body.Length == 0 || body.StartsWith('#')) continue;
            int indent = line.Length - body.Length;
            if (indent == 0)
            {
                if (inInfo) break;
                inInfo = body.TrimEnd() == "DriverInfo:";
                continue;
            }
            if (!inInfo) continue;
            if (rootIndent < 0) rootIndent = indent;
            bool item = body.StartsWith("- ", StringComparison.Ordinal);
            if (item) body = body[2..].TrimStart();
            int colon = body.IndexOf(':');
            if (colon < 0) continue;
            string key = body[..colon];
            string value = body[(colon + 1)..].Trim().Trim('"', '\'');
            if (indent == rootIndent && !item)
            {
                inDrivers = key == "Drivers";
                driver = null;
                if (key is "DriverCarIdx" or "DriverCarRedLine" or "DriverCarSLFirstRPM" or
                    "DriverCarSLLastRPM" or "DriverCarSLShiftRPM" or "DriverCarSLBlinkRPM") info[key] = value;
            }
            else if (inDrivers && item)
            {
                driver = null;
                if (key == "CarIdx" && int.TryParse(value, out int index))
                    drivers[index] = driver = new();
            }
            else if (driver != null && key is ("CarID" or "CarPath" or "CarScreenName"))
                driver[key] = value;
        }

        double Read(string field) => double.TryParse(info.GetValueOrDefault(field), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) ? value : 0;
        var points = Thresholds.Resolve(Read("DriverCarSLFirstRPM"), Read("DriverCarSLLastRPM"),
            Read("DriverCarSLShiftRPM"), Read("DriverCarSLBlinkRPM"), Read("DriverCarRedLine"));
        if (int.TryParse(info.GetValueOrDefault("DriverCarIdx"), out int player) && drivers.TryGetValue(player, out var car))
        {
            string path = car.GetValueOrDefault("CarPath", "");
            string id = int.TryParse(car.GetValueOrDefault("CarID"), out int number) && number > 0
                ? $"id-{number}" : path;
            return new(id, car.GetValueOrDefault("CarScreenName", path), points);
        }
        return new("", "iRacing", points);
    }
}
