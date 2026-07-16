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
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Text.ReadOnly;
using System.Collections.Generic;

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
        private static Lazy<IntPtr> GetSetPosFunPtr = new(TeleportProtected.ScanSetPositionPointer);

        // 飞行 / 潜水用：定位坐标结构体，写入坐标即可在该状态下平移。
        internal static Lazy<IntPtr> GetPosPtr4FlyDive = new(TeleportProtected.ScanFlyDivePointer);

        private static IntPtr SetPosFunPtr => GetSetPosFunPtr.Value;
        internal static IntPtr PosPtr4FlyDive => GetPosPtr4FlyDive.Value;

        internal static void RefreshPtr() =>
            PluginLog.Log($"获取ptr：地面移动：{GetSetPosFunPtr.Value}|飞行潜水：{GetPosPtr4FlyDive.Value}");

        internal static void RefreshFlyDive() => GetPosPtr4FlyDive = new Lazy<IntPtr>(TeleportProtected.ScanFlyDivePointer);

        private static readonly TeleportProtected.SetPositionDelegate setPosition =
            TeleportProtected.CreateSetPositionDelegate();

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
            var rotation = LocalPlayer?.Rotation ?? 0f;
            return TeleportProtected.CreateDiveTpPacket(
                x,
                y,
                z,
                rotation,
                Svc.Condition[ConditionFlag.Mounted]);
        }

        internal static byte[] BuildDiveStartPacket(float x, float y, float z)
        {
            return TeleportProtected.CreateDiveStartPacket(x, y, z, LocalPlayer?.Rotation ?? 0f);
        }

        internal static void TeleportMeByDivePacket(Vector3 pos) => TeleportMeByDivePacket(pos.X, pos.Y, pos.Z);

        internal static void TeleportMeByDivePacket(float x, float y, float z)
        {
            if (LocalPlayer == null)
            {
                PluginLog.Debug("[潜水TP] LocalPlayer 为空，已跳过");
                return;
            }

            if (!CanTeleportInCurrentState(requireProActivation: true))
                return;

            var packet = Svc.Condition[ConditionFlag.Diving]
                ? BuildDiveStartPacket(x, y, z)
                : BuildDiveTpPacket(x, y, z);
            if (!TeleportProtected.TrySendDiveTpPacket(packet))
                Svc.Chat.PrintError("潜水发包TP发送失败。");
        }

        #endregion

        #region 传送入口

        private static bool CanTeleportInCurrentState(bool requireProActivation)
        {
            var territory = Svc.ClientState.TerritoryType;
            if (territory is 1165 or 1197)
            {
                Svc.Chat.PrintError("收到用户反馈，在该区域TP有较高的被封可能性，请您爱惜账号，尽量避免使用！");
                return false;
            }

            if (requireProActivation)
            {
                if (!Plugin.GetActivationPro())
                {
                    Svc.Chat.PrintError("潜水发包TP需要先通过码2验证。");
                    return false;
                }
            }
            else if (!Plugin.GetActivation())
            {
                Svc.Chat.PrintError("请先激活，/tpconfig打开激活界面");
                return false;
            }

            XCountResults.RefreshPlayerCount();
            if (Plugin.Configuration.XCountThreshold > 0 && !XCountResults.AllowTP())
            {
                Svc.Chat.PrintError(
                    $"当前周边有 {XCountResults.EffectiveNearbyPlayerCount} 个绿玩，超过你设定的阈值 {Plugin.Configuration.XCountThreshold}，不会传送");
                return false;
            }

            return true;
        }

        private static void SetPos(IntPtr address, float x, float y, float z)
        {
            if (!CanTeleportInCurrentState(requireProActivation: false))
                return;

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

        internal static bool CanUsePartyTeleport() => LocalPlayer != null && Plugin.GetActivationPro();

        internal static void TeleportMe(Vector3 pos) => TeleportMe(pos.X, pos.Y, pos.Z);

        internal static void TeleportSmartInZone(Vector3 pos)
        {
            var player = LocalPlayer;
            if (player == null)
                return;

            if (Plugin.Configuration.UseDivePacketTpInQuickWindow)
            {
                if (!Plugin.GetActivationPro())
                {
                    Plugin.Configuration.UseDivePacketTpInQuickWindow = false;
                    Plugin.Configuration.Save();
                }
                else if (DiveTpTerritoryHelper.ShouldUseDivePacket(player.Position, pos))
                {
                    TeleportMeByDivePacket(pos);
                    return;
                }
            }

            TeleportMe(pos);
        }

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

            TeleportMeByDivePacket(member.Position);
        }

        internal static void TeleportToPartyMember(IPartyMember member)
        {
            TeleportMeByDivePacket(member.Position);
        }

        internal static IPartyMember? GetValidPartyMember(int index)
        {
            index--;
            if (index < 0 || index >= Svc.Party.Length)
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

            return member;
        }

        internal static IPartyMember? GetPartyMemberByName(string name, int occurrence = 0)
        {
            if (string.IsNullOrWhiteSpace(name) || occurrence < 0)
                return null;

            var currentOccurrence = 0;
            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var address = Svc.Party.GetPartyMemberAddress(i);
                var member = Svc.Party.CreatePartyMemberReference(address);
                if (member == null)
                    continue;

                if (!string.Equals(member.Name.TextValue, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (currentOccurrence == occurrence)
                    return member;

                currentOccurrence++;
            }

            return null;
        }

        internal static IPartyMember? GetPartyMemberByContentId(ulong contentId)
        {
            if (contentId == 0)
                return null;

            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var address = Svc.Party.GetPartyMemberAddress(i);
                var member = Svc.Party.CreatePartyMemberReference(address);
                if (member == null)
                    continue;

                if (member.ContentId == contentId)
                    return member;
            }

            return null;
        }

        internal static IPartyMember? GetPartyMemberByEntityId(uint entityId)
        {
            if (entityId == 0)
                return null;

            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var address = Svc.Party.GetPartyMemberAddress(i);
                var member = Svc.Party.CreatePartyMemberReference(address);
                if (member == null)
                    continue;

                if (member.EntityId == entityId || member.ObjectId == entityId)
                    return member;
            }

            return null;
        }

        internal static unsafe List<(int UiIndex, IPartyMember Member)> GetPartyMembersByVisualOrder()
        {
            var orderedMembers = new List<(int UiIndex, IPartyMember Member)>(Svc.Party.Length);
            var seenContentIds = new HashSet<ulong>();
            var hud = AgentHUD.Instance();

            if (hud != null)
            {
                var hudMembers = hud->PartyMembers;
                var count = Math.Min((int)hud->PartyMemberCount, hudMembers.Length);

                for (var i = 0; i < count; i++)
                {
                    var hudMember = hudMembers[i];
                    var member = GetPartyMemberByContentId(hudMember.ContentId) ??
                                 GetPartyMemberByEntityId(hudMember.EntityId);

                    if (member == null)
                        continue;

                    if (member.ContentId != 0 && !seenContentIds.Add(member.ContentId))
                        continue;

                    orderedMembers.Add((hudMember.Index, member));
                }
            }

            if (orderedMembers.Count > 0)
            {
                orderedMembers.Sort(static (left, right) => left.UiIndex.CompareTo(right.UiIndex));
                return orderedMembers;
            }

            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var address = Svc.Party.GetPartyMemberAddress(i);
                var member = Svc.Party.CreatePartyMemberReference(address);
                if (member == null)
                    continue;

                orderedMembers.Add((i, member));
            }

            return orderedMembers;
        }

        internal static IEnumerable<string> GetPartyMemberPositionSummaries()
        {
            for (var i = 0; i < Svc.Party.Length; i++)
            {
                var address = Svc.Party.GetPartyMemberAddress(i);
                var member = Svc.Party.CreatePartyMemberReference(address);
                if (member == null)
                    continue;

                yield return
                    $"{i + 1}. {member.Name.TextValue}: {Math.Round(member.Position.X, 2)}, {Math.Round(member.Position.Y, 2)}, {Math.Round(member.Position.Z, 2)}";
            }
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
