using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;

namespace Teleport.Windows;

public sealed class QuickTPWindow : Window, IDisposable
{
    private enum PanelPage
    {
        Position,
        SavedPoints,
        MapMarkers,
        Quests,
        Targets
    }

    private readonly Plugin plugin;
    private readonly Configuration config;
    private readonly QuickTeleportMarkerScanner markerScanner;

    private PanelPage selectedPage = PanelPage.Position;
    private Vector3 manualPosition;
    private Vector3 lastPosition;
    private bool hasLastPosition;
    private string listFilter = string.Empty;
    private string markerFilter = string.Empty;

    public QuickTPWindow(Plugin plugin)
        : base("快捷传送面板###XTeleportQuickTeleportPanel")
    {
        this.plugin = plugin;
        config = Plugin.Configuration;
        markerScanner = new QuickTeleportMarkerScanner();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720f, 480f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose()
    {
        markerScanner.Dispose();
    }

    public override void Draw()
    {
        var player = StaticUtils.LocalPlayer;
        if (player is null)
        {
            ImGui.TextDisabled("当前没有可用角色。");
            return;
        }

        DrawHeader(player.Position);
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Columns(2, "QuickTeleportPanelLayout", false);
        ImGui.SetColumnWidth(0, 150f);
        DrawNavigation();
        ImGui.NextColumn();

        switch (selectedPage)
        {
            case PanelPage.Position:
                DrawPositionPage(player.Position);
                break;
            case PanelPage.SavedPoints:
                DrawSavedPointsPage();
                break;
            case PanelPage.MapMarkers:
                DrawMarkerPage(false);
                break;
            case PanelPage.Quests:
                DrawMarkerPage(true);
                break;
            case PanelPage.Targets:
                DrawTargetsPage();
                break;
        }

        ImGui.Columns(1);
    }

    internal void TogglePanel()
    {
        IsOpen = !IsOpen;
    }

    private void DrawHeader(Vector3 playerPosition)
    {
        var territoryId = Svc.ClientState.TerritoryType;
        ImGui.TextColored(ImGuiColors.TankBlue, GetTerritoryName(territoryId));
        ImGui.SameLine();
        ImGui.TextDisabled($"#{territoryId}");
        ImGui.SameLine();
        ImGui.TextDisabled($"X {playerPosition.X:F2}  Y {playerPosition.Y:F2}  Z {playerPosition.Z:F2}");

        var autoOpen = config.UseQuickTp;
        if (ImGui.Checkbox("自动显示", ref autoOpen))
        {
            config.UseQuickTp = autoOpen;
            config.Save();
        }

        var currentList = GetCurrentList();
        if (currentList is not null)
        {
            ImGui.SameLine();
            var autoOpenHere = currentList.UseQuickWindow;
            if (ImGui.Checkbox("本地图自动显示", ref autoOpenHere))
            {
                currentList.UseQuickWindow = autoOpenHere;
                config.Save();
            }
        }

        ImGui.SameLine();
        var hideCoordinates = config.HideXYZ;
        if (ImGui.Checkbox("隐藏列表坐标", ref hideCoordinates))
        {
            config.HideXYZ = hideCoordinates;
            config.Save();
        }

        ImGui.SameLine();
        var useDivePacket = config.UseDivePacketTpInQuickWindow;
        if (ImGui.Checkbox("DR 智能潜水TP", ref useDivePacket))
        {
            if (useDivePacket && !Plugin.GetActivationPro())
            {
                Svc.Chat.PrintError("DR 智能潜水TP需要先通过码2验证。");
                useDivePacket = false;
            }

            config.UseDivePacketTpInQuickWindow = useDivePacket;
            config.Save();
        }
    }

    private void DrawNavigation()
    {
        DrawNavigationButton(PanelPage.Position, "坐标传送");
        DrawNavigationButton(PanelPage.SavedPoints, "传送列表");
        DrawNavigationButton(PanelPage.MapMarkers, "地图标记");
        DrawNavigationButton(PanelPage.Quests, "任务目标");
        DrawNavigationButton(PanelPage.Targets, "标记与队伍");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var width = ImGui.GetContentRegionAvail().X;
        if (ImGui.Button("主界面", new Vector2(width, 0f)))
            plugin.DrawMain();

        if (ImGui.Button("完整列表", new Vector2(width, 0f)))
            plugin.DrawList();

        if (ImGui.Button("设置", new Vector2(width, 0f)))
            plugin.DrawConfigUI();
    }

    private void DrawNavigationButton(PanelPage page, string label)
    {
        var selected = selectedPage == page;
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.TankBlue);

        if (ImGui.Button(label, new Vector2(ImGui.GetContentRegionAvail().X, 38f)))
            selectedPage = page;

        if (selected)
            ImGui.PopStyleColor();
    }

    private void DrawPositionPage(Vector3 playerPosition)
    {
        DrawSectionTitle("快速目标");

        var buttonWidth = Math.Max(80f, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2f) / 3f);
        if (ImGui.Button("地图旗标", new Vector2(buttonWidth, 0f)))
            TeleportToFlag();

        ImGui.SameLine();
        if (ImGui.Button("鼠标位置", new Vector2(buttonWidth, 0f)))
            TeleportToMouse();

        ImGui.SameLine();
        if (ImGui.Button("当前目标", new Vector2(buttonWidth, 0f)))
            TeleportToTarget();

        ImGui.Spacing();
        DrawSectionTitle("坐标输入");
        ImGui.SetNextItemWidth(Math.Min(360f, ImGui.GetContentRegionAvail().X));
        ImGui.InputFloat3("目标坐标", ref manualPosition);
        if (ImGui.Button("传送到坐标"))
            TeleportTo(manualPosition);

        ImGui.SameLine();
        if (ImGui.Button("填入当前位置"))
            manualPosition = playerPosition;

        if (hasLastPosition)
        {
            ImGui.SameLine();
            if (ImGui.Button("返回上一个位置"))
                TeleportTo(lastPosition, rememberCurrentPosition: false);
        }

        ImGui.Spacing();
        DrawSectionTitle("轴向偏移");

        var offset = config.QuickTpOffsetDistance;
        ImGui.SetNextItemWidth(150f);
        if (ImGui.InputFloat("偏移步长", ref offset, 0.5f, 5f, "%.1f"))
        {
            config.QuickTpOffsetDistance = Math.Clamp(Math.Abs(offset), 0.1f, 1000f);
            config.Save();
        }

        DrawOffsetAxis("X", Vector3.UnitX, playerPosition);
        DrawOffsetAxis("Y", Vector3.UnitY, playerPosition);
        DrawOffsetAxis("Z", Vector3.UnitZ, playerPosition);

        ImGui.Spacing();
        DrawSectionTitle("保存到当前地图");
        if (ImGui.Button("添加当前位置"))
            AddPoint("当前位置", playerPosition);

        ImGui.SameLine();
        if (ImGui.Button("添加鼠标位置"))
            AddMousePoint();
    }

    private void DrawOffsetAxis(string axis, Vector3 direction, Vector3 playerPosition)
    {
        var delta = config.QuickTpOffsetDistance;
        ImGui.TextDisabled(axis);
        ImGui.SameLine(28f);
        if (ImGui.Button($"-{delta:0.#}##Offset{axis}Minus", new Vector2(90f, 0f)))
            TeleportTo(playerPosition - direction * delta);

        ImGui.SameLine();
        if (ImGui.Button($"+{delta:0.#}##Offset{axis}Plus", new Vector2(90f, 0f)))
            TeleportTo(playerPosition + direction * delta);
    }

    private void DrawSavedPointsPage()
    {
        DrawSectionTitle("当前地图传送列表");

        var list = GetCurrentList();
        if (list is null)
        {
            ImGui.TextDisabled("当前地图还没有传送列表。");
            if (ImGui.Button("创建当前地图列表"))
            {
                list = EnsureCurrentList();
                list.UseQuickWindow = true;
                config.Save();
            }

            return;
        }

        if (ImGui.Button("添加当前位置"))
            AddPoint("当前位置", StaticUtils.LocalPlayer!.Position);

        ImGui.SameLine();
        if (ImGui.Button("添加鼠标位置"))
            AddMousePoint();

        ImGui.SameLine();
        if (ImGui.Button("添加空白"))
        {
            list.TPs.Add(new TP());
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("导出"))
            ImGui.SetClipboardText(list.ToJson());

        ImGui.SameLine();
        if (ImGui.Button("导入"))
            ImportCurrentList();

        ImGui.SetNextItemWidth(280f);
        ImGui.InputText("筛选", ref listFilter, 128);

        var tableFlags = ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.BordersInnerH |
                         ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("CurrentTerritoryTeleportList", 4, tableFlags))
            return;

        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch, 1.2f);
        ImGui.TableSetupColumn("坐标", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("传送", ImGuiTableColumnFlags.WidthFixed, 72f);
        ImGui.TableSetupColumn("管理", ImGuiTableColumnFlags.WidthFixed, 122f);
        ImGui.TableHeadersRow();

        for (var index = 0; index < list.TPs.Count; index++)
        {
            var point = list.TPs[index];
            if (!string.IsNullOrWhiteSpace(listFilter) &&
                !point.Name.Contains(listFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(index);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var name = point.Name;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText("##PointName", ref name, 256))
            {
                point.Name = name;
                config.Save();
            }

            ImGui.TableNextColumn();
            var position = point.GetPos();
            if (config.HideXYZ)
            {
                ImGui.TextUnformatted($"{position.X:F2}, {position.Y:F2}, {position.Z:F2}");
            }
            else
            {
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputFloat3("##PointPosition", ref position))
                {
                    point.X = position.X;
                    point.Y = position.Y;
                    point.Z = position.Z;
                    config.Save();
                }
            }

            ImGui.TableNextColumn();
            if (ImGui.Button("传送", new Vector2(-1f, 0f)))
                TeleportTo(point.GetPos());

            ImGui.TableNextColumn();
            var deleted = false;
            if (ImGui.SmallButton("↑") && index > 0)
            {
                (list.TPs[index - 1], list.TPs[index]) = (list.TPs[index], list.TPs[index - 1]);
                config.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("↓") && index < list.TPs.Count - 1)
            {
                (list.TPs[index + 1], list.TPs[index]) = (list.TPs[index], list.TPs[index + 1]);
                config.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("删除"))
            {
                list.TPs.RemoveAt(index);
                config.Save();
                deleted = true;
            }

            ImGui.PopID();
            if (deleted)
                break;
        }

        ImGui.EndTable();
    }

    private void DrawTargetsPage()
    {
        DrawSectionTitle("场地标点");
        var waymarks = new[] { "A", "B", "C", "D", "1", "2", "3", "4" };
        for (var index = 0; index < waymarks.Length; index++)
        {
            if (index > 0)
                ImGui.SameLine();

            var waymark = waymarks[index];
            var position = Vector3.Zero;
            var exists = StaticUtils.TryGetWayMarkPos(waymark, ref position);
            if (!exists)
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.45f);

            if (ImGui.Button($"{waymark}##Waymark", new Vector2(44f, 0f)) && exists)
                TeleportTo(position);

            if (!exists)
                ImGui.PopStyleVar();
        }

        ImGui.Spacing();
        DrawSectionTitle("当前目标");
        var target = StaticUtils.CurrentTarget;
        if (target is null)
        {
            ImGui.TextDisabled("未选择目标。");
        }
        else
        {
            DrawTargetRow(target.Name.TextValue, target.Position, target.ObjectKind.ToString(), "CurrentTarget");
        }

        ImGui.Spacing();
        DrawSectionTitle("小队成员");
        var members = StaticUtils.GetPartyMembersByVisualOrder();
        if (members.Count == 0)
        {
            ImGui.TextDisabled("当前没有可用的小队成员位置。");
            return;
        }

        foreach (var (uiIndex, member) in members)
        {
            if (member.ContentId == StaticUtils.LocalContentId)
                continue;

            DrawTargetRow(member.Name.TextValue, member.Position, $"队伍位置 {uiIndex + 1}", $"Party{uiIndex}");
        }
    }

    private void DrawMarkerPage(bool quests)
    {
        markerScanner.RefreshIfNeeded();
        var markers = quests ? markerScanner.QuestMarkers : markerScanner.MapMarkers;

        DrawSectionTitle(quests ? "任务目标" : "地图标记");
        if (ImGui.Button("刷新"))
        {
            markerScanner.RefreshIfNeeded(true);
            markers = quests ? markerScanner.QuestMarkers : markerScanner.MapMarkers;
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"已扫描 {markers.Count} 个");

        if (quests && !string.IsNullOrEmpty(markerScanner.PendingInteractionName))
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.HealerGreen, $"等待交互：{markerScanner.PendingInteractionName}");
        }

        if (!string.IsNullOrEmpty(markerScanner.LastScanError))
            ImGui.TextColored(ImGuiColors.DalamudRed, $"扫描失败：{markerScanner.LastScanError}");

        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("筛选##MarkerFilter", ref markerFilter, 128);

        if (markers.Count == 0)
        {
            ImGui.TextDisabled(quests
                ? "当前没有扫描到活动任务标记或任务对象。"
                : "当前没有扫描到可用地图标记。");
            return;
        }

        var tableFlags = ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.BordersInnerH |
                         ImGuiTableFlags.SizingStretchProp |
                         ImGuiTableFlags.ScrollY;
        var columnCount = quests ? 4 : 3;
        if (!ImGui.BeginTable(
                quests ? "QuickTeleportQuestMarkers" : "QuickTeleportMapMarkers",
                columnCount,
                tableFlags,
                new Vector2(0f, ImGui.GetContentRegionAvail().Y)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("标记", ImGuiTableColumnFlags.WidthStretch, 1.4f);
        ImGui.TableSetupColumn("位置", ImGuiTableColumnFlags.WidthStretch, 1.1f);
        ImGui.TableSetupColumn("传送", ImGuiTableColumnFlags.WidthFixed, 76f);
        if (quests)
            ImGui.TableSetupColumn("任务操作", ImGuiTableColumnFlags.WidthFixed, 112f);
        ImGui.TableHeadersRow();

        var playerPosition = StaticUtils.LocalPlayer?.Position ?? Vector3.Zero;
        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            if (!MarkerMatchesFilter(marker))
                continue;

            ImGui.PushID(index);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(marker.Name);
            ImGui.TextDisabled($"{marker.Source}  图标 #{marker.IconId}");
            if (!string.IsNullOrWhiteSpace(marker.ExtraInfo))
                ImGui.TextDisabled(marker.ExtraInfo);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{marker.Position.X:F2}, {marker.Position.Y:F2}, {marker.Position.Z:F2}");
            var distance = Vector3.Distance(playerPosition, marker.Position);
            ImGui.TextDisabled($"距离 {distance:F1}m  半径 {marker.Radius:F1}m");
            if (!marker.IsYAxisCorrect)
                ImGui.TextColored(ImGuiColors.DalamudYellow, "高度沿用当前位置");

            ImGui.TableNextColumn();
            if (ImGui.Button("传送", new Vector2(-1f, 0f)))
                TeleportTo(marker.Position);

            if (quests)
            {
                ImGui.TableNextColumn();
                if (ImGui.Button("传送并交互", new Vector2(-1f, 0f)))
                {
                    TeleportTo(marker.Position);
                    markerScanner.QueueQuestInteraction(marker);
                }
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private bool MarkerMatchesFilter(QuickTeleportMarker marker)
    {
        if (string.IsNullOrWhiteSpace(markerFilter))
            return true;

        return marker.Name.Contains(markerFilter, StringComparison.OrdinalIgnoreCase) ||
               marker.Source.Contains(markerFilter, StringComparison.OrdinalIgnoreCase) ||
               marker.ExtraInfo.Contains(markerFilter, StringComparison.OrdinalIgnoreCase) ||
               marker.IconId.ToString().Contains(markerFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawTargetRow(string name, Vector3 position, string description, string id)
    {
        ImGui.PushID(id);
        if (ImGui.BeginTable("TargetRow", 3, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80f);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(name);
            ImGui.TextDisabled(description);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{position.X:F2}, {position.Y:F2}, {position.Z:F2}");
            ImGui.TableNextColumn();
            if (ImGui.Button("传送", new Vector2(-1f, 0f)))
                TeleportTo(position);
            ImGui.EndTable();
        }

        ImGui.PopID();
        ImGui.Separator();
    }

    private static void DrawSectionTitle(string title)
    {
        ImGui.TextColored(ImGuiColors.TankBlue, title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void TeleportTo(Vector3 position, bool rememberCurrentPosition = true)
    {
        var player = StaticUtils.LocalPlayer;
        if (player is null)
            return;

        if (rememberCurrentPosition)
        {
            lastPosition = player.Position;
            hasLastPosition = true;
        }

        StaticUtils.TeleportSmartInZone(position);
    }

    private void TeleportToMouse()
    {
        if (TryGetMousePosition(out var position))
            TeleportTo(position);
        else
            Svc.Chat.PrintError("鼠标当前没有指向可用的世界位置。");
    }

    private void TeleportToTarget()
    {
        var target = StaticUtils.CurrentTarget;
        if (target is null)
        {
            Svc.Chat.PrintError("请先选择目标。");
            return;
        }

        TeleportTo(target.Position);
    }

    private void TeleportToFlag()
    {
        if (TryGetFlagPosition(out var position))
            TeleportTo(position);
        else
            Svc.Chat.PrintError("当前地图没有可用旗标。");
    }

    private void TeleportToWaymark(string waymark)
    {
        var position = Vector3.Zero;
        if (!StaticUtils.TryGetWayMarkPos(waymark, ref position))
        {
            Svc.Chat.PrintError("场地标点不存在，支持 A/B/C/D/1/2/3/4。");
            return;
        }

        TeleportTo(position);
    }

    private void AddMousePoint()
    {
        if (!TryGetMousePosition(out var position))
        {
            Svc.Chat.PrintError("鼠标当前没有指向可用的世界位置。");
            return;
        }

        AddPoint("鼠标位置", position);
    }

    private void AddPoint(string name, Vector3 position)
    {
        EnsureCurrentList().TPs.Add(new TP(name, position));
        config.Save();
    }

    private TPList EnsureCurrentList()
    {
        var current = GetCurrentList();
        if (current is not null)
            return current;

        current = new TPList(Svc.ClientState.TerritoryType);
        config.TPLists.Add(current);
        config.Save();
        return current;
    }

    private TPList? GetCurrentList()
    {
        var territoryId = Svc.ClientState.TerritoryType;
        return config.TPLists.FirstOrDefault(x => x.MapId == territoryId);
    }

    private void ImportCurrentList()
    {
        var text = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            var imported = TPList.FromJson(text);
            if (imported is null || imported.MapId != Svc.ClientState.TerritoryType)
            {
                Svc.Chat.PrintError("剪切板中的列表不属于当前地图。");
                return;
            }

            var current = EnsureCurrentList();
            foreach (var point in imported.TPs)
            {
                if (!current.TPs.Any(x => x.Name == point.Name && x.GetPos() == point.GetPos()))
                    current.TPs.Add(point);
            }

            config.Save();
        }
        catch (Exception exception)
        {
            Svc.Chat.PrintError($"导入传送列表失败：{exception.Message}");
        }
    }

    private static bool TryGetMousePosition(out Vector3 position)
    {
        return Svc.GameGui.ScreenToWorld(ImGui.GetMousePos(), out position, 100000f);
    }

    private static unsafe bool TryGetFlagPosition(out Vector3 position)
    {
        position = Vector3.Zero;
        var player = StaticUtils.LocalPlayer;
        var agentMap = AgentMap.Instance();
        if (player is null || agentMap is null || agentMap->FlagMarkerCount == 0)
            return false;

        var marker = agentMap->FlagMapMarkers[0];
        if (marker.TerritoryId != Svc.ClientState.TerritoryType)
            return false;

        position = new Vector3(marker.XFloat, player.Position.Y, marker.YFloat);
        return true;
    }

    private static string GetTerritoryName(uint territoryId)
    {
        try
        {
            return Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territoryId).PlaceName.Value.Name.ToString();
        }
        catch
        {
            return "未知地图";
        }
    }

}
