using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TawanOS.SceneDressing
{
    // Dresses CombatTestScene with the Blender set in ProjectAsset/CombatDemo/Table.fbx (table, scarecrow, candles,
    // red strings, trees) and matches the concept render's camera and candle lighting.
    // Visual only: it never adds, removes or edits combat components. The only system objects it touches are
    // renderers (hides the grey CombatTable cube and the slot Quads; their colliders stay) and the Main Camera
    // transform, which CombatCameraRig3D reads as its home view on Start.
    // Safe to run again: the previous dressing and volume are replaced.
    public static class CombatSceneDressingTool
    {
        private const string TablePath = "Assets/ProjectAsset/CombatDemo/Table.fbx";
        private const string VolumeProfilePath = "Assets/ProjectAsset/CombatDemo/CombatDressingVolume.asset";
        private const string DressingName = "CombatDressing";
        private const string VolumeName = "CombatDressingVolume";

        private const string BackCardMatPath = "Assets/ProjectAsset/CombatDemo/BackCardMat.mat";

        // FBX mock cards and decks (the system spawns the real ones). Matched by material, not name:
        // two of the board mock cards are named Tree.018 / Tree.019 in the Blender file.
        private static readonly string[] MockMaterials = { "CardMat", "BackCardMat" };

        // Uniform set scale measured on the first run: FBX slot rows as wide as the original system rows.
        // Kept constant because the slots are snapped onto the FBX outlines afterwards (re-measuring would drift).
        private const float SetScale = 1.574f;
        private const int SlotsPerRow = 5;

        [MenuItem("Tools/TawanOS/Combat Scene/Apply Blender Dressing")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "CombatTestScene")
            {
                EditorUtility.DisplayDialog("Combat Scene Dressing", "Open CombatTestScene first.", "OK");
                return;
            }

            var tableAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TablePath);
            var playerSlots = FindSlots("PlayerSlot_");
            var enemySlots = FindSlots("EnemySlot_");
            if (tableAsset == null || playerSlots.Length == 0 || enemySlots.Length == 0)
            {
                Debug.LogError($"[CombatDressing] Missing Table.fbx ({tableAsset != null}) or board slots (player {playerSlots.Length}, enemy {enemySlots.Length}).");
                return;
            }

            var old = GameObject.Find(DressingName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var dressing = (GameObject)PrefabUtility.InstantiatePrefab(tableAsset, scene);
            dressing.name = DressingName;
            Undo.RegisterCreatedObjectUndo(dressing, "Apply Combat Dressing");

            foreach (var r in dressing.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterials.Any(m => m != null && MockMaterials.Contains(m.name))) r.gameObject.SetActive(false);
            }

            var playerDeck = FindSlots("Player_DeckSlot");
            var enemyDeck = FindSlots("Enemy_DeckSlot");
            AlignToBoard(dressing, playerSlots, enemySlots);
            SnapSlotsToOutlines(dressing, playerSlots, enemySlots, playerDeck, enemyDeck);
            CopyBlenderCamera(dressing);
            HideSystemPlaceholders(playerSlots.Concat(enemySlots).Concat(playerDeck).Concat(enemyDeck));
            UseCardBackOnDeck(playerDeck);
            TuneLights(dressing);
            ApplyAtmosphere(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CombatDressing] Done. Save the scene (Ctrl+S) to keep it.");
        }

        private static Transform[] FindSlots(string prefix)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith(prefix) && t.GetComponent<Renderer>() != null)
                .OrderBy(t => t.name).ToArray();
        }

        // Scale the set, then centre the FBX slot rows on the system's slot rows, just under the slot height.
        private static void AlignToBoard(GameObject dressing, Transform[] playerSlots, Transform[] enemySlots)
        {
            dressing.transform.localScale = Vector3.one * SetScale;

            Bounds zones = RendererBounds(dressing, "PlayerZone", "EnemyZone");
            Vector3 slotCenter = playerSlots.Concat(enemySlots).Aggregate(Vector3.zero, (sum, s) => sum + s.position) / (playerSlots.Length + enemySlots.Length);
            float slotY = playerSlots[0].position.y;
            dressing.transform.position += new Vector3(slotCenter.x - zones.center.x, slotY - 0.005f - zones.max.y, slotCenter.z - zones.center.z);
        }

        // The FBX rows are deeper apart than the system rows, so the system slots (drop targets and card anchors)
        // move onto the printed outlines. Each row mesh holds SlotsPerRow outlines at an even pitch.
        private static void SnapSlotsToOutlines(GameObject dressing, Transform[] playerSlots, Transform[] enemySlots, Transform[] playerDeck, Transform[] enemyDeck)
        {
            SnapRow(RendererBounds(dressing, "PlayerZone"), playerSlots);
            SnapRow(RendererBounds(dressing, "EnemyZone"), enemySlots);
            SnapTo(RendererBounds(dressing, "PlayerDeckZone"), playerDeck);
            SnapTo(RendererBounds(dressing, "EnemyDeckZone"), enemyDeck);
        }

        private static void SnapRow(Bounds row, Transform[] slots)
        {
            if (slots.Length != SlotsPerRow)
            {
                Debug.LogWarning($"[CombatDressing] Expected {SlotsPerRow} slots in a row, found {slots.Length}; row left in place.");
                return;
            }
            var ordered = slots.OrderBy(s => s.position.x).ToArray(); // slot_0 is leftmost on screen
            float pitch = row.size.x / SlotsPerRow;
            for (int i = 0; i < ordered.Length; i++)
            {
                Undo.RecordObject(ordered[i], "Snap slot");
                ordered[i].position = new Vector3(row.min.x + pitch * (i + 0.5f), ordered[i].position.y, row.center.z);
            }
        }

        private static void SnapTo(Bounds zone, Transform[] slots)
        {
            foreach (var s in slots)
            {
                Undo.RecordObject(s, "Snap deck slot");
                s.position = new Vector3(zone.center.x, s.position.y, zone.center.z);
            }
        }

        private static void UseCardBackOnDeck(Transform[] playerDeck)
        {
            var backMat = AssetDatabase.LoadAssetAtPath<Material>(BackCardMatPath);
            foreach (var s in playerDeck)
            {
                var pile = s.GetComponent<TawanOS.CardEngine.DeckPileView3D>();
                if (pile == null || backMat == null) continue;
                Undo.RecordObject(pile, "Deck card back");
                pile.pileMaterial = backMat;
            }
        }

        private static Bounds RendererBounds(GameObject root, params string[] names)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => names.Any(n => r.name.StartsWith(n))).ToArray();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // The FBX carries the concept render's camera. Use it as the Main Camera's home view.
        private static void CopyBlenderCamera(GameObject dressing)
        {
            var blenderCam = dressing.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            var mainCam = Camera.main;
            if (blenderCam == null || mainCam == null)
            {
                Debug.LogWarning("[CombatDressing] No Blender camera or Main Camera found; camera left unchanged.");
                return;
            }

            Undo.RecordObject(mainCam.transform, "Combat Dressing Camera");
            Undo.RecordObject(mainCam, "Combat Dressing Camera");
            mainCam.transform.SetPositionAndRotation(blenderCam.transform.position, blenderCam.transform.rotation);
            mainCam.fieldOfView = blenderCam.fieldOfView;
            mainCam.nearClipPlane = 0.05f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.02f, 0.012f, 0.01f, 1f);
            mainCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            blenderCam.gameObject.SetActive(false);
        }

        // The grey cube table and flat slot Quads would cover the FBX table art. Only their renderers are turned off;
        // colliders (click / drop targets) and the BoardSlotView components are untouched.
        private static void HideSystemPlaceholders(System.Collections.Generic.IEnumerable<Transform> slots)
        {
            var table = GameObject.Find("CombatTable");
            if (table != null && table.TryGetComponent<Renderer>(out var tr)) { Undo.RecordObject(tr, "Hide table"); tr.enabled = false; }
            foreach (var s in slots)
            {
                var r = s.GetComponent<Renderer>();
                Undo.RecordObject(r, "Hide slot quad");
                r.enabled = false;
            }
        }

        private static void TuneLights(GameObject dressing)
        {
            // Blender point-light wattage imports far too bright or dark; set candle values by hand.
            bool first = true;
            foreach (var light in dressing.GetComponentsInChildren<Light>(true))
            {
                light.type = LightType.Point;
                light.color = new Color(1f, 0.52f, 0.22f);
                light.intensity = 2.5f;
                light.range = 9f;
                light.shadows = first ? LightShadows.Soft : LightShadows.None; // one shadow caster keeps it cheap
                first = false;
            }

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Dim directional");
                light.intensity = 0.25f;
                light.color = new Color(0.62f, 0.4f, 0.36f);
            }
        }

        private static void ApplyAtmosphere(UnityEngine.SceneManagement.Scene scene)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.045f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.012f, 0.01f);
            RenderSettings.fogDensity = 0.02f;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            if (!profile.TryGet<Vignette>(out var vignette)) vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.45f);
            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(0.9f);
            foreach (var component in profile.components)
            {
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var old = GameObject.Find(VolumeName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var go = new GameObject(VolumeName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            Undo.RegisterCreatedObjectUndo(go, "Combat Dressing Volume");
        }
    }
}
