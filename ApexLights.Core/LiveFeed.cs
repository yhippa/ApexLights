using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ApexLights.Core;

public sealed class LiveFeed : IDisposable
{
    public const string Mapping = @"Local\IRSDKMemMapFileName";
    private const string DataEvent = @"Local\IRSDKDataValidEvent";
    private readonly ManualResetEvent stop = new(false);
    private readonly Thread worker;
    private readonly string mapping, eventName;
    private Sample? latest;
    public Sample? Latest => Volatile.Read(ref latest);

    public LiveFeed(string mapping = Mapping, string eventName = DataEvent)
    {
        this.mapping = mapping;
        this.eventName = eventName;
        worker = new Thread(Loop) { IsBackground = true, Name = "iRacing SDK" };
        worker.Start();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern SafeWaitHandle OpenEvent(uint access, bool inherit, string name);

    private void Loop()
    {
        while (!stop.WaitOne(0))
        {
            try
            {
                using var map = MemoryMappedFile.OpenExisting(mapping, MemoryMappedFileRights.Read);
                using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                using var handle = OpenEvent(0x00100000, false, eventName); // SYNCHRONIZE only
                using var ready = new EventWaitHandle(false, EventResetMode.AutoReset);
                bool signaled = !handle.IsInvalid;
                if (signaled) ready.SafeWaitHandle = handle;
                WaitHandle[] waits = signaled ? [stop, ready] : [stop];
                var reader = new SdkReader(view);
                // A stopped producer may leave a readable mapping behind. Require a new tick
                // after attachment before presenting its data as live.
                reader.Read();
                long lastData = Stopwatch.GetTimestamp();
                do
                {
                    if (reader.Read() is { } sample)
                    {
                        Volatile.Write(ref latest, sample);
                        lastData = Stopwatch.GetTimestamp();
                    }
                    if (Stopwatch.GetElapsedTime(lastData).TotalSeconds >= 1.5) break;
                } while (WaitHandle.WaitAny(waits, signaled ? 250 : 16) != 0);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or OverflowException or WaitHandleCannotBeOpenedException)
            {
                // During session transitions the map can disappear or contain an incomplete header.
            }
            Volatile.Write(ref latest, null);
            if (stop.WaitOne(1000)) break;
        }
    }

    public void Dispose()
    {
        stop.Set();
        worker.Join();
        stop.Dispose();
    }
}
