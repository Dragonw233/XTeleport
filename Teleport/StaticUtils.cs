using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Text.ReadOnly;

namespace Teleport
{
    internal static class StaticUtils
    {
        #region 基础访问

        internal static IPlayerCharacter? LocalPlayer => Svc.Objects.LocalPlayer;
        internal static IGameObject? CurrentTarget => Svc.Targets.Target;
        internal static ulong LocalContentId => Svc.PlayerState.ContentId;

        #endregion

        #region 指针 / sig 扫描

        // 地面移动用：直接拿到 setPosition 函数地址。
        private static Lazy<IntPtr> GetSetPosFunPtr = new(() =>
            Svc.SigScanner.TryScanText(
                "40 53 48 83 EC 20 F3 0F 11 89 B0 00 00 00 48 8B D9 F3 0F 11 91 B4 00 00 00 F3 0F 11 99 B8 00 00 00",
                out var addr)
                ? addr
                : IntPtr.Zero);

        // 飞行 / 潜水用：定位坐标结构体，写入坐标即可在该状态下平移。
        internal static Lazy<IntPtr> GetPosPtr4FlyDive = new(ScanFlyDivePtr);

        private static IntPtr ScanFlyDivePtr()
        {
            if (!Svc.SigScanner.TryScanText("4C 8D 35 ?? ?? ?? ?? 48 8B 09 48 8B F2 BF 01 00 00 00", out var ptr))
                return IntPtr.Zero;

            var nextInstr = ptr + 7;                  // 下一条指令地址
            var offset = Marshal.ReadInt32(ptr + 3);  // lea 的相对偏移
            var instance = nextInstr + offset;
            return instance + 0x5520 + 0x150;
        }

        private static IntPtr SetPosFunPtr => GetSetPosFunPtr.Value;
        internal static IntPtr PosPtr4FlyDive => GetPosPtr4FlyDive.Value;

        internal static void RefreshPtr() =>
            PluginLog.Log($"获取ptr：地面移动：{GetSetPosFunPtr.Value}|飞行潜水：{GetPosPtr4FlyDive.Value}");

        internal static void RefreshFlyDive() => GetPosPtr4FlyDive = new Lazy<IntPtr>(ScanFlyDivePtr);

        private delegate long SetPositionDelegate(long playerAddress, float x, float y, float z);

        private static readonly SetPositionDelegate setPosition =
            Marshal.GetDelegateForFunctionPointer<SetPositionDelegate>(SetPosFunPtr);

        #endregion

        #region 飞行 / 潜水：写内存平移

        // 在飞行 / 潜水状态下，把坐标写进对应结构体即可平移；该状态下客户端会持续上报坐标，
        // 因此服务器不会与本地坐标失配——这是做超远距离 TP 的关键。
        private static void TP4FlyDive(float x, float y, float z)
        {
            var addr = PosPtr4FlyDive;
            if (addr == IntPtr.Zero)
            {
                Svc.Chat.PrintError("用于飞行/潜水的sig扫描失败，无法进行传送！");
                return;
            }

            SafeMemory.Write(addr + 16, x);
            SafeMemory.Write(addr + 20, y);
            SafeMemory.Write(addr + 24, z);
        }

        #endregion

        #region 潜水发包 TP

        // 抓包模板（坐标在 0x34/0x38/0x3C 注入）：
        //   0x00: 0F 01 ...  外层 opcode 0x010F
        //   0x08: 30 ...     固定 0x30 (48)
        //   0x10: 00 ...
        //   0x20: 5F 02 ...  内层 opcode 0x025F
        //   0x40/0x48:       抓包为进程内指针，属内部字段/非线路数据，保持 0
        internal static byte[] BuildDiveTpPacket(float x, float y, float z)
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

        internal static void TeleportMeByDivePacket(Vector3 pos) => TeleportMeByDivePacket(pos.X, pos.Y, pos.Z);

        internal static void TeleportMeByDivePacket(float x, float y, float z)
        {
            if (LocalPlayer == null)
            {
                PluginLog.Debug("[潜水TP] LocalPlayer 为空，已跳过");
                return;
            }

            if (!Plugin.GetActivationPro())
            {
                Svc.Chat.PrintError("潜水发包TP需要先通过码2验证。");
                return;
            }

            var packet = BuildDiveTpPacket(x, y, z);
            if (!TrySendDiveTpPacket(packet))
                Svc.Chat.PrintError("潜水发包TP发送失败。");
        }

        private static unsafe bool TrySendDiveTpPacket(byte[] packet)
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

            // NetworkModule 是有类型指针，"+ 2672" 会按 sizeof(NetworkModule) 缩放并被整除截断为 0，
            // 必须先转 byte* 再加字节偏移，否则拿到错误指针 → 发包崩溃/掉线。
            var zoneClient = *(ZoneClient**)((byte*)proxy->NetworkModule + 2672);
            PluginLog.Debug($"[潜水TP] NetworkModule=0x{(nint)proxy->NetworkModule:X} ZoneClient=0x{(nint)zoneClient:X}");
            if (zoneClient == null)
            {
                PluginLog.Debug("[潜水TP] ZoneClient 为空（2672 偏移可能不对）");
                return false;
            }

            fixed (byte* packetPtr = packet)
            {
                PluginLog.Debug($"[潜水TP] SendPacket payload: {BitConverter.ToString(packet).Replace("-", " ")}");
                var ok = zoneClient->SendPacket((nint)packetPtr, 0U, 0U, false);
                PluginLog.Debug($"[潜水TP] SendPacket 返回 {ok}");
                return ok;
            }
        }

        #endregion

        #region 传送入口

        private static void SetPos(IntPtr address, float x, float y, float z)
        {
            var territory = Svc.ClientState.TerritoryType;
            if (territory is 1165 or 1197)
            {
                Svc.Chat.PrintError("收到用户反馈，在该区域TP有较高的被封可能性，请您爱惜账号，尽量避免使用！");
                return;
            }

            if (!Plugin.GetActivation())
            {
                Svc.Chat.PrintError("请先激活，/tpconfig打开激活界面");
                return;
            }

            XCountResults.RefreshPlayerCount();
            if (Plugin.Configuration.XCountThreshold > 0 && !XCountResults.AllowTP())
            {
                Svc.Chat.PrintError(
                    $"当前周围人数{XCountResults.CountsDict["<all>"]}多于你设定的阈值{Plugin.Configuration.XCountThreshold}，不会传送");
                return;
            }

            // 传送的是自己且正处于飞行 / 潜水状态时，走写内存平移以避免坐标失配。
            if (LocalPlayer != null && address == LocalPlayer.Address &&
                (Svc.Condition[ConditionFlag.InFlight] || Svc.Condition[ConditionFlag.Diving]))
            {
                Svc.Chat.Print("当前状态为飞行/潜水，使用飞行/潜水传送");
                TP4FlyDive(x, y, z);
                return;
            }

            setPosition((long)address, x, y, z);
        }

        internal static void TeleportMe(Vector3 pos) => TeleportMe(pos.X, pos.Y, pos.Z);

        internal static void TeleportMe(float x, float y, float z)
        {
            var player = LocalPlayer;
            if (player == null) return;
            SetPos(player.Address, x, y, z);
        }

        internal static void TeleportTarget(float x, float y, float z)
        {
            var target = CurrentTarget;
            if (target == null) return;
            SetPos(target.Address, x, y, z);
        }

        internal static void TeleportTargetToMe()
        {
            var player = LocalPlayer;
            if (player == null) return;
            var target = CurrentTarget;
            if (target == null) return;
            SetPos(target.Address, player.Position.X, player.Position.Y, player.Position.Z);
        }

        internal static void TeleportToPartyMember(int index)
        {
            var member = GetValidPartyMember(index);
            if (member == null) return;
            var player = LocalPlayer;
            if (player == null) return;
            SetPos(player.Address, member.Position.X, member.Position.Y, member.Position.Z);
        }

        internal static IPlayerCharacter? GetValidPartyMember(int index)
        {
            index--;
            if (index > Svc.Party.Length)
            {
                Svc.Chat.PrintError("小队成员超过长度限制");
                return null;
            }

            var address = Svc.Party.GetPartyMemberAddress(index);
            IPartyMember? member = Svc.Party.CreatePartyMemberReference(address);
            if (member == null)
            {
                Svc.Chat.PrintError("该成员可能不在你身边");
                return null;
            }

            // 注意：此处沿用原有行为返回 LocalPlayer。若小队传送实际无效，
            // 需改为返回该成员对应的角色对象（member.Position）。
            return LocalPlayer;
        }

        internal static Vector3 MousePos()
        {
            var io = ImGui.GetIO();
            Svc.GameGui.ScreenToWorld(io.MousePos, out var world, 100000f);
            return world;
        }

        internal static void MoveToMousePos()
        {
            var player = LocalPlayer;
            if (player == null) return;
            var pos = MousePos();
            SetPos(player.Address, pos.X, pos.Y, pos.Z);
        }

        internal static void TP2WayMark(string mark)
        {
            var player = LocalPlayer;
            if (player == null) return;

            var pos = default(Vector3);
            if (!TryGetWayMarkPos(mark, ref pos))
            {
                Svc.Chat.PrintError("传送标点格式错误或不存在！");
                return;
            }

            SetPos(player.Address, pos.X, pos.Y, pos.Z);
        }

        internal static unsafe void SetPos2XX(float x, float y, float z)
        {
            var player = LocalPlayer;
            if (player == null) return;
            var obj = (GameObject*)player.Address;
            obj->Position.X = x;
            obj->Position.Y = y;
            obj->Position.Z = z;
        }

        internal static bool IsSameMapId(uint id)
        {
            if (LocalPlayer == null) return false;

            if (Svc.ClientState.TerritoryType != id)
            {
                Svc.Chat.PrintError($"当前地图id({Svc.ClientState.TerritoryType})与输入id({id})不符！");
                return false;
            }

            return true;
        }

        #endregion

        #region 场地标点

        internal static bool TryGetWayMarkPos(string waymark, ref Vector3 pos)
        {
            var preset = default(FieldMarkerPreset);
            if (!MemoryHandler.GetCurrentWaymarksAsPresetData(ref preset))
                return false;

            var idx = waymark switch
            {
                "A" or "a" => 0,
                "B" or "b" => 1,
                "C" or "c" => 2,
                "D" or "d" => 3,
                "1" => 4,
                "2" => 5,
                "3" => 6,
                "4" => 7,
                _ => -1
            };

            if (idx < 0 || !preset.IsMarkerActive(idx))
                return false;

            var marker = preset.Markers[idx];
            pos = new Vector3(marker.X / 1000f, marker.Y / 1000f, marker.Z / 1000f);
            return true;
        }

        #endregion

        #region 角色属性 / 朝向 / 速度

        internal static void SetSpeed(float speed) => Plugin.Speed = speed;

        private static unsafe PlayerState* ModuleSaved;

        internal static unsafe PlayerState* Module
        {
            get
            {
                if (ModuleSaved == null) ModuleSaved = PlayerState.Instance();
                return ModuleSaved;
            }
        }

        internal static unsafe ushort CurrentSkillSpeed
        {
            get => (ushort)PlayerState.Instance()->Attributes[45];
            set => PlayerState.Instance()->Attributes[45] = value;
        }

        internal static unsafe ushort CurrentSpellSpeed
        {
            get => (ushort)PlayerState.Instance()->Attributes[46];
            set => PlayerState.Instance()->Attributes[46] = value;
        }

        internal static unsafe ushort GetCurrentValue(Attributes statusType) =>
            (ushort)PlayerState.Instance()->Attributes[(int)statusType];

        internal static unsafe void SetCurrentValue(Attributes statusType, ushort value) =>
            PlayerState.Instance()->Attributes[(int)statusType] = value;

        internal static float Rotation
        {
            get => LocalPlayer?.Rotation ?? 0f;
            set
            {
                if (LocalPlayer == null) return;
                Marshal.StructureToPtr(value, LocalPlayer.Address + 192, true);
            }
        }

        internal static float ConvertDegreesToRadians(float degrees) => (float)(degrees * Math.PI / 180.0);

        #endregion

        #region 杂项

        public static string ToStr(ReadOnlySeString content) => content.ToDalamudString().ToString();

        public static string TranslateAttribute(Attributes attribute) => attribute switch
        {
            Attributes.Strength => "力量",
            Attributes.Dexterity => "灵巧",
            Attributes.Vitality => "耐力",
            Attributes.Intelligence => "智力",
            Attributes.Mind => "精神",
            Attributes.Piety => "信仰",
            Attributes.MaxHp => "最大生命值",
            Attributes.MaxMp => "最大魔法值",
            Attributes.MaxGp => "最大工艺点",
            Attributes.MaxCp => "最大制作点",
            Attributes.Tenacity => "坚韧",
            Attributes.AttackPower => "攻击力",
            Attributes.Defense => "防御力",
            Attributes.DirectHit => "直击",
            Attributes.MagicDefense => "魔法防御力",
            Attributes.CriticalHit => "暴击",
            Attributes.AttackMagicPotency => "攻击魔法性能",
            Attributes.HealingMagicPotency => "治疗魔法性能",
            Attributes.Determination => "决心",
            Attributes.SkillSpeed => "技能速度",
            Attributes.SpellSpeed => "咒语速度",
            Attributes.Craftsmanship => "工艺性能",
            Attributes.Control => "控制性能",
            Attributes.Gathering => "采集力",
            Attributes.Perception => "感知",
            _ => "未知"
        };

        #endregion
    }
}
