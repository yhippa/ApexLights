using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace ApexLights.Core;

public sealed record Sample(float Rpm, int Gear, int Tick, CarSession Car);

/// <summary>Decoder of the IRSDK v2 live mapped view. The caller owns the view.</summary>
public sealed class SdkReader(MemoryMappedViewAccessor view)
{
    // irsdk_defines.h: 12 header ints, four 16-byte varBuf slots; each varHeader is 144 bytes.
    private const int FixedHeader = 112, SlotBase = 48, SlotSize = 16, VariableSize = 144;
    private int? knownRevision;
    private int variables, table, rowSize, rpmOffset = -1, gearOffset = -1;
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
        int gear = Int(checked(row + gearOffset));
        Thread.MemoryBarrier();
        if (Int(best) != tick || Int(12) != revision || (Int(4) & 1) == 0) return null;
        if (!float.IsFinite(rpm) || rpm is < 0 or > 100000 || gear is < -1 or > 30)
            throw new InvalidDataException("Invalid RPM or gear.");
        lastTick = tick;
        return new(rpm, gear, tick, session!);
    }

    private void LoadVariables(int count, int start, int size)
    {
        Range(start, (long)count * VariableSize);
        int rpm = -1, gear = -1;
        var nameBytes = new byte[32];
        for (int i = 0; i < count; i++)
        {
            long entry = (long)start + (long)i * VariableSize;
            view.ReadArray(entry + 16, nameBytes, 0, nameBytes.Length);
            int end = Array.IndexOf(nameBytes, (byte)0);
            string name = Encoding.ASCII.GetString(nameBytes, 0, end < 0 ? 32 : end);
            if (name != "RPM" && name != "Gear") continue;
            int type = view.ReadInt32(entry), offset = view.ReadInt32(entry + 4);
            if (view.ReadInt32(entry + 8) != 1 || offset < 0 || offset > size - 4) continue;
            if (name == "RPM" && type == 4) rpm = offset;
            if (name == "Gear" && type == 2) gear = offset;
        }
        if (rpm < 0 || gear < 0) throw new InvalidDataException("Required SDK variables missing.");
        (variables, table, rowSize, rpmOffset, gearOffset) = (count, start, size, rpm, gear);
    }
}
