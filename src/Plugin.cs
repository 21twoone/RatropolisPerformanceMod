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
        public const string PluginVersion = "1.1.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowCompactHud;
        internal static ConfigEntry<int> UnitThreshold;
        internal static ConfigEntry<bool> SuppressRoutineDiskLogs;
        internal static ConfigEntry<bool> OptimizeFriendlyAttackRanges;
        internal static ConfigEntry<bool> OptimizeFriendlyBodyCollisions;
        internal static ConfigEntry<int> FriendlyRangeBatchSize;
        internal static ConfigEntry<int> DiagnosticDurationSeconds;
        internal static ConfigEntry<int> IsolationPhaseSeconds;
        internal static ConfigEntry<bool> LowVisualEffects;
        internal static ConfigEntry<bool> CrowdDisplayEnabled;
        internal static ConfigEntry<bool> CrowdUltraMode;
        internal static ConfigEntry<int> CrowdDisplayThreshold;
        internal static ConfigEntry<int> CrowdDisplayRatio;

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
                "Show the compact FPS and optimizer status in the top-left corner.");
            UnitThreshold = Config.Bind("General", "UnitThreshold", 80,
                "Enable the attack-range optimization at or above this total unit count.");
            SuppressRoutineDiskLogs = Config.Bind("I/O", "SuppressRoutineDiskLogs", true,
                "Skip high-volume object-pool lines that synchronously reopen RatLog.txt.");
            OptimizeFriendlyAttackRanges = Config.Bind("Physics", "OptimizeFriendlyAttackRanges", true,
                "Replace friendly AttackRange trigger colliders with a centralized distance scanner.");
            OptimizeFriendlyBodyCollisions = Config.Bind(
                "Physics",
                "OptimizeFriendlyBodyCollisions",
                true,
                "Disable MyUnit-to-MyUnit Physics2D contacts. F6 toggles this with AttackRange.");
            FriendlyRangeBatchSize = Config.Bind("Physics", "FriendlyRangeBatchSize", 256,
                "Number of friendly attack ranges refreshed per frame.");
            DiagnosticDurationSeconds = Config.Bind("Diagnostics", "DurationSeconds", 30,
                "Length of an internal performance capture in seconds.");
            IsolationPhaseSeconds = Config.Bind("Diagnostics", "IsolationPhaseSeconds", 8,
                "Length of each internal isolation-test phase in seconds.");
            LowVisualEffects = Config.Bind(
                "Visual",
                "LowVisualEffects",
                false,
                "Disable animated water reflection and GUI post-processing.");
            CrowdDisplayEnabled = Config.Bind(
                "Visual",
                "CrowdDisplayEnabled",
                true,
                "Render a representative fraction of very large friendly armies.");
            CrowdUltraMode = Config.Bind(
                "Visual",
                "CrowdUltraMode",
                true,
                "Start crowd display in Ultra mode. F7 cycles Ultra, ratio, and off.");
            CrowdDisplayThreshold = Config.Bind(
                "Visual",
                "CrowdDisplayThreshold",
                500,
                "Minimum friendly unit count before crowd display reduction starts.");
            CrowdDisplayRatio = Config.Bind(
                "Visual",
                "CrowdDisplayRatio",
                10,
                "Show one friendly unit mesh for every N real units.");

            UnitThreshold.Value = Mathf.Max(UnitThreshold.Value, 1);
            FriendlyRangeBatchSize.Value =
                Mathf.Clamp(FriendlyRangeBatchSize.Value, 32, 2048);
            DiagnosticDurationSeconds.Value =
                Mathf.Clamp(DiagnosticDurationSeconds.Value, 10, 120);
            IsolationPhaseSeconds.Value =
                Mathf.Clamp(IsolationPhaseSeconds.Value, 5, 20);
            CrowdDisplayThreshold.Value =
                Mathf.Clamp(CrowdDisplayThreshold.Value, 100, 5000);
            CrowdDisplayRatio.Value = NormalizeCrowdRatio(
                CrowdDisplayRatio.Value);

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            PerformanceDiagnostics.Initialize(Logger);
            IsolationDiagnostics.Initialize(Logger, harmony);

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

            PerformanceDiagnostics.RecordFrame(unscaledDelta);
            IsolationDiagnostics.RecordFrame(unscaledDelta);

            if (Time.unscaledTime >= nextPopulationRefresh)
            {
                nextPopulationRefresh = Time.unscaledTime + 0.5f;
                Population.Refresh();
                PerformanceDiagnostics.RecordPopulation();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                bool enabled =
                    !(
                        OptimizeFriendlyAttackRanges.Value
                        && OptimizeFriendlyBodyCollisions.Value
                    );
                OptimizeFriendlyAttackRanges.Value = enabled;
                OptimizeFriendlyBodyCollisions.Value = enabled;
                if (!enabled)
                {
                    FriendlyRangeOptimizer.RestoreAll();
                    FriendlyBodyPhysicsOptimizer.Restore();
                    PhysicsTransformSyncOptimizer.Restore();
                }

                Config.Save();
                Logger.LogInfo(
                    "Core optimizations "
                    + (enabled ? "enabled" : "disabled"));
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                CycleCrowdDisplayMode();
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                ChangeCrowdRatio(10);
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                ChangeCrowdRatio(-10);
            }

            CrowdDisplayOptimizer.ProcessFrame();
            FriendlyBodyPhysicsOptimizer.ProcessFrame(Logger);
            PhysicsTransformSyncOptimizer.ProcessFrame(Logger);
            VisualEffectsOptimizer.ProcessFrame();
            FriendlyRangeOptimizer.ProcessFrame();
            PerformanceDiagnostics.Update();
            IsolationDiagnostics.Update();
        }

        private void OnGUI()
        {
            if (ShowCompactHud == null || !ShowCompactHud.Value)
            {
                return;
            }

            EnsureHudStyles();

            bool optimizerEnabled =
                Enabled.Value
                && OptimizeFriendlyAttackRanges.Value
                && OptimizeFriendlyBodyCollisions.Value;
            string text =
                "FPS " + Mathf.RoundToInt(smoothedFps)
                + " | Optimizer " + (optimizerEnabled ? "ON" : "OFF")
                + " | Crowd " + (
                    CrowdDisplayEnabled.Value
                        ? (
                            CrowdUltraMode.Value
                                ? "ULTRA"
                                : "ON 1:" + CrowdDisplayRatio.Value
                        )
                        : "OFF");

            GUI.Label(new Rect(13f, 13f, 420f, 24f), text, hudShadowStyle);
            GUI.Label(new Rect(12f, 12f, 420f, 24f), text, hudStyle);

            if (PerformanceDiagnostics.IsRecording)
            {
                string diagnosticText =
                    "Profiling "
                    + Mathf.CeilToInt(PerformanceDiagnostics.SecondsRemaining)
                    + "s";
                GUI.Label(
                    new Rect(13f, 31f, 260f, 24f),
                    diagnosticText,
                    hudShadowStyle);
                GUI.Label(
                    new Rect(12f, 30f, 260f, 24f),
                    diagnosticText,
                    hudStyle);
            }
            else if (IsolationDiagnostics.IsRunning)
            {
                string diagnosticText = IsolationDiagnostics.HudText;
                GUI.Label(
                    new Rect(13f, 31f, 360f, 24f),
                    diagnosticText,
                    hudShadowStyle);
                GUI.Label(
                    new Rect(12f, 30f, 360f, 24f),
                    diagnosticText,
                    hudStyle);
            }
        }

        private void ChangeCrowdRatio(int change)
        {
            int nextRatio = CrowdDisplayRatio.Value;
            if (change > 0 && nextRatio <= int.MaxValue - change)
            {
                nextRatio += change;
            }
            else if (change < 0)
            {
                nextRatio = Mathf.Max(10, nextRatio + change);
            }

            if (nextRatio == CrowdDisplayRatio.Value)
            {
                return;
            }

            CrowdDisplayRatio.Value = nextRatio;
            Config.Save();
            Logger.LogInfo(
                "Crowd display ratio changed to 1:"
                + CrowdDisplayRatio.Value);
        }

        private void CycleCrowdDisplayMode()
        {
            bool turningOff = false;
            if (!CrowdDisplayEnabled.Value)
            {
                CrowdDisplayEnabled.Value = true;
                CrowdUltraMode.Value = true;
            }
            else if (CrowdUltraMode.Value)
            {
                CrowdUltraMode.Value = false;
            }
            else
            {
                CrowdDisplayEnabled.Value = false;
                CrowdUltraMode.Value = false;
                turningOff = true;
            }

            if (turningOff)
            {
                CrowdDisplayOptimizer.RestoreAll();
            }
            else
            {
                CrowdDisplayOptimizer.RebuildSelection();
            }
            Config.Save();
            Logger.LogInfo("Crowd display mode: " + CrowdModeText());
        }

        private static string CrowdModeText()
        {
            if (!CrowdDisplayEnabled.Value)
            {
                return "OFF";
            }

            return CrowdUltraMode.Value
                ? "ULTRA"
                : "1:" + CrowdDisplayRatio.Value;
        }

        private static int NormalizeCrowdRatio(int value)
        {
            if (value < 10)
            {
                return 10;
            }

            return value - value % 10;
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
            IsolationDiagnostics.Cancel();
            PerformanceDiagnostics.CancelCapture();
            CrowdDisplayOptimizer.RestoreAll();
            FriendlyBodyPhysicsOptimizer.Restore();
            PhysicsTransformSyncOptimizer.Restore();
            VisualEffectsOptimizer.Restore();
            FriendlyRangeOptimizer.RestoreAll();
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }
    }
}
