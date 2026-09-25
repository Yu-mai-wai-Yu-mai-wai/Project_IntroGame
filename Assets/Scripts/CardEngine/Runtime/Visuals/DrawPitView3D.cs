using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // หลุมจั่ว: a clickable pit on the table next to the draw pile. Each click during the player's turn
    // draws one random card from every card in the game and adds Corruption (see CardManager.DrawFromPit).
    // Bootstraps itself beside the DeckPileView3D, so no scene wiring is needed.
    public class DrawPitView3D : MonoBehaviour
    {
        [Header("Look")]
        public Vector3 offsetFromDeck = new Vector3(1.3f, 0f, 0f);
        public Vector3 pitSize = new Vector3(0.9f, 0.05f, 0.9f);
        public Color pitColor = new Color(0.05f, 0.02f, 0.08f);
        public Color hoverColor = new Color(0.35f, 0.1f, 0.45f);

        private Renderer pitRenderer;
        private Vector3 baseScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CardManager>() == null) return;
            if (FindFirstObjectByType<DrawPitView3D>() != null) return;

            var deck = FindFirstObjectByType<DeckPileView3D>();
            if (deck == null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "DrawPit3D";
            var pit = go.AddComponent<DrawPitView3D>();
            go.transform.position = deck.transform.position + pit.offsetFromDeck;
        }

        private void Start()
        {
            transform.localScale = new Vector3(pitSize.x, pitSize.y, pitSize.z);
            baseScale = transform.localScale;
            pitRenderer = GetComponent<Renderer>();
            if (pitRenderer != null) pitRenderer.material.color = pitColor;
        }

        private void OnMouseEnter()
        {
            if (pitRenderer != null) pitRenderer.material.color = hoverColor;
        }

        private void OnMouseExit()
        {
            if (pitRenderer != null) pitRenderer.material.color = pitColor;
        }

        private void OnMouseDown()
        {
            if (CardManager.Instance == null || !CardManager.Instance.DrawFromPit()) return;

            transform.DOKill();
            transform.localScale = baseScale;
            transform.DOPunchScale(baseScale * 0.15f, 0.25f, 6);
        }
    }
}
