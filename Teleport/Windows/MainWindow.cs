using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Numerics;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using ECommons.Automation;

namespace Teleport.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin plugin;

    public MainWindow(Plugin plugin) : base(
        "传送 Home")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    internal static int currentStatusType = 01;
    internal static int currentDIYValue = 100;
    private static float _speed = 1;

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudRed, "本插件完全免费，禁止一切形式的倒卖！使用时请不要跳脸绿玩！");
        ImGui.TextColored(ImGuiColors.HealerGreen, "希望与ACT联动？请使用鲶鱼精发送默语命令，详见聊天解析页面");
        ImGui.TextColored(ImGuiColors.HealerGreen, "一级码系列功能直接开放！（进入Discord频道获取密码）！");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "目前XTP不使用任何验证方式，请不要倒卖或通过付费渠道获取本插件");
        // 打开设置
        if (ImGui.Button("设置"))
        {
            plugin.DrawConfigUI();
        }
        ImGui.SameLine();
        // 打开传送列表
        if (ImGui.Button("传送列表"))
        {
            plugin.DrawList();
        }

        ImGui.SameLine();
        var useQuickTP = Plugin.Configuration.UseQuickTp;
        if (ImGui.Checkbox("使用快速传送窗口", ref useQuickTP))
        {
            Plugin.Configuration.UseQuickTp = useQuickTP;
            Plugin.Configuration.Save();
        }

        if (StaticUtils.LocalPlayer is null)
            return;
        var position = StaticUtils.LocalPlayer.Position;
        if (ImGui.BeginTabBar("XTeleport Main"))
        {
            if (ImGui.BeginTabItem("坐标信息"))
            {
                // 显示玩家当前坐标
                ImGui.TextColored(ImGuiColors.DPSRed,
                                  $"当前玩家坐标：\nx:{Math.Round(position.X, 6)}\ny:{Math.Round(position.Y, 6)}\nz:{Math.Round(position.Z, 6)}");
                // 显示当前鼠标坐标
                var pos = StaticUtils.MousePos();
                ImGui.TextColored(ImGuiColors.TankBlue,
                                  $"当前鼠标坐标：\nx:{Math.Round(pos.X, 6)}\ny:{Math.Round(pos.Y, 6)}\nz:{Math.Round(pos.Z, 6)}");
                // 增加一个按钮用于复制位置
                if (ImGui.Button("复制位置"))
                {
                    ImGui.SetClipboardText(
                        $"{Math.Round(position.X, 6)} {Math.Round(position.Y, 6)} {Math.Round(position.Z, 6)}");
                }

                ImGui.SameLine();
                // 复制位置，并保留三位小数
                if (ImGui.Button("复制位置(保留三位小数)"))
                {
                    ImGui.SetClipboardText(
                        $"{Math.Round(position.X, 3)} {Math.Round(position.Y, 3)} {Math.Round(position.Z, 3)}");
                }

                ImGui.TextColored(ImGuiColors.HealerGreen,
                                  $"当前地图：\n名称->{Svc.Data.GetExcelSheet<TerritoryType>().GetRow(Svc.ClientState.TerritoryType).PlaceName.Value.Name}\nid->{Svc.ClientState.TerritoryType}");
                // 复制位置和地图id，保留三位小数
                if (ImGui.Button("复制位置和地图id(保留三位小数)"))
                {
                    ImGui.SetClipboardText(
                        $"{Math.Round(position.X, 3)} {Math.Round(position.Y, 3)} {Math.Round(position.Z, 3)} {Svc.ClientState.TerritoryType}");
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("快速传送"))
            {
                // 便捷传送
                var easyX = Plugin.Configuration.EasyTP.X;
                if (ImGui.InputFloat("X", ref easyX, 0.1f))
                {
                    Plugin.Configuration.EasyTP = Plugin.Configuration.EasyTP.WithX(easyX);
                    Plugin.Configuration.Save();
                }

                var easyY = Plugin.Configuration.EasyTP.Y;
                if (ImGui.InputFloat("Y", ref easyY, 0.1f))
                {
                    Plugin.Configuration.EasyTP = Plugin.Configuration.EasyTP.WithY(easyY);
                    Plugin.Configuration.Save();
                }

                var easyZ = Plugin.Configuration.EasyTP.Z;
                if (ImGui.InputFloat("Z", ref easyZ, 0.1f))
                {
                    Plugin.Configuration.EasyTP = Plugin.Configuration.EasyTP.WithZ(easyZ);
                    Plugin.Configuration.Save();
                }

                int easyID = (int)Plugin.Configuration.EasyTPMap;
                if (ImGui.InputInt("id:", ref easyID, 1))
                {
                    if (easyID < 0)
                    {
                        easyID = 0;
                    }

                    if (easyID > 65535)
                    {
                        easyID = 65535;
                    }

                    Plugin.Configuration.EasyTPMap = (uint)easyID;
                    Plugin.Configuration.Save();
                }

                // 复制当前坐标到便捷传送
                if (ImGui.Button("填入当前坐标与地图"))
                {
                    plugin.DoFastSave();
                }

                ImGui.SameLine();

                if (ImGui.Button("传送"))
                {
                    plugin.DoFastLoad();
                }

                ImGui.SameLine();

                if (ImGui.Button("传送（2）"))
                {
                    StaticUtils.SetPos2XX(easyX, easyY, easyZ);
                }
                //潜水调试按钮
                ImGui.Separator();
                ImGui.TextColored(ImGuiColors.DalamudYellow, "潜水发包TP调试");
                if (!Plugin.GetActivationPro())
                {
                    ImGui.TextColored(ImGuiColors.DPSRed, "使用潜水发包TP需要先完成码2验证");
                }
                if (ImGui.Button("潜水发包TP（上方坐标）"))
                {
                    if (!Plugin.GetActivationPro())
                    {
                        Svc.Chat.PrintError("潜水发包TP需要先通过码2验证。");
                    }
                    else
                    {
                        StaticUtils.TeleportMeByDivePacket(easyX, easyY, easyZ);
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("潜水发包TP（当前坐标）"))
                {
                    if (!Plugin.GetActivationPro())
                    {
                        Svc.Chat.PrintError("潜水发包TP需要先通过码2验证。");
                    }
                    else
                    {
                        StaticUtils.TeleportMeByDivePacket(position);
                    }
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("速度设置"))
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, "使用iChing，畅享开挂狒生！");
                if (ImGui.Button("点击加入I-Ching的DC"))
                {
                    Dalamud.Utility.Util.OpenLink("https://discord.gg/g8QKPAnCBa");
                }

                ImGui.SameLine();
                if (ImGui.Button($"复制I-Ching裤链到剪切板"))
                {
                    ImGui.SetClipboardText(
                        "https://raw.githubusercontent.com/extrant/DalamudPlugins/main/pluginmaster.json");
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("咏速技速修改"))
            {
                ImGui.TextColored(ImGuiColors.DPSRed,
                                  "别想着召唤多喷一口巴哈或者直爆黑魔变成咏速黑魔\n服务器会回弹的，最后可能得不偿失，小改一下改善手感可以。\n换句话说，技速和咏速的修改是在本地进行的，服务器并不会“接受”\n修改后的速度，你原本打不出来的技能还是打不出来");
                var interval = Plugin.Configuration.SpeedRefreshInterval;
                if (ImGui.InputDouble("咏速刷新间隔", ref interval, 0.1))
                {
                    if (interval < 0.1)
                    {
                        interval = 0.1;
                    }

                    Plugin.Configuration.SpeedRefreshInterval = interval;
                    Plugin.Configuration.Save();
                    Plugin.SpeedTesk.SetInterval(Plugin.Configuration.SpeedRefreshInterval);
                }


                var useSpellSpeed = Plugin.Configuration.UseSpellSpeed;
                if (ImGui.Checkbox("##咏速修改", ref useSpellSpeed))
                {
                    Plugin.Configuration.UseSpellSpeed = useSpellSpeed;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                int spellSpeed = Plugin.Configuration.SpellSpeed;
                if (ImGui.InputInt("设定咏速", ref spellSpeed, 100))
                {
                    if (spellSpeed < 400)
                    {
                        spellSpeed = 400;
                    }

                    if (spellSpeed > 10000)
                    {
                        spellSpeed = 10000;
                    }

                    Plugin.Configuration.SpellSpeed = (ushort)spellSpeed;
                    Plugin.Configuration.Save();
                }

                var useSkillSpeed = Plugin.Configuration.UseSkillSpeed;
                if (ImGui.Checkbox("##技速修改", ref useSkillSpeed))
                {
                    Plugin.Configuration.UseSkillSpeed = useSkillSpeed;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                int SkillSpeed = Plugin.Configuration.SkillSpeed;
                if (ImGui.InputInt("设定技速", ref SkillSpeed, 100))
                {
                    if (SkillSpeed < 400)
                    {
                        SkillSpeed = 400;
                    }

                    if (SkillSpeed > 10000)
                    {
                        SkillSpeed = 10000;
                    }

                    Plugin.Configuration.SkillSpeed = (ushort)SkillSpeed;
                    Plugin.Configuration.Save();
                }

                if (ImGui.Button("填入当前值"))
                {
                    unsafe
                    {
                        Plugin.Configuration.SpellSpeed = StaticUtils.CurrentSpellSpeed;
                        Plugin.Configuration.SkillSpeed = StaticUtils.CurrentSkillSpeed;
                        Plugin.Configuration.Save();
                    }
                }

                ImGui.TextColored(ImGuiColors.DalamudRed, "以下内容仅为愚人节彩蛋，实际上是无效的");
                ImGui.Text("当前修改的项目是：");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.HealerGreen,
                                  StaticUtils.TranslateAttribute((Attributes)currentStatusType));
                if (ImGui.InputInt("属性代码", ref currentStatusType))
                {
                    if (currentStatusType > (int)Attributes.HealingMagicPotency)
                    {
                        currentStatusType = (int)Attributes.HealingMagicPotency;
                    }

                    if (currentStatusType < (int)Attributes.Strength)
                    {
                        currentStatusType = (int)Attributes.Strength;
                    }
                }

                if (ImGui.InputInt("属性值", ref currentDIYValue))
                {
                    if (currentDIYValue > 23333)
                    {
                        currentDIYValue = 23333;
                    }

                    if (currentDIYValue < 0)
                    {
                        currentDIYValue = 0;
                    }
                }

                if (ImGui.Button("确认修改"))
                {
                    StaticUtils.SetCurrentValue((Attributes)currentStatusType, (ushort)currentDIYValue);
                }

                if (ImGui.CollapsingHeader("属性代码表"))
                {
                    foreach (Attributes type in Enum.GetValues(typeof(Attributes)))
                    {
                        ImGui.Text($"{StaticUtils.TranslateAttribute(type)}:{(int)type}");
                    }
                }

                ImGui.EndTabItem();
            }

            // 命令列表，列出可用指令
            if (ImGui.BeginTabItem("命令列表"))
            {
                ImGui.Text("点击复制");
                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 100);
                foreach (var (command, helpMsg) in plugin.CommandsDictionary)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
                    if (ImGui.Button(command))
                    {
                        ImGui.SetClipboardText(command);
                    }

                    ImGui.PopStyleColor();
                    ImGui.NextColumn();
                    ImGui.Text(helpMsg);
                    ImGui.NextColumn();
                }

                ImGui.Columns(1);
                ImGui.EndTabItem();
            }

            // 聊天解析
            if (ImGui.BeginTabItem("聊天解析"))
            {
                ImGui.TextColored(ImGuiColors.DPSRed, "从“聊天解析”功能推出开始，所有解析都只会解析默语频道，不用担心别人给你私聊一句你就被传送走了。");
                ImGui.Text(
                    "命令解析说明：格式是/e @CMD 命令，这个命令必须是斜杠(/)开头的，就像你在游戏里输的命令一样。\n比如一个能写在鲶鱼精里的完整语句就是“/e @CMD /tp 格里达尼亚新街”\n（这是Teleport插件，不是本插件），可以自己试一下。");
                var useCMD = Plugin.Configuration.UseCommand;
                if (ImGui.Checkbox("解析@CMD开头的卫月命令", ref useCMD))
                {
                    Plugin.Configuration.UseCommand = useCMD;
                    Plugin.Configuration.Save();
                }

                var useAllCmd = Plugin.Configuration.ExeAllCommand;
                if (ImGui.Checkbox("执行所有命令（而不仅仅是卫月命令）", ref useAllCmd))
                {
                    Plugin.Configuration.ExeAllCommand = useAllCmd;
                    Plugin.Configuration.Save();
                }

                ImGui.Text(
                    "聊天解析说明：\n目前支持直接使用XTP的指令（本插件简称为XTP吧），格式为“/e @XTP 指令 参数”\n，复制一下就明白到底是啥格式了。也支持FfxivPythonTrigger的格式，虽然我没有fpt的使用权，\n无法实机测试，但是我猜这个插件和fpt一起开会起冲突，所以如果你开着f的tp，请取消勾选下面解析FPT的选项\nPS：如果你不需要这个聊天解析功能，请取消勾选以下两项。\n对聊天内容的解析可能在极端情况下略微降低游戏性能，或者造成误操作");
                var useTP = Plugin.Configuration.UseTeleport;
                if (ImGui.Checkbox("解析XTP格式的传送指令", ref useTP))
                {
                    Plugin.Configuration.UseTeleport = useTP;
                    Plugin.Configuration.Save();
                }

                var useFPT = Plugin.Configuration.UseFPT;
                if (ImGui.Checkbox("解析FPT格式的传送指令", ref useFPT))
                {
                    Plugin.Configuration.UseFPT = useFPT;
                    Plugin.Configuration.Save();
                }

                // 复制当前坐标到插件传送指令格式
                if (ImGui.Button("复制当前坐标(XTP)"))
                {
                    ImGui.SetClipboardText(
                        $"/e @XTP stp {Math.Round(position.X, 3)} {Math.Round(position.Y, 3)} {Math.Round(position.Z, 3)} {Svc.ClientState.TerritoryType}");
                }

                ImGui.SameLine();
                if (ImGui.Button("复制当前坐标(FPT)"))
                {
                    ImGui.SetClipboardText(
                        $"/e @tp set {Math.Round(position.X, 3)} {Math.Round(position.Z, 3)} {Math.Round(position.Y, 3)}");
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("聊天触发器"))
            {
                DrawIftttSettings();
                ImGui.EndTabItem();
            }

            // 面向
            if (ImGui.BeginTabItem("面向调整"))
            {
                ImGui.Text(
                    "在FF14中，面向正南时，rot是0，正北的rot是π/-π（就是说180°和-180°实际上是一个角度），\n逆时针方向rot增加，顺时针减小。所以正北是180°，正南是0°，正东是90°，正西是-90°，\n写命令时，写成/xrot 角度，比如正东就是/xrot 90，正西就是/xrot -90\n在dc的文件分享频道可以下载一个PP预设来测试面向功能。");
                ImGui.Text(
                    $"当前面向：{Math.Round(StaticUtils.Rotation, 3)}r <===> {Math.Round(StaticUtils.Rotation / Math.PI * 180, 3)}°");
                if (ImGui.Button("+90°"))
                {
                    StaticUtils.Rotation += (float)Math.PI * 90 / 180;
                }

                ImGui.SameLine();
                if (ImGui.Button("-90°"))
                {
                    StaticUtils.Rotation -= (float)Math.PI * 90 / 180;
                }

                ImGui.Text("什么？太复杂了不会写？点击下面的按钮，在更改面向的同时复制对应的命令");
                if (ImGui.Button("正北(R=π)"))
                {
                    StaticUtils.Rotation = (float)Math.PI;
                    ImGui.SetClipboardText("/xrot 180");
                }

                ImGui.SameLine();
                if (ImGui.Button("正东(R=π/2)"))
                {
                    StaticUtils.Rotation = (float)Math.PI / 2;
                    ImGui.SetClipboardText("/xrot 90");
                }

                ImGui.SameLine();
                if (ImGui.Button("正南(R=0)"))
                {
                    StaticUtils.Rotation = 0;
                    ImGui.SetClipboardText("/xrot 0");
                }

                ImGui.SameLine();
                if (ImGui.Button("正西(R=-π/2)"))
                {
                    StaticUtils.Rotation = (float)Math.PI / -2;
                    ImGui.SetClipboardText("/xrot -90");
                }

                ImGui.EndTabItem();
                ImGui.TextColored(ImGuiColors.DPSRed, "/tpface功能介绍");
                ImGui.Text(
                    "命令格式/tpface 角度 距离，比如/tpface 0 1就是向你当前面向的正前方移动1m\n但是请注意，这个角度是顺时针转的，比如你要往右移动1m，就写/tpface 90 1\n还不明白自己试试就明白了。这个移动不会改变你的高度，如果你在有高低差的地方位移，请自己小心。\n\n怎么计算的？\n有这样的一个平面坐标系，有水平轴x轴和纵轴z轴，\n当人物向东走时，x减小，当人物向北走时，z减小。\n现在可以把一个人的坐标和面向用（x,z,angle）表示。当人物面向正南时，angle为0，\n面向角随逆时针旋转增大，顺时针旋转减小。现在，请计算人物面向顺时针旋转angleDelta后，以旋转后的面向向前走distance米的坐标\n这就是这个功能的数学模型，我可能算错了，请多反馈");
            }

            ImGui.EndTabBar();
        }
    }

    public void DrawIftttSettings()
    {
        bool enableAllTri = Plugin.Configuration.EnableTrigger;
        // 启用/禁用复选框
        if (ImGui.Checkbox("启用", ref enableAllTri))
        {
            Plugin.Configuration.EnableTrigger = enableAllTri;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("新建触发器"))
        {
            Plugin.Configuration.IftttTriggers.Add(new IftttTrigger());
            Plugin.Configuration.Save();
        }

        ImGui.Text("如果消息内容匹配正则表达式，则执行相应命令。");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 使用 for 循环而不是 foreach，以便在循环中安全地删除元素
        for (int i = Plugin.Configuration.IftttTriggers.Count - 1; i >= 0; i--)
        {
            var trigger = Plugin.Configuration.IftttTriggers[i];

            // 为每个触发器创建一个独立的区域
            ImGui.PushID($"trigger_{i}");

            if (ImGui.CollapsingHeader($"触发器 #{i + 1} ({trigger.ChatType}: {trigger.RegexPattern})###header"))
            {
                bool enable = trigger.IsEnabled;
                // 启用/禁用复选框
                if (ImGui.Checkbox("启用", ref enable))
                {
                    trigger.IsEnabled = enable;
                    Plugin.Configuration.Save();
                }

                // 聊天类型下拉菜单
                XivChatType currentChatTypeIndex = trigger.ChatType;
                if (ImGui.BeginCombo("类型", currentChatTypeIndex.ToString()))
                {
                    foreach (XivChatType type in Enum.GetValues(typeof(XivChatType)))
                    {
                        if (ImGui.Selectable(type.ToString()))
                        {
                            currentChatTypeIndex = type;
                            trigger.ChatType = currentChatTypeIndex;
                            Plugin.Configuration.Save();
                        }
                    }

                    ImGui.EndCombo();
                }

                // 正则表达式输入框
                string inputRegax = trigger.RegexPattern;
                if (ImGui.InputText("正则表达式", ref inputRegax, 512))
                {
                    trigger.RegexPattern = inputRegax;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                HelpMarker("用于匹配消息内容的正则表达式。");

                // 命令输入框
                string inputCMD = trigger.CommandToExecute;
                if (ImGui.InputText("执行命令", ref inputCMD, 512))
                {
                    trigger.CommandToExecute = inputCMD;
                    Plugin.Configuration.Save();
                }

                ImGui.SameLine();
                HelpMarker(
                    "要执行的命令。可以使用 {0}, {1}, ... 来引用正则表达式的捕获组。\n例如: ^(\\d+)\\s(\\d+)$ 匹配的是空格连接的两个数字\n第一个数字引用时写{1}，第二个写{2}");

                ImGui.Spacing();

                // 测试区域
                ImGui.Text("测试工具");
                ImGui.InputText("测试消息", ref trigger.TestMessage, 512);
                if (ImGui.Button("测试正则匹配"))
                {
                    try
                    {
                        var match = Regex.Match(trigger.TestMessage, trigger.RegexPattern);
                        if (match.Success)
                        {
                            // 跳过第一个捕获组 (即整个匹配)，并格式化输出
                            var capturedGroups = match.Groups.Cast<Group>()
                                                      .Skip(1) // 跳过代表整个匹配的组0
                                                      .Select((g, index) => $"{{{index + 1}}} = {g.Value}")
                                                      .ToArray();

                            if (capturedGroups.Any())
                            {
                                trigger.TestResult = $"匹配成功! 捕获组: {string.Join(" ", capturedGroups)}";
                            }
                            else
                            {
                                trigger.TestResult = "匹配成功! (没有捕获组)";
                            }
                        }
                        else
                        {
                            trigger.TestResult = "匹配失败。";
                        }
                    }
                    catch (Exception ex)
                    {
                        trigger.TestResult = $"正则错误: {ex.Message}";
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("测试执行命令"))
                {
                    try
                    {
                        var match = Regex.Match(trigger.TestMessage, trigger.RegexPattern);
                        if (match.Success)
                        {
                            var groups = match.Groups.Cast<Group>().Select(g => g.Value).ToArray();
                            string formattedCommand = string.Format(trigger.CommandToExecute, groups);
                            Chat.SendMessage(formattedCommand);
                            trigger.TestResult = $"命令已发送: {formattedCommand}";
                        }
                        else
                        {
                            trigger.TestResult = "匹配失败，无法执行命令。";
                        }
                    }
                    catch (Exception ex)
                    {
                        trigger.TestResult = $"命令格式化错误: {ex.Message}";
                    }
                }

                if (!string.IsNullOrEmpty(trigger.TestResult))
                {
                    ImGui.TextWrapped($"测试结果:{trigger.TestResult}");
                }


                ImGui.Spacing();
                // 删除按钮
                if (ImGui.Button("删除此触发器"))
                {
                    Plugin.Configuration.IftttTriggers.RemoveAt(i);
                    Plugin.Configuration.Save();
                }

                ImGui.Separator();
            }

            ImGui.PopID();
        }
    }

    private void HelpMarker(string description)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
