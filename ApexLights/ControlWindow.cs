using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using ApexLights.Core;

namespace ApexLights;

public sealed partial class ControlWindow : Window
{
    private readonly Runtime app;
    private Settings S => app.Config;
    private readonly StackPanel page = new(), tabs = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock sourceLabel = Label("", 11), reading = Label("", 13);
    private readonly TextBlock notice = Label("", 12);
    private readonly Button edit = new(), hide = new() { Content = "Hide settings" };
    private readonly LedStrip preview = new();
    private int tab;
    private bool demo;
    private string carKey = "", carName = "";
    private Thresholds sessionPoints = Thresholds.Default;
    private string? editorKey;
    private double? previousScale;
    private string? previousWarning;
    private static readonly Brush Muted = BrushOf("#ADB4B3"), Accent = BrushOf("#FF784A");
    private static readonly FontFamily Mono = new("Consolas");
    private static Brush BrushOf(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static TextBlock Label(string text, double size = 13) => new() { Text = text, FontSize = size,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private static void Named(FrameworkElement control, string name) => AutomationProperties.SetName(control, name);

    public ControlWindow(Runtime app)
    {
        this.app = app;
        Title = "ApexLights Settings";
        Width = 560; Height = 675; MinWidth = 490; MinHeight = 430;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 20);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BrushOf("#191D1F"); Foreground = BrushOf("#F0F2ED");
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        ShowInTaskbar = false;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ApexLights;component/Theme.xaml", UriKind.Relative) });
        SourceInitialized += (_, _) =>
        {
            int dark = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, 4);
        };
        Closing += (_, e) => { if (!app.Exiting) { e.Cancel = true; Hide(); } };

        var shell = new DockPanel();
        Content = shell;
        var header = new DockPanel { Margin = new(24, 18, 24, 16) };
        var mark = new Path { Data = Geometry.Parse("M0,24 L16,0 25,0 11,24Z M19,24 L29,8 37,24Z"),
            Fill = Accent, Width = 37, Height = 24, Margin = new(0, 0, 12, 0) };
        header.Children.Add(mark);
        var wordmark = Label("ApexLights", 27);
        wordmark.FontWeight = FontWeights.SemiBold;
        header.Children.Add(wordmark);
        DockPanel.SetDock(header, Dock.Top); shell.Children.Add(header);

        var well = new DockPanel { Margin = new(16, 10, 16, 10) };
        var readout = new DockPanel();
        sourceLabel.Foreground = Accent; sourceLabel.FontFamily = Mono;
        reading.FontFamily = Mono; reading.HorizontalAlignment = HorizontalAlignment.Right;
        readout.Children.Add(sourceLabel); readout.Children.Add(reading);
        DockPanel.SetDock(readout, Dock.Top); well.Children.Add(readout);
        preview.Height = 39; preview.Margin = new(0, 8, 0, 0);
        well.Children.Add(preview);
        var panel = new Border { Background = BrushOf("#101416"), BorderBrush = BrushOf("#383E40"),
            BorderThickness = new(1), CornerRadius = new(7), Margin = new(24, 0, 24, 12), Child = well };
        DockPanel.SetDock(panel, Dock.Top); shell.Children.Add(panel);

        tabs.Margin = new(24, 0, 24, 12);
        DockPanel.SetDock(tabs, Dock.Top); shell.Children.Add(tabs);
        string[] names = ["Telemetry", "Appearance", "Placement", "Indicators"];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var option = new RadioButton { Content = names[i], GroupName = "pages", Margin = new(0, 0, 5, 0), IsChecked = i == 0 };
            option.Checked += (_, _) => { tab = index; BuildPage(); scroll.ScrollToHome(); };
            tabs.Children.Add(option);
        }
        var footer = new DockPanel { Margin = new(24, 10, 24, 14) };
        edit.Click += (_, _) => app.ToggleEdit();
        hide.Click += (_, _) => Hide();
        hide.Margin = new(8, 0, 0, 0);
        DockPanel.SetDock(hide, Dock.Right); footer.Children.Add(hide);
        DockPanel.SetDock(edit, Dock.Left); footer.Children.Add(edit);
        var shortcut = Label(app.Overlay.Shortcut ?? "", 11); shortcut.Foreground = Muted; shortcut.Margin = new(10, 0, 0, 0);
        footer.Children.Add(shortcut); DockPanel.SetDock(footer, Dock.Bottom); shell.Children.Add(footer);
        notice.Margin = new(24, 0, 24, 0); notice.Foreground = Accent;
        DockPanel.SetDock(notice, Dock.Bottom); shell.Children.Add(notice);
        scroll.Content = page; page.Margin = new(24, 0, 24, 6); shell.Children.Add(scroll);
        Configure();
        BuildPage();
    }

    public void Configure()
    {
        preview.Configure(app.Appearance);
        preview.Opacity = S.Opacity;
        preview.Height = Math.Max(39, LedStrip.Footprint(app.Appearance).Height);
    }
    public void RefreshSourceSelection() => BuildPage();
    public void DisplayFrame(LightFrame frame, IndicatorFrame indicators)
    {
        preview.Display(frame);
        preview.DisplayIndicators(indicators);
    }
    public void ModeChanged() { edit.Content = app.Overlay.Editing ? "Lock overlay" : "Edit overlay"; }

    public void Display(LightFrame frame, double rpm, int? gear, bool isDemo, Sample? live, string? warning)
    {
        preview.Display(frame);
        string identity = live?.Car.Key ?? "";
        if (isDemo != demo || identity != carKey || (live is not null && live.Car.Points != sessionPoints))
        {
            demo = isDemo; carKey = identity;
            carName = live?.Car.Name ?? "";
            sessionPoints = live?.Car.Points ?? Thresholds.Default;
            BuildPage();
        }
        string mode = live != null ? "LIVE  /  " + carName : demo ? "DEMO  /  " +
            (!app.DemoSignals.Connected ? "DISCONNECTED" : S.Source == SourceMode.Automatic ? "AUTO" : "MANUAL") : "WAITING FOR IRACING";
        if (sourceLabel.Text != mode) sourceLabel.Text = mode;
        string output = live == null && (!demo || !app.DemoSignals.Connected) ? "— RPM" : $"{rpm:N0} RPM" + (gear != null ? $"   G {GearDisplay.Format(gear)}" : "");
        if (reading.Text != output) reading.Text = output;
        if (previousWarning != warning) { previousWarning = warning; SetNotice(warning ?? ""); }
        if (tab == 2 && previousScale != S.Scale) BuildPage();
    }

    private void SetNotice(string text) { notice.Text = text; notice.Visibility = text == "" ? Visibility.Collapsed : Visibility.Visible; }

    private void BuildPage()
    {
        page.Children.Clear();
        SetNotice(previousWarning ?? "");
        ModeChanged();
        if (tab == 0) TelemetryPage();
        if (tab == 1) AppearancePage();
        if (tab == 2) PlacementPage();
        if (tab == 3) IndicatorsPage();
    }

    private void Heading(string title)
    {
        var label = Label(title, 16); label.FontWeight = FontWeights.SemiBold; label.Margin = new(0, 8, 0, 6);
        page.Children.Add(label);
    }
    private void Hint(string text)
    {
        var label = Label(text, 12); label.Foreground = Muted; label.Margin = new(0, 3, 0, 7);
        page.Children.Add(label);
    }

    private void Choices(string label, string[] options, int selected, Action<int> change)
    {
        var row = new DockPanel { Margin = new(0, 5, 0, 5) };
        var title = Label(label); title.Width = 110; row.Children.Add(title);
        var buttons = new WrapPanel();
        string group = Guid.NewGuid().ToString();
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var choice = new RadioButton { Content = options[i], GroupName = group, IsChecked = i == selected, Margin = new(0, 0, 4, 0) };
            choice.Checked += (_, _) => change(index);
            buttons.Children.Add(choice);
        }
        row.Children.Add(buttons); page.Children.Add(row);
    }

    private Slider Range(string label, double min, double max, double value, double step, Func<double, string> format, Action<double> change)
    {
        var row = new DockPanel { Margin = new(0, 4, 0, 4) };
        var title = Label(label); title.Width = 110; row.Children.Add(title);
        var number = Label(format(value), 12); number.Width = 76; number.TextAlignment = TextAlignment.Right; number.FontFamily = Mono;
        DockPanel.SetDock(number, Dock.Right); row.Children.Add(number);
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = value,
            SmallChange = step, LargeChange = Math.Max(step, (max - min) / 10) };
        Named(slider, label);
        slider.ValueChanged += (_, e) => { number.Text = format(e.NewValue); change(e.NewValue); };
        row.Children.Add(slider); page.Children.Add(row);
        return slider;
    }

    private CheckBox Check(string label, bool value, Action<bool> change)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Margin = new(0, 6, 0, 6) };
        box.Click += (_, _) => change(box.IsChecked == true);
        page.Children.Add(box);
        return box;
    }

    private void Changed() => app.SettingsChanged();

    private void TelemetryPage()
    {
        Heading("Signal source");
        Choices("Input", ["Automatic", "Demo", "iRacing"], (int)S.Source, i =>
        {
            app.SetSource((SourceMode)i); BuildPage();
        });
        if (S.Source == SourceMode.Automatic) Hint("Runs a demo sweep until the first live session connects.");
        if (demo)
        {
            DemoScenarios();
            Heading("Demo controls");
            var sweep = Check("Sweep RPM automatically", app.Sweep, value => app.Sweep = value);
            Range("Manual RPM", 0, app.DemoMaximum, app.ManualRpm, 1, v => $"{v:N0}", value =>
            {
                app.Sweep = false; sweep.IsChecked = false; app.ManualRpm = value;
            });
            ThresholdEditor(S.Demo, true);
            DemoIndicators();
        }
        else if (carKey != "")
        {
            Heading(carName);
            ThresholdEditor(S.Cars.GetValueOrDefault(carKey, sessionPoints), false);
        }
        else Hint("Waiting for a session with engine RPM and gear telemetry.");
    }

    private void ThresholdEditor(Thresholds points, bool forDemo)
    {
        editorKey = forDemo ? null : carKey;
        var grid = new UniformGrid { Columns = 4, Margin = new(0, 6, 0, 4) };
        string[] names = ["First LED", "Last LED", "Shift", "Blink"];
        double[] values = [points.First, points.Last, points.Shift, points.Blink];
        var boxes = new TextBox[4];
        for (int i = 0; i < boxes.Length; i++)
        {
            var column = new StackPanel { Margin = new(0, 0, i == 3 ? 0 : 7, 0) };
            var label = Label(names[i], 11); label.Foreground = Muted; label.Margin = new(0, 0, 0, 4);
            column.Children.Add(label);
            boxes[i] = new TextBox { Text = values[i].ToString("0.##", CultureInfo.CurrentCulture), FontFamily = Mono };
            Named(boxes[i], names[i] + " RPM");
            column.Children.Add(boxes[i]); grid.Children.Add(column);
        }
        page.Children.Add(grid);
        var actions = new WrapPanel { Margin = new(0, 4, 0, 4) };
        var apply = new Button { Content = forDemo ? "Apply thresholds" : "Save car override" };
        apply.Click += (_, _) =>
        {
            double[] n = boxes.Select(b => double.TryParse(b.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) ? v : 0).ToArray();
            var next = new Thresholds(n[0], n[1], n[2], n[3]);
            if (!next.Valid) { SetNotice("Use 100–50,000 RPM. Last must exceed First. Shift and Blink must be at least First."); return; }
            if (forDemo) S.Demo = next;
            else if (editorKey == carKey && carKey != "") S.Cars[carKey] = next;
            Changed(); BuildPage(); SetNotice(forDemo ? "Demo thresholds applied." : "Override saved for this car.");
        };
        actions.Children.Add(apply);
        if (!forDemo && S.Cars.ContainsKey(carKey))
        {
            var reset = new Button { Content = "Use car defaults", Margin = new(8, 0, 0, 0) };
            reset.Click += (_, _) => { S.Cars.Remove(carKey); Changed(); BuildPage(); };
            actions.Children.Add(reset);
        }
        page.Children.Add(actions);
    }

    private void AppearancePage()
    {
        var cockpit = new Button { Content = "Apply cockpit preset", HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 5, 0, 3), ToolTip = "Use neon LEDs, a large right-hand gear pod, side chevrons and the status window." };
        cockpit.Click += (_, _) => { app.ApplyCockpitPreset(); BuildPage(); SetNotice("Cockpit preset applied."); };
        page.Children.Add(cockpit);
        Heading("LED strip");
        Choices("Segments", ["10", "11", "12"], S.Count - 10, i => { S.Count = 10 + i; Changed(); });
        Choices("Shape", ["Blade", "Round", "Square"], (int)S.Shape, i => { S.Shape = (LensShape)i; Changed(); });
        Choices("Progression", ["Left → right", "Right → left", "Outside in"], (int)S.Direction, i => { S.Direction = (Progression)i; Changed(); });
        var colors = new DockPanel { Margin = new(0, 5, 0, 5) };
        var title = Label("Zone colors"); title.Width = 110; colors.Children.Add(title);
        var swatches = new WrapPanel();
        for (int i = 0; i < 4; i++)
        {
            int zone = i;
            var swatch = new Button { Background = BrushOf(S.Colors[i]), Width = 42, Height = 24, Margin = new(0, 0, 5, 0),
                ToolTip = $"Zone {i+1} · {S.Colors[i]}" };
            Named(swatch, $"Zone {i+1} color");
            swatch.Click += (_, _) =>
            {
                using var dialog = new Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(S.Colors[zone]) };
                if (dialog.ShowDialog(new NativeOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
                S.Colors[zone] = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                swatch.Background = BrushOf(S.Colors[zone]); swatch.ToolTip = $"Zone {zone+1} · {S.Colors[zone]}";
                Changed();
            };
            swatches.Children.Add(swatch);
        }
        colors.Children.Add(swatches); page.Children.Add(colors);
        Range("Lens size", 12, 44, S.LensSize, 1, v => $"{v:0} DIP", v => { S.LensSize = v; Changed(); });
        Range("Spacing", 1, 14, S.Gap, 1, v => $"{v:0} DIP", v => { S.Gap = v; Changed(); });
        Range("Brightness", .1, 1, S.Brightness, .01, v => $"{v:P0}", v => { S.Brightness = v; Changed(); });
        Range("Inactive LEDs", 0, 1, S.InactiveBrightness, .01, v => $"{v:P0}", v => { S.InactiveBrightness = v; Changed(); });
        Range("Opacity", .15, 1, S.Opacity, .01, v => $"{v:P0}", v => { S.Opacity = v; Changed(); });
        Check("Dark strip housing", S.DarkHousing, v => { S.DarkHousing = v; Changed(); });
        Heading("Shift cue");
        Choices("Flash at", ["Shift RPM", "Blink RPM", "Off"], (int)S.Flash, i => { S.Flash = (FlashTrigger)i; Changed(); });
        Range("Flash rate", 1, 10, S.Hz, 1, v => $"{v:0} Hz", v => { S.Hz = (int)v; Changed(); });
        Range("RPM offset", -1500, 1500, S.Offset, 25, v => $"{v:+0;-0;0} RPM", v => { S.Offset = (int)v; Changed(); });
    }

    private void PlacementPage()
    {
        Heading("Overlay placement");
        Hint("Enter Edit Mode to drag the strip. Drag its lower-right grip to resize.");
        previousScale = S.Scale;
        Range("Scale", .5, 2.5, S.Scale, .01, v => $"{v:P0}", v =>
        {
            app.Overlay.PreviewAtScale(null); S.Scale = v; previousScale = v; Changed();
        });
        var center = new Button { Content = "Center on primary display", HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 6, 0, 10) };
        center.Click += (_, _) => app.Overlay.Place(null, null);
        page.Children.Add(center);
        Heading("Startup");
        Check("Start with settings hidden", S.StartMinimized, v => { S.StartMinimized = v; Changed(); });
        Hint("Open settings or exit from the ApexLights tray icon.");
    }

    private sealed record NativeOwner(IntPtr Handle) : Forms.IWin32Window;
    [DllImport("dwmapi")] private static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);
}
