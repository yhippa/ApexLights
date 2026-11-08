using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace ApexLights.Core;

public sealed record Sample(float Rpm, int Tick, CarSession Car, RaceSignal Signal)
{
    public int? Gear => Signal.Gear;
}

/// <summary>Decoder of the IRSDK v2 live mapped view. The caller owns the view.</summary>
public sealed class SdkReader(MemoryMappedViewAccessor view)
{
    // irsdk_defines.h: 12 header ints, four 16-byte varBuf slots; each varHeader is 144 bytes.
    private const int FixedHeader = 112, SlotBase = 48, SlotSize = 16, VariableSize = 144;
    private int? knownRevision;
    private int variables, table, rowSize, rpmOffset = -1, gearOffset = -1;
    private static long nextConnection;
    private long connection = Interlocked.Increment(ref nextConnection);
    private int flagsOffset, spotterOffset, uniqueOffset, numberOffset, engineWarningsOffset;
    private const int PitSpeedLimiter = 0x0010; // irsdk_EngineWarnings, distinct from rev limiter (0x0020).
    private CarSession? session;
    private int? lastTick;

    private void Range(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset > view.Capacity - length)
            throw new InvalidDataException("SDK range outside mapping.");
    }

    private int Int(int position) => view.ReadInt32(position);

    public Sample? Read()
    {
        Range(0, FixedHeader);
        if (Int(0) != 2 || (Int(4) & 1) == 0) throw new InvalidDataException("SDK disconnected or unsupported.");
        int revision = Int(12), count = Int(24), start = Int(28), slots = Int(32), size = Int(36);
        if (count is < 1 or > 16384 || size is < 4 or > 16_777_216 || slots is < 1 or > 4)
            throw new InvalidDataException("Invalid SDK header.");
        if (knownRevision != revision || variables != count || table != start || rowSize != size)
        {
            LoadVariables(count, start, size);
            int length = Int(16), offset = Int(20);
            if (length is < 0 or > 8_388_608) throw new InvalidDataException("Invalid session length.");
            Range(offset, length);
            var bytes = new byte[length];
            view.ReadArray(offset, bytes, 0, length);
            var next = CarSession.Parse(Encoding.Latin1.GetString(bytes));
            Thread.MemoryBarrier();
            if (Int(12) != revision) return null;
            session = next;
            knownRevision = revision;
            lastTick = null;
        }
        int best = SlotBase;
        int tick = Int(best);
        for (int i = 1; i < slots; i++)
        {
            int slot = SlotBase + i * SlotSize, candidate = Int(slot);
            if (candidate > tick) { best = slot; tick = candidate; }
        }
        if (tick == lastTick) return null;
        int row = Int(best + 4);
        Range(row, size);
        Thread.MemoryBarrier();
        float rpm = view.ReadSingle((long)row + rpmOffset);
        int? Channel(int offset) => offset < 0 ? null : view.ReadInt32((long)row + offset);
        int? gear = Channel(gearOffset);
        var flags = (RaceFlag)(uint)(Channel(flagsOffset) ?? 0);
        bool limiter = ((Channel(engineWarningsOffset) ?? 0) & PitSpeedLimiter) != 0;
        int side = Channel(spotterOffset) ?? 0;
        var spotter = side is >= 0 and <= 6 ? (SpotterState)side : SpotterState.Off;
        int? uniqueId = Channel(uniqueOffset), sessionNum = Channel(numberOffset);
        Thread.MemoryBarrier();
        if (Int(best) != tick || Int(12) != revision || (Int(4) & 1) == 0) return null;
        if (!float.IsFinite(rpm) || rpm is < 0 or > 100000)
            throw new InvalidDataException("Invalid RPM.");
        if (lastTick.HasValue && tick < lastTick.Value) connection = Interlocked.Increment(ref nextConnection);
        var key = new SessionKey(connection, uniqueId, sessionNum, session!.Key);
        lastTick = tick;
        return new(rpm, tick, session, new(gear is >= -1 and <= 30 ? gear : null, flags, spotter, key,
            limiter, flagsOffset >= 0));
    }

    private void LoadVariables(int count, int start, int size)
    {
        Range(start, (long)count * VariableSize);
        rpmOffset = gearOffset = flagsOffset = spotterOffset = uniqueOffset = numberOffset = engineWarningsOffset = -1;
        var nameBytes = new byte[32];
        for (int i = 0; i < count; i++)
        {
            long entry = (long)start + (long)i * VariableSize;
            view.ReadArray(entry + 16, nameBytes, 0, nameBytes.Length);
            int end = Array.IndexOf(nameBytes, (byte)0);
            string name = Encoding.ASCII.GetString(nameBytes, 0, end < 0 ? 32 : end);
            int type = view.ReadInt32(entry), offset = view.ReadInt32(entry + 4);
            if (view.ReadInt32(entry + 8) != 1 || offset < 0 || offset > size - 4) continue;
            switch (name, type)
            {
                case ("RPM", 4): rpmOffset = offset; break;
                case ("Gear", 2): gearOffset = offset; break;
                case ("SessionFlags", 3): flagsOffset = offset; break;
                case ("EngineWarnings", 3): engineWarningsOffset = offset; break;
                case ("CarLeftRight", 2): spotterOffset = offset; break;
                case ("SessionUniqueID", 2): uniqueOffset = offset; break;
                case ("SessionNum", 2): numberOffset = offset; break;
            }
        }
        if (rpmOffset < 0) throw new InvalidDataException("Required RPM channel missing.");
        (variables, table, rowSize) = (count, start, size);
    }
}
