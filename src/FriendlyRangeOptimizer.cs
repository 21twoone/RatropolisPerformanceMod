using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class FriendlyRangeOptimizer
    {
        private sealed class ColliderState
        {
            internal BoxCollider2D Collider;
            internal bool WasEnabled;
        }

        private static readonly Dictionary<int, ColliderState> Disabled =
            new Dictionary<int, ColliderState>();

        private static readonly List<GameUnit> Enemies = new List<GameUnit>();
        private static readonly HashSet<GameUnit> Overlapping = new HashSet<GameUnit>();
        private static readonly HashSet<GameUnit> StaleSet = new HashSet<GameUnit>();
        private static readonly List<GameUnit> Stale = new List<GameUnit>();

        private static List<GameUnit> teamUnits;
        private static int teamCursor;
        private static float maxEnemyHalfWidth = 1f;

        internal static bool Active
        {
            get
            {
                return Plugin.IsHeavyLoad
                    && Plugin.OptimizeFriendlyAttackRanges.Value;
            }
        }

        internal static int DisabledCount
        {
            get { return Disabled.Count; }
        }

        internal static void ProcessFrame()
        {
            if (!Active)
            {
                RestoreAll();
                return;
            }

            if (Time.timeScale <= 0f)
            {
                return;
            }

            GameMgr game = GameMgr.Instance;
            if (game == null || game._PoolMgr == null || game._WaveMgr == null)
            {
                return;
            }

            long diagnosticStart = PerformanceDiagnostics.IsRecording
                ? Stopwatch.GetTimestamp()
                : 0L;

            if (teamUnits == null || teamCursor >= teamUnits.Count)
            {
                BeginCycle(game);
            }

            int end = Mathf.Min(
                teamCursor + Plugin.FriendlyRangeBatchSize.Value,
                teamUnits.Count);
            int processed = end - teamCursor;

            for (; teamCursor < end; teamCursor++)
            {
                RefreshUnit(teamUnits[teamCursor]);
            }

            PerformanceDiagnostics.RecordOptimizer(
                diagnosticStart,
                processed,
                Enemies.Count,
                Disabled.Count);
        }

        internal static void RegisterIfNeeded(AttackRange range)
        {
            if (!Active || range == null || range.m_Master == null)
            {
                return;
            }

            if (range.m_Master.m_UnitIndex == UnitIndex.Team)
            {
                Disable(range.m_BoxCollider);
            }
        }

        internal static void RestoreAll()
        {
            foreach (ColliderState state in Disabled.Values)
            {
                if (state.Collider != null)
                {
                    AttackRange range = state.Collider.GetComponentInParent<AttackRange>();
                    GameUnit master = range != null ? range.m_Master : null;
                    bool canRestore =
                        master != null
                        && master.gameObject.activeSelf
                        && master.m_State != UnitState.Death;
                    state.Collider.enabled = canRestore && state.WasEnabled;
                }
            }

            Disabled.Clear();
            teamUnits = null;
            teamCursor = 0;
        }

        private static void BeginCycle(GameMgr game)
        {
            teamUnits = game._PoolMgr.List_OurUnit;
            teamCursor = 0;
            Enemies.Clear();
            maxEnemyHalfWidth = 1f;

            AddEnemies(game._WaveMgr.List_Enemy);
            AddEnemies(game._WaveMgr.List_Zombie);
            Enemies.Sort(CompareByX);
        }

        private static void AddEnemies(List<GameUnit> source)
        {
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                GameUnit enemy = source[index];
                if (!IsActiveEnemy(enemy))
                {
                    continue;
                }

                Enemies.Add(enemy);
                Collider2D collider = GetBodyCollider(enemy);
                if (collider != null)
                {
                    maxEnemyHalfWidth = Mathf.Max(
                        maxEnemyHalfWidth,
                        collider.bounds.extents.x);
                }
            }
        }

        private static void RefreshUnit(GameUnit unit)
        {
            if (unit == null
                || unit.m_UnitIndex != UnitIndex.Team
                || !unit.gameObject.activeSelf
                || unit.m_State == UnitState.Death
                || unit.m_AttackRange == null
                || unit.m_AttackRange.m_BoxCollider == null)
            {
                return;
            }

            AttackRange range = unit.m_AttackRange;
            Disable(range.m_BoxCollider);

            Overlapping.Clear();
            Stale.Clear();
            StaleSet.Clear();
            float scaleX = Mathf.Abs(range.m_BoxCollider.transform.lossyScale.x);
            float halfWidth = range.m_BoxCollider.size.x * scaleX * 0.5f;
            float centerX = range.m_BoxCollider.transform
                .TransformPoint(range.m_BoxCollider.offset).x;
            float queryMin = centerX - halfWidth - maxEnemyHalfWidth;
            float queryMax = centerX + halfWidth + maxEnemyHalfWidth;

            int start = LowerBound(queryMin);
            for (int index = start; index < Enemies.Count; index++)
            {
                GameUnit enemy = Enemies[index];
                float enemyX = enemy.m_Tf.position.x;
                if (enemyX > queryMax)
                {
                    break;
                }

                Collider2D enemyCollider = GetBodyCollider(enemy);
                if (enemyCollider == null || !OverlapsX(centerX, halfWidth, enemyCollider))
                {
                    continue;
                }

                Overlapping.Add(enemy);
                range.RangeCheck(enemyCollider);
            }

            CollectStale(range.List_RangeUnit);
            CollectStale(range.List_AttackUnit);
            for (int index = 0; index < Stale.Count; index++)
            {
                Collider2D collider = GetBodyCollider(Stale[index]);
                if (collider != null)
                {
                    range.RangeOutCheck(collider);
                }
            }
        }

        private static void CollectStale(List<GameUnit> tracked)
        {
            if (tracked == null)
            {
                return;
            }

            for (int index = 0; index < tracked.Count; index++)
            {
                GameUnit unit = tracked[index];
                if (unit != null
                    && !Overlapping.Contains(unit)
                    && StaleSet.Add(unit))
                {
                    Stale.Add(unit);
                }
            }

        }

        private static void Disable(BoxCollider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            int id = collider.GetInstanceID();
            if (!Disabled.ContainsKey(id))
            {
                Disabled[id] = new ColliderState
                {
                    Collider = collider,
                    WasEnabled = collider.enabled
                };
            }

            collider.enabled = false;
        }

        private static bool IsActiveEnemy(GameUnit unit)
        {
            return unit != null
                && unit.gameObject.activeSelf
                && unit.m_State != UnitState.Death
                && unit.m_HP > 0;
        }

        private static Collider2D GetBodyCollider(GameUnit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.m_Box2d != null)
            {
                return unit.m_Box2d;
            }

            return unit.m_Pol2d;
        }

        private static bool OverlapsX(
            float rangeCenterX,
            float rangeHalfWidth,
            Collider2D target)
        {
            Bounds bounds = target.bounds;
            return Mathf.Abs(bounds.center.x - rangeCenterX)
                <= rangeHalfWidth + bounds.extents.x;
        }

        private static int LowerBound(float x)
        {
            int low = 0;
            int high = Enemies.Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (Enemies[middle].m_Tf.position.x < x)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private static int CompareByX(GameUnit left, GameUnit right)
        {
            return left.m_Tf.position.x.CompareTo(right.m_Tf.position.x);
        }
    }

    [HarmonyPatch(typeof(AttackRange), "AttackRangeSet")]
    internal static class AttackRangeSetPatch
    {
        private static void Postfix(AttackRange __instance)
        {
            FriendlyRangeOptimizer.RegisterIfNeeded(__instance);
        }
    }
}
