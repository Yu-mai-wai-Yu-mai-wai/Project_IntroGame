using UnityEngine;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class CardView3D : MonoBehaviour
    {
        [Header("Card Data Reference")]
        public CardInstance CardData;

        [Header("Visuals")]
        public Renderer cardRenderer;
        public TMP_Text nameLabel;

        [Header("Card Face Pictures (from CardDataSO: Card Background / Artwork)")]
        [Tooltip("Frame used when a White Magic card has no Card Background of its own (text-free frame).")]
        public Sprite defaultWhiteFrame;
        [Tooltip("Frame used when a Black Magic card has no Card Background of its own (text-free frame).")]
        public Sprite defaultBlackFrame;
        [Tooltip("Artwork area on the card face, as a fraction of the face (width, height): the frame's picture window.")]
        public Vector2 artworkArea = new Vector2(0.84f, 0.54f);
        [Tooltip("Artwork centre, up from the middle of the face (fraction of the face height).")]
        public float artworkOffsetY = 0.16f;

        [Header("Face Text Colours")]
        public Color textOnArt = Color.white;
        public Color typeTextOnArt = new Color(0.95f, 0.72f, 0.72f);
        public Color textOnPlainWhite = new Color(0.12f, 0.08f, 0.05f);
        public Color textOnPlainBlack = new Color(0.95f, 0.9f, 0.85f);

        [Header("Interaction Settings")]
        public float hoverLift = 0.4f;
        public float hoverPullToCamera = 0.3f;
        public float hoverScale = 1.15f;

        private Vector3 baseLocalScale;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private bool isPlacedOnBoard;
        private bool isHeld;

        private SpriteRenderer backgroundFace;
        private SpriteRenderer artworkFace;
        private bool faceVisible = true;

        // Face text laid out like the card design: cost top-left, name + type top-right,
        // attack / Khwan in the middle band, ability text in the bottom box. nameLabel is the name.
        private TMP_Text costLabel, typeLabel, attackLabel, khwanLabel, descriptionLabel;

        public Vector3 BaseScale => baseLocalScale;
        public bool IsInHand => !isPlacedOnBoard && enabled;

        private void Awake()
        {
            baseLocalScale = transform.localScale;
        }

        public void Bind(CardInstance card)
        {
            CardData = card;
            if (card == null) return;

            RefreshLabel();

            if (cardRenderer != null)
            {
                cardRenderer.material.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.85f, 0.8f, 0.55f)
                    : new Color(0.35f, 0.1f, 0.15f);
            }

            BuildFacePictures();
            BuildFaceText();
            RefreshLabel();
        }

        // The card cube's face is its -Z side (the side that faces the camera in the hand and faces up
        // on the board). Pictures sit just in front of it; the name label stays on top of them.
        private const float FaceZ = -0.505f;

        private void BuildFacePictures()
        {
            Sprite background = FaceBackground();
            Sprite artwork = CardData.artwork;

            backgroundFace = SetFacePicture(backgroundFace, "FaceBackground", background, Vector2.one, 0f, FaceZ, keepAspect: false);
            artworkFace = SetFacePicture(artworkFace, "FaceArtwork", artwork, artworkArea, artworkOffsetY, FaceZ - 0.005f, keepAspect: true);

            ApplyFaceVisibility();
        }

        // The card's own background, else the default frame of its magic school
        private Sprite FaceBackground()
        {
            if (CardData.cardBackground != null) return CardData.cardBackground;
            return CardData.magicSchool == MagicSchool.WhiteMagic ? defaultWhiteFrame : defaultBlackFrame;
        }

        // area / offsetY are fractions of the card face (the cube's local -0.5..0.5 square)
        private SpriteRenderer SetFacePicture(SpriteRenderer sr, string name, Sprite sprite, Vector2 area, float offsetY, float z, bool keepAspect)
        {
            if (sprite == null)
            {
                if (sr != null) sr.gameObject.SetActive(false);
                return sr;
            }

            if (sr == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            sr.gameObject.SetActive(true);
            sr.sprite = sprite;

            Vector2 size = sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) size = Vector2.one;

            // The face is stretched by the cube's scale (e.g. 0.7 x 1), so aspect is kept in world terms
            float faceAspect = baseLocalScale.y != 0f ? baseLocalScale.x / baseLocalScale.y : 1f;
            Vector2 target = area;
            if (keepAspect)
            {
                float spriteAspect = size.x / size.y;
                float areaAspect = area.x * faceAspect / area.y;
                if (spriteAspect > areaAspect) target.y = area.x * faceAspect / spriteAspect;
                else target.x = area.y * spriteAspect / faceAspect;
            }

            var t = sr.transform;
            t.localPosition = new Vector3(0f, offsetY, z);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(target.x / size.x, target.y / size.y, 1f);
            return sr;
        }

        // Face-down cards (the enemy's hand) hide their pictures
        public void SetFaceVisible(bool visible)
        {
            faceVisible = visible;
            ApplyFaceVisibility();
        }

        private void ApplyFaceVisibility()
        {
            if (backgroundFace != null) backgroundFace.enabled = faceVisible;
            if (artworkFace != null) artworkFace.enabled = faceVisible;

            foreach (var label in new[] { costLabel, typeLabel, descriptionLabel })
            {
                if (label != null) label.gameObject.SetActive(faceVisible);
            }
            RefreshLabel(); // attack / Khwan follow the face too
        }

        // Refreshes every value on the face (called again whenever the card takes damage or changes)
        public void RefreshLabel()
        {
            if (nameLabel == null || CardData == null) return;

            nameLabel.text = CardData.cardNameThai;

            if (costLabel != null)
            {
                costLabel.text = CardData.magicSchool == MagicSchool.WhiteMagic
                    ? CardData.meritCost.ToString()
                    : CardData.corruptionGain.ToString();
            }
            if (typeLabel != null) typeLabel.text = TypeLine(CardData);

            bool familiar = CardData.cardType == CardType.Familiar;
            bool hasKhwan = familiar || CardData.maxKhwan > 0;
            if (attackLabel != null)
            {
                attackLabel.gameObject.SetActive(familiar && faceVisible);
                attackLabel.text = CardData.familiarDamage.ToString();
            }
            if (khwanLabel != null)
            {
                khwanLabel.gameObject.SetActive(hasKhwan && faceVisible);
                khwanLabel.text = CardData.familiarHealth.ToString();
            }
            if (descriptionLabel != null) descriptionLabel.text = Description(CardData);
        }

        // "บริวาร  มนต์ดำ" (the • separator from the data becomes a gap, as on the card design)
        private static string TypeLine(CardInstance card)
        {
            string text = card.GetFormattedTypeText() ?? "";
            return text.Replace(" • ", "  ").Replace("•", " ");
        }

        private static string Description(CardInstance card)
        {
            string format = card.descriptionFormat ?? "";
            try { return string.Format(format, card.baseValue); }
            catch { return format; }
        }

        // ---------------------------------------------------------------- face text layout

        // Positions are fractions of the card face: x -0.5 (left) .. 0.5 (right), y 0.5 (top) .. -0.5 (bottom),
        // measured from the card design (939 x 1312). Sizes are fractions of the face width / height.
        private void BuildFaceText()
        {
            if (nameLabel == null || costLabel != null) return;

            costLabel = CloneLabel("CostLabel");
            typeLabel = CloneLabel("TypeLabel");
            attackLabel = CloneLabel("AttackLabel");
            khwanLabel = CloneLabel("KhwanLabel");
            descriptionLabel = CloneLabel("DescriptionLabel");

            PlaceLabel(nameLabel, new Vector2(0.225f, 0.365f), new Vector2(0.44f, 0.075f), TextAlignmentOptions.Center, 1.5f);
            PlaceLabel(typeLabel, new Vector2(0.225f, 0.3f), new Vector2(0.44f, 0.035f), TextAlignmentOptions.Center, 0.7f);
            PlaceLabel(costLabel, new Vector2(-0.374f, 0.397f), new Vector2(0.16f, 0.1f), TextAlignmentOptions.Center, 2f);
            PlaceLabel(attackLabel, new Vector2(-0.106f, -0.114f), new Vector2(0.1f, 0.055f), TextAlignmentOptions.Center, 1.2f);
            PlaceLabel(khwanLabel, new Vector2(0.105f, -0.114f), new Vector2(0.1f, 0.055f), TextAlignmentOptions.Center, 1.2f);
            PlaceLabel(descriptionLabel, new Vector2(0f, -0.29f), new Vector2(0.78f, 0.22f), TextAlignmentOptions.Center, 0.4f);
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;

            bool onArt = FaceBackground() != null;
            Color main = onArt ? textOnArt : (CardData.magicSchool == MagicSchool.WhiteMagic ? textOnPlainWhite : textOnPlainBlack);
            foreach (var label in new[] { nameLabel, costLabel, attackLabel, khwanLabel, descriptionLabel }) label.color = main;
            typeLabel.color = onArt ? typeTextOnArt : main;
        }

        private TMP_Text CloneLabel(string name)
        {
            var copy = Instantiate(nameLabel.gameObject, nameLabel.transform.parent);
            copy.name = name;
            return copy.GetComponent<TMP_Text>();
        }

        // Autosized into its box so it fits whatever the font's scale is; maxSize caps short texts
        private static void PlaceLabel(TMP_Text label, Vector2 facePos, Vector2 faceSize, TextAlignmentOptions align, float maxSize)
        {
            var t = label.transform;
            float z = t.localPosition.z;
            t.localPosition = new Vector3(facePos.x, facePos.y, z);

            // The face spans 1 x 1 in the card's local units; the label's own scale is undone so the box
            // covers exactly that fraction of the face
            var s = t.localScale;
            label.rectTransform.sizeDelta = new Vector2(
                faceSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                faceSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)));

            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = maxSize;
            label.margin = Vector4.zero;
        }

        // applyImmediately=false records the resting pose only, so the caller can animate to it
        // (used for the draw fly-in from the deck).
        public void SetRestingTransform(Vector3 localPos, Quaternion localRot, bool applyImmediately = true)
        {
            if (isPlacedOnBoard) return;
            originalLocalPosition = localPos;
            originalLocalRotation = localRot;
            // A held card keeps its pose at the side of the screen; it returns here afterwards
            if (!applyImmediately || isHeld) return;
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }

        // Held = selected and waiting at the side of the screen (CardPlayController3D)
        public bool IsHeld => isHeld;

        public void SetHeld(bool held)
        {
            isHeld = held;
        }

        // Board cards and the enemy's cards (disabled views) never react to the mouse
        private bool IgnoresMouse => !enabled || isPlacedOnBoard || isHeld || CardTargeting3D.BlocksInput;

        private void OnMouseEnter()
        {
            if (IgnoresMouse) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition + new Vector3(0, hoverLift, -hoverPullToCamera), 0.15f);
            transform.DOScale(baseLocalScale * hoverScale, 0.15f);
        }

        private void OnMouseExit()
        {
            if (IgnoresMouse) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.15f);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.15f);
            transform.DOScale(baseLocalScale, 0.15f);
        }

        // Click a hand card to pick it up (it waits at the side of the screen until played or cancelled)
        private void OnMouseDown()
        {
            if (IgnoresMouse) return;
            CardPlayController3D.Ensure().Select(this);
        }

        public void ReturnToHand()
        {
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            transform.DOScale(baseLocalScale, 0.25f);
        }

        // Called once the engine has put the card in its column
        public void PlaceOnBoard()
        {
            SnapToBoardSlot();
        }

        // Moves this 3D card itself onto its board slot, so the physical card sits in the slot
        private void SnapToBoardSlot()
        {
            // The engine has already put the card in a column; sit in that column's slot
            BoardSlotView targetSlot = FindPlayerSlot(CardData.boardSlot);
            if (targetSlot == null)
            {
                Debug.LogWarning($"[CardView3D] SnapToBoardSlot: no empty Player BoardSlotView available for {CardData.cardNameThai}.");
                transform.DOKill();
                Destroy(gameObject);
                return;
            }

            transform.SetParent(targetSlot.transform, worldPositionStays: true);
            transform.DOKill();
            transform.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.localPosition = Vector3.zero);
            // Local rotation identity: the card inherits the slot's tilt and lies flat on the table.
            transform.DOLocalRotateQuaternion(Quaternion.identity, 0.3f)
                .OnComplete(() => transform.localRotation = Quaternion.identity);
            transform.DOScale(baseLocalScale, 0.3f);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // The card has been committed to the board; it no longer drags/hovers like a hand card
            isPlacedOnBoard = true;
            enabled = false;
        }

        private static BoardSlotView FindPlayerSlot(int index)
        {
            if (index < 0) return null;
            foreach (BoardSlotView slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.side != BoardSlotView.SlotSide.Player) continue;
                if (slot.GetComponent<DeckPileView3D>() != null) continue;
                if (slot.slotIndex == index) return slot;
            }
            return null;
        }
    }
}
