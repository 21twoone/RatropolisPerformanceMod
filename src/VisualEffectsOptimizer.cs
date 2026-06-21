using System;
using System.Collections.Generic;
using UnityEngine;

namespace RatropolisPerformanceMod
{
    internal static class VisualEffectsOptimizer
    {
        private static readonly Dictionary<int, Camera> WaterCameras =
            new Dictionary<int, Camera>();
        private static readonly Dictionary<int, bool> WaterCameraStates =
            new Dictionary<int, bool>();
        private static readonly Dictionary<int, GUICamera> PostEffects =
            new Dictionary<int, GUICamera>();
        private static readonly Dictionary<int, bool> PostEffectStates =
            new Dictionary<int, bool>();
        private static readonly Dictionary<int, Water> AnimatedWater =
            new Dictionary<int, Water>();
        private static readonly Dictionary<int, bool> AnimatedWaterStates =
            new Dictionary<int, bool>();

        private static float nextRefresh;

        internal static void ProcessFrame()
        {
            if (Plugin.LowVisualEffects == null
                || !Plugin.LowVisualEffects.Value)
            {
                Restore();
                return;
            }

            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + 1f;
            DisableWaterCameras();
            DisablePostEffects();
            DisableAnimatedWater();
        }

        internal static void Restore()
        {
            foreach (KeyValuePair<int, Camera> item in WaterCameras)
            {
                Camera camera = item.Value;
                if (camera != null)
                {
                    camera.enabled = WaterCameraStates[item.Key];
                }
            }

            foreach (KeyValuePair<int, GUICamera> item in PostEffects)
            {
                GUICamera effect = item.Value;
                if (effect != null)
                {
                    effect.enabled = PostEffectStates[item.Key];
                }
            }

            foreach (KeyValuePair<int, Water> item in AnimatedWater)
            {
                Water water = item.Value;
                if (water != null)
                {
                    water.enabled = AnimatedWaterStates[item.Key];
                }
            }

            WaterCameras.Clear();
            WaterCameraStates.Clear();
            PostEffects.Clear();
            PostEffectStates.Clear();
            AnimatedWater.Clear();
            AnimatedWaterStates.Clear();
            nextRefresh = 0f;
        }

        private static void DisableWaterCameras()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null
                    || camera.name.IndexOf(
                        "WaterCamera",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int id = camera.GetInstanceID();
                if (!WaterCameras.ContainsKey(id))
                {
                    WaterCameras.Add(id, camera);
                    WaterCameraStates.Add(id, camera.enabled);
                    Debug.Log(
                        "[RatropolisPerformanceMod] Disabled water reflection camera: "
                        + camera.name);
                }
                camera.enabled = false;
            }
        }

        private static void DisablePostEffects()
        {
            GUICamera[] effects = UnityEngine.Object.FindObjectsOfType<GUICamera>();
            for (int index = 0; index < effects.Length; index++)
            {
                GUICamera effect = effects[index];
                if (effect == null)
                {
                    continue;
                }

                int id = effect.GetInstanceID();
                if (!PostEffects.ContainsKey(id))
                {
                    PostEffects.Add(id, effect);
                    PostEffectStates.Add(id, effect.enabled);
                    Debug.Log(
                        "[RatropolisPerformanceMod] Disabled GUI post-processing.");
                }
                effect.enabled = false;
            }
        }

        private static void DisableAnimatedWater()
        {
            Water[] waters = UnityEngine.Object.FindObjectsOfType<Water>();
            for (int index = 0; index < waters.Length; index++)
            {
                Water water = waters[index];
                if (water == null)
                {
                    continue;
                }

                int id = water.GetInstanceID();
                if (!AnimatedWater.ContainsKey(id))
                {
                    AnimatedWater.Add(id, water);
                    AnimatedWaterStates.Add(id, water.enabled);
                    Debug.Log(
                        "[RatropolisPerformanceMod] Disabled animated water updates.");
                }
                water.enabled = false;
            }
        }
    }
}
