using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using MEC;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class PerformanceDiagnostics
    {
        private sealed class TimingMetric
        {
            internal double TotalMilliseconds;
            internal double MaximumMilliseconds;
            internal int Calls;

            internal void Add(long startTimestamp)
            {
                if (startTimestamp == 0L)
                {
                    return;
                }

                double elapsed = TimestampToMilliseconds(
                    Stopwatch.GetTimestamp() - startTimestamp);
                TotalMilliseconds += elapsed;
                MaximumMilliseconds = Math.Max(MaximumMilliseconds, elapsed);
                Calls++;
            }
        }

        private static readonly List<float> FrameMilliseconds =
            new List<float>(8192);
        private static readonly TimingMetric MecUpdate = new TimingMetric();
        private static readonly TimingMetric MecFixedUpdate = new TimingMetric();
        private static readonly TimingMetric MecLateUpdate = new TimingMetric();
        private static readonly TimingMetric Optimizer = new TimingMetric();

        private static ManualLogSource logger;
        private static float captureStartedAt;
        private static float captureEndsAt;
        private static int friendlyMinimum;
        private static int friendlyMaximum;
        private static int enemyMaximum;
        private static int updateCoroutinesMaximum;
        private static int fixedCoroutinesMaximum;
        private static int lateCoroutinesMaximum;
        private static int rangesProcessed;
        private static int enemiesMaximum;
        private static int disabledRangesMaximum;
        private static int gc0Started;
        private static int gc1Started;
        private static int gc2Started;
        private static long memoryStarted;

        internal static bool IsRecording { get; private set; }

        internal static float SecondsRemaining
        {
            get
            {
                return IsRecording
                    ? Mathf.Max(0f, captureEndsAt - Time.unscaledTime)
                    : 0f;
            }
        }

        internal static void Initialize(ManualLogSource pluginLogger)
        {
            logger = pluginLogger;
        }

        internal static void ToggleCapture(int durationSeconds)
        {
            if (IsRecording)
            {
                FinishCapture("Stopped manually");
                return;
            }

            Reset();
            IsRecording = true;
            captureStartedAt = Time.unscaledTime;
            captureEndsAt = captureStartedAt + durationSeconds;
            gc0Started = GC.CollectionCount(0);
            gc1Started = GC.CollectionCount(1);
            gc2Started = GC.CollectionCount(2);
            memoryStarted = GC.GetTotalMemory(false);
            RecordPopulation();

            if (logger != null)
            {
                logger.LogInfo(
                    "Performance capture started for "
                    + durationSeconds
                    + " seconds.");
            }
        }

        internal static void CancelCapture()
        {
            IsRecording = false;
        }

        internal static void Update()
        {
            if (IsRecording && Time.unscaledTime >= captureEndsAt)
            {
                FinishCapture("Completed");
            }
        }

        internal static void RecordFrame(float unscaledDeltaTime)
        {
            if (!IsRecording || unscaledDeltaTime <= 0f)
            {
                return;
            }

            FrameMilliseconds.Add(unscaledDeltaTime * 1000f);
        }

        internal static void RecordPopulation()
        {
            if (!IsRecording)
            {
                return;
            }

            int friendly = Population.TeamCount;
            int enemies = Population.EnemyCount;
            friendlyMinimum = Math.Min(friendlyMinimum, friendly);
            friendlyMaximum = Math.Max(friendlyMaximum, friendly);
            enemyMaximum = Math.Max(enemyMaximum, enemies);

            try
            {
                Timing timing = Timing.Instance;
                if (timing != null)
                {
                    updateCoroutinesMaximum = Math.Max(
                        updateCoroutinesMaximum,
                        timing.UpdateCoroutines);
                    fixedCoroutinesMaximum = Math.Max(
                        fixedCoroutinesMaximum,
                        timing.FixedUpdateCoroutines);
                    lateCoroutinesMaximum = Math.Max(
                        lateCoroutinesMaximum,
                        timing.LateUpdateCoroutines);
                }
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordMecUpdate(long startTimestamp)
        {
            if (IsRecording)
            {
                MecUpdate.Add(startTimestamp);
            }
        }

        internal static void RecordMecFixedUpdate(long startTimestamp)
        {
            if (IsRecording)
            {
                MecFixedUpdate.Add(startTimestamp);
            }
        }

        internal static void RecordMecLateUpdate(long startTimestamp)
        {
            if (IsRecording)
            {
                MecLateUpdate.Add(startTimestamp);
            }
        }

        internal static void RecordOptimizer(
            long startTimestamp,
            int processed,
            int enemyCount,
            int disabledCount)
        {
            if (!IsRecording)
            {
                return;
            }

            Optimizer.Add(startTimestamp);
            rangesProcessed += processed;
            enemiesMaximum = Math.Max(enemiesMaximum, enemyCount);
            disabledRangesMaximum = Math.Max(
                disabledRangesMaximum,
                disabledCount);
        }

        private static void FinishCapture(string status)
        {
            if (!IsRecording)
            {
                return;
            }

            IsRecording = false;
            float duration = Mathf.Max(
                0.001f,
                Time.unscaledTime - captureStartedAt);
            string report = BuildReport(status, duration);
            string reportPath = Path.Combine(
                Paths.ConfigPath,
                "RatropolisPerformanceReport.txt");

            try
            {
                File.WriteAllText(reportPath, report);
                if (logger != null)
                {
                    logger.LogInfo(
                        "Performance capture finished. Report: "
                        + reportPath);
                    logger.LogInfo(Environment.NewLine + report);
                }
            }
            catch (Exception exception)
            {
                if (logger != null)
                {
                    logger.LogError(
                        "Could not write performance report: "
                        + exception);
                }
            }
        }

        private static string BuildReport(string status, float durationSeconds)
        {
            float[] sortedFrames = FrameMilliseconds.ToArray();
            Array.Sort(sortedFrames);
            double totalFrameMilliseconds = 0d;
            int over50 = 0;
            int over100 = 0;
            int over200 = 0;

            for (int index = 0; index < sortedFrames.Length; index++)
            {
                float milliseconds = sortedFrames[index];
                totalFrameMilliseconds += milliseconds;
                if (milliseconds >= 50f)
                {
                    over50++;
                }
                if (milliseconds >= 100f)
                {
                    over100++;
                }
                if (milliseconds >= 200f)
                {
                    over200++;
                }
            }

            double averageFrameMilliseconds = sortedFrames.Length > 0
                ? totalFrameMilliseconds / sortedFrames.Length
                : 0d;
            double averageFps = averageFrameMilliseconds > 0d
                ? 1000d / averageFrameMilliseconds
                : 0d;
            int gc0 = GC.CollectionCount(0) - gc0Started;
            int gc1 = GC.CollectionCount(1) - gc1Started;
            int gc2 = GC.CollectionCount(2) - gc2Started;
            long memoryDelta = GC.GetTotalMemory(false) - memoryStarted;

            StringBuilder report = new StringBuilder(1600);
            report.AppendLine("Ratropolis Performance Diagnostic");
            report.AppendLine("Mod version: " + Plugin.PluginVersion);
            report.AppendLine("Status: " + status);
            report.AppendLine(
                "Duration: "
                + Format(durationSeconds)
                + " s");
            report.AppendLine();
            report.AppendLine("[Frame Time]");
            report.AppendLine("Samples: " + sortedFrames.Length);
            report.AppendLine("Average FPS: " + Format(averageFps));
            report.AppendLine(
                "Average / P50 / P95 / P99 / Max ms: "
                + Format(averageFrameMilliseconds)
                + " / "
                + Format(Percentile(sortedFrames, 0.50d))
                + " / "
                + Format(Percentile(sortedFrames, 0.95d))
                + " / "
                + Format(Percentile(sortedFrames, 0.99d))
                + " / "
                + Format(Maximum(sortedFrames)));
            report.AppendLine(
                "Frames >= 50 / 100 / 200 ms: "
                + over50
                + " / "
                + over100
                + " / "
                + over200);
            report.AppendLine();
            report.AppendLine("[Population]");
            report.AppendLine(
                "Friendly min / max: "
                + friendlyMinimum
                + " / "
                + friendlyMaximum);
            report.AppendLine("Enemy max: " + enemyMaximum);
            report.AppendLine();
            report.AppendLine("[MEC Coroutine Scheduler]");
            AppendTiming(report, "Update", MecUpdate, durationSeconds);
            AppendTiming(report, "FixedUpdate", MecFixedUpdate, durationSeconds);
            AppendTiming(report, "LateUpdate", MecLateUpdate, durationSeconds);
            report.AppendLine(
                "Max coroutines Update / Fixed / Late: "
                + updateCoroutinesMaximum
                + " / "
                + fixedCoroutinesMaximum
                + " / "
                + lateCoroutinesMaximum);
            report.AppendLine();
            report.AppendLine("[AttackRange Optimizer]");
            AppendTiming(report, "ProcessFrame", Optimizer, durationSeconds);
            report.AppendLine("Ranges processed: " + rangesProcessed);
            report.AppendLine("Enemy scan list max: " + enemiesMaximum);
            report.AppendLine(
                "Disabled friendly range colliders max: "
                + disabledRangesMaximum);
            report.AppendLine();
            report.AppendLine("[Managed GC]");
            report.AppendLine(
                "Collections Gen0 / Gen1 / Gen2: "
                + gc0
                + " / "
                + gc1
                + " / "
                + gc2);
            report.AppendLine(
                "Managed memory delta: "
                + Format(memoryDelta / 1048576d)
                + " MB");
            report.AppendLine();
            report.AppendLine("[Interpretation]");
            report.AppendLine(BuildInterpretation(durationSeconds));
            return report.ToString();
        }

        private static void AppendTiming(
            StringBuilder report,
            string name,
            TimingMetric metric,
            float durationSeconds)
        {
            double average = metric.Calls > 0
                ? metric.TotalMilliseconds / metric.Calls
                : 0d;
            double profileShare = durationSeconds > 0f
                ? metric.TotalMilliseconds / (durationSeconds * 1000d) * 100d
                : 0d;
            report.AppendLine(
                name
                + " total / avg / max ms, capture share: "
                + Format(metric.TotalMilliseconds)
                + " / "
                + Format(average)
                + " / "
                + Format(metric.MaximumMilliseconds)
                + ", "
                + Format(profileShare)
                + "% ("
                + metric.Calls
                + " calls)");
        }

        private static string BuildInterpretation(float durationSeconds)
        {
            double mecMilliseconds =
                MecUpdate.TotalMilliseconds
                + MecFixedUpdate.TotalMilliseconds
                + MecLateUpdate.TotalMilliseconds;
            double mecShare = mecMilliseconds
                / Math.Max(1d, durationSeconds * 1000d)
                * 100d;
            double optimizerShare = Optimizer.TotalMilliseconds
                / Math.Max(1d, durationSeconds * 1000d)
                * 100d;

            if (optimizerShare >= 20d)
            {
                return "The AttackRange optimizer is now a major CPU cost. "
                    + "The next step should optimize its scan and caching.";
            }

            if (mecShare >= 35d)
            {
                return "MEC coroutine work is a major CPU cost. "
                    + "The next step should profile unit AI and movement coroutines.";
            }

            return "Measured MEC and AttackRange work do not dominate the capture. "
                + "Rendering, Spine animation, Unity Physics2D, UI, or frame presentation "
                + "is the likely next area to isolate.";
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

        private static double Maximum(float[] values)
        {
            return values.Length > 0 ? values[values.Length - 1] : 0d;
        }

        private static double TimestampToMilliseconds(long timestampDelta)
        {
            return timestampDelta * 1000d / Stopwatch.Frequency;
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void Reset()
        {
            FrameMilliseconds.Clear();
            ResetMetric(MecUpdate);
            ResetMetric(MecFixedUpdate);
            ResetMetric(MecLateUpdate);
            ResetMetric(Optimizer);
            friendlyMinimum = int.MaxValue;
            friendlyMaximum = 0;
            enemyMaximum = 0;
            updateCoroutinesMaximum = 0;
            fixedCoroutinesMaximum = 0;
            lateCoroutinesMaximum = 0;
            rangesProcessed = 0;
            enemiesMaximum = 0;
            disabledRangesMaximum = 0;
        }

        private static void ResetMetric(TimingMetric metric)
        {
            metric.TotalMilliseconds = 0d;
            metric.MaximumMilliseconds = 0d;
            metric.Calls = 0;
        }
    }
}
