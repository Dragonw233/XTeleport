using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Teleport
{
    //	A helper struct to make working with the active flags in a waymark preset structure easier.
    [StructLayout(LayoutKind.Sequential, Pack = 0, Size = 1)]
    public struct BitField8
    {
        public byte Data;

        public bool this[int i]
        {
            get
            {
                return i switch
                {
                    0 => (Data & 1) > 0,
                    1 => (Data & 2) > 0,
                    2 => (Data & 4) > 0,
                    3 => (Data & 8) > 0,
                    4 => (Data & 16) > 0,
                    5 => (Data & 32) > 0,
                    6 => (Data & 64) > 0,
                    7 => (Data & 128) > 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(i), "Array index out of bounds.")
                };
            }
            set
            {
                Data = i switch
                {
                    0 => (byte)((value ? 1 : 0) | Data),
                    1 => (byte)((value ? 2 : 0) | Data),
                    2 => (byte)((value ? 4 : 0) | Data),
                    3 => (byte)((value ? 8 : 0) | Data),
                    4 => (byte)((value ? 16 : 0) | Data),
                    5 => (byte)((value ? 32 : 0) | Data),
                    6 => (byte)((value ? 64 : 0) | Data),
                    7 => (byte)((value ? 128 : 0) | Data),
                    _ => throw new ArgumentOutOfRangeException(nameof(i), "Array index out of bounds.")
                };
            }
        }

        public override string ToString()
        {
            return $"0x{Data:X}";
        }
    }

    public static class FieldMarkerPresetExt {
        public static string AsString(this FieldMarkerPreset preset)
        {
            return $"A: {preset.Markers[0]}\r\n" +
                   $"B: {preset.Markers[1]}\r\n" +
                   $"C: {preset.Markers[2]}\r\n" +
                   $"D: {preset.Markers[3]}\r\n" +
                   $"1: {preset.Markers[4]}\r\n" +
                   $"2: {preset.Markers[5]}\r\n" +
                   $"3: {preset.Markers[6]}\r\n" +
                   $"4: {preset.Markers[7]}\r\n" +
                   $"Active Flags: {preset.ActiveMarkers}\r\n" +
                   $"ContentFinderCondition: {preset.ContentFinderConditionId}\r\n" +
                   $"Timestamp: {preset.Timestamp}";
        }
    }
}
