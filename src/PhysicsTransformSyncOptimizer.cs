using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class PhysicsTransformSyncOptimizer
    {
        private static bool applied;
        private static bool originalAutoSync;

        internal static void ProcessFrame(ManualLogSource logger)
        {
            bool active =
                Plugin.IsHeavyLoad
                && Plugin.OptimizeFriendlyBodyCollisions != null
                && Plugin.OptimizeFriendlyBodyCollisions.Value;

            if (!active)
            {
                Restore();
                return;
            }

            if (applied)
            {
                return;
            }

            originalAutoSync = Physics2D.autoSyncTransforms;
            Physics2D.autoSyncTransforms = false;
            applied = true;

            if (logger != null)
            {
                logger.LogInfo(
                    "Physics2D transform synchronization is now batched. "
                    + "Previous auto-sync state="
                    + originalAutoSync);
            }
        }

        internal static void SyncAfterMovement()
        {
            if (applied)
            {
                Physics2D.SyncTransforms();
            }
        }

        internal static void Restore()
        {
            if (!applied)
            {
                return;
            }

            Physics2D.SyncTransforms();
            Physics2D.autoSyncTransforms = originalAutoSync;
            applied = false;
        }
    }

    [HarmonyPatch(typeof(CocosFunc), "Update")]
    internal static class CocosMovementPhysicsSyncPatch
    {
        private static void Postfix()
        {
            PhysicsTransformSyncOptimizer.SyncAfterMovement();
        }
    }
}
