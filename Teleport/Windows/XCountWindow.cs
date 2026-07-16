using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using System;
using System.Linq;
using System.Numerics;

namespace Teleport.Windows;

public class XCountWindow : Window, IDisposable
{
    public XCountWindow() : base("周边安全检测")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        XCountResults.RefreshPlayerCount();
        var unsafePlayers = XCountResults.EffectiveNearbyPlayerCount;

        ImGui.Separator();
        ImGui.Text($"本地周边人数：{XCountResults.NearbyPlayerCount}");
        ImGui.Text($"白名单人数：{XCountResults.WhitelistedNearbyPlayerCount}");
        if (XCountResults.IsNearbySafe)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "当前状态：安全");
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DPSRed, $"周边绿玩：{unsafePlayers} 个");
        }

        var ignoreUnsafePlayers = Plugin.Configuration.IgnoreUnsafePlayersForTP;
        if (ImGui.Checkbox("无视周边绿玩", ref ignoreUnsafePlayers))
        {
            Plugin.Configuration.IgnoreUnsafePlayersForTP = ignoreUnsafePlayers;
            Plugin.Configuration.Save();
        }

        ImGui.TextColored(ImGuiColors.TankBlue, "周边绿玩会用于传送安全检测。");

        var threshold = Plugin.Configuration.XCountThreshold;
        if (ImGui.InputInt("人数阈值", ref threshold))
        {
            Plugin.Configuration.XCountThreshold = Math.Max(0, threshold);
            Plugin.Configuration.Save();
        }

        var whitelist = Plugin.Configuration.XCountWhitelist;
        if (ImGui.InputText("白名单", ref whitelist, 2048))
        {
            Plugin.Configuration.XCountWhitelist = whitelist;
            Plugin.Configuration.Save();
            XCountResults.RefreshPlayerCount();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("多个角色名请用 | 分隔。");

        ImGui.Separator();
        ImGui.Text("周边玩家：");
        var whitelistNames = XCountResults.GetWhitelistNames();
        var nearbyPlayers = XCountResults.GetNearbyPlayerNames();
        if (nearbyPlayers.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "当前没有可添加的周边玩家。");
        }
        else
        {
            foreach (var playerName in nearbyPlayers)
            {
                var isWhitelisted = whitelistNames.Contains(playerName);
                ImGui.TextUnformatted(playerName);
                ImGui.SameLine();

                if (isWhitelisted)
                {
                    ImGui.BeginDisabled();
                    ImGui.SmallButton($"已添加##nearby-{playerName}");
                    ImGui.EndDisabled();
                }
                else if (ImGui.SmallButton($"添加##nearby-{playerName}"))
                {
                    XCountResults.AddPlayerToWhitelist(playerName);
                    whitelistNames = XCountResults.GetWhitelistNames();
                }
            }
        }

        ImGui.Separator();
        ImGui.Text($"当前地图：{Svc.ClientState.TerritoryType}");
        var partySummaries = StaticUtils.GetPartyMemberPositionSummaries().ToList();
        ImGui.Text("队友坐标：");
        if (partySummaries.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "当前没有可显示的队友坐标。");
            return;
        }

        foreach (var member in partySummaries)
            ImGui.TextWrapped(member);
    }
}
