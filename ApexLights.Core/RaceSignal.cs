using System;

namespace ApexLights.Core;

// IRSDK types irsdk_Flags and irsdk_CarLeftRight. Unknown flag bits are ignored.
[Flags]
public enum RaceFlag : uint
{
    None = 0, Checkered = 0x1, White = 0x2, Green = 0x4, Yellow = 0x8,
    Red = 0x10, Blue = 0x20, WavingYellow = 0x100, Caution = 0x4000,
    WavingCaution = 0x8000, Black = 0x10000, Repair = 0x100000
}

public enum SpotterState { Off = 0, Clear = 1, Left = 2, Right = 3, Both = 4, TwoLeft = 5, TwoRight = 6 }

public readonly record struct SessionKey(long Connection, int? UniqueId, int? Number, string Car);
public readonly record struct RaceSignal(int? Gear, RaceFlag Flags, SpotterState Spotter, SessionKey Session,
    bool PitLimiter = false, bool FlagsAvailable = true);
