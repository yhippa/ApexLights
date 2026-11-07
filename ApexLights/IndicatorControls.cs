using System;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class ControlWindow
{
    private void IndicatorsPage()
    {
        var options = S.Indicators;
        Heading("Gear");
        Check("Show gear", options.ShowGear, value => { options.ShowGear = value; Changed(); });
        Choices("Position", ["Left", "Center", "Right"], (int)options.GearPosition,
            i => { options.GearPosition = (GearPosition)i; Changed(); });
        string[] fonts = ["Consolas", "Bahnschrift", "Segoe UI"];
        Choices("Typeface", fonts, Array.IndexOf(fonts, options.GearFont),
            i => { options.GearFont = fonts[i]; Changed(); });
        Range("Type size", 14, 40, options.GearSize, 1, n => $"{n:0} DIP", n => { options.GearSize = n; Changed(); });
    }
}
