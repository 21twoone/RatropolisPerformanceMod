using System;

namespace RatropolisPerformanceMod
{
    internal static class Population
    {
        internal static int TeamCount { get; private set; }
        internal static int EnemyCount { get; private set; }
        internal static int TotalCount
        {
            get { return TeamCount + EnemyCount; }
        }

        internal static void Refresh()
        {
            try
            {
                GameMgr game = GameMgr.Instance;
                if (game == null)
                {
                    Reset();
                    return;
                }

                TeamCount = game._PoolMgr != null && game._PoolMgr.List_OurUnit != null
                    ? game._PoolMgr.List_OurUnit.Count
                    : 0;

                EnemyCount = game._WaveMgr != null && game._WaveMgr.List_Enemy != null
                    ? game._WaveMgr.List_Enemy.Count
                    : 0;

                if (game._WaveMgr != null && game._WaveMgr.List_Zombie != null)
                {
                    EnemyCount += game._WaveMgr.List_Zombie.Count;
                }
            }
            catch (Exception)
            {
                Reset();
            }
        }

        private static void Reset()
        {
            TeamCount = 0;
            EnemyCount = 0;
        }
    }
}
