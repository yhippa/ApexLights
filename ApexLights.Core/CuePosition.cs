using System;

namespace ApexLights.Core;

public enum CuePlace { Left, Right, Flag }

public static class CuePosition
{
    // The caller supplies dimensions in one coordinate space (screen pixels or preview DIPs).
    public static (double X, double Y) Within(CuePlace place, double width, double height,
        double cueWidth, double cueHeight, double inset, double elevation, double? monitorAspect = null)
    {
        // Scale lateral reach with display height, even when the monitor wraps around the cockpit.
        // The settings preview supplies the real monitor aspect because its own height is compressed.
        double aspect = monitorAspect ?? (height > 0 ? width / height : 1);
        double span = width * Math.Min(1, (4.0 / 3) / Math.Max(.01, aspect));
        double margin = (width - span) / 2 + span * inset;
        margin = Math.Min(margin, Math.Max(0, width / 2 - cueWidth));
        double x = place switch
        {
            CuePlace.Left => margin,
            CuePlace.Right => width - margin - cueWidth,
            _ => (width - cueWidth) / 2
        };
        double y = height * (place == CuePlace.Flag ? elevation : .48) - cueHeight / 2;
        return (Math.Clamp(x, 0, Math.Max(0,width - cueWidth)), Math.Clamp(y, 0, Math.Max(0,height - cueHeight)));
    }
}
