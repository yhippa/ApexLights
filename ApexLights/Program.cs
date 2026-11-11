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
    private SessionKey? previousSession;
    private readonly DispatcherTimer update = new(DispatcherPriority.Render), save = new();
    private readonly Stopwatch time = Stopwatch.StartNew();
    private readonly bool forceOpen;
    private LiveFeed? feed;
    private ControlWindow? controls;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? editItem;
    private bool dirty;
    private double lastReadout = -1, sweepOrigin;
    private bool previousDemo;
    private string? previousCar;
    public Settings Config { get; }
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
        var oldSize = LedStrip.Footprint(Config);
        Config.UseShiftLightLayout();
        Overlay = new Overlay(Config, ScheduleSave, ToggleEdit, oldSize);
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
        Config.Source = mode;
        UpdateFeed();
        ScheduleSave();
        lastReadout = -1;
        Tick();
        controls?.RefreshSourceSelection();
    }

    public void RefreshDemo() { lastReadout = -1; Tick(); }

    private void UpdateFeed()
    {
        if (Config.Source != SourceMode.Demo) feed ??= new();
        else { feed?.Dispose(); feed = null; }
    }

    public void SettingsChanged()
    {
        ManualRpm = Math.Min(ManualRpm, DemoMaximum);
        UpdateAppearance();
        ScheduleSave();
        lastReadout = -1;
        Tick();
    }

    private void UpdateAppearance()
    {
        Overlay.ApplySize();
        controls?.Configure();
    }

    private void ScheduleSave()
    {
        dirty = true;
        save.Stop();
        if (!Exiting) save.Start();
    }
    public void InterfaceChanged() => ScheduleSave();
    private void Flush()
    {
        if (dirty) dirty = !store.Save(Config);
    }

    private void Tick()
    {
        var live = feed?.Latest;
        bool demo = Config.Source == SourceMode.Demo;
        var points = live == null ? Config.Demo : Config.Cars.GetValueOrDefault(live.Car.Key, live.Car.Points);
        double seconds = time.Elapsed.TotalSeconds;
        bool demoOnline = demo && DemoSignals.Connected;
        string? car = live?.Car.Key;
        if (car != previousCar || demoOnline != previousDemo || previousSession != live?.Signal.Session)
        {
            engine.Evaluate(0, points, Config.Count, 0, FlashTrigger.Off, 1, seconds);
            sweepOrigin = seconds;
            previousCar = car;
            previousSession = live?.Signal.Session;
            previousDemo = demoOnline;
        }
        double rpm = live?.Rpm ?? 0;
        if (demoOnline)
        {
            // A slow climb and a short downshift make every zone and flash interval visible.
            double floor = Math.Max(0, (points.First + Config.Offset) * .84);
            double cycle = (seconds - sweepOrigin + 1.7) % 6;
            double progress = cycle < 5.2 ? cycle / 5.2 : (6 - cycle) / 0.8;
            rpm = Sweep ? floor + (DemoMaximum - floor) * progress : ManualRpm;
        }
        var frame = engine.Evaluate(rpm, points, Config.Count, Config.Offset, Config.Flash, Config.Hz, seconds);
        RaceSignal? signal = demo ? DemoSignals.Read() : live?.Signal;
        int? gear = signal?.Gear;
        var indicators = new IndicatorFrame(Config.Indicators.ShowGear ? GearDisplay.Format(gear) : "");
        Overlay.Strip.Display(frame);
        Overlay.Strip.DisplayIndicators(indicators);
        bool animated = live != null || (demoOnline && Sweep) || frame.Flashing;
        var interval = TimeSpan.FromMilliseconds(animated ? 16 : 180);
        if (update.Interval != interval) update.Interval = interval;
        if (controls?.IsVisible == true && controls.WindowState != WindowState.Minimized)
        {
            controls.DisplayFrame(frame, indicators);
            if (seconds - lastReadout >= .12)
            {
                lastReadout = seconds;
                controls.Display(rpm, gear, demo, live,
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
