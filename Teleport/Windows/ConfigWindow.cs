using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System;
using System.Threading.Tasks;

namespace Teleport.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin plugin;
    private static string info = "";
    private static bool displayNeko = false;

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
        if (ImGui.Checkbox("全局使用快速传送窗口", ref useQuickTP))
        {
            Plugin.Configuration.UseQuickTp = useQuickTP;
            Plugin.Configuration.Save();
        }

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

        if (recvAeCmd)
        {
            ImGui.Text($"您的cid为：{StaticUtils.LocalContentId}");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("开关此选项后需要重新加载插件才能生效");
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
                ImGui.SetClipboardText("https://raw.githubusercontent.com/extrant/DalamudPlugins/main/pluginmaster.json");
            }
        }

        if (ImGui.CollapsingHeader("XCount联动"))
        {
            ImGui.TextColored(XCountResults.IsXCountInstalled ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                              XCountResults.IsXCountInstalled ? "XCount连接成功" : "XCount未找到");
            if (XCountResults.IsXCountInstalled)
            {
                ImGui.SameLine();
                ImGui.Text($"当前人数：{XCountResults.CountsDict["<all>"]}");
                if (ImGui.Button("尝试连接XCount"))
                {
                    XCountResults.GetPlayerCount();
                }

                ImGui.TextColored(ImGuiColors.TankBlue, "当人数超过阈值时，禁止使用传送功能。设为0时禁用该功能");
                var xcountThreshold = Configuration.XCountThreshold;
                if (ImGui.InputInt("人数阈值", ref xcountThreshold))
                {
                    Configuration.XCountThreshold = xcountThreshold;
                    Configuration.Save();
                }
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
            ImGui.TextColored(ImGuiColors.DPSRed, "没有激活码？现在不需要激活码啦！随意使用吧~");
            ImGui.SameLine();
        }


    }
}
