using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Teleport
{
    internal static class XCountResults
    {
        public static Dictionary<string, int> CountsDict = new();

        internal static void RefreshPlayerCount()
        {
            RefreshLocalCount();
        }

        internal static int NearbyPlayerCount => GetCount("<all>");

        internal static int WhitelistedNearbyPlayerCount => GetCount("<whitelist>");

        internal static int EffectiveNearbyPlayerCount => GetCount("<unsafe>");

        internal static bool IsNearbySafe => EffectiveNearbyPlayerCount <= 0;

        internal static bool AllowTP() =>
            Plugin.Configuration.IgnoreUnsafePlayersForTP ||
            Plugin.Configuration.XCountThreshold <= 0 ||
            EffectiveNearbyPlayerCount < Plugin.Configuration.XCountThreshold;

        internal static HashSet<string> GetWhitelistNames() =>
            Plugin.Configuration.XCountWhitelist
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        internal static List<string> GetNearbyPlayerNames()
        {
            var localPlayer = StaticUtils.LocalPlayer;
            if (localPlayer == null)
                return [];

            return Svc.Objects
                .OfType<IPlayerCharacter>()
                .Where(player => player.GameObjectId != localPlayer.GameObjectId && player.Address != IntPtr.Zero)
                .Select(player => player.Name.TextValue)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static bool AddPlayerToWhitelist(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return false;

            var whitelist = GetWhitelistNames();
            if (!whitelist.Add(playerName))
                return false;

            Plugin.Configuration.XCountWhitelist = string.Join('|', whitelist.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            Plugin.Configuration.Save();
            RefreshPlayerCount();
            return true;
        }

        private static void RefreshLocalCount()
        {
            var localPlayer = StaticUtils.LocalPlayer;
            if (localPlayer == null)
            {
                CountsDict["<all>"] = 0;
                CountsDict["<whitelist>"] = 0;
                CountsDict["<unsafe>"] = 0;
                return;
            }

            var whitelist = GetWhitelistNames();
            var nearbyPlayers = Svc.Objects
                .OfType<IPlayerCharacter>()
                .Where(player => player.GameObjectId != localPlayer.GameObjectId && player.Address != IntPtr.Zero)
                .ToList();

            CountsDict["<all>"] = nearbyPlayers.Count;
            CountsDict["<whitelist>"] = nearbyPlayers.Count(player => whitelist.Contains(player.Name.TextValue));
            CountsDict["<unsafe>"] = nearbyPlayers.Count(player => !whitelist.Contains(player.Name.TextValue));
        }

        private static int GetCount(string key) =>
            CountsDict.TryGetValue(key, out var value) ? value : 0;
    }
}
