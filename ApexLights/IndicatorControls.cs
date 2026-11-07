using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    private void DemoScenarios()
    {
        Heading("Driving scenarios");
        var buttons = new WrapPanel();
        (string, DemoScenario)[] scenes = [
            ("Normal driving", DemoScenario.Normal), ("Approach shift", DemoScenario.ApproachShift),
            ("Side by side", DemoScenario.SideBySide), ("Local yellow", DemoScenario.LocalYellow),
            ("Repair required", DemoScenario.Repair), ("Competing alerts", DemoScenario.Competing),
            ("Return to normal", DemoScenario.ReturnToNormal)];
        foreach (var (label, scenario) in scenes)
        {
            var button = new Button { Content = label, Margin = new(0, 0, 5, 5), Padding = new(10, 5, 10, 5) };
            button.Click += (_, _) => app.ApplyScenario(scenario);
            buttons.Children.Add(button);
        }
        page.Children.Add(buttons);
        Hint("Scenarios preview all indicators. Return to normal clears alerts and restores your indicator choices.");
        double?[] scales = [null, .75, 1, 1.5];
        Choices("Test scale", ["Saved", "75%", "100%", "150%"], Array.IndexOf(scales, app.Overlay.PreviewScale),
            i => app.Overlay.PreviewAtScale(scales[i]));
    }

    private void DemoIndicators()
    {
        var demo = app.DemoSignals;
        Heading("Demo indicators");
        var actions = new WrapPanel();
        var show = new Button { Content = "Enable all indicators", Margin = new(0, 0, 6, 0) };
        show.Click += (_, _) =>
        {
            S.Indicators.ShowGear = S.Indicators.ShowFlags = S.Indicators.ShowProximity = true;
            Changed();
            SetNotice("Gear, flags, and proximity enabled.");
        };
        var session = new Button { Content = "New demo session" };
        session.Click += (_, _) => { demo.NewSession(); app.RefreshDemo(); SetNotice("New demo session started."); };
        actions.Children.Add(show); actions.Children.Add(session); page.Children.Add(actions);
        Check("Demo telemetry connected", demo.Connected, value => { demo.SetConnection(value); app.RefreshDemo(); });
        Range("Demo gear", -1, 12, demo.Gear, 1, n => GearDisplay.Format((int)n),
            n => { demo.Gear = (int)n; app.RefreshDemo(); });
        Choices("Alongside", ["Off", "Clear", "Left", "Right", "Both", "Two left", "Two right"], (int)demo.Spotter,
            i => { demo.Spotter = (SpotterState)i; app.RefreshDemo(); });
        Heading("Demo flags");
        var grid = new UniformGrid { Columns = 3 };
        (string, RaceFlag)[] flags = [
            ("Green", RaceFlag.Green), ("Yellow", RaceFlag.Yellow), ("Waving yellow", RaceFlag.WavingYellow),
            ("Blue", RaceFlag.Blue), ("White", RaceFlag.White), ("Checkered", RaceFlag.Checkered),
            ("Black", RaceFlag.Black), ("Red", RaceFlag.Red), ("Repair", RaceFlag.Repair),
            ("Full caution", RaceFlag.Caution), ("Waving caution", RaceFlag.WavingCaution)];
        foreach (var (name, mask) in flags)
        {
            var check = new CheckBox { Content = name, IsChecked = (demo.Flags & mask) != 0, Margin = new(0, 3, 0, 3) };
            Named(check, "Demo flag " + name);
            check.Click += (_, _) =>
            {
                demo.Flags = check.IsChecked == true ? demo.Flags | mask : demo.Flags & ~mask;
                app.RefreshDemo();
            };
            grid.Children.Add(check);
        }
        page.Children.Add(grid);
    }

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
        Range("Side cue size", .75, 1.75, options.SideScale, .05, n => $"{n:P0}", n => { options.SideScale = n; Changed(); });
        Hint("One or two marks show the spotter’s count on each side. The spotter reports lateral overlap only.");

        Heading("Racing flags");
        Check("Show racing flags", options.ShowFlags, value => { options.ShowFlags = value; Changed(); });
        Choices("Display", ["Symbol", "With label"], (int)options.FlagStyle, i => { options.FlagStyle = (FlagStyle)i; Changed(); });
        Range("Flag size", .75, 1.75, options.FlagScale, .05, n => $"{n:P0}", n => { options.FlagScale = n; Changed(); });
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
