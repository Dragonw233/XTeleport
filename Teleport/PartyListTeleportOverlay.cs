using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.NativeWrapper;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;

namespace Teleport;

internal sealed class PartyListTeleportOverlay
{
    private const string AddonName = "_PartyList";
    private const string ButtonLabel = "\u4F20";
    private const string DisabledTooltip =
        "\u9700\u8981\u5148\u901A\u8FC7\u78012\u9A8C\u8BC1\uFF0C\u624D\u80FD\u4F7F\u7528\u961F\u53CB\u4F20\u9001\u3002";

    internal void Draw()
    {
        if (!Plugin.Configuration.ShowPartyListTeleportButtons)
            return;

        if (Svc.Party.Length <= 1)
            return;

        var addon = Svc.GameGui.GetAddonByName(AddonName, 1);
        if (addon.IsNull || !addon.IsReady || !addon.IsVisible)
            return;

        DrawOverlay(addon);
    }

    private void DrawOverlay(AtkUnitBasePtr addon)
    {
        var origin = addon.Position;
        var size = addon.ScaledSize;
        var scale = addon.Scale;
        var buttonWidth = Plugin.Configuration.PartyListTeleportButtonWidth;
        var rowHeight = Plugin.Configuration.PartyListTeleportRowHeight * scale;
        var xOffset = Plugin.Configuration.PartyListTeleportXOffset;
        var yOffset = Plugin.Configuration.PartyListTeleportYOffset * scale;
        var showOnLeft = Plugin.Configuration.PartyListTeleportButtonsOnLeft;

        var overlayX = showOnLeft
            ? origin.X - buttonWidth - xOffset
            : origin.X + size.X + xOffset;
        var overlayPos = new Vector2(overlayX, origin.Y + yOffset);
        var overlaySize = new Vector2(buttonWidth, size.Y);

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(overlayPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(overlaySize, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoBringToFrontOnFocus;

        if (!ImGui.Begin("PartyListTeleportOverlay###PartyListTeleportOverlay", flags))
        {
            ImGui.End();
            return;
        }

        var visualMembers = GetVisualPartyMembers();
        var canUsePartyTeleport = StaticUtils.CanUsePartyTeleport();

        for (var visualIndex = 0; visualIndex < visualMembers.Count; visualIndex++)
        {
            var y = visualMembers[visualIndex].UiIndex * rowHeight;
            ImGui.SetCursorPos(new Vector2(0f, y));

            if (!canUsePartyTeleport)
                ImGui.BeginDisabled();

            if (ImGui.SmallButton($"{ButtonLabel}##party-overlay-{visualIndex}"))
                StaticUtils.TeleportToPartyMember(visualMembers[visualIndex].Member);

            if (!canUsePartyTeleport)
            {
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(DisabledTooltip);
            }
        }

        ImGui.End();
    }

    private static List<(int UiIndex, IPartyMember Member)> GetVisualPartyMembers()
    {
        var members = new List<(int UiIndex, IPartyMember Member)>(Svc.Party.Length);
        var localPlayer = StaticUtils.LocalPlayer;
        if (localPlayer == null)
            return members;

        foreach (var entry in StaticUtils.GetPartyMembersByVisualOrder())
        {
            if (entry.Member.ContentId == StaticUtils.LocalContentId ||
                entry.Member.GameObject?.GameObjectId == localPlayer.GameObjectId)
            {
                continue;
            }

            members.Add(entry);
        }

        return members;
    }
}
