using System;

namespace ApexLights.Core;

public enum CuePlace { Left, Right, Flag }

public static class CuePosition
{
    // The caller supplies dimensions in one coordinate space (screen pixels or preview DIPs).
    public static (double X, double Y) Within(CuePlace place, double width, double height,
        double cueWidth, double cueHeight, double inset, double elevation)
    {
        double x = place switch
        {
            CuePlace.Left => width * inset,
            CuePlace.Right => width * (1 - inset) - cueWidth,
            _ => (width - cueWidth) / 2
        };
        double y = height * (place == CuePlace.Flag ? elevation : .48) - cueHeight / 2;
        return (Math.Clamp(x, 0, Math.Max(0,width - cueWidth)), Math.Clamp(y, 0, Math.Max(0,height - cueHeight)));
    }
}
