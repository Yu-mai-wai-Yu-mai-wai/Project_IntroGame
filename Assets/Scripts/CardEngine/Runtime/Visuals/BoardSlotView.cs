using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class BoardSlotView : MonoBehaviour
    {
        public enum SlotSide
        {
            Player, // Functional: mirrors CombatManager.State.activeBoardCards
            Enemy   // Visual placeholder only - not wired to any game state yet
        }

        [Header("Slot Identity")]
        public SlotSide side = SlotSide.Player;
        public int slotIndex = 0;

        [Header("Card Fit")]
        [Tooltip("Card width as a fraction of the slot's width (1 = exactly as wide). The card keeps its own shape, so its height follows.")]
        [Range(0.5f, 1f)] public float cardFill = 1f;

        [Header("UI Elements")]
        public Image slotFrameImage;
        public Image cardArtworkImage;
        public TMP_Text nameText;
        public TMP_Text statBadgeText;
        public GameObject occupiedRoot;
        public GameObject emptyRoot;

        [Header("Amulet Slot")]
        [Tooltip("Colour of the sigil drawn in the amulet slot's floor (column 0)")]
        public Color amuletSigilColour = new Color(0.95f, 0.78f, 0.42f, 0.55f);
        [Tooltip("Sigil size against the slot's width")]
        [Range(0.2f, 1f)] public float amuletSigilSize = 0.7f;

        private CardInstance currentCard;
        private Transform amuletIcon;
        private static Sprite sigilSprite;

        // Local scale for a card parented to this slot: as wide as the slot's mesh (a quad lying in the
        // slot's local X/Y), with the card's own shape kept, so it may reach past the slot's ends.
        // cardScale is the card's normal scale; its thickness is kept. Worked out in world size and then
        // divided by the slot's own scale, so a slot scaled unevenly (e.g. 1.8 x 3) does not stretch the card.
        public Vector3 FitCardScale(Vector3 cardScale)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null || cardScale.x <= 0f || cardScale.y <= 0f) return cardScale;

            Vector3 slotScale = transform.lossyScale;
            if (slotScale.x == 0f || slotScale.y == 0f || slotScale.z == 0f) return cardScale;

            float width = meshFilter.sharedMesh.bounds.size.x * Mathf.Abs(slotScale.x) * cardFill;
            float height = width * cardScale.y / cardScale.x;
            return new Vector3(width / Mathf.Abs(slotScale.x), height / Mathf.Abs(slotScale.y), cardScale.z / Mathf.Abs(slotScale.z));
        }

        private void Start()
        {
            if (side == SlotSide.Player && EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied += HandleSlotOccupied;
                EffectResolver.Instance.OnSlotCleared += HandleSlotCleared;
            }
            ClearSlot(playDeathFx: false);
            BuildAmuletIcon();
        }

        // The amulet slot (column 0) has a sigil drawn in its floor, like the marked slots in Inscryption, so it reads
        // as the place for an amulet (Game Designer, 22 Oct). It lies flat just above the slot at the scene root (the
        // slot is scaled unevenly, which would skew a child); a card placed there covers it.
        private void BuildAmuletIcon()
        {
            if (slotIndex != EffectResolver.AmuletSlot || amuletIcon != null) return;
            if (GetComponent<DeckPileView3D>() != null || name.Contains("Deck")) return;

            var go = new GameObject("AmuletSlotSigil");
            go.transform.position = transform.position + Vector3.up * 0.005f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SigilSprite();
            sr.color = amuletSigilColour;
            var meshFilter = GetComponent<MeshFilter>();
            float slotWidth = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.bounds.size.x * Mathf.Abs(transform.lossyScale.x) : 1.5f;
            go.transform.localScale = Vector3.one * (slotWidth * amuletSigilSize / sr.sprite.bounds.size.x);
            amuletIcon = go.transform;
        }

        // A ยันต์-like sigil drawn once in code: two rings, a square turned on its corner, and a ตะกรุด (a rolled
        // tube on a cord) in the middle
        private static Sprite SigilSprite()
        {
            if (sigilSprite != null) return sigilSprite;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ink = 0f;
                ink = Mathf.Max(ink, Line(Mathf.Abs(r - 0.95f), 0.025f));
                ink = Mathf.Max(ink, Line(Mathf.Abs(r - 0.85f), 0.012f));
                ink = Mathf.Max(ink, Line(Mathf.Abs(Mathf.Abs(dx) + Mathf.Abs(dy) - 0.78f), 0.018f) * (r < 0.85f ? 1f : 0f));
                // the ตะกรุด: an upright tube with bands, and the cord looping over its top
                if (Mathf.Abs(dx) < 0.13f && Mathf.Abs(dy + 0.05f) < 0.38f)
                {
                    float edge = Mathf.Min(0.13f - Mathf.Abs(dx), 0.38f - Mathf.Abs(dy + 0.05f));
                    ink = Mathf.Max(ink, edge < 0.03f ? 1f : 0.35f);
                    if (Mathf.Abs(Mathf.Repeat(dy + 0.05f, 0.19f) - 0.095f) < 0.012f) ink = 1f;
                }
                ink = Mathf.Max(ink, Line(Mathf.Abs(Mathf.Sqrt(dx * dx + (dy - 0.48f) * (dy - 0.48f)) - 0.12f), 0.018f) * (dy > 0.36f ? 1f : 0f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ink) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            sigilSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return sigilSprite;
        }

        private static float Line(float distance, float halfWidth) => Mathf.Clamp01(1f - (distance - halfWidth) / 0.012f);

        private void OnDestroy()
        {
            if (amuletIcon != null) Destroy(amuletIcon.gameObject);
            if (side == SlotSide.Player && EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied -= HandleSlotOccupied;
                EffectResolver.Instance.OnSlotCleared -= HandleSlotCleared;
            }
        }

        private void HandleSlotOccupied(CardInstance card, int targetIndex)
        {
            RemoveCardNoLongerOnBoard();
            if (targetIndex != slotIndex) return;
            OccupySlot(card);
        }

        private void HandleSlotCleared(int targetIndex)
        {
            RemoveCardNoLongerOnBoard();
            if (targetIndex != slotIndex) return;
            if (HasLiveBoardCard()) return; // see RemoveCardNoLongerOnBoard
            ClearSlot();
        }

        // A slot's physical card is removed only once it has left the board list (cards keep their
        // column, CardInstance.boardSlot, so this never removes a card that is still in play).
        private void RemoveCardNoLongerOnBoard()
        {
            if (side != SlotSide.Player) return;

            var card = GetComponentInChildren<CardView3D>();
            if (card == null || HasLiveBoardCard()) return;
            ClearSlot();
        }

        private bool HasLiveBoardCard()
        {
            var card = GetComponentInChildren<CardView3D>();
            if (card == null || CombatManager.Instance == null) return false;
            return CombatManager.Instance.State.activeBoardCards.Contains(card.CardData);
        }

        public void OccupySlot(CardInstance card)
        {
            currentCard = card;
            if (occupiedRoot != null) occupiedRoot.SetActive(true);
            if (emptyRoot != null) emptyRoot.SetActive(false);

            if (nameText != null) nameText.text = card.cardNameThai;
            if (cardArtworkImage != null && card.artwork != null)
            {
                cardArtworkImage.sprite = card.artwork;
                cardArtworkImage.gameObject.SetActive(true);
            }

            if (statBadgeText != null)
            {
                if (card.cardType == CardType.Familiar)
                    statBadgeText.text = $"ขวัญ:{card.familiarHealth} | ATK:{card.familiarDamage}";
                else if (card.maxKhwan > 0)
                    statBadgeText.text = $"ขวัญ:{card.familiarHealth}";
                else
                    statBadgeText.text = $"ความคงทน: {card.currentDurability}";
            }
        }

        public void ClearSlot(bool playDeathFx = true)
        {
            currentCard = null;
            if (occupiedRoot != null) occupiedRoot.SetActive(false);
            if (emptyRoot != null) emptyRoot.SetActive(true);

            // Remove the physical 3D card that was dropped into this slot, if any
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                var cardView = child.GetComponent<CardView3D>();
                if (cardView != null)
                {
                    if (cardView.IsDying) continue;

                    if (playDeathFx)
                    {
                        cardView.PlayDeathAnimation(this);
                    }
                    else
                    {
                        child.DOKill();
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}
