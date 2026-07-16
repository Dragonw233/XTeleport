#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using LuminaAetheryte = Lumina.Excel.Sheets.Aetheryte;
using LuminaMap = Lumina.Excel.Sheets.Map;

namespace Teleport;

internal sealed record QuickTeleportMarker(
    string Name,
    uint IconId,
    Vector3 Position,
    float Radius,
    string Source,
    string ExtraInfo,
    bool IsQuest,
    bool CanInteract,
    uint QuestRowId = 0,
    bool IsYAxisCorrect = true);

internal sealed class QuickTeleportMarkerScanner : IDisposable
{
    private sealed class PendingQuestInteraction
    {
        internal required QuickTeleportMarker Marker { get; init; }
        internal required uint TerritoryId { get; init; }
        internal required long Deadline { get; init; }
        internal long NextAttempt { get; set; }
        internal int PositionAdjustments { get; set; }
    }

    private readonly Dictionary<uint, List<QuickTeleportMarker>> staticMarkerCache = new();
    private long lastScanAt;
    private uint lastTerritoryId;
    private PendingQuestInteraction? pendingInteraction;
    private bool disposed;

    internal IReadOnlyList<QuickTeleportMarker> MapMarkers { get; private set; } = [];
    internal IReadOnlyList<QuickTeleportMarker> QuestMarkers { get; private set; } = [];
    internal string LastScanError { get; private set; } = string.Empty;
    internal string PendingInteractionName => pendingInteraction?.Marker.Name ?? string.Empty;

    internal QuickTeleportMarkerScanner()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Svc.Framework.Update -= OnFrameworkUpdate;
        pendingInteraction = null;
    }

    internal void RefreshIfNeeded(bool force = false)
    {
        var territoryId = Svc.ClientState.TerritoryType;
        var now = Environment.TickCount64;
        var territoryChanged = territoryId != lastTerritoryId;
        if (!force && territoryId == lastTerritoryId && now - lastScanAt < 1000)
            return;

        if (territoryChanged)
        {
            MapMarkers = [];
            QuestMarkers = [];
        }

        lastTerritoryId = territoryId;
        lastScanAt = now;
        LastScanError = string.Empty;

        if (territoryId == 0 || StaticUtils.LocalPlayer is null)
        {
            MapMarkers = [];
            QuestMarkers = [];
            return;
        }

        try
        {
            Scan(territoryId);
        }
        catch (Exception exception)
        {
            LastScanError = exception.Message;
            PluginLog.Warning($"刷新快捷传送地图标记失败：{exception}");
        }
    }

    internal void QueueQuestInteraction(QuickTeleportMarker marker)
    {
        var now = Environment.TickCount64;
        pendingInteraction = new PendingQuestInteraction
        {
            Marker = marker,
            TerritoryId = Svc.ClientState.TerritoryType,
            NextAttempt = now + 120,
            Deadline = now + 3500
        };
    }

    private unsafe void Scan(uint territoryId)
    {
        var mapMarkers = new List<QuickTeleportMarker>();
        var questMarkers = new List<QuickTeleportMarker>();

        var playerY = StaticUtils.LocalPlayer?.Position.Y ?? 0f;
        mapMarkers.AddRange(GetStaticMarkers(territoryId).Select(marker => marker.IsYAxisCorrect
            ? marker
            : marker with
            {
                Position = new Vector3(marker.Position.X, playerY, marker.Position.Z)
            }));

        var agentHud = AgentHUD.Instance();
        if (agentHud != null)
        {
            foreach (var marker in agentHud->MapMarkers.AsSpan())
                AddDynamicMarker(marker, territoryId, "地图任务", mapMarkers, questMarkers);
        }

        var agentMap = AgentMap.Instance();
        if (agentMap != null)
        {
            foreach (var marker in agentMap->EventMarkers.AsSpan())
                AddDynamicMarker(marker, territoryId, "地图事件", mapMarkers, questMarkers);
        }

        var activeQuests = GetActiveQuests();
        AddActiveQuestObjects(activeQuests, questMarkers);

        var playerPosition = StaticUtils.LocalPlayer?.Position ?? Vector3.Zero;
        MapMarkers = Deduplicate(mapMarkers)
            .OrderBy(marker => Vector3.DistanceSquared(playerPosition, marker.Position))
            .ThenBy(marker => marker.Name, StringComparer.CurrentCulture)
            .ToArray();
        QuestMarkers = Deduplicate(questMarkers)
            .OrderBy(marker => Vector3.DistanceSquared(playerPosition, marker.Position))
            .ThenBy(marker => marker.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    private List<QuickTeleportMarker> GetStaticMarkers(uint territoryId)
    {
        if (staticMarkerCache.TryGetValue(territoryId, out var cached))
            return cached;

        var result = new List<QuickTeleportMarker>();
        var maps = Svc.Data.GetExcelSheet<LuminaMap>()
            .Where(map => map.TerritoryType.RowId == territoryId && map.MapMarkerRange != 0)
            .ToArray();

        if (maps.Length > 0)
        {
            var mapsByMarkerRange = maps
                .GroupBy(map => (uint)map.MapMarkerRange)
                .ToDictionary(group => group.Key, group => group.ToArray());

            foreach (var markerCollection in Svc.Data.GetSubrowExcelSheet<MapMarker>())
            {
                foreach (var marker in markerCollection)
                {
                    if (marker.Icon == 0 || !mapsByMarkerRange.TryGetValue(marker.RowId, out var markerMaps))
                        continue;

                    foreach (var map in markerMaps)
                    {
                        var name = GetStaticMarkerName(marker);
                        if (string.IsNullOrWhiteSpace(name))
                            name = $"地图标记 {marker.Icon}";

                        var world = TextureToWorld(new Vector2(marker.X, marker.Y), map);
                        var y = StaticUtils.LocalPlayer?.Position.Y ?? 0f;
                        result.Add(new QuickTeleportMarker(
                            name,
                            marker.Icon,
                            new Vector3(world.X, y, world.Y),
                            1f,
                            GetStaticMarkerSource(marker.DataType),
                            $"Map #{map.RowId}",
                            false,
                            false,
                            IsYAxisCorrect: false));
                    }
                }
            }
        }

        foreach (var warp in Svc.Data.GetExcelSheet<Warp>().Where(warp => warp.TerritoryType.RowId == territoryId))
        {
            if (!warp.PopRange.ValueNullable.HasValue)
                continue;

            var level = warp.PopRange.Value;
            var name = !string.IsNullOrWhiteSpace(warp.Name.ToString())
                ? warp.Name.ToString()
                : warp.Question.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = "区域出口";

            result.Add(new QuickTeleportMarker(
                name,
                60441,
                new Vector3(level.X, level.Y, level.Z),
                level.Radius,
                "区域出口",
                string.Empty,
                false,
                false));
        }

        staticMarkerCache[territoryId] = result;
        return result;
    }

    private static string GetStaticMarkerName(MapMarker marker)
    {
        try
        {
            switch (marker.DataType)
            {
                case 3:
                    return Svc.Data.GetExcelSheet<LuminaAetheryte>()
                        .GetRow(marker.DataKey.RowId)
                        .PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
                case 4:
                    return Svc.Data.GetExcelSheet<PlaceName>()
                        .GetRow(marker.DataKey.RowId)
                        .Name.ToString();
            }

            var label = marker.PlaceNameSubtext.ValueNullable?.Name.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            return Svc.Data.GetExcelSheet<MapSymbol>()
                .GetRow(marker.Icon)
                .PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetStaticMarkerSource(byte dataType)
    {
        return dataType switch
        {
            3 => "以太之光",
            4 => "以太网",
            _ => "静态地图点"
        };
    }

    private static Vector2 TextureToWorld(Vector2 texture, LuminaMap map)
    {
        var scale = map.SizeFactor / 100f;
        return (texture - new Vector2(1024f)) / scale - new Vector2(map.OffsetX, map.OffsetY);
    }

    private static unsafe void AddDynamicMarker(
        MapMarkerData marker,
        uint territoryId,
        string source,
        ICollection<QuickTeleportMarker> mapMarkers,
        ICollection<QuickTeleportMarker> questMarkers)
    {
        if (marker.IconId == 0 || marker.IconId == 60490)
            return;
        if (marker.TerritoryTypeId != 0 && marker.TerritoryTypeId != territoryId)
            return;
        if (marker.TooltipString == null || marker.TooltipString->IsEmpty)
            return;

        var name = marker.TooltipString->ToString();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var position = marker.Position;
        var radius = marker.Radius;
        if (marker.LevelId != 0)
        {
            try
            {
                var level = Svc.Data.GetExcelSheet<Level>().GetRow(marker.LevelId);
                position = new Vector3(level.X, level.Y, level.Z);
                radius = level.Radius;
            }
            catch
            {
                // The marker-provided coordinates are the fallback used by the game UI.
            }
        }

        var isQuest = QuickTeleportQuestIcons.IsQuest(marker.IconId);
        var extraInfo = marker.RecommendedLevel > 0 ? $"推荐等级 {marker.RecommendedLevel}" : string.Empty;
        var result = new QuickTeleportMarker(
            name,
            marker.IconId,
            position,
            radius,
            source,
            extraInfo,
            isQuest,
            isQuest && QuickTeleportQuestIcons.IsCompletionMarker(marker.IconId));

        if (isQuest)
            questMarkers.Add(result);
        else
            mapMarkers.Add(result);
    }

    private static unsafe Dictionary<uint, ActiveQuestInfo> GetActiveQuests()
    {
        var result = new Dictionary<uint, ActiveQuestInfo>();
        var questManager = QuestManager.Instance();
        if (questManager == null)
            return result;

        foreach (var questWork in questManager->NormalQuests)
        {
            if (questWork.QuestId == 0 || questWork.IsHidden)
                continue;

            var rowId = (uint)(questWork.QuestId + 65536);
            try
            {
                var quest = Svc.Data.GetExcelSheet<Quest>().GetRow(rowId);
                var iconId = quest.EventIconType.ValueNullable?.MapIconAvailable + 1u ?? 71021u;
                result[rowId] = new ActiveQuestInfo(quest.Name.ToString(), iconId);
            }
            catch
            {
                // A missing localized row should not prevent the remaining active quests from scanning.
            }
        }

        return result;
    }

    private static unsafe void AddActiveQuestObjects(
        IReadOnlyDictionary<uint, ActiveQuestInfo> activeQuests,
        ICollection<QuickTeleportMarker> questMarkers)
    {
        if (activeQuests.Count == 0)
            return;

        var eventFramework = EventFramework.Instance();
        if (eventFramework == null)
            return;

        foreach (var handlerPointer in eventFramework->EventHandlerModule.EventHandlerMap.Values)
        {
            var handler = handlerPointer.Value;
            if (handler == null || handler->Info.EventId.ContentId != EventHandlerContent.Quest)
                continue;

            var questRowId = handler->Info.EventId.Id;
            if (!activeQuests.TryGetValue(questRowId, out var quest) || handler->EventObjects.Count == 0)
                continue;

            foreach (var objectPointer in handler->EventObjects)
            {
                var gameObject = objectPointer.Value;
                if (gameObject == null || gameObject->RenderFlags != VisibilityFlags.None)
                    continue;

                var canInteract = gameObject->TargetableStatus.HasFlag(ObjectTargetableFlags.IsTargetable);
                questMarkers.Add(new QuickTeleportMarker(
                    quest.Name,
                    quest.IconId,
                    gameObject->Position,
                    1f,
                    "活动任务对象",
                    canInteract ? "可交互" : "暂不可交互",
                    true,
                    canInteract,
                    questRowId));
            }
        }
    }

    private static IEnumerable<QuickTeleportMarker> Deduplicate(IEnumerable<QuickTeleportMarker> markers)
    {
        var seen = new HashSet<MarkerKey>();
        foreach (var marker in markers)
        {
            var key = new MarkerKey(
                marker.IconId,
                marker.QuestRowId,
                marker.Name,
                (int)MathF.Round(marker.Position.X * 10f),
                (int)MathF.Round(marker.Position.Y * 10f),
                (int)MathF.Round(marker.Position.Z * 10f));
            if (seen.Add(key))
                yield return marker;
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        var pending = pendingInteraction;
        if (pending is null)
            return;

        var now = Environment.TickCount64;
        if (now < pending.NextAttempt)
            return;
        if (now >= pending.Deadline)
        {
            Svc.Chat.PrintError($"没有找到可交互的任务目标：{pending.Marker.Name}");
            pendingInteraction = null;
            return;
        }
        if (pending.TerritoryId != Svc.ClientState.TerritoryType)
        {
            pendingInteraction = null;
            return;
        }

        try
        {
            var gameObject = FindQuestObject(pending.Marker);
            var player = StaticUtils.LocalPlayer;
            if (gameObject == null || player is null)
            {
                pending.NextAttempt = now + 100;
                return;
            }

            var objectPosition = gameObject->Position;
            if (Vector3.DistanceSquared(player.Position, objectPosition) > 16f)
            {
                if (pending.PositionAdjustments++ < 2)
                    StaticUtils.TeleportSmartInZone(objectPosition);

                pending.NextAttempt = now + 120;
                return;
            }

            var targetSystem = TargetSystem.Instance();
            targetSystem->SetHardTarget(gameObject, true);
            targetSystem->InteractWithObject(gameObject);
            pendingInteraction = null;
        }
        catch (Exception exception)
        {
            PluginLog.Warning($"与任务目标交互失败：{exception}");
            pending.NextAttempt = now + 150;
        }
    }

    private static unsafe GameObject* FindQuestObject(QuickTeleportMarker marker)
    {
        var activeQuests = GetActiveQuests();
        if (activeQuests.Count == 0)
            return null;

        var eventFramework = EventFramework.Instance();
        if (eventFramework == null)
            return null;

        GameObject* nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var handlerPointer in eventFramework->EventHandlerModule.EventHandlerMap.Values)
        {
            var handler = handlerPointer.Value;
            if (handler == null || handler->Info.EventId.ContentId != EventHandlerContent.Quest)
                continue;

            var questRowId = handler->Info.EventId.Id;
            if (!activeQuests.ContainsKey(questRowId) ||
                (marker.QuestRowId != 0 && marker.QuestRowId != questRowId))
                continue;

            foreach (var objectPointer in handler->EventObjects)
            {
                var gameObject = objectPointer.Value;
                if (gameObject == null || gameObject->RenderFlags != VisibilityFlags.None ||
                    !gameObject->TargetableStatus.HasFlag(ObjectTargetableFlags.IsTargetable))
                    continue;

                var distance = Vector3.DistanceSquared(marker.Position, gameObject->Position);
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = gameObject;
            }
        }

        return nearest;
    }

    private readonly record struct ActiveQuestInfo(string Name, uint IconId);

    private readonly record struct MarkerKey(
        uint IconId,
        uint QuestRowId,
        string Name,
        int X,
        int Y,
        int Z);
}

internal static class QuickTeleportQuestIcons
{
    private static readonly HashSet<uint> Quest =
    [
        70961, 70965, 70967, 70971, 70973, 70981, 70985, 70987, 70991, 70993,
        71001, 71002, 71021, 71022, 71041, 71042, 71061, 71062, 71081, 71082,
        71111, 71122, 71141, 71142, 71201, 71202, 71221, 72222, 71241, 71242,
        71261, 71262, 71281, 71282, 71311, 71341, 71342,
        70963, 70969, 70975, 70983, 70989, 70995, 71003, 71005, 71006, 71023,
        71025, 71026, 71043, 71045, 71046, 71063, 71065, 71066, 71083, 71085,
        71086, 71112, 71113, 71123, 71125, 71126, 71143, 71145, 71146, 71203,
        71205, 71206, 71223, 71225, 71226, 71243, 71245, 71246, 71263, 71265,
        71266, 71283, 71285, 71286, 71312, 71313, 71322, 71323, 71325, 71326,
        71343, 71345, 71346, 71224,
        70962, 70964, 70966, 70968, 70970, 70972, 70974, 70976, 70982, 70984,
        70986, 70988, 70990, 70992, 70994, 70996, 71011, 71012, 71013, 71015,
        71016, 71031, 71032, 71033, 71035, 71036, 71051, 71052, 71053, 71055,
        71056, 71071, 71072, 71073, 71075, 71076, 71091, 71092, 71093, 71095,
        71096, 71131, 71132, 71133, 71135, 71136, 71151, 71152, 71153, 71155,
        71156, 71211, 71212, 71213, 71215, 71216, 71231, 71232, 71233, 71235,
        71236, 71251, 71252, 71253, 71255, 71256, 71271, 71272, 71273, 71275,
        71276, 71291, 71292, 71293, 71295, 71296, 71331, 71332, 71333, 71335,
        71336, 71351, 71352, 71353, 71355, 71356
    ];

    private static readonly HashSet<uint> Completion =
    [
        70963, 70969, 70975, 70983, 70989, 70995, 71003, 71005, 71006, 71023,
        71025, 71026, 71043, 71045, 71046, 71063, 71065, 71066, 71083, 71085,
        71086, 71112, 71113, 71123, 71125, 71126, 71143, 71145, 71146, 71203,
        71205, 71206, 71223, 71225, 71226, 71243, 71245, 71246, 71263, 71265,
        71266, 71283, 71285, 71286, 71312, 71313, 71322, 71323, 71325, 71326,
        71343, 71345, 71346, 71224
    ];

    internal static bool IsQuest(uint iconId) => Quest.Contains(iconId);
    internal static bool IsCompletionMarker(uint iconId) => Completion.Contains(iconId);
}
