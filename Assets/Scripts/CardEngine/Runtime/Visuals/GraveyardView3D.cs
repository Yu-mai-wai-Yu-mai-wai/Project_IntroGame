using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // หลุมศพ: holds both the player's and the enemy's discarded / destroyed cards. Click it to open the
    // graveyard screen (GraveyardPanelUI), which lists them.
    // Drag the scene's graveyard model (e.g. GraveyardZone from the table set) into Graveyard Object and
    // that model becomes the graveyard: it gets the click and the hover tint.
    // Left empty, the object this script sits on is used (a plain stone slab is built if it has no mesh).
    public class GraveyardView3D : MonoBehaviour
    {
        public static GraveyardView3D Instance { get; private set; }

        [Header("Graveyard Object")]
        [Tooltip("The scene object that is the graveyard. Empty = this object.")]
        public GameObject graveyardObject;

        [Header("Hover")]
        public bool tintOnHover = true;
        public Color hoverColor = new Color(0.75f, 0.7f, 0.85f);

        [Header("Fallback Slab (only when there is no graveyard model)")]
        public Vector3 slabSize = new Vector3(1f, 0.12f, 1.35f);
        public Color slabColor = new Color(0.22f, 0.21f, 0.23f);

        private GameObject target;
        private Renderer[] targetRenderers;
        private readonly List<Color> baseColors = new List<Color>();
        private bool hovered;

        // Player's and enemy's graveyard together
        public static int CardCount
        {
            get
            {
                int count = CardManager.Instance != null ? CardManager.Instance.DiscardPile.Count : 0;
                var enemy = CombatManager.Instance != null ? CombatManager.Instance.EnemyCards : null;
                if (enemy != null) count += enemy.DiscardCount;
                return count;
            }
        }

        // Where a card flies to when it goes to the graveyard: the top of the graveyard model
        public Vector3 DropPoint
        {
            get
            {
                if (target == null || targetRenderers == null) return transform.position;
                var bounds = WorldBounds();
                return bounds.center + Vector3.up * bounds.extents.y;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            target = graveyardObject != null ? graveyardObject : gameObject;
            targetRenderers = target.GetComponentsInChildren<Renderer>();
            if (targetRenderers.Length == 0) targetRenderers = new[] { BuildSlab(target.transform) };

            EnsureCollider();

            baseColors.Clear();
            foreach (var r in targetRenderers) baseColors.Add(GetColor(r.material));
        }

        // Raycast instead of OnMouse* so the graveyard object needs no script of its own
        private void Update()
        {
            if (TawanOS.UI.PauseMenu.IsPaused) return;
            bool over = IsPointerOver();
            if (over != hovered)
            {
                hovered = over;
                SetTint(over && tintOnHover);
            }

            if (over && Input.GetMouseButtonDown(0)
                && !CardTargeting3D.BlocksInput && !CardPlayController3D.BlocksInput && !CombatCameraRig3D.BlocksInput)
            {
                GraveyardPanelUI.Ensure().Show();
            }
        }

        private bool IsPointerOver()
        {
            var cam = Camera.main;
            if (cam == null || target == null) return false;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out var hit, 500f) && hit.transform.IsChildOf(target.transform);
        }

        private void SetTint(bool on)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null) continue;
                SetColor(targetRenderers[i].material, on ? baseColors[i] * hoverColor : baseColors[i]);
            }
        }

        // ---------------------------------------------------------------- setup helpers

        // The model needs a collider to be clicked; one fitted to its meshes is added if it has none
        private void EnsureCollider()
        {
            if (target.GetComponentInChildren<Collider>() != null) return;

            var bounds = WorldBounds();
            var box = target.AddComponent<BoxCollider>();
            var t = target.transform;
            box.center = t.InverseTransformPoint(bounds.center);
            var size = t.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        private Renderer BuildSlab(Transform parent)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = new Vector3(0f, slabSize.y * 0.5f, 0f);
            slab.transform.localScale = slabSize;
            var renderer = slab.GetComponent<Renderer>();
            SetColor(renderer.material, slabColor);
            return renderer;
        }

        private Bounds WorldBounds()
        {
            var bounds = new Bounds(target.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in targetRenderers)
            {
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        // URP Lit uses _BaseColor, built-in shaders use _Color
        private static Color GetColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            return m.HasProperty("_Color") ? m.color : Color.white;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.color = c;
        }
    }
}
