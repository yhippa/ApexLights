using System;
using System.Windows;
using System.Windows.Controls;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class ControlWindow
{
    private static readonly (string Name, RaceFlag Mask)[] FlagGroups =
    [
        ("Red", RaceFlag.Red), ("Black", RaceFlag.Black), ("Repair", RaceFlag.Repair),
        ("Local yellow", RaceFlag.Yellow | RaceFlag.WavingYellow),
        ("Full-course caution", RaceFlag.Caution | RaceFlag.WavingCaution),
        ("Blue", RaceFlag.Blue), ("White", RaceFlag.White), ("Checkered", RaceFlag.Checkered), ("Green", RaceFlag.Green)
    ];

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

        Heading("Proximity");
        Check("Show cars alongside", options.ShowProximity, value => { options.ShowProximity = value; Changed(); });
        Choices("Side marks", ["Chevrons", "Lamps"], (int)options.SideStyle, i => { options.SideStyle = (SideStyle)i; Changed(); });
        Hint("One or two marks show the spotter’s count on each side.");

        Heading("Racing flags");
        Check("Show racing flags", options.ShowFlags, value => { options.ShowFlags = value; Changed(); });
        Choices("Display", ["Symbol", "With label"], (int)options.FlagStyle, i => { options.FlagStyle = (FlagStyle)i; Changed(); });
        Choices("Dismissal", ["Timed", "Until clear"], (int)options.FlagLifetime, i => { options.FlagLifetime = (FlagLifetime)i; Changed(); });
        Range("Alert duration", 1, 20, options.AlertSeconds, 1, n => $"{n:0} s", n => { options.AlertSeconds = n; Changed(); });
        Check("Accent a newly raised flag", options.AnimateEntry, value => { options.AnimateEntry = value; Changed(); });
        var dismiss = new Button { Content = "Dismiss current flag", HorizontalAlignment = HorizontalAlignment.Left };
        dismiss.Click += (_, _) => app.DismissFlag();
        page.Children.Add(dismiss);

        Heading("Flag categories");
        foreach (var (name, mask) in FlagGroups)
            Check(name, (options.EnabledFlags & mask) != 0, enabled =>
            {
                options.EnabledFlags = enabled ? options.EnabledFlags | mask : options.EnabledFlags & ~mask;
                Changed();
            });
    }
}
