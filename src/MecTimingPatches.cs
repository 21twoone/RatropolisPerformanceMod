using System.Diagnostics;
using HarmonyLib;
using MEC;

namespace RatropolisPerformanceMod
{
    [HarmonyPatch(typeof(Timing), "Update")]
    internal static class MecUpdateTimingPatch
    {
        private static void Prefix(ref long __state)
        {
            __state = PerformanceDiagnostics.IsRecording
                ? Stopwatch.GetTimestamp()
                : 0L;
        }

        private static void Postfix(long __state)
        {
            PerformanceDiagnostics.RecordMecUpdate(__state);
        }
    }

    [HarmonyPatch(typeof(Timing), "FixedUpdate")]
    internal static class MecFixedUpdateTimingPatch
    {
        private static void Prefix(ref long __state)
        {
            __state = PerformanceDiagnostics.IsRecording
                ? Stopwatch.GetTimestamp()
                : 0L;
        }

        private static void Postfix(long __state)
        {
            PerformanceDiagnostics.RecordMecFixedUpdate(__state);
        }
    }

    [HarmonyPatch(typeof(Timing), "LateUpdate")]
    internal static class MecLateUpdateTimingPatch
    {
        private static void Prefix(ref long __state)
        {
            __state = PerformanceDiagnostics.IsRecording
                ? Stopwatch.GetTimestamp()
                : 0L;
        }

        private static void Postfix(long __state)
        {
            PerformanceDiagnostics.RecordMecLateUpdate(__state);
        }
    }
}
