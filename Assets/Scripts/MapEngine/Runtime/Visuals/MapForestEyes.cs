using System.Collections.Generic;
using TawanOS.VFX;
using UnityEngine;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// The pairs of glowing eyes from the main menu, watching from the forest behind the map. Each pair is placed at
    /// a random tree of the map model, a little in front of its trunk, turned to face the camera, and driven by
    /// <see cref="GlowingEyes"/> (fade in, blink, fade out, follow the mouse). Sprite and material are set on the
    /// scene object; nothing is created when they are missing.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MapForestEyes : MonoBehaviour
    {
        [Header("Look (same sprite and material as the main menu)")]
        public Sprite eyeSprite;
        public Material eyeMaterial;
        [Tooltip("Width of one pair in world units.")]
        public float eyeWidth = 0.9f;

        [Header("Placement")]
        [Min(1)] public int pairCount = 14;
        [Tooltip("Name of the scene object that holds the map model and its trees.")]
        public string environmentName = "MapNavigateEnvironment";
        [Tooltip("Height above the paper (min, max).")]
        public Vector2 height = new Vector2(1.1f, 2.4f);
        [Tooltip("How far in front of the tree (toward the camera) a pair sits.")]
        public float frontOffset = 0.6f;
        public int seed = 7;

        [Header("Behaviour")]
        [Min(1)] public int maxOpen = 3;

        private readonly List<Transform> anchors = new List<Transform>();
        private Transform cam;

        private void Start()
        {
            if (eyeSprite == null || eyeMaterial == null) return;
            var env = GameObject.Find(environmentName);
            if (env == null) return;

            var trees = new List<Transform>();
            foreach (var t in env.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Tree") && t.gameObject.activeInHierarchy) trees.Add(t);
            }
            if (trees.Count == 0) return;

            var rng = new System.Random(seed);
            var renderers = new List<SpriteRenderer>();
            var watcher = new GameObject("ForestEyes").AddComponent<GlowingEyes>();
            watcher.transform.SetParent(transform, false);

            float scale = eyeWidth / Mathf.Max(0.01f, eyeSprite.bounds.size.x);
            for (int i = 0; i < pairCount; i++)
            {
                var tree = trees[rng.Next(trees.Count)];
                var anchor = new GameObject("EyeAnchor" + i).transform;
                anchor.SetParent(watcher.transform, false);
                float y = Mathf.Lerp(height.x, height.y, (float)rng.NextDouble());
                anchor.position = new Vector3(tree.position.x + ((float)rng.NextDouble() - 0.5f) * 1.2f, y, tree.position.z + frontOffset);
                anchors.Add(anchor);

                var go = new GameObject("EyePair" + i);
                go.transform.SetParent(anchor, false);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = eyeSprite;
                sr.sharedMaterial = eyeMaterial;
                sr.color = new Color(1f, 1f, 1f, 0f);
                renderers.Add(sr);
            }

            watcher.eyes = renderers.ToArray();
            watcher.maxOpen = maxOpen;
            watcher.brightness = 0.9f;
            if (Camera.main != null) cam = Camera.main.transform;
        }

        // Pairs face the camera plane, so the mouse lean in GlowingEyes (local X/Y) reads as screen left/right/up/down
        private void LateUpdate()
        {
            if (cam == null) return;
            foreach (var a in anchors) a.rotation = cam.rotation;
        }
    }
}
