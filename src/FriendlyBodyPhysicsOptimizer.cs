using BepInEx.Logging;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class FriendlyBodyPhysicsOptimizer
    {
        private static bool applied;
        private static bool originalIgnored;
        private static int friendlyLayer = -1;
        private static bool invalidLayerLogged;

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

            friendlyLayer = LayerMask.NameToLayer("MyUnit");
            if (friendlyLayer < 0)
            {
                if (!invalidLayerLogged && logger != null)
                {
                    invalidLayerLogged = true;
                    logger.LogWarning(
                        "MyUnit layer was not found. "
                        + "Friendly body collision optimization was skipped.");
                }
                return;
            }

            originalIgnored = Physics2D.GetIgnoreLayerCollision(
                friendlyLayer,
                friendlyLayer);
            Physics2D.IgnoreLayerCollision(
                friendlyLayer,
                friendlyLayer,
                true);
            applied = true;

            if (logger != null)
            {
                logger.LogInfo(
                    "Friendly body collision optimization applied to "
                    + "MyUnit layer "
                    + friendlyLayer
                    + ". Previous ignore state="
                    + originalIgnored);
            }
        }

        internal static void Restore()
        {
            if (!applied || friendlyLayer < 0)
            {
                return;
            }

            Physics2D.IgnoreLayerCollision(
                friendlyLayer,
                friendlyLayer,
                originalIgnored);
            applied = false;
        }
    }
}
