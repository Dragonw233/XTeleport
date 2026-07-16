using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using ECommons.Logging;

namespace Teleport.Windows
{
    public class TPListWindow : Window, IDisposable
    {
        private Configuration config;

        public TPListWindow() : base(
            "传送列表")
        {
            config = Plugin.Configuration;
        }

        public void Dispose() { }

        public override void Draw()
        {
            // 显示当前MapID
            ImGui.Text($"当前地图：{Svc.ClientState.TerritoryType}");
            ImGui.SameLine();
            // 添加当前MapID的传送信息组到列表
            if (ImGui.Button("添加当前地图"))
            {
                // 判断是否已经存在于列表中
                if (config.TPLists.Any(x => x.MapId == Svc.ClientState.TerritoryType))
                {
                    Svc.Chat.PrintError("地图已存在！<se.1>");
                    return;
                }

                config.TPLists.Add(new TPList(Svc.ClientState.TerritoryType));
                config.Save();
            }

            // 从剪切板获取TPList
            ImGui.SameLine();
            if (ImGui.Button("从剪切板粘贴"))
            {
                var text = ImGui.GetClipboardText();
                if (text != null)
                {
                    try
                    {
                        var tpList = TPList.FromJson(text);
                        if (tpList != null)
                        {
                            // 判断是否已经存在于列表中
                            if (config.TPLists.Any(x => x.MapId == tpList.MapId))
                            {
                                // 此时，合并列表
                                var oldList = config.TPLists.First(x => x.MapId == tpList.MapId);
                                foreach (var tp in tpList.TPs)
                                {
                                    if (!oldList.TPs.Any(x => x.Name == tp.Name))
                                    {
                                        oldList.TPs.Add(tp);
                                    }
                                }
                            }
                            else
                            {
                                config.TPLists.Add(tpList);
                            }

                            config.Save();
                        }
                    }
                    catch (Exception e)
                    {
                        PluginLog.LogError(e.ToString());
                    }
                }
            }
            ImGui.SameLine();
            var useQuickTP = Plugin.Configuration.UseQuickTp;
            if (ImGui.Checkbox("自动显示快捷传送面板", ref useQuickTP))
            {
                Plugin.Configuration.UseQuickTp = useQuickTP;
                Plugin.Configuration.Save();
            }
            // 遍历传送列表
            for (var i = 0; i < config.TPLists.Count; i++)
            {
                // 判断是否是当前地图
                var isCurrent = config.TPLists[i].MapId == Svc.ClientState.TerritoryType;
                ImGui.PushStyleColor(ImGuiCol.Header, isCurrent ? ImGuiColors.HealerGreen : ImGuiColors.ParsedGrey);
                if (ImGui.CollapsingHeader((isCurrent ? "当前 ->" + config.TPLists[i].MapName : config.TPLists[i].MapName) + $" ID:{config.TPLists[i].MapId}"))
                {
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
                        ImGui.PopStyleColor();
                        continue;
                    }
                    ImGui.SameLine();
                    // 上移
                    if (ImGui.Button($"↑##{i}"))
                    {
                        if (i > 0)
                        {
                            (config.TPLists[i], config.TPLists[i - 1]) = (config.TPLists[i - 1], config.TPLists[i]);
                            config.Save();
                        }
                    }
                    ImGui.SameLine();
                    // 下移
                    if (ImGui.Button($"↓##{i}"))
                    {
                        if (i < config.TPLists.Count - 1)
                        {
                            (config.TPLists[i], config.TPLists[i + 1]) = (config.TPLists[i + 1], config.TPLists[i]);
                            config.Save();
                        }
                    }
                    ImGui.SameLine();
                    var useQuickWin = config.TPLists[i].UseQuickWindow;
                    if (ImGui.Checkbox($"快捷窗口##{i}", ref useQuickWin))
                    {
                        config.TPLists[i].UseQuickWindow = useQuickWin;
                        config.Save();
                    }
                    // 使用列表展示各个传送点信息
                    ImGui.Columns(8);
                    ImGui.Text("名称");
                    ImGui.NextColumn();
                    ImGui.Text("X");
                    ImGui.NextColumn();
                    ImGui.Text("Y");
                    ImGui.NextColumn();
                    ImGui.Text("Z");
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

                        ImGui.NextColumn();
                        // 传送按钮
                        if (ImGui.Button($"传送##{i}--{j}"))
                        {
                            if (StaticUtils.IsSameMapId(config.TPLists[i].MapId))
                            {
                                StaticUtils.TeleportMe(config.TPLists[i].TPs[j].X, config.TPLists[i].TPs[j].Y,
                                                       config.TPLists[i].TPs[j].Z);
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
                ImGui.PopStyleColor();
            }
        }
    }
}
