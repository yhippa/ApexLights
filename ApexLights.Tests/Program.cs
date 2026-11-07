using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using ApexLights.Core;

namespace ApexLights.Tests;

// Run with dotnet run --project ApexLights.Tests. A failure returns a nonzero exit code.
internal static partial class Program
{
    private static readonly Thresholds Points = new(5000, 7000, 6750, 7300);
    private static int passed, failed;
    private static void Main(string[] args)
    {
        Run("Simultaneous alerts preserve gear and both sides during a shift", Priority);
        Run("Flags expire, clear, dismiss and re-arm by edge", Transitions);
        Run("All seven discrete spotter states activate and clear", Proximity);
        Run("Disconnect, new session and disabled categories reset alert state", SessionChanges);
        Run("1.0 settings migrate with indicators off and round-trip 1.1 options", Upgrade);
        Run("1.1 keeps enabled cues and adopts bounded independent cue sizes", CueSizeUpgrade);
        Run("Demo feeds real alert logic through disconnect and reconnect", DemoCycle);
        Run("Critical alerts blocked by a higher flag get display time only while still raised", PendingCritical);
        Run("Informational status settles once and clears on edge or session change", QuietNotice);
        Run("All driving scenarios reconnect, apply thresholds and clear previous inputs", ScenarioInputs);
        Run("Scenario visibility and inactive LED brightness preserve the saved profile", ScenarioSettings);
        Run("Gear formatting covers reverse, neutral, forward and unavailable", () =>
            Assert(GearDisplay.Format(-1) == "R" && GearDisplay.Format(0) == "N" && GearDisplay.Format(12) == "12"
                && GearDisplay.Format(null) == "" && GearDisplay.Format(31) == "", "gear label"));
        Run("Each LED boundary, below and at threshold, for 10–12 LEDs", Boundaries);
        Run("Shift flash cadence, reset on RPM drop and re-entry", Flash);
        Run("SDK blink point, disabled flash and RPM adjustment", Triggers);
        Run("All progression directions cover every lens once", Directions);
        Run("Partial, missing and invalid car thresholds", Fallback);
        Run("JSON round trip retains styling, negative position and per-car overrides", Persistence);
        Run("Malformed JSON and invalid persisted values recover", CorruptSettings);
        Run("SDK latest buffer selection, revision changes and car identity", Decode);
        Run("Optional SDK channels cannot interrupt RPM", OptionalChannels);
        Run("SDK reads verified flag and spotter types from the selected row", AwarenessChannels);
        Run("SDK rejects invalid metadata, channel types and values", BadMapping);
        Run("SDK waits, attaches, disconnects and attaches to a replacement mapping", Reconnect);
        if (args.Length == 2 && args[0] == "--recording")
            Run("Recorded iRacing session metadata and sampled telemetry rows", () => Recording(args[1]));
        Console.WriteLine($"{passed} passed, {failed} failed.");
        Environment.ExitCode = failed > 0 ? 1 : 0;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS  " + name); }
        catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL  {name}\n{e}"); }
    }
    private static void Assert(bool condition, string why)
    {
        if (!condition) throw new InvalidOperationException(why);
    }

    private static void Boundaries()
    {
        var engine = new ShiftLights();
        for (int n = 10; n <= 12; n++)
        {
            int Lit(double rpm) => engine.Evaluate(rpm, Points, n, 0, FlashTrigger.Off, 4, 0).Lit;
            for (int i = 0; i < n; i++)
            {
                double edge = Points.First + (Points.Last - Points.First) * i / (n - 1);
                Assert(Lit(edge) == i + 1 && Lit(edge - .001) == i, $"Segment {i} of {n}");
            }
            Assert(Lit(double.NaN) == 0 && Lit(0) == 0 && Lit(25000) == n, "Invalid/saturated RPM");
        }
    }
    private static void Flash()
    {
        var engine = new ShiftLights();
        LightFrame At(double rpm, double t) => engine.Evaluate(rpm, Points, 12, 0, FlashTrigger.Shift, 4, t);
        Assert(!At(6749, 10).Flashing, "early shift");
        Assert(At(6750, 10.01) == new LightFrame(12, true, true), "flash entrance");
        Assert(!At(6755, 10.15).BrightPhase && At(6755, 10.27).BrightPhase, "4 Hz phase");
        Assert(!At(6000, 10.29).Flashing && At(6800, 10.30).BrightPhase, "reset phase");
    }
    private static void Triggers()
    {
        var e = new ShiftLights();
        LightFrame At(double rpm, FlashTrigger f) => e.Evaluate(rpm, Points, 10, 200, f, 2, 5);
        Assert(At(5000, FlashTrigger.Off).Lit == 0 && At(5200, FlashTrigger.Off).Lit == 1, "offset");
        Assert(!At(7499, FlashTrigger.Blink).Flashing && At(7500, FlashTrigger.Blink).Flashing, "blink offset");
        Assert(At(9000, FlashTrigger.Off) == new LightFrame(10, false, true), "disabled");
    }
    private static void Directions()
    {
        for (int n = 10; n <= 12; n++)
            foreach (var direction in Enum.GetValues<Progression>())
                Assert(Enumerable.Range(0, n).Select(i => ShiftLights.Rank(i, n, direction)).Order()
                    .SequenceEqual(Enumerable.Range(0, n)), "invalid rank");
        Assert(ShiftLights.Rank(11, 12, Progression.OutsideIn) == 1, "second outside lens");
    }
    private static void Fallback()
    {
        foreach (var n in new[] { 0, -2000, double.NaN, double.PositiveInfinity, 100d, 49999.9, 50001 })
            Assert(Thresholds.Resolve(n, n, n, n, n).Valid, $"Bad fallback for {n}");
        var resolved = Thresholds.Resolve(0, 6800, 6750, 6900, 7000);
        Assert(resolved.First == 5040 && resolved.Shift == 6750 && resolved.Last == 6800, "partial recovery");
    }

    private static void InTemp(Action<string> test)
    {
        string dir = Directory.CreateTempSubdirectory("ApexLights.Test.").FullName;
        try { test(Path.Combine(dir, "settings.json")); } finally { Directory.Delete(dir, true); }
    }
    private static void Persistence() => InTemp(path =>
    {
        var store = new SettingsStore(path);
        var s = new Settings { Count = 11, X = -1600, Y = 140, Source = SourceMode.Live, Scale = 1.24,
            Shape = LensShape.Round, DarkHousing = false, StartMinimized = true, Demo = Points };
        s.Cars.Add("id-146", Points);
        s.Colors[2] = "#AA1234";
        Assert(store.Save(s), store.Warning ?? "first save");
        s.Offset = -175;
        Assert(store.Save(s), "replace");
        var read = store.Load();
        Assert(read.Count == 11 && read.X == -1600 && read.Y == 140 && read.Offset == -175 && read.Scale == 1.24 &&
            read.Shape == LensShape.Round && !read.DarkHousing && read.StartMinimized && read.Source == SourceMode.Live &&
            read.Cars["id-146"] == Points && read.Demo == Points && read.Colors[2] == "#AA1234", "round trip");
        Assert(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "temporary file leaked");
    });
    private static void CorruptSettings() => InTemp(path =>
    {
        var store = new SettingsStore(path);
        File.WriteAllText(path, "{broken");
        Assert(store.Load().Count == 12 && store.Warning != null, "invalid JSON");
        File.WriteAllText(path, """{"Count":10000,"Brightness":-4,"Scale":100,"Colors":[null,"orange"],"Cars":null,"Demo":null}""");
        var s = store.Load();
        Assert(s.Count == 12 && s.Brightness == .1 && s.Scale == 2.5 && s.Colors.Length == 4 &&
            s.Demo.Valid && s.Cars.Count == 0 && s.Colors[0] == Settings.DefaultColors()[0], "range repair");
    });

    private const int Meta = 512, Row0 = 2048, Row1 = 2064;
    private static string Yaml(int car) => $"""
        WeekendInfo:
         TrackName: example
        DriverInfo:
         DriverCarIdx: 2
         DriverCarRedLine: 7000.000
         DriverCarSLFirstRPM: 5000.000
         DriverCarSLLastRPM: 7000.000
         DriverCarSLShiftRPM: 6750.000
         DriverCarSLBlinkRPM: 7300.000
         Drivers:
         - CarIdx: 1
           CarID: 111
         - CarIdx: 2
           CarID: {car}
           CarScreenName: Test GT
        SessionInfo:
         DriverCarSLFirstRPM: 90000
        """;
    private static void Fill(MemoryMappedViewAccessor w, int car)
    {
        byte[] yaml = Encoding.ASCII.GetBytes(Yaml(car) + "\0");
        int[] header = [2, 1, 60, 1, yaml.Length, Meta, 2, 112, 2, 16, 0, 0, 9, Row0, 0, 0, 10, Row1];
        w.WriteArray(0, header, 0, header.Length);
        void Variable(int pos, int type, int offset, string name)
        {
            w.Write(pos, type); w.Write(pos + 4, offset); w.Write(pos + 8, 1);
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            w.WriteArray(pos + 16, nameBytes, 0, nameBytes.Length);
        }
        Variable(112, 2, 8, "Gear");
        Variable(256, 4, 4, "RPM");
        w.WriteArray(Meta, yaml, 0, yaml.Length);
        w.Write(Row0 + 4, 4200f); w.Write(Row0 + 8, 2);
        w.Write(Row1 + 4, 6900f); w.Write(Row1 + 8, 4);
    }
    private static void Decode()
    {
        using var map = MemoryMappedFile.CreateNew(null, 4096);
        using var w = map.CreateViewAccessor();
        using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        Fill(w, 146);
        var decoder = new SdkReader(view);
        var s = decoder.Read()!;
        Assert(s.Rpm == 6900 && s.Gear == 4 && s.Tick == 10 && s.Car.Key == "id-146" &&
            s.Car.Points == Points && s.Car.Name == "Test GT", "newest coherent slot and driver");
        Assert(decoder.Read() == null, "duplicate tick");
        Fill(w, 817); w.Write(12, 2); w.Write(48, 11);
        s = decoder.Read()!;
        Assert(s.Tick == 11 && s.Rpm == 4200 && s.Car.Key == "id-817", "car/row refresh");
    }
    private static void OptionalChannels()
    {
        using var map = MemoryMappedFile.CreateNew(null, 4096);
        using var w = map.CreateViewAccessor();
        using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        Fill(w, 146);
        w.Write(112, 4); // wrong Gear type
        var s = new SdkReader(view).Read()!;
        Assert(s.Rpm == 6900 && s.Gear == null && s.Signal.Flags == 0 &&
            s.Signal.Spotter == SpotterState.Off, "missing optional channels");
    }

    private static void AwarenessChannels()
    {
        using var map = MemoryMappedFile.CreateNew(null, 8192);
        using var w = map.CreateViewAccessor();
        using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        Fill(w, 146);
        // Copy descriptors to a larger table with room for the optional channels.
        var descriptors = new byte[288];
        w.ReadArray(112, descriptors, 0, descriptors.Length);
        w.WriteArray(4096, descriptors, 0, descriptors.Length);
        w.Write(28, 4096); w.Write(24, 4);
        void Add(int index, string name, int type, int offset)
        {
            int address = 4096 + index * 144;
            w.Write(address, type); w.Write(address + 4, offset); w.Write(address + 8, 1);
            byte[] bytes = Encoding.ASCII.GetBytes(name);
            w.WriteArray(address + 16, bytes, 0, bytes.Length);
        }
        Add(2, "SessionFlags", 3, 0); Add(3, "CarLeftRight", 2, 12);
        w.Write(Row1, 0x10020); w.Write(Row1 + 12, 5);
        var reader = new SdkReader(view);
        var s = reader.Read()!;
        Assert(s.Signal.Flags == (RaceFlag.Black | RaceFlag.Blue) && s.Signal.Spotter == SpotterState.TwoLeft, "typed fields");
        Add(2, "SessionFlags", 2, 0); w.Write(12, 2); w.Write(Row1 + 12, 127);
        s = reader.Read()!;
        Assert(s.Signal.Flags == 0 && s.Signal.Spotter == SpotterState.Off && s.Rpm == 6900, "optional channel recovery");
    }

    private static void BadMapping()
    {
        using var map = MemoryMappedFile.CreateNew(null, 4096);
        using var w = map.CreateViewAccessor();
        using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        void Reject(Action damage)
        {
            Fill(w, 146); damage();
            try { new SdkReader(view).Read(); } catch (InvalidDataException) { return; }
            throw new Exception("Accepted corrupt mapping");
        }
        Reject(() => w.Write(28, int.MaxValue));
        Reject(() => w.Write(16, -1));
        Reject(() => w.Write(32, 12));
        Reject(() => w.Write(256, 2));
        Reject(() => w.Write(68, int.MaxValue));
        Reject(() => w.Write(Row1 + 4, float.NaN));
        Reject(() => w.Write(4, 0));
    }

    private static void Reconnect()
    {
        string name = @"Local\ApexLights.Test." + Guid.NewGuid().ToString("N");
        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".ready");
        using var feed = new LiveFeed(name, name + ".ready");
        Assert(feed.Latest == null, "absent simulator");
        foreach (int id in new[] { 146, 800 })
        {
            using var map = MemoryMappedFile.CreateNew(name, 4096);
            using var writer = map.CreateViewAccessor();
            Fill(writer, id);
            int tick = 10;
            bool attached = SpinWait.SpinUntil(() =>
            {
                writer.Write(64, ++tick); eventHandle.Set();
                Thread.Sleep(18);
                return feed.Latest?.Car.Key == $"id-{id}";
            }, 4500);
            Assert(attached, $"did not connect to {id}");
            writer.Write(4, 0); eventHandle.Set();
            Assert(SpinWait.SpinUntil(() => feed.Latest == null, 1800), "stale connected sample");
        }
    }

    private static void Recording(string path)
    {
        using var file = File.OpenRead(path);
        using var recording = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read,
            HandleInheritability.None, true);
        using var input = recording.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        int row = input.ReadInt32(52), size = input.ReadInt32(36);
        byte[] metadata = new byte[row], data = new byte[size];
        input.ReadArray(0, metadata, 0, row);
        using var fixture = MemoryMappedFile.CreateNew(null, row + size);
        using var writer = fixture.CreateViewAccessor();
        using var view = fixture.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        writer.WriteArray(0, metadata, 0, row); writer.Write(32, 1); writer.Write(4, 1);
        var decoder = new SdkReader(view);
        int total = (int)((file.Length - row) / size), sampled = 0;
        float peak = 0;
        for (int index = 0; index < total; index += Math.Max(1, total / 120))
        {
            input.ReadArray((long)row + (long)index * size, data, 0, size);
            writer.WriteArray(row, data, 0, size); writer.Write(48, index + 1);
            var sample = decoder.Read()!;
            Assert(sample.Car.Key != "" && sample.Car.Points.Valid, "SDK car identity or thresholds");
            peak = Math.Max(peak, sample.Rpm); sampled++;
        }
        Assert(sampled > 1 && peak > 0, "recording contains no running engine samples");
        Console.WriteLine($"      Decoded {sampled} real telemetry rows; peak sampled RPM {peak:N0}.");
    }
}
