using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System;

namespace Teleport.Windows
{
    public class QuickTPWindow : Window, IDisposable
    {
        private Plugin plugin;
        private Configuration config;

        public QuickTPWindow(Plugin plugin) : base("快速传送窗口", ImGuiWindowFlags.NoTitleBar)
        {
            this.plugin = plugin;
            config = Plugin.Configuration;
        }

        public override void Draw()
        {
            var i = plugin.MapIndex;
            var useQuickTP = Plugin.Configuration.TPLists[i].UseQuickWindow;
            if (ImGui.Checkbox("使用快速传送窗口", ref useQuickTP))
            {
                Plugin.Configuration.TPLists[i].UseQuickWindow = useQuickTP;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            var hideXYZ = Plugin.Configuration.HideXYZ;
            if (ImGui.Checkbox("隐藏XYZ", ref hideXYZ))
            {
                Plugin.Configuration.HideXYZ = hideXYZ;
                Plugin.Configuration.Save();
            }
            
            ImGui.SameLine();
            var useDivePacketTp = Plugin.Configuration.UseDivePacketTpInQuickWindow;
            if (ImGui.Checkbox("野外潜水发包TP", ref useDivePacketTp))
            {
                if (useDivePacketTp && !Plugin.GetActivationPro())
                {
                    Svc.Chat.PrintError("快捷窗口的潜水发包TP需要先通过码2验证。");
                    useDivePacketTp = false;
                }

                Plugin.Configuration.UseDivePacketTpInQuickWindow = useDivePacketTp;
                Plugin.Configuration.Save();
            }

            if (Plugin.Configuration.UseDivePacketTpInQuickWindow)
            {
                ImGui.SameLine();
                ImGui.TextColored(Dalamud.Interface.Colors.ImGuiColors.TankBlue, "码2已验证");
            }
            
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

            ImGui.SameLine();
            // 打开设置
            if (ImGui.Button("设置"))
            {
                plugin.DrawConfigUI();
            }

            if (i == -1)
                return;
            // 用于添加点
            if (ImGui.Button($"添加##{i}"))
            {
                config.TPLists[i].TPs.Add(new TP());
                config.Save();
            }

            ImGui.SameLine();
            // 添加当前位置
            if (ImGui.Button($"添加当前位置##{i}"))
            {
                config.TPLists[i].TPs.Add(new TP("无", StaticUtils.LocalPlayer.Position));
                config.Save();
            }

            ImGui.SameLine();
            // 导出到剪切板
            if (ImGui.Button($"导出到剪切板##{i}"))
            {
                ImGui.SetClipboardText(config.TPLists[i].ToJson());
            }

            ImGui.SameLine();
            // 删除当前地图
            if (ImGui.Button($"删除##{i}"))
            {
                config.TPLists.RemoveAt(i);
                config.Save();
            }

            // 使用列表展示各个传送点信息
            ImGui.Columns(hideXYZ ? 5 : 8);
            ImGui.Text("名称");
            if (!hideXYZ)
            {
                ImGui.NextColumn();
                ImGui.Text("X");
                ImGui.NextColumn();
                ImGui.Text("Y");
                ImGui.NextColumn();
                ImGui.Text("Z");
            }

            ImGui.NextColumn();
            ImGui.Text("传送");
            ImGui.NextColumn();
            ImGui.Text("上移");
            ImGui.NextColumn();
            ImGui.Text("下移");
            ImGui.NextColumn();
            ImGui.Text("删除");
            ImGui.NextColumn();
            for (var j = 0; j < config.TPLists[i].TPs.Count; j++)
            {
                // 输入名称和坐标
                var name = config.TPLists[i].TPs[j].Name;
                if (ImGui.InputText($"##{i}--{j}Name", ref name, 500))
                {
                    config.TPLists[i].TPs[j].Name = name;
                    config.Save();
                }

                if (!hideXYZ)
                {
                    ImGui.NextColumn();
                    var x = config.TPLists[i].TPs[j].X;
                    if (ImGui.InputFloat($"##{i}--{j}X", ref x))
                    {
                        config.TPLists[i].TPs[j].X = x;
                        config.Save();
                    }

                    ImGui.NextColumn();
                    var y = config.TPLists[i].TPs[j].Y;
                    if (ImGui.InputFloat($"##{i}--{j}Y", ref y))
                    {
                        config.TPLists[i].TPs[j].Y = y;
                        config.Save();
                    }

                    ImGui.NextColumn();
                    var z = config.TPLists[i].TPs[j].Z;
                    if (ImGui.InputFloat($"##{i}--{j}Z", ref z))
                    {
                        config.TPLists[i].TPs[j].Z = z;
                        config.Save();
                    }
                }

                ImGui.NextColumn();
                // 传送按钮
                if (ImGui.Button($"传送##{i}--{j}"))
                {
                    if (StaticUtils.IsSameMapId(config.TPLists[i].MapId))
                    {
                        if (Plugin.Configuration.UseDivePacketTpInQuickWindow)
                        {
                            if (!Plugin.GetActivationPro())
                            {
                                Svc.Chat.PrintError("快捷窗口的潜水发包TP需要先通过码2验证。");
                                Plugin.Configuration.UseDivePacketTpInQuickWindow = false;
                                Plugin.Configuration.Save();
                                continue;
                            }

                            StaticUtils.TeleportMeByDivePacket(config.TPLists[i].TPs[j].X, config.TPLists[i].TPs[j].Y,
                                                               config.TPLists[i].TPs[j].Z);
                        }
                        else
                        {
                            StaticUtils.TeleportMe(config.TPLists[i].TPs[j].X, config.TPLists[i].TPs[j].Y,
                                                   config.TPLists[i].TPs[j].Z);
                        }
                    }
                }

                ImGui.NextColumn();
                // 上移按钮
                if (ImGui.Button($"↑##{i}--{j}"))
                {
                    if (j > 0)
                    {
                        (config.TPLists[i].TPs[j - 1], config.TPLists[i].TPs[j]) = (config.TPLists[i].TPs[j], config.TPLists[i].TPs[j - 1]);
                        config.Save();
                    }
                }

                ImGui.NextColumn();
                // 下移按钮
                if (ImGui.Button($"↓##{i}--{j}"))
                {
                    if (j < config.TPLists[i].TPs.Count - 1)
                    {
                        (config.TPLists[i].TPs[j + 1], config.TPLists[i].TPs[j]) = (config.TPLists[i].TPs[j], config.TPLists[i].TPs[j + 1]);
                        config.Save();
                    }
                }

                ImGui.NextColumn();
                // 删除按钮
                if (ImGui.Button($"删除##{i}--{j}"))
                {
                    config.TPLists[i].TPs.RemoveAt(j);
                    config.Save();
                }

                ImGui.NextColumn();
            }

            ImGui.Columns(1);
        }

        public void Dispose() { }
    }
}
