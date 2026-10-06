using System.Collections.Generic;
using TawanOS.UI;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Horror 3D stage controller for the Main Menu:
    /// - Scarecrow enemy marionette with subtle idle breathing/sway
    /// - Red ritual cords (สายสิญจน์ / ด้ายแดง) suspended from above
    /// - Red flickering horror lighting (<= 3 Hz, WCAG 2.3.1 compliant)
    /// - Atmospheric fog and embers
    /// </summary>
    public class MainMenuStage : MonoBehaviour
    {
        [Header("Scarecrow Marionette")]
        public Transform scarecrowTransform;
        public float swaySpeed = 1.0f;
        public float swayAmount = 0.04f;

        [Header("Red Horror Light (WCAG 2.3.1 Safe <= 3 Hz)")]
        public Light redLight;
        public float baseIntensity = 2.8f;
        public float flickerAmplitude = 1.2f;
        [Range(0.5f, 3.0f)]
        public float flickerFrequency = 2.0f;

        [Header("Strings (สายสิญจน์)")]
        public LineRenderer[] strings = new LineRenderer[0];
        public Transform[] stringTargets = new Transform[0];
        public Vector3[] overheadAnchors = new Vector3[0];

        [Header("Atmosphere")]
        public ParticleSystem fogParticles;

        private Vector3 initialScarecrowPos;
        private Quaternion initialScarecrowRot;

        private void Start()
        {
            if (scarecrowTransform != null)
            {
                initialScarecrowPos = scarecrowTransform.localPosition;
                initialScarecrowRot = scarecrowTransform.localRotation;
            }
        }

        private void Update()
        {
            float time = Time.time;

            // 1. Subtle Scarecrow puppet sway
            if (scarecrowTransform != null)
            {
                float swayX = Mathf.Sin(time * swaySpeed) * swayAmount;
                float swayY = Mathf.Cos(time * swaySpeed * 1.3f) * (swayAmount * 0.4f);
                scarecrowTransform.localPosition = initialScarecrowPos + new Vector3(swayX, swayY, 0f);

                float rotZ = Mathf.Sin(time * swaySpeed * 0.9f) * 1.5f;
                float rotY = Mathf.Cos(time * swaySpeed * 0.7f) * 2.0f;
                scarecrowTransform.localRotation = initialScarecrowRot * Quaternion.Euler(0f, rotY, rotZ);
            }

            // 2. Red light flicker (respects SettingsPanelUI.ReduceFlicker for accessibility)
            if (redLight != null)
            {
                if (SettingsPanelUI.ReduceFlicker)
                {
                    redLight.intensity = baseIntensity;
                }
                else
                {
                    // Smooth, non-violent flicker <= 3 Hz
                    float phase = time * flickerFrequency * Mathf.PI * 2f;
                    float wave = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 0.67f + 1.1f) * 0.4f;
                    redLight.intensity = Mathf.Max(0.5f, baseIntensity + wave * flickerAmplitude);
                }
            }

            // 3. Update string end positions
            if (strings != null && stringTargets != null)
            {
                for (int i = 0; i < strings.Length; i++)
                {
                    if (strings[i] == null) continue;
                    Vector3 anchor = i < overheadAnchors.Length ? overheadAnchors[i] : transform.position + new Vector3(0, 6, 0);
                    Vector3 targetPos = (i < stringTargets.Length && stringTargets[i] != null)
                        ? stringTargets[i].position
                        : (scarecrowTransform != null ? scarecrowTransform.position : transform.position);

                    strings[i].SetPosition(0, anchor);
                    strings[i].SetPosition(1, targetPos);
                }
            }
        }

        /// <summary>
        /// Builds the 3D stage and ritual cords in the editor.
        /// </summary>
        public static MainMenuStage BuildStage(GameObject stageParent, GameObject tableFbxAsset, Vector3 stageOffset = default)
        {
            var stage = stageParent.AddComponent<MainMenuStage>();

            if (stageOffset == default) stageOffset = new Vector3(1.6f, 0f, 0f);

            // Instantiate or extract from Table.fbx
            GameObject dressing = null;
            if (tableFbxAsset != null)
            {
#if UNITY_EDITOR
                dressing = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(tableFbxAsset, stageParent.scene);
#else
                dressing = Object.Instantiate(tableFbxAsset);
#endif
                dressing.name = "TableDressing";
                dressing.transform.SetParent(stageParent.transform, false);
                dressing.transform.localPosition = stageOffset;

                // Hide card mockups and oversized DarkBackground
                string[] hideNames = { "DarkBackground" };
                string[] mockMats = { "CardMat", "BackCardMat" };
                foreach (var r in dressing.GetComponentsInChildren<Renderer>(true))
                {
                    if (System.Array.Exists(hideNames, n => r.name.Contains(n)))
                    {
                        r.gameObject.SetActive(false);
                        continue;
                    }
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null && System.Array.Exists(mockMats, name => name == m.name))
                        {
                            r.gameObject.SetActive(false);
                            break;
                        }
                    }
                }

                // Locate Scarecrow
                var scarecrow = dressing.transform.Find("Scarecrow");
                if (scarecrow != null)
                {
                    stage.scarecrowTransform = scarecrow;
                }
            }

            // Fallback if Scarecrow not found in dressing
            if (stage.scarecrowTransform == null)
            {
                var scGo = new GameObject("Scarecrow");
                scGo.transform.SetParent(stageParent.transform, false);
                scGo.transform.localPosition = stageOffset + new Vector3(0, 1.74f, 7.83f);
                stage.scarecrowTransform = scGo.transform;
            }

            // Overhead control bar / anchor root
            var barGo = new GameObject("RitualCordAnchors");
            barGo.transform.SetParent(stageParent.transform, false);
            barGo.transform.localPosition = stageOffset + new Vector3(0, 6.5f, 7.5f);

            // Create LineRenderers for >= 6 strings (สายสิญจน์)
            var cordMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            cordMat.color = new Color(0.95f, 0.85f, 0.80f, 0.95f); // Raw sacred thread white-cream

            int cordCount = 8;
            stage.strings = new LineRenderer[cordCount];
            stage.stringTargets = new Transform[cordCount];
            stage.overheadAnchors = new Vector3[cordCount];

            // Anchor offsets
            Vector3[] anchorOffsets = new[]
            {
                new Vector3(-2.5f, 6.5f, 6.5f), // Left arm anchor
                new Vector3(-1.2f, 6.8f, 7.0f), // Left shoulder
                new Vector3(-0.4f, 7.0f, 7.5f), // Head left
                new Vector3( 0.4f, 7.0f, 7.5f), // Head right
                new Vector3( 1.2f, 6.8f, 7.0f), // Right shoulder
                new Vector3( 2.5f, 6.5f, 6.5f), // Right arm anchor
                new Vector3(-0.8f, 6.0f, 8.0f), // Left torso
                new Vector3( 0.8f, 6.0f, 8.0f), // Right torso
            };

            Vector3[] targetOffsets = new[]
            {
                new Vector3(-2.8f, 1.2f, 0.0f), // Left hand
                new Vector3(-1.3f, 1.8f, 0.1f), // Left shoulder
                new Vector3(-0.3f, 2.5f, 0.0f), // Head
                new Vector3( 0.3f, 2.5f, 0.0f), // Head
                new Vector3( 1.3f, 1.8f, 0.1f), // Right shoulder
                new Vector3( 2.8f, 1.2f, 0.0f), // Right hand
                new Vector3(-0.6f, 0.2f, 0.1f), // Left hip
                new Vector3( 0.6f, 0.2f, 0.1f), // Right hip
            };

            for (int i = 0; i < cordCount; i++)
            {
                var cordGo = new GameObject($"RitualCord_{i}");
                cordGo.transform.SetParent(stageParent.transform, false);

                var lr = cordGo.AddComponent<LineRenderer>();
                lr.material = cordMat;
                lr.startWidth = 0.025f;
                lr.endWidth = 0.020f;
                lr.positionCount = 2;
                lr.startColor = new Color(0.95f, 0.85f, 0.80f, 0.9f);
                lr.endColor = new Color(0.85f, 0.25f, 0.20f, 0.85f); // Crimson tainted end
                lr.useWorldSpace = true;

                // Attachment joint
                var jointGo = new GameObject($"CordJoint_{i}");
                jointGo.transform.SetParent(stage.scarecrowTransform, false);
                jointGo.transform.localPosition = targetOffsets[i];

                stage.strings[i] = lr;
                stage.stringTargets[i] = jointGo.transform;
                stage.overheadAnchors[i] = stage.scarecrowTransform.position + anchorOffsets[i];

                lr.SetPosition(0, stage.overheadAnchors[i]);
                lr.SetPosition(1, jointGo.transform.position);
            }

            // Red Horror Light (WCAG 2.3.1 compliant <= 3 Hz)
            var redLightGo = new GameObject("RedHorrorLight");
            redLightGo.transform.SetParent(stageParent.transform, false);
            redLightGo.transform.localPosition = stageOffset + new Vector3(0.5f, 2.5f, 5.5f);

            var rLight = redLightGo.AddComponent<Light>();
            rLight.type = LightType.Point;
            rLight.color = new Color(0.95f, 0.18f, 0.12f); // Deep blood crimson
            rLight.intensity = stage.baseIntensity;
            rLight.range = 14f;
            rLight.shadows = LightShadows.Soft;
            stage.redLight = rLight;

            // Soft candle ambience
            var ambientLightGo = new GameObject("CandleAmbience");
            ambientLightGo.transform.SetParent(stageParent.transform, false);
            ambientLightGo.transform.localPosition = stageOffset + new Vector3(-0.5f, 1.2f, 3.5f);
            var aLight = ambientLightGo.AddComponent<Light>();
            aLight.type = LightType.Point;
            aLight.color = new Color(1.0f, 0.55f, 0.22f); // Warm candle gold
            aLight.intensity = 1.6f;
            aLight.range = 10f;
            aLight.shadows = LightShadows.None;

            // Fog / Mist Particles (<= 300 particles)
            var fogGo = new GameObject("AtmosphericFog");
            fogGo.transform.SetParent(stageParent.transform, false);
            fogGo.transform.localPosition = stageOffset + new Vector3(0f, 0.2f, 6.0f);

            var ps = fogGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 120;
            main.startLifetime = 6.0f;
            main.startSpeed = 0.2f;
            main.startSize = 3.5f;
            main.startColor = new Color(0.12f, 0.08f, 0.09f, 0.25f); // Dark incense mist
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 0.5f, 6f);

            stage.fogParticles = ps;

            return stage;
        }
    }
}
