using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.ratropolis.performance";
        public const string PluginName = "Ratropolis Performance Mod";
        public const string PluginVersion = "1.0.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowCompactHud;
        internal static ConfigEntry<int> UnitThreshold;
        internal static ConfigEntry<bool> SuppressRoutineDiskLogs;
        internal static ConfigEntry<bool> OptimizeFriendlyAttackRanges;
        internal static ConfigEntry<int> FriendlyRangeBatchSize;

        private Harmony harmony;
        private float nextPopulationRefresh;
        private float smoothedFps;
        private GUIStyle hudStyle;
        private GUIStyle hudShadowStyle;

        internal static bool IsHeavyLoad
        {
            get
            {
                return Enabled != null
                    && Enabled.Value
                    && Population.TotalCount >= UnitThreshold.Value;
            }
        }

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch for all performance optimizations.");
            ShowCompactHud = Config.Bind("General", "ShowCompactHud", true,
                "Show the compact FPS and optimizer status in the top-left corner. Press F7 to toggle.");
            UnitThreshold = Config.Bind("General", "UnitThreshold", 80,
                "Enable the attack-range optimization at or above this total unit count.");
            SuppressRoutineDiskLogs = Config.Bind("I/O", "SuppressRoutineDiskLogs", true,
                "Skip high-volume object-pool lines that synchronously reopen RatLog.txt.");
            OptimizeFriendlyAttackRanges = Config.Bind("Physics", "OptimizeFriendlyAttackRanges", true,
                "Replace friendly AttackRange trigger colliders with a centralized distance scanner.");
            FriendlyRangeBatchSize = Config.Bind("Physics", "FriendlyRangeBatchSize", 256,
                "Number of friendly attack ranges refreshed per frame.");

            UnitThreshold.Value = Mathf.Max(UnitThreshold.Value, 1);
            FriendlyRangeBatchSize.Value =
                Mathf.Clamp(FriendlyRangeBatchSize.Value, 32, 2048);

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo(
                PluginName + " " + PluginVersion
                + " loaded. Threshold=" + UnitThreshold.Value
                + ", range batch=" + FriendlyRangeBatchSize.Value);
        }

        private void Update()
        {
            float unscaledDelta = Time.unscaledDeltaTime;
            if (unscaledDelta > 0.0001f)
            {
                float currentFps = 1f / unscaledDelta;
                smoothedFps = smoothedFps <= 0f
                    ? currentFps
                    : Mathf.Lerp(smoothedFps, currentFps, 0.08f);
            }

            if (Time.unscaledTime >= nextPopulationRefresh)
            {
                nextPopulationRefresh = Time.unscaledTime + 0.5f;
                Population.Refresh();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                OptimizeFriendlyAttackRanges.Value =
                    !OptimizeFriendlyAttackRanges.Value;
                if (!OptimizeFriendlyAttackRanges.Value)
                {
                    FriendlyRangeOptimizer.RestoreAll();
                }

                Config.Save();
                Logger.LogInfo(
                    "Friendly attack-range optimization "
                    + (OptimizeFriendlyAttackRanges.Value ? "enabled" : "disabled"));
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                ShowCompactHud.Value = !ShowCompactHud.Value;
                Config.Save();
            }

            FriendlyRangeOptimizer.ProcessFrame();
        }

        private void OnGUI()
        {
            if (ShowCompactHud == null || !ShowCompactHud.Value)
            {
                return;
            }

            EnsureHudStyles();

            bool optimizerEnabled =
                Enabled.Value && OptimizeFriendlyAttackRanges.Value;
            string text =
                "FPS " + Mathf.RoundToInt(smoothedFps)
                + " | Optimizer " + (optimizerEnabled ? "ON" : "OFF");

            GUI.Label(new Rect(13f, 13f, 260f, 24f), text, hudShadowStyle);
            GUI.Label(new Rect(12f, 12f, 260f, 24f), text, hudStyle);
        }

        private void EnsureHudStyles()
        {
            if (hudStyle != null)
            {
                return;
            }

            hudStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(0, 0, 0, 0)
            };
            hudStyle.normal.textColor = Color.white;

            hudShadowStyle = new GUIStyle(hudStyle);
            hudShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        private void OnDestroy()
        {
            FriendlyRangeOptimizer.RestoreAll();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }
    }
}
