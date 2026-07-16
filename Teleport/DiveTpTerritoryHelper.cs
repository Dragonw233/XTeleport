using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Teleport;

internal static class DiveTpTerritoryHelper
{
    private static readonly HashSet<uint> DrCnTcIntendedUses =
        [1, 18, 31, 41, 47, 48, 52, 53, 61];

    internal static void EnsureInitialized()
    {
        var config = Plugin.Configuration;
        if (config.DiveTpTerritoriesInitialized && config.DiveTpTerritories is not null)
            return;

        config.DiveTpTerritories = BuildDrDefaultTerritories();
        config.DiveTpTerritoriesInitialized = true;
        config.Save();
    }

    internal static HashSet<uint> BuildDrDefaultTerritories()
    {
        return Svc.Data.GetExcelSheet<TerritoryType>()
                  .Where(x => DrCnTcIntendedUses.Contains(x.TerritoryIntendedUse.RowId))
                  .Select(x => x.RowId)
                  .ToHashSet();
    }

    internal static void ResetToDrDefaults()
    {
        Plugin.Configuration.DiveTpTerritories = BuildDrDefaultTerritories();
        Plugin.Configuration.DiveTpTerritoriesInitialized = true;
        Plugin.Configuration.Save();
    }

    internal static void ReplaceTerritories(IEnumerable<uint> territories)
    {
        Plugin.Configuration.DiveTpTerritories = territories.Where(x => x != 0).ToHashSet();
        Plugin.Configuration.DiveTpTerritoriesInitialized = true;
        Plugin.Configuration.Save();
    }

    internal static bool ShouldUseDivePacket(Vector3 origin, Vector3 target)
    {
        EnsureInitialized();

        var territoryId = Svc.ClientState.TerritoryType;
        if (!Plugin.Configuration.DiveTpTerritories.Contains(territoryId))
            return false;

        if (IsOnlyYAxisChanged(origin, target))
            return false;

        var distanceSquared = Vector3.DistanceSquared(origin, target);
        if (distanceSquared < 8f)
            return false;

        try
        {
            var intendedUse = Svc.Data.GetExcelSheet<TerritoryType>()
                                 .GetRow(territoryId)
                                 .TerritoryIntendedUse.RowId;
            if (intendedUse == 61 && distanceSquared < 9216f)
                return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool IsOnlyYAxisChanged(
        Vector3 origin,
        Vector3 target,
        float xzTolerance = 2f,
        float yTolerance = 0.0001f)
    {
        return Math.Abs(origin.X - target.X) < xzTolerance &&
               Math.Abs(origin.Z - target.Z) < xzTolerance &&
               Math.Abs(origin.Y - target.Y) >= yTolerance;
    }
}
