using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

[assembly: Obfuscation(Feature = @"rule: (-renaming,-stringencryption,-controlflow)")]
[assembly: Obfuscation(Feature = @"rule: (+stringencryption,+controlflow) (namespace=Teleport; types=TeleportProtected)")]

namespace Teleport
{
    // Keep the sensitive signatures and packet details isolated so Release-Pro can protect only this class.
    [Obfuscation(Exclude = false, ApplyToMembers = true, Feature = "stringencryption,controlflow")]
    internal static class TeleportProtected
    {
        private const string SetPosSignature =
            "40 53 48 83 EC 20 F3 0F 11 89 B0 00 00 00 48 8B D9 F3 0F 11 91 B4 00 00 00 F3 0F 11 99 B8 00 00 00";
        private const string FlyDiveSignature = "4C 8D 35 ?? ?? ?? ?? 48 8B 09 48 8B F2 BF 01 00 00 00";

        internal static IntPtr ScanSetPositionPointer()
        {
            return Svc.SigScanner.TryScanText(SetPosSignature, out var addr) ? addr : IntPtr.Zero;
        }

        [Obfuscation(Exclude = false, Feature = "stringencryption,controlflow")]
        internal static IntPtr ScanFlyDivePointer()
        {
            if (!Svc.SigScanner.TryScanText(FlyDiveSignature, out var ptr))
                return IntPtr.Zero;

            var nextInstr = ptr + 7;
            var offset = Marshal.ReadInt32(ptr + 3);
            var instance = nextInstr + offset;
            return instance + 0x5520 + 0x150;
        }

        [Obfuscation(Exclude = false, Feature = "stringencryption,controlflow")]
        internal static byte[] CreateDiveTpPacket(float x, float y, float z)
        {
            byte[] packet =
            {
                15, 1, 0, 0, 0, 0, 0, 0,
                48, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                95, 2, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0
            };
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, packet, 52, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, packet, 56, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, packet, 60, 4);
            return packet;
        }

        [Obfuscation(Exclude = false, Feature = "stringencryption,controlflow")]
        internal static unsafe bool TrySendDiveTpPacket(byte[] packet)
        {
            if (packet.Length == 0) return false;

            var framework = Framework.Instance();
            if (framework == null)
            {
                PluginLog.Debug("[潜水TP] framework 为空");
                return false;
            }

            var proxy = framework->NetworkModuleProxy;
            if (proxy == null)
            {
                PluginLog.Debug("[潜水TP] NetworkModuleProxy 为空");
                return false;
            }

            if (proxy->NetworkModule == null)
            {
                PluginLog.Debug("[潜水TP] NetworkModule 为空");
                return false;
            }

            var zoneClient = *(ZoneClient**)((byte*)proxy->NetworkModule + 2672);
            PluginLog.Debug($"[潜水TP] NetworkModule=0x{(nint)proxy->NetworkModule:X} ZoneClient=0x{(nint)zoneClient:X}");
            if (zoneClient == null)
            {
                PluginLog.Debug("[潜水TP] ZoneClient 为空（2672 偏移可能不对）");
                return false;
            }

            fixed (byte* packetPtr = packet)
            {
                var ok = zoneClient->SendPacket((nint)packetPtr, 0U, 0U, false);
                PluginLog.Debug($"[潜水TP] SendPacket 返回 {ok}，长度={packet.Length}");
                return ok;
            }
        }

        internal delegate long SetPositionDelegate(long playerAddress, float x, float y, float z);

        [Obfuscation(Exclude = false, Feature = "stringencryption,controlflow")]
        internal static SetPositionDelegate CreateSetPositionDelegate()
        {
            var funPtr = ScanSetPositionPointer();
            return Marshal.GetDelegateForFunctionPointer<SetPositionDelegate>(funPtr);
        }
    }
}
