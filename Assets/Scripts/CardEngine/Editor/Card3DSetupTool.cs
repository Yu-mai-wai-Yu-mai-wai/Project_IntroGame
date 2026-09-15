#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

namespace TawanOS.CardEngine
{
    public static class Card3DSetupTool
    {
        [MenuItem("Tools/TawanOS/Card Engine/Convert Hand To 3D Cube Cards")]
        public static void ConvertHandTo3D()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Run in Play Mode", "Please exit Play Mode before running this tool.", "OK");
                return;
            }

            GameObject oldHandContainer = GameObject.Find("HandContainer");
            if (oldHandContainer == null)
            {
                Debug.LogError("[Card3DSetupTool] HandContainer not found in the open scene. Open CombatTestScene first.");
                return;
            }

            // Disable (don't delete) the old UI-based hand so it can be restored later if needed
            HandLayoutController oldHandLayout = oldHandContainer.GetComponent<HandLayoutController>();
            if (oldHandLayout != null) Object.DestroyImmediate(oldHandLayout);
            oldHandContainer.SetActive(false);

            CardView3D cardPrefab = BuildOrGetCardCubePrefab();

            GameObject handRoot = GameObject.Find("HandContainer3D");
            if (handRoot == null)
            {
                handRoot = new GameObject("HandContainer3D");
            }

            CameraAnchoredHand cameraAnchor = handRoot.GetComponent<CameraAnchoredHand>();
            if (cameraAnchor == null) cameraAnchor = handRoot.AddComponent<CameraAnchoredHand>();
            if (cameraAnchor.targetCamera == null)
            {
                GameObject camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) cameraAnchor.targetCamera = camGo.GetComponent<Camera>();
            }

            HandLayoutController3D handLayout3D = handRoot.GetComponent<HandLayoutController3D>();
            if (handLayout3D == null) handLayout3D = handRoot.AddComponent<HandLayoutController3D>();
            handLayout3D.cardPrefab = cardPrefab;
            handLayout3D.handContainer = handRoot.transform;
            handLayout3D.cardSpacing = 0.9f;
            handLayout3D.maxTotalWidth = 6f;
            handLayout3D.arcAngle = 5f;
            handLayout3D.curveDepth = 0.15f;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[Card3DSetupTool] Hand now uses 3D cube cards (HandContainer3D). Save the scene to keep this change.</color>");
        }

        private static CardView3D BuildOrGetCardCubePrefab()
        {
            const string prefabFolder = "Assets/CardEngineData/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/CardEngineData", "Prefabs");
            }
            const string path = prefabFolder + "/CardCube3DPrefab.prefab";

            GameObject existingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingAsset != null)
            {
                CardView3D existingView = existingAsset.GetComponent<CardView3D>();
                if (existingView != null) return existingView;
            }

            GameObject cardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cardGo.name = "CardCube3DPrefab";
            cardGo.transform.localScale = new Vector3(0.7f, 1f, 0.08f);

            var renderer = cardGo.GetComponent<MeshRenderer>();
            renderer.material.color = new Color(0.85f, 0.8f, 0.55f);

            GameObject labelGo = new GameObject("NameLabel");
            labelGo.transform.SetParent(cardGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            labelGo.transform.localRotation = Quaternion.identity;
            // Counter-scale so the label reads at a normal size despite the card's flattened, narrow parent scale
            labelGo.transform.localScale = new Vector3(1f / 0.7f, 1f, 1f);

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = "Card";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3f;
            tmp.color = Color.black;
            TMP_FontAsset charmFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            if (charmFont != null) tmp.font = charmFont;

            var view = cardGo.AddComponent<CardView3D>();
            view.cardRenderer = renderer;
            view.nameLabel = tmp;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cardGo, path);
            Object.DestroyImmediate(cardGo);
            return prefab.GetComponent<CardView3D>();
        }
    }
}
#endif
