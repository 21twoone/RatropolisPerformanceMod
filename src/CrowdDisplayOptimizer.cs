using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class CrowdDisplayOptimizer
    {
        private sealed class RendererState
        {
            internal GameUnit Unit;
            internal MeshRenderer Renderer;
            internal bool WasEnabled;
        }

        private static readonly Dictionary<int, RendererState> Hidden =
            new Dictionary<int, RendererState>(4096);
        private static readonly Dictionary<int, GameUnit> HealEffectOwners =
            new Dictionary<int, GameUnit>(4096);
        private static readonly List<int> StaleIds = new List<int>(512);
        private static readonly List<BuffIdx> MissingBuffSlots =
            new List<BuffIdx>(32);
        private static readonly HashSet<UnitKind> RepresentedKinds =
            new HashSet<UnitKind>();
        private static readonly FieldInfo BuffIconsField =
            AccessTools.Field(typeof(NewBuff), "List_BuffIcon");
        private static readonly MethodInfo BuffPositionMethod =
            AccessTools.Method(typeof(NewBuff), "PosSetting");

        private static float nextRefresh;

        internal static void ProcessFrame()
        {
            if (Plugin.CrowdDisplayEnabled == null
                || !Plugin.CrowdDisplayEnabled.Value
                || Population.TeamCount < Plugin.CrowdDisplayThreshold.Value)
            {
                RestoreAll();
                return;
            }

            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + 0.25f;

            GameMgr game = GameMgr.Instance;
            if (game == null
                || game._PoolMgr == null
                || game._PoolMgr.List_OurUnit == null)
            {
                return;
            }

            int ratio = Plugin.CrowdDisplayRatio.Value;
            bool ultra = Plugin.CrowdUltraMode.Value;
            List<GameUnit> units = game._PoolMgr.List_OurUnit;
            int activeIndex = 0;
            RepresentedKinds.Clear();
            for (int index = 0; index < units.Count; index++)
            {
                GameUnit unit = units[index];
                if (unit == null || unit.m_MeshRender == null)
                {
                    continue;
                }

                if (unit.m_HealEffect != null)
                {
                    HealEffectOwners[unit.m_HealEffect.GetInstanceID()] = unit;
                }

                int id = unit.GetInstanceID();
                bool active =
                    unit.gameObject.activeSelf
                    && unit.m_State != UnitState.Death
                    && unit.m_HP > 0;
                bool firstOfKind =
                    active
                    && RepresentedKinds.Add(unit.m_Index);
                bool representative =
                    firstOfKind
                    || (
                        !ultra
                        && (
                            activeIndex % ratio == 0
                            || unit.m_Outline
                        )
                    );

                if (active && !representative)
                {
                    if (!Hidden.ContainsKey(id))
                    {
                        Hidden.Add(id, new RendererState
                        {
                            Unit = unit,
                            Renderer = unit.m_MeshRender,
                            WasEnabled = unit.m_MeshRender.enabled
                        });
                        SuppressUnitEffects(unit);
                    }
                    unit.m_MeshRender.enabled = false;
                    SetBuffSlotsVisible(unit, false);
                }
                else
                {
                    RestoreOne(id);
                    EnsureBuffSlots(unit);
                }

                if (active)
                {
                    activeIndex++;
                }
            }

            RemoveStaleEntries();
        }

        internal static void RestoreAll()
        {
            RestoreAll(true);
        }

        internal static void RebuildSelection()
        {
            RestoreAll(false);
        }

        private static void RestoreAll(bool restoreMissingBuffSlots)
        {
            foreach (RendererState state in Hidden.Values)
            {
                RestoreState(state, restoreMissingBuffSlots);
            }

            Hidden.Clear();
            HealEffectOwners.Clear();
            StaleIds.Clear();
            RepresentedKinds.Clear();
            nextRefresh = 0f;
        }

        internal static bool ShouldShowUnitEffects(GameUnit unit)
        {
            if (unit == null || unit.m_UnitIndex != UnitIndex.Team)
            {
                return true;
            }

            if (Plugin.CrowdDisplayEnabled == null
                || !Plugin.CrowdDisplayEnabled.Value
                || Population.TeamCount < Plugin.CrowdDisplayThreshold.Value)
            {
                return true;
            }

            return !Hidden.ContainsKey(unit.GetInstanceID());
        }

        internal static GameUnit ResolveEffectOwner(HealEffect effect)
        {
            if (effect == null)
            {
                return null;
            }

            GameUnit unit = effect.GetComponentInParent<GameUnit>();
            if (unit != null)
            {
                return unit;
            }

            HealEffectOwners.TryGetValue(effect.GetInstanceID(), out unit);
            return unit;
        }

        internal static void FinalizeBuffVisual(
            GameUnit unit,
            BuffIdx index)
        {
            if (unit == null || unit.m_Buff == null)
            {
                return;
            }

            NewBuffInfo info;
            if (!unit.m_Buff.Dic_BuffSlot.TryGetValue(index, out info)
                || info.m_Slot == null)
            {
                return;
            }

            if (ShouldShowUnitEffects(unit))
            {
                SetBuffSlotVisible(info.m_Slot, true);
                return;
            }

            RecycleBuffSlot(unit, index, info);
        }

        private static void RestoreOne(int id)
        {
            RendererState state;
            if (!Hidden.TryGetValue(id, out state))
            {
                return;
            }

            RestoreState(state, true);
            Hidden.Remove(id);
        }

        private static void RestoreState(
            RendererState state,
            bool restoreMissingBuffSlots)
        {
            if (state.Renderer != null
                && state.Unit != null
                && state.Unit.gameObject.activeSelf
                && state.Unit.m_State != UnitState.Death
                && state.Unit.m_HP > 0)
            {
                state.Renderer.enabled = state.WasEnabled;
                if (restoreMissingBuffSlots)
                {
                    EnsureBuffSlots(state.Unit);
                }
                SetBuffSlotsVisible(state.Unit, true);
                SetUnitTextVisible(state.Unit.m_UnitDmg, true);
                SetUnitTextVisible(state.Unit.m_UnitHeal, true);
                SetUnitTextVisible(state.Unit.m_UnitLife, true);
                SetUnitTextVisible(state.Unit.m_UnitPlusExp, true);
            }
        }

        private static void SuppressUnitEffects(GameUnit unit)
        {
            if (unit.m_HealEffect != null)
            {
                unit.m_HealEffect.AllEffectOff();
            }

            SetUnitTextVisible(unit.m_UnitDmg, false);
            SetUnitTextVisible(unit.m_UnitHeal, false);
            SetUnitTextVisible(unit.m_UnitLife, false);
            SetUnitTextVisible(unit.m_UnitPlusExp, false);
        }

        private static void SetUnitTextVisible(UnitDmg text, bool visible)
        {
            if (text == null)
            {
                return;
            }

            TMPro.TextMeshPro label =
                text.GetComponentInChildren<TMPro.TextMeshPro>(true);
            if (label != null)
            {
                label.enabled = visible;
            }
        }

        private static void SetBuffSlotsVisible(GameUnit unit, bool visible)
        {
            if (unit == null
                || unit.m_Buff == null
                || unit.m_Buff.Dic_BuffSlot == null)
            {
                return;
            }

            foreach (NewBuffInfo info in unit.m_Buff.Dic_BuffSlot.Values)
            {
                if (info.m_Slot != null)
                {
                    SetBuffSlotVisible(info.m_Slot, visible);
                }
            }
        }

        private static void RecycleBuffSlot(
            GameUnit unit,
            BuffIdx index,
            NewBuffInfo info)
        {
            BuffSlot slot = info.m_Slot;
            if (slot == null)
            {
                return;
            }

            info.m_Slot = null;
            unit.m_Buff.Dic_BuffSlot[index] = info;
            List<BuffSlot> icons = GetBuffIcons(unit.m_Buff);
            if (icons != null)
            {
                icons.Remove(slot);
            }

            GameMgr game = GameMgr.Instance;
            if (game != null
                && game._PoolMgr != null
                && game._PoolMgr.Pool_BuffSlot != null)
            {
                game._PoolMgr.Pool_BuffSlot.AddObjTf(slot.gameObject);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        private static void EnsureBuffSlots(GameUnit unit)
        {
            if (unit == null
                || unit.m_Buff == null
                || unit.m_Buff.Dic_BuffSlot == null)
            {
                return;
            }

            List<BuffSlot> icons = GetBuffIcons(unit.m_Buff);
            if (icons == null)
            {
                return;
            }

            GameMgr game = GameMgr.Instance;
            if (game == null
                || game._PoolMgr == null
                || game._PoolMgr.Pool_BuffSlot == null)
            {
                return;
            }

            MissingBuffSlots.Clear();
            foreach (KeyValuePair<BuffIdx, NewBuffInfo> item
                in unit.m_Buff.Dic_BuffSlot)
            {
                if (item.Value.m_Slot == null)
                {
                    MissingBuffSlots.Add(item.Key);
                }
            }

            for (int index = 0; index < MissingBuffSlots.Count; index++)
            {
                BuffIdx buffIndex = MissingBuffSlots[index];
                NewBuffInfo info =
                    unit.m_Buff.Dic_BuffSlot[buffIndex];
                GameObject slotObject =
                    game._PoolMgr.Pool_BuffSlot.GetNextObj();
                BuffSlot slot = slotObject.GetComponent<BuffSlot>();
                if (slot == null)
                {
                    game._PoolMgr.Pool_BuffSlot.AddObjTf(slotObject);
                    continue;
                }

                ConfigureBuffSlot(unit, slot, buffIndex);
                info.m_Slot = slot;
                unit.m_Buff.Dic_BuffSlot[buffIndex] = info;
                icons.Add(slot);
            }

            if (MissingBuffSlots.Count > 0
                && BuffPositionMethod != null)
            {
                BuffPositionMethod.Invoke(unit.m_Buff, null);
            }
        }

        private static List<BuffSlot> GetBuffIcons(NewBuff buff)
        {
            if (buff == null || BuffIconsField == null)
            {
                return null;
            }

            return BuffIconsField.GetValue(buff) as List<BuffSlot>;
        }

        private static void ConfigureBuffSlot(
            GameUnit unit,
            BuffSlot slot,
            BuffIdx index)
        {
            int numericIndex = (int)index;
            if (numericIndex == 4)
            {
                string spritePath =
                    unit.m_Buff.m_ArrSum[numericIndex] > 0f
                        ? "GameScene/UI/UI_WallMoveArrow2"
                        : "GameScene/UI/UI_WallMoveArrow";
                slot.Spr_Icon.sprite = Func.Instance.LoadSprite(spritePath);
            }
            else if (numericIndex == 5)
            {
                slot.Spr_Icon.sprite = Func.Instance.LoadSprite(
                    "GameScene/UI/UI_Icon_Rout");
            }
            else
            {
                slot.Spr_Icon.sprite = Func.Instance.LoadSprite(
                    "GameScene/UI/NewBuffIcon/Test/Icon_"
                    + index);
            }

            if (slot.Tf == null)
            {
                slot.Tf = slot.transform;
            }

            slot.Idx = index;
            bool showText = BuffUsesText(numericIndex);
            slot.Txt.gameObject.SetActive(showText);
            if (showText)
            {
                slot.Txt.color = BuffUsesRedText(numericIndex)
                    ? Collector.Color_Red
                    : (Color)Collector.m_Col_Green;
            }

            slot.Tf.SetParent(unit.m_Buff.transform);
            slot.Spr_Icon.sortingOrder = 10;
            slot.Txt.sortingOrder = 10;
        }

        private static bool BuffUsesText(int index)
        {
            return index == 0
                || index == 3
                || index == 7
                || index == 16
                || index == 22
                || index == 24
                || index == 26
                || index == 28
                || index == 29
                || index == 32
                || index == 33
                || index == 34
                || index == 36
                || index == 38
                || index == 39;
        }

        private static bool BuffUsesRedText(int index)
        {
            return index == 3
                || index == 7
                || index == 24
                || index == 28
                || index == 33;
        }

        private static void SetBuffSlotVisible(BuffSlot slot, bool visible)
        {
            if (slot.Spr_Icon != null)
            {
                slot.Spr_Icon.enabled = visible;
            }
            if (slot.Txt != null)
            {
                slot.Txt.enabled = visible;
            }
        }

        private static void RemoveStaleEntries()
        {
            StaleIds.Clear();
            foreach (KeyValuePair<int, RendererState> item in Hidden)
            {
                RendererState state = item.Value;
                if (state.Unit == null
                    || state.Renderer == null
                    || !state.Unit.gameObject.activeSelf
                    || state.Unit.m_State == UnitState.Death
                    || state.Unit.m_HP <= 0)
                {
                    StaleIds.Add(item.Key);
                }
            }

            for (int index = 0; index < StaleIds.Count; index++)
            {
                Hidden.Remove(StaleIds[index]);
            }
        }

    }
}
