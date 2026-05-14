using ECommons.Reflection;
using System.Collections.Generic;

namespace Teleport
{
    internal static class XCountResults
    {
        public static Dictionary<string, int> CountsDict;
        internal static bool IsXCountInstalled = false;

        internal static void RefreshPlayerCount()
        {
            if (Plugin.Configuration.XCountThreshold > 0 && IsXCountInstalled)
            {
                GetPlayerCount();
            }
        }

        internal static bool AllowTP() => CountsDict?["<all>"] < Plugin.Configuration.XCountThreshold;

        internal static void GetPlayerCount()
        {
            if (DalamudReflector.TryGetDalamudPlugin("XCount", out var xc, false, true))
            {
                IsXCountInstalled = true;
                CountsDict = (Dictionary<string, int>)xc.GetStaticFoP("XCount.CountResults", "CountsDict");
            }
            else
            {
                IsXCountInstalled = false;
            }
        }
    }
}
