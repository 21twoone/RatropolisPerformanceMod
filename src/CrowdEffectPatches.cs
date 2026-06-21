using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    [HarmonyPatch]
    internal static class HealEffectActivationPatch
    {
        private static readonly HashSet<string> VisualMethods =
            new HashSet<string>
            {
                "EffectActive",
                "LifeEffectActive",
                "PowerEffectActive",
                "ShieldUpEffect",
                "SoulChainEffectActive",
                "FireSpiritEffectActive",
                "InvincibilityEffect",
                "SpeedUpEffect"
            };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodInfo> methods =
                AccessTools.GetDeclaredMethods(typeof(HealEffect));
            for (int index = 0; index < methods.Count; index++)
            {
                MethodInfo method = methods[index];
                if (VisualMethods.Contains(method.Name))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix(HealEffect __instance)
        {
            return CrowdDisplayOptimizer.ShouldShowUnitEffects(
                CrowdDisplayOptimizer.ResolveEffectOwner(__instance));
        }
    }

    [HarmonyPatch(typeof(HealEffect), "ShielEffectActive")]
    internal static class ShieldEffectActivationPatch
    {
        private static bool Prefix(HealEffect __instance, bool act)
        {
            if (!act)
            {
                return true;
            }

            return CrowdDisplayOptimizer.ShouldShowUnitEffects(
                CrowdDisplayOptimizer.ResolveEffectOwner(__instance));
        }
    }

    [HarmonyPatch]
    internal static class UnitTextEffectPatch
    {
        private static readonly HashSet<string> VisualMethods =
            new HashSet<string>
            {
                "ActiveMaxHP_UpEffect",
                "ActiveMaxHP_DownEffect",
                "ActiveAtk_UpEffect",
                "ActiveDmg",
                "ActiveLife",
                "ActiveExp"
            };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodInfo> methods =
                AccessTools.GetDeclaredMethods(typeof(UnitDmg));
            for (int index = 0; index < methods.Count; index++)
            {
                MethodInfo method = methods[index];
                if (VisualMethods.Contains(method.Name))
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix(
            UnitDmg __instance,
            object[] __args,
            MethodBase __originalMethod)
        {
            GameUnit unit = null;
            for (int index = 0; index < __args.Length; index++)
            {
                Transform parent = __args[index] as Transform;
                if (parent != null)
                {
                    unit = parent.GetComponentInParent<GameUnit>();
                    if (unit != null)
                    {
                        break;
                    }
                }
            }

            if (unit == null)
            {
                unit = __instance.GetComponentInParent<GameUnit>();
            }

            bool show =
                CrowdDisplayOptimizer.ShouldShowUnitEffects(unit);
            if (!show
                && (
                    __originalMethod.Name == "ActiveMaxHP_UpEffect"
                    || __originalMethod.Name == "ActiveMaxHP_DownEffect"
                    || __originalMethod.Name == "ActiveAtk_UpEffect"
                )
                && GameMgr.Instance != null
                && GameMgr.Instance._BEffectMgr != null
                && GameMgr.Instance._BEffectMgr.Pool_UnitTxtEffect != null)
            {
                GameMgr.Instance._BEffectMgr.Pool_UnitTxtEffect.AddObjTf(
                    __instance.gameObject);
            }

            return show;
        }
    }

    [HarmonyPatch(typeof(NewBuff), "BuffSet")]
    internal static class BuffSlotVisibilityPatch
    {
        private static void Postfix(NewBuff __instance, BuffIdx idx)
        {
            CrowdDisplayOptimizer.FinalizeBuffVisual(
                __instance.m_Master,
                idx);
        }
    }

    [HarmonyPatch(typeof(ConfusingEffect), "ConfusingSet")]
    internal static class ConfusingVisualPatch
    {
        private static bool Prefix(
            ConfusingEffect __instance,
            GameUnit _unit)
        {
            if (CrowdDisplayOptimizer.ShouldShowUnitEffects(_unit))
            {
                return true;
            }

            GameMgr game = GameMgr.Instance;
            if (game != null
                && game._PoolMgr != null
                && game._PoolMgr.Pool_ConfusingEffect != null)
            {
                game._PoolMgr.Pool_ConfusingEffect.AddObjTf(
                    __instance.gameObject);
            }
            else
            {
                __instance.gameObject.SetActive(false);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(CurseEffect), "CurseEffectSet")]
    internal static class CurseVisualPatch
    {
        private static bool Prefix(
            CurseEffect __instance,
            GameUnit _unit)
        {
            if (CrowdDisplayOptimizer.ShouldShowUnitEffects(_unit))
            {
                return true;
            }

            GameMgr game = GameMgr.Instance;
            if (game != null
                && game._PoolMgr != null
                && game._PoolMgr.Pool_CurseEffect != null)
            {
                game._PoolMgr.Pool_CurseEffect.AddObjTf(
                    __instance.gameObject);
            }
            else
            {
                __instance.gameObject.SetActive(false);
            }
            return false;
        }
    }
}
