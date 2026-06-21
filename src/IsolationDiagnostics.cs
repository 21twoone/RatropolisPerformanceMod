using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class IsolationDiagnostics
    {
        private enum TestPhase
        {
            Baseline,
            FrozenSpine,
            DisabledFriendlyPhysics
        }

        private sealed class PhaseResult
        {
            internal readonly string Name;
            internal readonly List<float> FrameMilliseconds =
                new List<float>(2048);

            internal PhaseResult(string name)
            {
                Name = name;
            }
        }

        private sealed class PhysicsState
        {
            internal Collider2D Collider;
            internal bool ColliderEnabled;
            internal Rigidbody2D Rigidbody;
            internal bool RigidbodySimulated;
        }

        private static readonly TestPhase[] Phases =
        {
            TestPhase.Baseline,
            TestPhase.FrozenSpine,
            TestPhase.DisabledFriendlyPhysics
        };

        private static readonly List<PhaseResult> Results =
            new List<PhaseResult>(Phases.Length);
        private static readonly HashSet<int> FriendlySpines =
            new HashSet<int>();
        private static readonly List<PhysicsState> PhysicsStates =
            new List<PhysicsState>(4096);

        private static ManualLogSource logger;
        private static Harmony harmony;
        private static MethodInfo spineUpdateMethod;
        private static MethodInfo spineLateUpdateMethod;
        private static int phaseIndex;
        private static int phaseSeconds;
        private static float phaseEndsAt;

        internal static bool IsRunning { get; private set; }

        internal static string HudText
        {
            get
            {
                if (!IsRunning)
                {
                    return string.Empty;
                }

                return "Test "
                    + (phaseIndex + 1)
                    + "/"
                    + Phases.Length
                    + ": "
                    + PhaseName(Phases[phaseIndex])
                    + " "
                    + Mathf.CeilToInt(
                        Mathf.Max(0f, phaseEndsAt - Time.unscaledTime))
                    + "s";
            }
        }

        internal static void Initialize(
            ManualLogSource pluginLogger,
            Harmony pluginHarmony)
        {
            logger = pluginLogger;
            harmony = pluginHarmony;
        }

        internal static void Toggle(int secondsPerPhase)
        {
            if (IsRunning)
            {
                Finish("Stopped manually");
                return;
            }

            if (!CollectFriendlyComponents())
            {
                if (logger != null)
                {
                    logger.LogWarning(
                        "The isolation test requires an active game with friendly units.");
                }
                return;
            }

            phaseSeconds = secondsPerPhase;
            phaseIndex = 0;
            Results.Clear();
            for (int index = 0; index < Phases.Length; index++)
            {
                Results.Add(new PhaseResult(PhaseName(Phases[index])));
            }

            PatchSpine();
            IsRunning = true;
            ApplyPhase(Phases[phaseIndex]);
            phaseEndsAt = Time.unscaledTime + phaseSeconds;
            LogPhaseStart();
        }

        internal static void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            RestorePhysics();
            UnpatchSpine();
        }

        internal static void RecordFrame(float unscaledDeltaTime)
        {
            if (!IsRunning || unscaledDeltaTime <= 0f)
            {
                return;
            }

            Results[phaseIndex].FrameMilliseconds.Add(
                unscaledDeltaTime * 1000f);
        }

        internal static void Update()
        {
            if (!IsRunning || Time.unscaledTime < phaseEndsAt)
            {
                return;
            }

            RestorePhase(Phases[phaseIndex]);
            phaseIndex++;
            if (phaseIndex >= Phases.Length)
            {
                Finish("Completed");
                return;
            }

            ApplyPhase(Phases[phaseIndex]);
            phaseEndsAt = Time.unscaledTime + phaseSeconds;
            LogPhaseStart();
        }

        private static bool CollectFriendlyComponents()
        {
            FriendlySpines.Clear();
            PhysicsStates.Clear();

            GameMgr game = GameMgr.Instance;
            if (game == null
                || game._PoolMgr == null
                || game._PoolMgr.List_OurUnit == null)
            {
                return false;
            }

            List<GameUnit> units = game._PoolMgr.List_OurUnit;
            for (int index = 0; index < units.Count; index++)
            {
                GameUnit unit = units[index];
                if (unit == null || !unit.gameObject.activeSelf)
                {
                    continue;
                }

                if (unit.m_SpineAni != null)
                {
                    FriendlySpines.Add(unit.m_SpineAni.GetInstanceID());
                }

                PhysicsState state = new PhysicsState
                {
                    Collider = unit.m_Box2d != null
                        ? (Collider2D)unit.m_Box2d
                        : unit.m_Pol2d,
                    Rigidbody = unit.m_rd2d
                };
                if (state.Collider != null)
                {
                    state.ColliderEnabled = state.Collider.enabled;
                }
                if (state.Rigidbody != null)
                {
                    state.RigidbodySimulated = state.Rigidbody.simulated;
                }
                PhysicsStates.Add(state);
            }

            return FriendlySpines.Count > 0;
        }

        private static void PatchSpine()
        {
            spineUpdateMethod = AccessTools.Method(
                typeof(SkeletonAnimation),
                "Update",
                Type.EmptyTypes);
            spineLateUpdateMethod = AccessTools.Method(
                typeof(SkeletonRenderer),
                "LateUpdate",
                Type.EmptyTypes);

            harmony.Patch(
                spineUpdateMethod,
                prefix: new HarmonyMethod(
                    typeof(IsolationDiagnostics),
                    "SpineAnimationPrefix"));
            harmony.Patch(
                spineLateUpdateMethod,
                prefix: new HarmonyMethod(
                    typeof(IsolationDiagnostics),
                    "SpineRendererPrefix"));
        }

        private static void UnpatchSpine()
        {
            if (harmony == null)
            {
                return;
            }

            if (spineUpdateMethod != null)
            {
                harmony.Unpatch(
                    spineUpdateMethod,
                    HarmonyPatchType.Prefix,
                    Plugin.PluginGuid);
            }
            if (spineLateUpdateMethod != null)
            {
                harmony.Unpatch(
                    spineLateUpdateMethod,
                    HarmonyPatchType.Prefix,
                    Plugin.PluginGuid);
            }
        }

        private static bool SpineAnimationPrefix(SkeletonAnimation __instance)
        {
            return !ShouldFreezeSpine(__instance);
        }

        private static bool SpineRendererPrefix(SkeletonRenderer __instance)
        {
            return !ShouldFreezeSpine(__instance);
        }

        private static bool ShouldFreezeSpine(Component component)
        {
            return IsRunning
                && Phases[phaseIndex] == TestPhase.FrozenSpine
                && component != null
                && FriendlySpines.Contains(component.GetInstanceID());
        }

        private static void ApplyPhase(TestPhase phase)
        {
            if (phase == TestPhase.DisabledFriendlyPhysics)
            {
                for (int index = 0; index < PhysicsStates.Count; index++)
                {
                    PhysicsState state = PhysicsStates[index];
                    if (state.Collider != null)
                    {
                        state.Collider.enabled = false;
                    }
                    if (state.Rigidbody != null)
                    {
                        state.Rigidbody.simulated = false;
                    }
                }
            }
        }

        private static void RestorePhase(TestPhase phase)
        {
            if (phase == TestPhase.DisabledFriendlyPhysics)
            {
                RestorePhysics();
            }
        }

        private static void RestorePhysics()
        {
            for (int index = 0; index < PhysicsStates.Count; index++)
            {
                PhysicsState state = PhysicsStates[index];
                if (state.Collider != null)
                {
                    state.Collider.enabled = state.ColliderEnabled;
                }
                if (state.Rigidbody != null)
                {
                    state.Rigidbody.simulated = state.RigidbodySimulated;
                }
            }
        }

        private static void Finish(string status)
        {
            IsRunning = false;
            RestorePhysics();
            UnpatchSpine();

            string report = BuildReport(status);
            string reportPath = Path.Combine(
                Paths.ConfigPath,
                "RatropolisIsolationReport.txt");
            try
            {
                File.WriteAllText(reportPath, report);
                if (logger != null)
                {
                    logger.LogInfo(
                        "Isolation test finished. Report: "
                        + reportPath);
                    logger.LogInfo(Environment.NewLine + report);
                }
            }
            catch (Exception exception)
            {
                if (logger != null)
                {
                    logger.LogError(
                        "Could not write isolation report: "
                        + exception);
                }
            }
        }

        private static string BuildReport(string status)
        {
            StringBuilder report = new StringBuilder(1200);
            report.AppendLine("Ratropolis Bottleneck Isolation Test");
            report.AppendLine("Mod version: " + Plugin.PluginVersion);
            report.AppendLine("Status: " + status);
            report.AppendLine(
                "Friendly Spine components: "
                + FriendlySpines.Count);
            report.AppendLine(
                "Friendly physics bodies sampled: "
                + PhysicsStates.Count);
            report.AppendLine();

            for (int index = 0; index < Results.Count; index++)
            {
                AppendPhase(report, Results[index]);
            }

            report.AppendLine("[Interpretation]");
            report.AppendLine(Interpret());
            return report.ToString();
        }

        private static void AppendPhase(
            StringBuilder report,
            PhaseResult result)
        {
            float[] frames = result.FrameMilliseconds.ToArray();
            Array.Sort(frames);
            double total = 0d;
            int over100 = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                total += frames[index];
                if (frames[index] >= 100f)
                {
                    over100++;
                }
            }

            double average = frames.Length > 0 ? total / frames.Length : 0d;
            double fps = average > 0d ? 1000d / average : 0d;
            report.AppendLine("[" + result.Name + "]");
            report.AppendLine(
                "Samples / average FPS: "
                + frames.Length
                + " / "
                + Format(fps));
            report.AppendLine(
                "Average / P50 / P95 / P99 / Max ms: "
                + Format(average)
                + " / "
                + Format(Percentile(frames, 0.50d))
                + " / "
                + Format(Percentile(frames, 0.95d))
                + " / "
                + Format(Percentile(frames, 0.99d))
                + " / "
                + Format(Maximum(frames)));
            report.AppendLine("Frames >= 100 ms: " + over100);
            report.AppendLine();
        }

        private static string Interpret()
        {
            if (Results.Count < 3)
            {
                return "The test did not complete all phases.";
            }

            double baseline = AverageFrameMilliseconds(Results[0]);
            double frozenSpine = AverageFrameMilliseconds(Results[1]);
            double disabledPhysics = AverageFrameMilliseconds(Results[2]);
            double spineGain = Improvement(baseline, frozenSpine);
            double physicsGain = Improvement(baseline, disabledPhysics);

            if (spineGain >= 20d && spineGain >= physicsGain)
            {
                return "Freezing friendly Spine work produced the largest improvement ("
                    + Format(spineGain)
                    + "% lower average frame time). "
                    + "Animation and mesh update throttling is the next optimization target.";
            }

            if (physicsGain >= 20d)
            {
                return "Disabling friendly Physics2D produced the largest improvement ("
                    + Format(physicsGain)
                    + "% lower average frame time). "
                    + "Friendly body collision and rigidbody simulation is the next optimization target.";
            }

            return "Neither friendly Spine work nor friendly Physics2D changed average frame time by 20%. "
                + "The next test should isolate UI, buildings, effects, and render submission.";
        }

        private static double AverageFrameMilliseconds(PhaseResult result)
        {
            if (result.FrameMilliseconds.Count == 0)
            {
                return 0d;
            }

            double total = 0d;
            for (int index = 0; index < result.FrameMilliseconds.Count; index++)
            {
                total += result.FrameMilliseconds[index];
            }
            return total / result.FrameMilliseconds.Count;
        }

        private static double Improvement(double baseline, double candidate)
        {
            if (baseline <= 0d)
            {
                return 0d;
            }
            return (baseline - candidate) / baseline * 100d;
        }

        private static double Percentile(float[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0)
            {
                return 0d;
            }

            int index = (int)Math.Ceiling(
                percentile * sortedValues.Length) - 1;
            index = Math.Max(0, Math.Min(index, sortedValues.Length - 1));
            return sortedValues[index];
        }

        private static double Maximum(float[] sortedValues)
        {
            return sortedValues.Length > 0
                ? sortedValues[sortedValues.Length - 1]
                : 0d;
        }

        private static string PhaseName(TestPhase phase)
        {
            if (phase == TestPhase.FrozenSpine)
            {
                return "Frozen Spine";
            }
            if (phase == TestPhase.DisabledFriendlyPhysics)
            {
                return "Disabled Physics2D";
            }
            return "Baseline";
        }

        private static void LogPhaseStart()
        {
            if (logger != null)
            {
                logger.LogInfo(
                    "Isolation phase "
                    + (phaseIndex + 1)
                    + "/"
                    + Phases.Length
                    + ": "
                    + PhaseName(Phases[phaseIndex]));
            }
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
