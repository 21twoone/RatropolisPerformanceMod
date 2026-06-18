using System;
using HarmonyLib;

namespace RatropolisPerformanceMod
{
    [HarmonyPatch(typeof(Log_Controller), "Log")]
    internal static class RoutineLogPatch
    {
        private static bool Prefix(string _logmsg)
        {
            if (Plugin.SuppressRoutineDiskLogs == null
                || !Plugin.SuppressRoutineDiskLogs.Value
                || string.IsNullOrEmpty(_logmsg))
            {
                return true;
            }

            return !_logmsg.StartsWith("MakeAddObj - Name", StringComparison.Ordinal)
                && !_logmsg.StartsWith("Pool Add :", StringComparison.Ordinal);
        }
    }
}
