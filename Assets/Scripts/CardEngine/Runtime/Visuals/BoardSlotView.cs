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
        [Tooltip("Picture standing beside the amulet slot (column 0), like the candles beside the slots. " +
                 "Set by Tools/TawanOS/Combat Scene/Amulet Slot Icon; empty = no picture.")]
        public Sprite amuletSlotIcon;
        [Tooltip("Where the picture stands, in world units from the slot's centre (x = right, y = up, z = away from the player)")]
        public Vector3 amuletIconOffset = new Vector3(-0.75f, 0.3f, 0f);
        [Tooltip("Height of the picture in world units")]
        public float amuletIconHeight = 0.6f;

        private CardInstance currentCard;
        private Transform amuletIcon;

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

        // The amulet slot gets a small standing picture of an amulet beside it. It sits at the scene root, not under
        // the slot: the slot is scaled unevenly, which would skew a child. It turns to face the camera (LateUpdate).
        private void BuildAmuletIcon()
        {
            if (slotIndex != EffectResolver.AmuletSlot || amuletSlotIcon == null || amuletIcon != null) return;
            if (GetComponent<DeckPileView3D>() != null || name.Contains("Deck")) return;

            var go = new GameObject("AmuletSlotIcon");
            go.transform.position = transform.position + amuletIconOffset;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = amuletSlotIcon;
            float h = amuletSlotIcon.bounds.size.y;
            float scale = h > 0f ? amuletIconHeight / h : 1f;
            go.transform.localScale = Vector3.one * scale;
            amuletIcon = go.transform;
            FaceCamera();
        }

        private void LateUpdate()
        {
            if (amuletIcon != null) FaceCamera();
        }

        private void FaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            amuletIcon.rotation = Quaternion.LookRotation(amuletIcon.position - cam.transform.position, Vector3.up);
        }

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
