using UnityEngine;

namespace TawanOS.CardEngine
{
    // Visual 3D draw pile lying on the table. Put it on the Player_DeckSlot object: the stack is
    // built at that slot's world position and its height follows CardManager.DrawPile.Count.
    // The stack is a standalone object (not a child) because the slot is tilted and non-uniformly
    // scaled, which would distort a child mesh.
    // Click the pile to list the cards left in it (DeckViewerPanelUI), sorted so the draw order stays hidden.
    public class DeckPileView3D : MonoBehaviour
    {
        [Header("Pile Look")]
        public Vector2 cardSize = new Vector2(0.7f, 1f); // width (x), depth (z) of a card lying flat
        public float thicknessPerCard = 0.02f;
        public float minVisibleThickness = 0.02f;
        public Color pileColor = new Color(0.25f, 0.1f, 0.15f);
        [Tooltip("Card-back material for the pile (e.g. CombatDemo/BackCardMat). Empty = plain pileColor.")]
        public Material pileMaterial;
        public float heightLerpSpeed = 10f;

        private Transform pile;
        private float currentThickness;

        // The pile's box in the world (also when empty): for clicks and the tutorial's spotlight
        public Bounds PileBounds
        {
            get
            {
                float height = Mathf.Max(currentThickness, minVisibleThickness);
                return new Bounds(transform.position + Vector3.up * (height * 0.5f), new Vector3(cardSize.x, height, cardSize.y));
            }
        }

        // Where drawn cards start flying from: the top face of the pile.
        public Vector3 DrawSpawnPosition => new Vector3(
            transform.position.x,
            transform.position.y + currentThickness + 0.05f,
            transform.position.z);

        private void Start()
        {
            var pileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pileGo.name = "DeckPile3D";
            Destroy(pileGo.GetComponent<Collider>()); // must not intercept card mouse events
            var pileRenderer = pileGo.GetComponent<Renderer>();
            if (pileMaterial != null) pileRenderer.sharedMaterial = pileMaterial;
            else pileRenderer.material.color = pileColor;
            pile = pileGo.transform;
            ApplyThickness(0f);
        }

        private void OnDestroy()
        {
            if (pile != null) Destroy(pile.gameObject);
        }

        private void Update()
        {
            if (TawanOS.UI.PauseMenu.IsPaused || !Input.GetMouseButtonDown(0)) return;
            if (CardTargeting3D.BlocksInput || CardPlayController3D.BlocksInput || CombatCameraRig3D.BlocksInput) return;
            if (!IsPointerOver()) return;

            DeckViewerPanelUI.Ensure().Show("กองจั่ว",
                () => CardManager.Instance != null ? CardManager.Instance.DrawPile : null,
                "กองจั่วหมดแล้ว");
        }

        // The pile has no collider (it must not intercept card mouse events), so test the ray against its box,
        // and let anything else in front of it (a card in hand) win
        private bool IsPointerOver()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!PileBounds.IntersectRay(ray, out float distance)) return false;

            return !Physics.Raycast(ray, out var hit, distance) || hit.transform.IsChildOf(transform);
        }

        private void LateUpdate()
        {
            if (pile == null) return;

            int count = CardManager.Instance != null ? CardManager.Instance.DrawPile.Count : 0;
            float target = count > 0 ? Mathf.Max(minVisibleThickness, count * thicknessPerCard) : 0f;
            ApplyThickness(Mathf.MoveTowards(currentThickness, target, heightLerpSpeed * Time.deltaTime));
        }

        private void ApplyThickness(float thickness)
        {
            currentThickness = thickness;
            pile.gameObject.SetActive(thickness > 0.0001f);
            pile.localScale = new Vector3(cardSize.x, Mathf.Max(thickness, 0.0001f), cardSize.y);
            pile.rotation = Quaternion.identity;
            pile.position = transform.position + Vector3.up * (thickness * 0.5f);
        }
    }
}
