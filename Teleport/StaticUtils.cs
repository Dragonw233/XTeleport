using Dalamud;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Text.ReadOnly;
using GameObjectPtr = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Teleport
{
    internal static class StaticUtils
    {
        internal static IPlayerCharacter? LocalPlayer => Svc.Objects.LocalPlayer;
        internal static IGameObject? CurrentTarget => Svc.Targets.Target;
        internal static ulong LocalContentId => Svc.PlayerState.ContentId;

        #region PtrManage

        internal static void RefreshPtr() => PluginLog.Log(
                $"获取ptr：地面移动：{GetSetPosFunPtr.Value}|飞行潜水：{GetPosPtr4FlyDive.Value}");

        private static Lazy<IntPtr> GetSetPosFunPtr = new(() =>
        {
            return Svc.SigScanner.TryScanText("E8 ?? ?? ?? ?? 44 89 A3 ?? ?? ?? ?? 66 C7 83", out var num)
                       ? num
                       : IntPtr.Zero;
        });


        internal static Lazy<IntPtr> GetPosPtr4FlyDive = new(() =>
        {
            IntPtr num;
            if (Svc.SigScanner.TryScanText("4C ?? ?? ?? ?? ?? ?? 48 ?? ?? 48 ?? ?? BF", out var ptr))
            {
                var nextptr = ptr + 7;                   //下一条汇编指令地址
                var offset = Marshal.ReadInt32(ptr + 3); //偏移
                var unkInstance = nextptr + offset;
                num = unkInstance + 0x5520 + 0x150;
            }
            else
            {
                num = IntPtr.Zero;
            }
            return num;
        });

        internal static void RefreshFlyDive() => GetPosPtr4FlyDive = new Lazy<IntPtr>(() =>
                                                                                                        {
                                                                                                            if (Svc.SigScanner.TryScanText("4C ?? ?? ?? ?? ?? ?? 48 ?? ?? 48 ?? ?? BF", out var ptr))
                                                                                                            {
                                                                                                                var nextptr = ptr + 7;                   //下一条汇编指令地址
                                                                                                                var offset = Marshal.ReadInt32(ptr + 3); //偏移
                                                                                                                var unkInstance = nextptr + offset;
                                                                                                                var num = unkInstance + 0x5520 + 0x150;
                                                                                                                return num;
                                                                                                            }
                                                                                                            return IntPtr.Zero;
                                                                                                        });
        

        #endregion


        #region TP

        private static IntPtr SetPosFunPtr
        {
            get { return GetSetPosFunPtr.Value; }
        }

        internal static IntPtr PosPtr4FlyDive
        {
            get { return GetPosPtr4FlyDive.Value; }
        }


        private static void TP4FlyDive(float x, float y, float z)
        {
            IntPtr addr;
            addr = PosPtr4FlyDive;
            if (addr == IntPtr.Zero)
            {
                Svc.Chat.PrintError("用于飞行/潜水的sig扫描失败，无法进行传送！");
                return;
            }

            SafeMemory.Write(addr + 16, x);
            SafeMemory.Write(addr + 20, y);
            SafeMemory.Write(addr + 24, z);
        }

        private static SetPositionDelegate setPosition =
            (SetPositionDelegate)Marshal.GetDelegateForFunctionPointer(SetPosFunPtr, typeof(SetPositionDelegate));

        private static unsafe void SetPos(nint adress, float x, float y, float z)
        {
            if (Svc.ClientState.TerritoryType is 1165 or 1197)
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

            if (adress == LocalPlayer?.Address)
            {
                if (Svc.Condition[ConditionFlag.InFlight] | Svc.Condition[ConditionFlag.Diving])
                {
#if DEBUG
                    Svc.Chat.Print($"当前状态为飞行/潜水，使用飞行/潜水传送");
#endif
                    TP4FlyDive(x, y, z);
                    return;
                }
            }

            var num = setPosition(adress, x, y, z);
        }

        private delegate long SetPositionDelegate(long playerAddress, float x, float y, float z);

        // 传送自己
        internal static void TeleportMe(float x, float y, float z)
        {
            var player = LocalPlayer;
            if (player == null)
            {
                return;
            }

            SetPos(player.Address, x, y, z);
        }

        internal static void TeleportMe(Vector3 pos) => TeleportMe(pos.X, pos.Y, pos.Z);

        // 传送当前目标
        internal static void TeleportTarget(float x, float y, float z)
        {
            var target = CurrentTarget;
            if (target == null)
            {
                return;
            }

            SetPos(target.Address, x, y, z);
        }

        // 把选中的目标传送到自己身边
        internal static void TeleportTargetToMe()
        {
            var player = LocalPlayer;
            if (player == null)
            {
                return;
            }

            var target = CurrentTarget;
            if (target == null)
            {
                return;
            }

            SetPos(target.Address, player.Position.X, player.Position.Y, player.Position.Z);
        }

        // 验证输入的地图id是否和当前地图id相同
        internal static bool IsSameMapId(uint id)
        {
            var player = LocalPlayer;
            if (player == null)
            {
                return false;
            }

            if (Svc.ClientState.TerritoryType != id)
            {
                Svc.Chat.PrintError($"当前地图id({Svc.ClientState.TerritoryType})与输入id({id})不符！");
            }

            return Svc.ClientState.TerritoryType == id;
        }

        // 把自己传送到小队列表的某人身边
        internal static void TeleportToPartyMember(int index)
        {
            var partyMember = GetValidPartyMember(index);
            if (partyMember == null)
            {
                return;
            }

            var player = LocalPlayer;
            SetPos(player.Address, partyMember.Position.X, partyMember.Position.Y, partyMember.Position.Z);
        }

        // 获取有效的队伍成员
        internal static IPlayerCharacter? GetValidPartyMember(int index)
        {
            index--;
            if (index > Svc.Party.Length)
            {
                Svc.Chat.PrintError($"小队成员超过长度限制");
                return null;
            }

            var address = Svc.Party.GetPartyMemberAddress(index);
            var partyMember = Svc.Party.CreatePartyMemberReference(address);
            if (partyMember == null)
            {
                Svc.Chat.PrintError($"该成员可能不在你身边");
                return null;
            }

            // var player = Svc.Objects.SearchById(partyMember.ObjectId);
            var player = LocalPlayer;
            return (IPlayerCharacter)player;
        }

        // 获取鼠标位置的游戏坐标
        internal static Vector3 MousePos()
        {
            var io = ImGui.GetIO();
            Svc.GameGui.ScreenToWorld(io.MousePos, out var vector3);
            return vector3;
        }

        // 移动到鼠标位置
        internal static void MoveToMousePos()
        {
            var player = LocalPlayer;
            if (player == null)
            {
                return;
            }

            var pos = MousePos();
            SetPos(player.Address, pos.X, pos.Y, pos.Z);
        }

        // 传送到场地标点
        internal static void TP2WayMark(string mark)
        {
            var player = LocalPlayer;
            Vector3 pos = new();
            if (!TryGetWayMarkPos(mark, ref pos))
            {
                Svc.Chat.PrintError("传送标点格式错误或不存在！");
                return;
            }

            SetPos(player.Address, pos.X, pos.Y, pos.Z);
        }

        #endregion

        // 另一种改变位置的方式
        internal static void SetPos2XX(float X, float Y, float Z)
        {
            var player = LocalPlayer;
            if (player == null)
            {
                return;
            }

            unsafe
            {
                var playerp = (GameObjectPtr*)player.Address;
                playerp->Position.X = X;
                playerp->Position.Y = Y;
                playerp->Position.Z = Z;
            }
        }

        #region Waymarks

        internal static bool TryGetWayMarkPos(string waymark, ref Vector3 pos)
        {
            FieldMarkerPreset preset = new();
            var bitArray = new BitField8 {Data = preset.ActiveMarkers};
            // 尝试获取标点信息
            if (!MemoryHandler.GetCurrentWaymarksAsPresetData(ref preset))
                return false;
            switch (waymark)
            {
                case "A":
                case "a":
                    if (preset.IsMarkerActive(0))
                    {
                        pos = new Vector3((float)preset.Markers[0].X / 1000, (float)preset.Markers[0].Y / 1000, (float)preset.Markers[0].Z / 1000);
                        return true;
                    }

                    return false;

                case "B":
                case "b":
                    if (preset.IsMarkerActive(1))
                    {
                        pos = new Vector3((float)preset.Markers[1].X / 1000, (float)preset.Markers[1].Y / 1000, (float)preset.Markers[1].Z / 1000);
                        return true;
                    }

                    return false;
                case "C":
                case "c":
                    if (preset.IsMarkerActive(2))
                    {
                        pos = new Vector3((float)preset.Markers[2].X / 1000, (float)preset.Markers[2].Y / 1000, (float)preset.Markers[2].Z / 1000);
                        return true;
                    }
                
                    return false;
                case "D":
                case "d":
                    if (preset.IsMarkerActive(3))
                    {
                        pos = new Vector3((float)preset.Markers[3].X / 1000, (float)preset.Markers[3].Y / 1000, (float)preset.Markers[3].Z / 1000);
                        return true;
                    }
                
                    return false;
                case "1":
                    if (preset.IsMarkerActive(4))
                    {
                        pos = new Vector3((float)preset.Markers[4].X / 1000, (float)preset.Markers[4].Y / 1000,
                                          (float)preset.Markers[4].Z / 1000);
                        return true;
                    }
                
                    return false;
                case "2":
                    if (preset.IsMarkerActive(5))
                    {
                        pos = new Vector3((float)preset.Markers[5].X / 1000, (float)preset.Markers[5].Y / 1000,
                                          (float)preset.Markers[5].Z / 1000);
                        return true;
                    }
                
                    return false;
                case "3":
                    if (preset.IsMarkerActive(6))
                    {
                        pos = new Vector3((float)preset.Markers[6].X / 1000, (float)preset.Markers[6].Y / 1000,
                                          (float)preset.Markers[6].Z / 1000);
                        return true;
                    }
                
                    return false;
                case "4":
                    if (preset.IsMarkerActive(7))
                    {
                        pos = new Vector3((float)preset.Markers[7].X / 1000, (float)preset.Markers[7].Y / 1000,
                                          (float)preset.Markers[7].Z / 1000);
                        return true;
                    }
                
                    return false;
                default:
                    return false;
            }
        }

        #endregion

        #region Speed

        internal static void SetSpeed(float speed)
        {
            Plugin.Speed = speed;
        }

        #endregion

        #region Status

        private static unsafe PlayerState* ModuleSaved;

        internal static unsafe PlayerState* Module
        {
            get
            {
                if (ModuleSaved == null)
                {
                    ModuleSaved = PlayerState.Instance();
                }

                return ModuleSaved;
            }
        }

        internal static unsafe ushort CurrentSkillSpeed
        {
            get { return (ushort)PlayerState.Instance()->Attributes[(int)Attributes.SkillSpeed]; }
            set { PlayerState.Instance()->Attributes[(int)Attributes.SkillSpeed] = Plugin.Configuration.SkillSpeed; }
        }

        internal static unsafe ushort CurrentSpellSpeed
        {
            get { return (ushort)PlayerState.Instance()->Attributes[(int)Attributes.SpellSpeed]; }
            set { PlayerState.Instance()->Attributes[(int)Attributes.SpellSpeed] = Plugin.Configuration.SpellSpeed; }
        }

        internal static unsafe ushort GetCurrentValue(Attributes statusType)
        {
            return (ushort)PlayerState.Instance()->Attributes[(int)statusType];
        }

        internal static unsafe void SetCurrentValue(Attributes statusType, ushort value)
        {
            PlayerState.Instance()->Attributes[(int)statusType] = value;
        }
        #endregion

        #region Rotation

        internal static float Rotation
        {
            get { return LocalPlayer.Rotation; }
            set
            {
                if (LocalPlayer == null)
                    return;
                Marshal.StructureToPtr(
                    value, LocalPlayer.Address + new IntPtr(192), true);
            }
        }

        internal static float ConvertDegreesToRadians(float degrees) => (float)(degrees * (Math.PI / 180));

        #endregion

        #region Others

        public static string ToStr(ReadOnlySeString content) => content.ToDalamudString().ToString();

        public static string TranslateAttribute(Attributes attribute)
        {
            switch (attribute)
            {
                case Attributes.Strength:
                    return "力量";
                case Attributes.Dexterity:
                    return "灵巧";
                case Attributes.Vitality:
                    return "耐力";
                case Attributes.Intelligence:
                    return "智力";
                case Attributes.Mind:
                    return "精神";
                case Attributes.Piety:
                    return "信仰";
                case Attributes.MaxHp:
                    return "最大生命值";
                case Attributes.MaxMp:
                    return "最大魔法值";
                case Attributes.MaxGp:
                    return "最大工艺点";
                case Attributes.MaxCp:
                    return "最大制作点";
                case Attributes.Tenacity:
                    return "坚韧";
                case Attributes.AttackPower:
                    return "攻击力";
                case Attributes.Defense:
                    return "防御力";
                case Attributes.DirectHit:
                    return "直击";
                case Attributes.MagicDefense:
                    return "魔法防御力";
                case Attributes.CriticalHit:
                    return "暴击";
                case Attributes.AttackMagicPotency:
                    return "攻击魔法性能";
                case Attributes.HealingMagicPotency:
                    return "治疗魔法性能";
                case Attributes.Determination:
                    return "决心";
                case Attributes.SkillSpeed:
                    return "技能速度";
                case Attributes.SpellSpeed:
                    return "咒语速度";
                case Attributes.Craftsmanship:
                    return "工艺性能";
                case Attributes.Control:
                    return "控制性能";
                case Attributes.Gathering:
                    return "采集力";
                case Attributes.Perception:
                    return "感知";
                default:
                    return "未知";
            }
        }
        #endregion
        
    }
}
