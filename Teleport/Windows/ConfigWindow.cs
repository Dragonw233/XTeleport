using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Teleport.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin plugin;
    private static string info = "";
    private static bool displayNeko = false;
    private static string diveTpTerritoryInput = string.Empty;
    private static bool diveTpTerritoryInputInitialized;

    public ConfigWindow(Plugin plugin) : base(
        "传送设置")
    {
        this.plugin = plugin;
        Configuration = Plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // 打开设置
        if (ImGui.Button("主界面"))
        {
            plugin.DrawMain();
        }

        ImGui.SameLine();
        // 打开传送列表
        if (ImGui.Button("传送列表"))
        {
            plugin.DrawList();
        }

        var useQuickTP = Plugin.Configuration.UseQuickTp;
        if (ImGui.Checkbox("自动显示快捷传送面板", ref useQuickTP))
        {
            Plugin.Configuration.UseQuickTp = useQuickTP;
            Plugin.Configuration.Save();
        }

        DrawQuickTeleportSettings();

        var canFlay = StaticUtils.PosPtr4FlyDive != IntPtr.Zero;

        ImGui.TextColored(canFlay ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed, canFlay ? "潜水功能加载成功" : "潜水功能加载失败");
        ImGui.SameLine();
        if (ImGui.Button("重新加载潜水sig"))
        {
            StaticUtils.RefreshFlyDive();
        }

        // 使用遁地时位移
        var useUnderGroundMove = Plugin.Configuration.UseUndergroundMove;
        if (ImGui.Checkbox("启用遁地时移动（按W向前移动）", ref useUnderGroundMove))
        {
            Plugin.Configuration.UseUndergroundMove = useUnderGroundMove;
            Plugin.Configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("请注意，这个选项并不会检测你是否遁地，也就是说，你在地面上按W\n也会向你的面向方向位移。没事的时候最好把这个选项关掉，或者开下面的Ctrl+W选项");
        }

        if (Plugin.Configuration.UseUndergroundMove)
        {
            ImGui.Indent();
            // 遁地位移快捷键默认为w，是否设置为ctrl+w

            var useCtrlWMove = Plugin.Configuration.UseCtrlWToMove;
            if (ImGui.Checkbox("需要按住Ctrl+W才能移动", ref useCtrlWMove))
            {
                Plugin.Configuration.UseCtrlWToMove = useCtrlWMove;
                Plugin.Configuration.Save();
            }

            var quickFly = Plugin.Configuration.UseQuickFly;
            if (ImGui.Checkbox("Ctrl+F 快速升天", ref quickFly))
            {
                Plugin.Configuration.UseQuickFly = quickFly;
                Plugin.Configuration.Save();
            }
            var quickDive = Plugin.Configuration.UseQuickDive;
            if (ImGui.Checkbox("Ctrl+G 快速遁地", ref quickDive))
            {
                Plugin.Configuration.UseQuickDive = quickDive;
                Plugin.Configuration.Save();
            }
            ImGui.Unindent();
        }
        // 遁地位移速度倍率
        var moveSpeedRatio = Plugin.Configuration.UnderMoveSpeedRatio;
        if (ImGui.InputFloat("遁地时速度倍率（正常移动速度为1）", ref moveSpeedRatio))
        {
            Plugin.Configuration.UnderMoveSpeedRatio = moveSpeedRatio;
            Plugin.Configuration.Save();
        }

        var useQuickFace = Plugin.Configuration.UseQuickFace;
        if (ImGui.Checkbox("使用方向键快速转身", ref useQuickFace))
        {
            Plugin.Configuration.UseQuickFace = useQuickFace;
            Plugin.Configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("按键盘的上下左右方向键时，立即面向正北↑，正西←，正南↓，正东→");
        }

        var recvAeCmd = Plugin.Configuration.RecvAeTpCmd;
        if (ImGui.Checkbox("接受AE格式的TP指令", ref recvAeCmd))
        {
            Plugin.Configuration.RecvAeTpCmd = recvAeCmd;
            Plugin.Configuration.Save();
        }

        var showPartyListTeleportButtons = Plugin.Configuration.ShowPartyListTeleportButtons;
        if (ImGui.Checkbox("小队列表显示传送按钮", ref showPartyListTeleportButtons))
        {
            Plugin.Configuration.ShowPartyListTeleportButtons = showPartyListTeleportButtons;
            Plugin.Configuration.Save();
        }

        if (showPartyListTeleportButtons)
        {
            if (ImGui.CollapsingHeader("传送按钮对齐调试"))
            {
                ImGui.Indent();

                var showOnLeft = Plugin.Configuration.PartyListTeleportButtonsOnLeft;
                if (ImGui.Checkbox("显示在左侧", ref showOnLeft))
                {
                    Plugin.Configuration.PartyListTeleportButtonsOnLeft = showOnLeft;
                    Plugin.Configuration.Save();
                }

                var buttonWidth = Plugin.Configuration.PartyListTeleportButtonWidth;
                if (ImGui.InputFloat("传送按钮宽度", ref buttonWidth, 1f, 5f))
                {
                    Plugin.Configuration.PartyListTeleportButtonWidth = Math.Max(16f, buttonWidth);
                    Plugin.Configuration.Save();
                }

                var rowHeight = Plugin.Configuration.PartyListTeleportRowHeight;
                if (ImGui.InputFloat("每行间距", ref rowHeight, 1f, 5f))
                {
                    Plugin.Configuration.PartyListTeleportRowHeight = Math.Max(1f, rowHeight);
                    Plugin.Configuration.Save();
                }

                var xOffset = Plugin.Configuration.PartyListTeleportXOffset;
                if (ImGui.InputFloat("水平偏移", ref xOffset, 1f, 5f))
                {
                    Plugin.Configuration.PartyListTeleportXOffset = xOffset;
                    Plugin.Configuration.Save();
                }

                var yOffset = Plugin.Configuration.PartyListTeleportYOffset;
                if (ImGui.InputFloat("垂直偏移", ref yOffset, 1f, 5f))
                {
                    Plugin.Configuration.PartyListTeleportYOffset = yOffset;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Button("重置传送按钮对齐参数"))
                {
                    Plugin.Configuration.PartyListTeleportButtonsOnLeft = true;
                    Plugin.Configuration.PartyListTeleportButtonWidth = 30f;
                    Plugin.Configuration.PartyListTeleportRowHeight = 45f;
                    Plugin.Configuration.PartyListTeleportXOffset = 6f;
                    Plugin.Configuration.PartyListTeleportYOffset = 0f;
                    Plugin.Configuration.Save();
                }

                ImGui.Unindent();
            }
        }

        if (recvAeCmd)
        {
            ImGui.Text($"您的cid为：{StaticUtils.LocalContentId}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("按钮显示开关可能需要重新加载插件；下方对齐参数为实时生效。");
        }
        if (ImGui.CollapsingHeader("Hacks"))
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "使用iChing，畅享开挂狒生！");
            if (ImGui.Button("点击加入I-Ching的DC"))
            {
                Dalamud.Utility.Util.OpenLink("https://discord.gg/g8QKPAnCBa");
            }     
            ImGui.SameLine();               
            if (ImGui.Button($"复制I-Ching裤链到剪切板"))
            {
                ImGui.SetClipboardText("https://github.com/Dragonw233/XTeleport/releases/latest/download/repo.json");
            }
        }

        if (ImGui.CollapsingHeader("周边安全检测"))
        {
            XCountResults.RefreshPlayerCount();
            var unsafePlayers = XCountResults.EffectiveNearbyPlayerCount;

            ImGui.Text(XCountResults.IsNearbySafe ? "当前状态：安全" : $"周边绿玩：{unsafePlayers} 个");
            ImGui.Text($"周边人数：{XCountResults.NearbyPlayerCount}");
            ImGui.Text($"白名单人数：{XCountResults.WhitelistedNearbyPlayerCount}");

            var ignoreUnsafePlayers = Configuration.IgnoreUnsafePlayersForTP;
            if (ImGui.Checkbox("无视周边绿玩", ref ignoreUnsafePlayers))
            {
                Configuration.IgnoreUnsafePlayersForTP = ignoreUnsafePlayers;
                Configuration.Save();
            }

            if (Configuration.IgnoreUnsafePlayersForTP)
            {
                ImGui.TextColored(ImGuiColors.DalamudYellow, "已开启无视模式，非白名单玩家不会阻止传送。");
            }

            ImGui.TextColored(ImGuiColors.TankBlue, "当生效人数超过阈值时，禁止使用传送功能。设为 0 时禁用该功能。");
            var xcountThreshold = Configuration.XCountThreshold;
            if (ImGui.InputInt("人数阈值", ref xcountThreshold))
            {
                Configuration.XCountThreshold = Math.Max(0, xcountThreshold);
                Configuration.Save();
            }

            if (ImGui.Button("打开周边安全检测面板"))
            {
                plugin.DrawXCountUI();
            }
        }

        if (ImGui.CollapsingHeader("激活码###"))
        {
            // 展示机器码
            ImGui.Text($"机器码：{Plugin.MacId}");
            ImGui.SameLine();
            // 复制机器码
            if (ImGui.Button("复制机器码"))
            {
                ImGui.SetClipboardText(Plugin.MacId);
            }

            ImGui.Text($"机器码v2：{Plugin.MacIdV2}");
            ImGui.SameLine();
            if (ImGui.Button("复制机器码v2"))
            {
                ImGui.SetClipboardText(Plugin.MacIdV2);
            }

            // 激活码
            var activation = Configuration.LocalActivation;
            if (ImGui.InputText("激活码", ref activation, 500))
            {
                Configuration.LocalActivation = activation;
                Configuration.Save();
            }

            var activationPro = Configuration.LocalActivationPro;
            if (ImGui.InputText("激活码2", ref activationPro, 500))
            {
                Configuration.LocalActivationPro = activationPro;
                Configuration.Save();
            }

            if (Plugin.GetActivationLocal())
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, "你激活了激活码1，你是初春的朋友~");
            }

            if (Plugin.GetActivationPro())
            {
                ImGui.TextColored(ImGuiColors.TankBlue, "你激活了激活码2，你为中文插件领域做出过某些贡献~");
            }
            
            ImGui.TextColored(Plugin.GetActivationLocal() ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                              Plugin.GetActivationLocal() ? "已激活1" : "未激活1");
            ImGui.SameLine();
            ImGui.TextColored(Plugin.GetActivationPro() ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                              Plugin.GetActivationPro() ? "已激活2" : "未激活2");
            ImGui.SameLine();
            // 验证
            if (ImGui.Button("一键验证"))
            {
                Plugin.CheckActivation();
                Plugin.CheckActivationPro();
                Plugin.CheckNekoVerifier();
            }
            // 从剪切板粘贴激活码
            ImGui.SameLine();
            if (ImGui.Button("从剪切板粘贴激活码"))
            {
                var text = ImGui.GetClipboardText();
                if (text != null)
                {
                    Configuration.LocalActivation = text;
                    Configuration.Save();
                }
            }
            // 增加DC链接
            ImGui.TextColored(ImGuiColors.DPSRed, "潜水功能需要激活码2，请在各个渠道获取我的联系方式发送码2机器码获取~");
            ImGui.SameLine();
        }


    }

    private static void DrawQuickTeleportSettings()
    {
        if (!ImGui.CollapsingHeader("快捷传送面板"))
            return;

        DiveTpTerritoryHelper.EnsureInitialized();
        if (!diveTpTerritoryInputInitialized)
            RefreshDiveTpTerritoryInput();

        ImGui.Indent();

        var useSmartDiveTp = Plugin.Configuration.UseDivePacketTpInQuickWindow;
        if (ImGui.Checkbox("DR 智能潜水TP", ref useSmartDiveTp))
        {
            if (useSmartDiveTp && !Plugin.GetActivationPro())
            {
                Svc.Chat.PrintError("DR 智能潜水TP需要先通过码2验证。");
                useSmartDiveTp = false;
            }

            Plugin.Configuration.UseDivePacketTpInQuickWindow = useSmartDiveTp;
            Plugin.Configuration.Save();
        }

        var offset = Plugin.Configuration.QuickTpOffsetDistance;
        if (ImGui.InputFloat("面板偏移步长", ref offset, 0.5f, 5f, "%.1f"))
        {
            Plugin.Configuration.QuickTpOffsetDistance = Math.Clamp(Math.Abs(offset), 0.1f, 1000f);
            Plugin.Configuration.Save();
        }

        var currentTerritory = Svc.ClientState.TerritoryType;
        var containsCurrent = Plugin.Configuration.DiveTpTerritories.Contains(currentTerritory);
        ImGui.TextColored(
            containsCurrent ? ImGuiColors.HealerGreen : ImGuiColors.ParsedGrey,
            containsCurrent
                ? $"当前地图 #{currentTerritory} 在潜水TP区域表中"
                : $"当前地图 #{currentTerritory} 不在潜水TP区域表中");
        ImGui.Text($"区域数量：{Plugin.Configuration.DiveTpTerritories.Count}");

        if (!containsCurrent)
        {
            if (ImGui.Button("加入当前地图"))
            {
                Plugin.Configuration.DiveTpTerritories.Add(currentTerritory);
                Plugin.Configuration.DiveTpTerritoriesInitialized = true;
                Plugin.Configuration.Save();
                RefreshDiveTpTerritoryInput();
            }
        }
        else if (ImGui.Button("移除当前地图"))
        {
            Plugin.Configuration.DiveTpTerritories.Remove(currentTerritory);
            Plugin.Configuration.Save();
            RefreshDiveTpTerritoryInput();
        }

        ImGui.SameLine();
        if (ImGui.Button("恢复 DR 默认表"))
        {
            DiveTpTerritoryHelper.ResetToDrDefaults();
            RefreshDiveTpTerritoryInput();
        }

        ImGui.InputTextMultiline(
            "Territory ID 列表",
            ref diveTpTerritoryInput,
            16384,
            new Vector2(-1f, 90f));

        if (ImGui.Button("应用区域表"))
        {
            var ids = new List<uint>();
            var invalidValues = new List<string>();
            foreach (var value in diveTpTerritoryInput.Split(
                         [',', ';', ' ', '\t', '\r', '\n'],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (uint.TryParse(value, out var id) && id != 0)
                    ids.Add(id);
                else
                    invalidValues.Add(value);
            }

            if (invalidValues.Count > 0)
            {
                Svc.Chat.PrintError($"无法识别的 Territory ID：{string.Join(", ", invalidValues)}");
            }
            else
            {
                DiveTpTerritoryHelper.ReplaceTerritories(ids);
                RefreshDiveTpTerritoryInput();
            }
        }

        ImGui.Unindent();
    }

    private static void RefreshDiveTpTerritoryInput()
    {
        diveTpTerritoryInput = string.Join(", ", Plugin.Configuration.DiveTpTerritories.OrderBy(x => x));
        diveTpTerritoryInputInitialized = true;
    }
}
