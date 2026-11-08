using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ApexLights.Core;
using Forms = System.Windows.Forms;

namespace ApexLights;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new Mutex(true, @"Local\ApexLights.Instance.v1", out bool first);
        using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ApexLights.Show.v1");
        using var demoWake = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ApexLights.Demo.v1");
        bool demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        if (!first) { (demo ? demoWake : wake).Set(); return; }
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var runtime = new Runtime(application, demo);
        var registration = ThreadPool.RegisterWaitForSingleObject(wake, (_, _) =>
            application.Dispatcher.BeginInvoke(runtime.OpenSettings), null, Timeout.Infinite, false);
        var demoRegistration = ThreadPool.RegisterWaitForSingleObject(demoWake, (_, _) =>
            application.Dispatcher.BeginInvoke(() => { runtime.SetSource(SourceMode.Demo); runtime.OpenSettings(); }),
            null, Timeout.Infinite, false);
        try { runtime.Start(); application.Run(); }
        finally { registration.Unregister(null); demoRegistration.Unregister(null); }
    }
}

public sealed class Runtime : IDisposable
{
    private readonly Application application;
    private readonly SettingsStore store;
    private readonly ShiftLights engine = new();
    private readonly IndicatorPolicy policy = new();
    private readonly DispatcherTimer update = new(DispatcherPriority.Render), save = new();
    private readonly Stopwatch time = Stopwatch.StartNew();
    private readonly bool forceOpen;
    private LiveFeed? feed;
    private ControlWindow? controls;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? editItem;
    private bool receivedLive, dirty;
    private double lastReadout = -1, sweepOrigin;
    private bool previousDemo;
    private string? previousCar;
    private Settings? demoAppearance;
    public bool DemoUsesCockpit { get; private set; } = true;
    private double? sweepFromRpm;
    public Settings Config { get; }
    public Settings Appearance => demoAppearance ?? Config;
    public Overlay Overlay { get; }
    public bool Exiting { get; private set; }
    public DemoSignals DemoSignals { get; } = new();
    public bool Sweep { get; set; } = true;
    public double ManualRpm { get; set; } = 6400;
    public double DemoMaximum
    {
        get
        {
            double highestCue = Math.Max(Config.Demo.Blink, Math.Max(Config.Demo.Last, Config.Demo.Shift));
            return Math.Ceiling((highestCue + Math.Max(0, Config.Offset)) * 1.08 / 100) * 100;
        }
    }

    public Runtime(Application application, bool demo)
    {
        this.application = application;
        string directory = Environment.GetEnvironmentVariable("APEXLIGHTS_SETTINGS_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApexLights");
        store = new(Path.Combine(directory, "settings.json"));
        Config = store.Load();
        if (demo) Config.Source = SourceMode.Demo;
        forceOpen = demo;
        Overlay = new Overlay(Config, ScheduleSave, ToggleEdit);
        save.Interval = TimeSpan.FromMilliseconds(450);
        save.Tick += (_, _) => { save.Stop(); Flush(); };
        update.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        Overlay.Show();
        controls = new ControlWindow(this);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => OpenSettings());
        editItem = new Forms.ToolStripMenuItem("Edit overlay", null, (_, _) => ToggleEdit());
        menu.Items.Add(editItem);
        menu.Items.Add("Demo Mode", null, (_, _) => { SetSource(SourceMode.Demo); OpenSettings(); });
        menu.Items.Add("Use iRacing", null, (_, _) => SetSource(SourceMode.Live));
        menu.Items.Add("Dismiss current flag", null, (_, _) => DismissFlag());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit ApexLights", null, (_, _) => application.Shutdown());
        using var resource = Application.GetResourceStream(new Uri("pack://application:,,,/ApexLights;component/apex.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(resource);
        tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)icon.Clone(), Text = "ApexLights", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => OpenSettings();
        UpdateFeed();
        if (!Config.StartMinimized || forceOpen) OpenSettings();
        Tick();
        update.Start();
    }

    public void OpenSettings()
    {
        if (Exiting || controls == null) return;
        controls.Show();
        if (controls.WindowState == WindowState.Minimized) controls.WindowState = WindowState.Normal;
        controls.Activate();
        lastReadout = -1;
        Tick();
    }

    public void ToggleEdit()
    {
        Overlay.SetEdit(!Overlay.Editing);
        controls?.ModeChanged();
        if (editItem != null) editItem.Text = Overlay.Editing ? "Lock overlay" : "Edit overlay";
    }

    public void SetSource(SourceMode mode)
    {
        Overlay.PreviewAtScale(null);
        Config.Source = mode;
        policy.Reset();
        receivedLive = false;
        UpdateFeed();
        ScheduleSave();
        lastReadout = -1;
        Tick();
        controls?.RefreshSourceSelection();
    }

    public void RefreshDemo() { lastReadout = -1; Tick(); }

    public void SetDemoLook(bool cockpit)
    {
        DemoUsesCockpit = cockpit;
        RefreshDemo();
    }

    public void ClearDemoAlerts()
    {
        DemoSignals.ClearAlerts();
        policy.Reset();
        RefreshDemo();
        controls?.RefreshSourceSelection();
    }

    public void ApplyScenario(DemoScenario scenario)
    {
        DemoSignals.BeginScenario();
        RefreshDemo(); // Establish a known quiet sample before staging an actual rising edge.
        var drive = DemoSignals.Apply(scenario, Config.Demo, Config.Offset, Config.Flash);
        ManualRpm = Math.Min(drive.Rpm, DemoMaximum);
        Sweep = drive.Sweep;
        sweepFromRpm = drive.Sweep ? ManualRpm : null;
        RefreshDemo();
        controls?.RefreshSourceSelection();
    }

    public void DismissFlag() { policy.Dismiss(); Tick(); }

    public void ApplyCockpitPreset()
    {
        Config.ApplyCockpitStyle();
        DemoUsesCockpit = false;
        SettingsChanged();
    }

    private void UpdateFeed()
    {
        if (Config.Source != SourceMode.Demo) feed ??= new();
        else { feed?.Dispose(); feed = null; }
    }

    public void SettingsChanged()
    {
        ManualRpm = Math.Min(ManualRpm, DemoMaximum);
        if (demoAppearance != null) demoAppearance = DemoUsesCockpit ? Config.PreviewAllIndicators(cockpitStyle: true) : null;
        UpdateAppearance();
        ScheduleSave();
        lastReadout = -1;
        Tick();
    }

    private void UpdateAppearance()
    {
        Overlay.ApplySize(Appearance);
        controls?.Configure();
    }

    private void ScheduleSave()
    {
        dirty = true;
        save.Stop();
        if (!Exiting) save.Start();
    }
    private void Flush()
    {
        if (dirty) dirty = !store.Save(Config);
    }

    private void Tick()
    {
        var live = feed?.Latest;
        receivedLive |= live != null;
        bool demo = Config.Source == SourceMode.Demo ||
            (Config.Source == SourceMode.Automatic && live == null && !receivedLive);
        var points = live == null ? Config.Demo : Config.Cars.GetValueOrDefault(live.Car.Key, live.Car.Points);
        double seconds = time.Elapsed.TotalSeconds;
        bool demoOnline = demo && DemoSignals.Connected;
        if ((demo && DemoUsesCockpit) != (demoAppearance != null))
        {
            demoAppearance = demo && DemoUsesCockpit ? Config.PreviewAllIndicators(cockpitStyle: true) : null;
            UpdateAppearance();
        }
        if (!demo) Overlay.PreviewAtScale(null);
        string? car = live?.Car.Key;
        if (car != previousCar || demoOnline != previousDemo)
        {
            engine.Evaluate(0, points, Config.Count, 0, FlashTrigger.Off, 1, seconds);
            policy.Reset();
            sweepOrigin = seconds;
            previousCar = car;
            previousDemo = demoOnline;
        }
        double rpm = live?.Rpm ?? 0;
        if (demoOnline)
        {
            // A slow climb and a short downshift make every zone and flash interval visible.
            double floor = Math.Max(0, (points.First + Config.Offset) * .84);
            if (sweepFromRpm is { } start)
            {
                double fraction = Math.Clamp((start - floor) / (DemoMaximum - floor), 0, 1);
                sweepOrigin = seconds + 1.7 - fraction * 5.2;
                sweepFromRpm = null;
            }
            double cycle = (seconds - sweepOrigin + 1.7) % 6;
            double progress = cycle < 5.2 ? cycle / 5.2 : (6 - cycle) / 0.8;
            rpm = Sweep ? floor + (DemoMaximum - floor) * progress : ManualRpm;
        }
        var frame = engine.Evaluate(rpm, points, Appearance.Count, Config.Offset, Config.Flash, Config.Hz, seconds);
        RaceSignal? signal = demo ? DemoSignals.Read() : live?.Signal;
        int? gear = signal?.Gear;
        var indicators = policy.Evaluate(signal, Appearance.Indicators, frame, seconds);
        Overlay.Strip.Display(frame);
        Overlay.Strip.DisplayIndicators(indicators);
        bool animated = live != null || (demoOnline && Sweep) || frame.Flashing || indicators.Accent > 0;
        var interval = TimeSpan.FromMilliseconds(animated ? 16 : 180);
        if (update.Interval != interval) update.Interval = interval;
        if (controls?.IsVisible == true)
        {
            controls.DisplayFrame(frame, indicators);
            if (seconds - lastReadout >= .12)
            {
                lastReadout = seconds;
                controls.Display(frame, rpm, gear, demo, live,
                    store.Warning ?? (Overlay.Shortcut == null ? "Global shortcut unavailable. Use Edit overlay here or in the tray." : null));
            }
        }
    }

    public void Dispose()
    {
        Exiting = true;
        update.Stop(); save.Stop();
        feed?.Dispose();
        Flush();
        if (tray != null)
        {
            tray.Visible = false;
            tray.ContextMenuStrip?.Dispose();
            tray.Icon?.Dispose();
            tray.Dispose();
        }
    }
}
