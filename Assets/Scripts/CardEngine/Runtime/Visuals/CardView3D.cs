using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using DG.Tweening;
using TawanOS.VFX;

namespace TawanOS.CardEngine
{
    public class CardView3D : MonoBehaviour
    {
        [Header("Card Data")]
        [Tooltip("ลาก Card Data มาใส่: การ์ด 3D นี้จะแสดงการ์ดใบนั้นทันทีใน Scene/Prefab และใช้ใบนั้นตอนกด Play " +
                 "(การ์ดบนมือที่เกมสร้างเองจะใช้ข้อมูลจากเด็คแทน)")]
        public CardDataSO cardDataAsset;

        // The live card this view shows (set by Bind at runtime, or from cardDataAsset)
        [System.NonSerialized] public CardInstance CardData;

        [Header("Visuals")]
        public Renderer cardRenderer;
        public TMP_Text nameLabel;

        [Header("Card Face Pictures (from CardDataSO: Card Background / Artwork)")]
        [Tooltip("Frame used when a White Magic card has no Card Background of its own (text-free frame).")]
        public Sprite defaultWhiteFrame;
        [Tooltip("Frame used when a Black Magic card has no Card Background of its own (text-free frame).")]
        public Sprite defaultBlackFrame;
        [Tooltip("Shown on face-down cards (the enemy's hand). Empty = the plain coloured block.")]
        public Sprite cardBack;

        [Header("Face Text Colours")]
        public Color textOnArt = Color.white;
        public Color typeTextOnArt = new Color(0.95f, 0.72f, 0.72f);
        public Color textOnPlainWhite = new Color(0.12f, 0.08f, 0.05f);
        public Color textOnPlainBlack = new Color(0.95f, 0.9f, 0.85f);
        [Tooltip("Khwan number colour while a Familiar / Amulet has lost Khwan (below its max).")]
        public Color khwanDamagedColor = new Color(0.9f, 0.1f, 0.1f);
        [Tooltip("Attack number while it hits for more than its printed attack (might, an adjacent aura).")]
        public Color attackRaisedColor = new Color(0.35f, 0.95f, 0.35f);
        [Tooltip("Attack number while it hits for less than its printed attack (ผวา).")]
        public Color attackLoweredColor = new Color(0.9f, 0.1f, 0.1f);

        [Header("Interaction Settings")]
        public float hoverLift = 1f;
        public float hoverPullToCamera = 0.6f;
        public float hoverScale = 1.15f;
        [Tooltip("A hovered hand card stands upright and rises at least this far, more if that is what it takes to show " +
                 "the whole card (its ability text) above the bottom of the screen, like Inscryption (Game Designer, 24 Oct).")]
        [Range(0f, 0.2f)] public float hoverScreenMargin = 0.02f;

        private Vector3 baseLocalScale;
        private BoxCollider box;
        private Vector3 boxSize, boxCenter; // the collider as the prefab has it; a hovered card stretches it down
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private bool isPlacedOnBoard;
        private bool isHeld;
        private bool isHovered;
        private bool wasIgnoringMouse;

        public bool IsHovered => isHovered;
        /// <summary>The hand card under the mouse, raised (null: none). The player's Khwan bar fades out of its way.</summary>
        public static CardView3D HoveredInHand { get; private set; }
        /// <summary>A hand card the tutorial keeps raised while it explains it: the mouse leaving it, or going over
        /// another card (to reach the tutorial's next button), does not drop it. Null = normal hovering.</summary>
        public static CardView3D PinnedHover;

        private SpriteRenderer backgroundFace;
        private SpriteRenderer artworkFace;
        private SpriteRenderer backFace;
        private MeshRenderer emberFace; // upgraded cards smoulder at the edge (CardEmber)
        private bool faceVisible = true;

        // Face text laid out like the card design: cost top-left, name + type top-right,
        // attack / Khwan in the middle band, ability text in the bottom box. nameLabel is the name.
        private TMP_Text costLabel, typeLabel, attackLabel, khwanLabel, descriptionLabel;
        // The face's parts, for the tutorial to point at
        public TMP_Text CostLabel => costLabel;
        public TMP_Text AttackLabel => attackLabel;
        public TMP_Text KhwanLabel => khwanLabel;
        public TMP_Text DescriptionLabel => descriptionLabel;

        // The statuses on a card in play are shown as symbols (CardStatusTags refreshes them): the set last drawn
        private string shownStatuses = "";
        private Color faceTextColor = Color.white; // the face's normal text colour (khwanLabel goes back to it when healed)

        public Vector3 BaseScale => baseLocalScale;
        public Vector3 RestingLocalPosition => originalLocalPosition;
        public Quaternion RestingLocalRotation => originalLocalRotation;
        public bool IsInHand => !isPlacedOnBoard && enabled;

        private void Awake()
        {
            baseLocalScale = transform.localScale;
            box = GetComponent<BoxCollider>();
            if (box != null) { boxSize = box.size; boxCenter = box.center; }
        }

        // A card placed in the scene by hand shows its Card Data; views the game binds itself are left alone
        private void Start()
        {
            if (cardDataAsset != null && (CardData == null || CardData.source == null)) Bind(new CardInstance(cardDataAsset));
        }

#if UNITY_EDITOR
        // Edit-mode preview: the card face updates as soon as Card Data is assigned or edited
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                // Colours changed in the Inspector while playing: redo the face text
                if (CardData != null && nameLabel != null)
                {
                    BuildFaceText();
                    ApplyDrawOrder();
                    RefreshLabel();
                }
                return;
            }
            UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
        }

        public void RefreshEditorPreview()
        {
            if (this == null || Application.isPlaying || cardDataAsset == null) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return; // only in a scene or Prefab Mode

            if (baseLocalScale == Vector3.zero) baseLocalScale = transform.localScale;
            Bind(new CardInstance(cardDataAsset));
        }
#endif

        public void Bind(CardInstance card)
        {
            CardData = card;
            if (card == null) return;

            RefreshLabel();

            // (edit-mode preview leaves the shared material alone; the frame covers the cube anyway)
            if (cardRenderer != null && Application.isPlaying)
            {
                cardRenderer.material.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.85f, 0.8f, 0.55f)
                    : new Color(0.35f, 0.1f, 0.15f);
            }

            BuildFacePictures();
            BuildFaceText();
            ApplyFaceVisibility(); // the labels BuildFaceText just made follow the face too
            ApplyDrawOrder();
        }

        // A finished card PNG already has everything printed on it except attack and Khwan
        private bool HasCardImage => CardData != null && CardData.cardImage != null;

        // The frame, artwork and text are all transparent and sit a hair apart, so Unity's distance sort
        // could draw the frame over the text while the card moves. A Sorting Group keeps each card's parts
        // together (cards still sort against each other by distance) and fixes the order inside the card.
        // The artwork is the full-face background; the frame (see-through inside) is drawn over it, then the text.
        private const int ArtworkOrder = 0, FrameOrder = 1, EmberOrder = 2, TextOrder = 3, SymbolOrder = 4;

        private void ApplyDrawOrder()
        {
            if (GetComponent<SortingGroup>() == null) gameObject.AddComponent<SortingGroup>();

            if (backgroundFace != null) backgroundFace.sortingOrder = FrameOrder;
            if (backFace != null) backFace.sortingOrder = FrameOrder;
            if (artworkFace != null) artworkFace.sortingOrder = ArtworkOrder;
            if (emberFace != null) emberFace.sortingOrder = EmberOrder;
            foreach (var label in new[] { nameLabel, costLabel, typeLabel, attackLabel, khwanLabel, descriptionLabel })
            {
                var textRenderer = label != null ? label.GetComponent<Renderer>() : null;
                if (textRenderer != null) textRenderer.sortingOrder = TextOrder;
            }
        }

        // The card cube's face is its -Z side (the side that faces the camera in the hand and faces up
        // on the board). Pictures sit just in front of it; the name label stays on top of them.
        private const float FaceZ = -0.505f;

        private void BuildFacePictures()
        {
            Sprite background = FaceBackground();
            Sprite artwork = HasCardImage ? null : CardData.artwork;

            backgroundFace = SetFacePicture(backgroundFace, "FaceBackground", background, Vector2.one, 0f, FaceZ, keepAspect: false);
            artworkFace = SetFacePicture(artworkFace, "FaceArtwork", artwork, CardFaceLayout.Artwork.size, CardFaceLayout.Artwork.center.y, FaceZ + 0.005f, keepAspect: false);
            backFace = SetFacePicture(backFace, "FaceBack", cardBack, Vector2.one, 0f, FaceZ, keepAspect: false);
            SetEmber(Application.isPlaying && CardEmber.IsUpgraded(CardData));

            ApplyFaceVisibility();
        }

        // Upgraded cards: the dying-card fire burning along the card's frame line (a sheet over the face, under the text)
        private void SetEmber(bool on)
        {
            if (!on || CardEmber.Material == null)
            {
                if (emberFace != null) emberFace.gameObject.SetActive(false);
                return;
            }
            if (emberFace == null)
            {
                var existing = transform.Find("FaceEmber");
                emberFace = existing != null ? existing.GetComponent<MeshRenderer>() : null;
            }
            if (emberFace == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "FaceEmber";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                emberFace = go.GetComponent<MeshRenderer>();
                emberFace.sharedMaterial = CardEmber.Material;
                emberFace.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                emberFace.receiveShadows = false;
            }
            emberFace.gameObject.SetActive(true);
            // The face spans the cube's local -0.5..0.5 square; the quad faces -Z like the face
            var t = emberFace.transform;
            t.localPosition = new Vector3(0f, 0f, FaceZ - 0.002f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(CardEmber.Pad, CardEmber.Pad, 1f);
            var block = new MaterialPropertyBlock();
            block.SetFloat(CardEmber.SeedProperty, CardEmber.SeedFor(CardData));
            block.SetFloat(CardEmber.AspectProperty, baseLocalScale.y != 0f ? baseLocalScale.x / baseLocalScale.y : 0.716f);
            emberFace.SetPropertyBlock(block);
            emberFace.sortingOrder = EmberOrder;
        }

        // The finished card PNG, else nothing when the artwork has its own frame, else the card's own background,
        // else the default frame of its magic school
        private Sprite FaceBackground()
        {
            return CardFaceLayout.FaceFrame(CardData.cardImage, CardData.cardBackground, CardData.artwork,
                defaultWhiteFrame, defaultBlackFrame, CardData.magicSchool);
        }

        // The artwork is a finished face with the name and type line printed on it
        private bool ArtCarriesFrame => CardData != null && !HasCardImage && CardFaceLayout.ArtCarriesFrame(CardData.artwork);

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
                // Reuse the one built earlier (e.g. by the edit-mode preview), else make it
                var existing = transform.Find(name);
                sr = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
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
            if (backFace != null) backFace.enabled = !faceVisible;
            if (emberFace != null) emberFace.enabled = faceVisible;

            // A face-up card with a frame shows only the picture: the 3D block behind it is hidden
            // (its collider stays, so the card can still be clicked). Face-down cards keep the block as their back.
            bool hasFrame = backgroundFace != null && backgroundFace.gameObject.activeSelf && backgroundFace.sprite != null;
            bool hasArt = artworkFace != null && artworkFace.gameObject.activeSelf && artworkFace.sprite != null;
            if (cardRenderer != null) cardRenderer.enabled = !(faceVisible && (hasFrame || hasArt));

            // A finished card PNG has these printed on it; a framed artwork has the name and type line
            bool printed = HasCardImage;
            bool nameOnArt = printed || ArtCarriesFrame;
            foreach (var label in new[] { costLabel, descriptionLabel })
            {
                if (label != null) label.gameObject.SetActive(faceVisible && !printed);
            }
            foreach (var label in new[] { nameLabel, typeLabel })
            {
                if (label != null) label.gameObject.SetActive(faceVisible && !nameOnArt);
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
                shownCost = ShownCost(CardData);
                costLabel.text = shownCost.ToString();
            }
            if (typeLabel != null) typeLabel.text = TypeLine(CardData);

            bool isSpell = CardData.cardType == CardType.Incantation;
            bool familiar = CardData.cardType == CardType.Familiar;
            bool hasKhwan = !isSpell && (familiar || (CardData.cardType == CardType.Amulet && CardData.maxKhwan > 0));
            if (attackLabel != null)
            {
                // What it hits for right now (ผวา, might and the adjacent aura included), coloured when it differs.
                // An amulet shows it only once it has some, since that is when it strikes
                int attack = EffectResolver.Instance != null ? EffectResolver.Instance.EffectiveAttack(CardData) : CardData.familiarDamage;
                bool amuletStrikes = CardData.cardType == CardType.Amulet && attack > 0;
                attackLabel.gameObject.SetActive(!isSpell && (familiar || amuletStrikes) && faceVisible);
                // Grown past the printed card (เปรต, ผีปอบ, an aura...): the gain is shown small beside it (Game Designer, 22 Oct)
                int printedAttack = CardData.source != null ? CardData.source.familiarDamage : CardData.familiarDamage;
                attackLabel.text = attack > printedAttack ? $"{attack}<size=55%>+{attack - printedAttack}</size>" : attack.ToString();
                attackLabel.color = attack > printedAttack ? attackRaisedColor
                    : attack < printedAttack ? attackLoweredColor : faceTextColor;
            }
            if (khwanLabel != null)
            {
                khwanLabel.gameObject.SetActive(hasKhwan && faceVisible);
                int printedKhwan = CardData.source != null ? CardData.source.familiarHealth : CardData.maxKhwan;
                khwanLabel.text = CardData.maxKhwan > printedKhwan && printedKhwan > 0
                    ? $"{CardData.familiarHealth}<size=55%>+{CardData.maxKhwan - printedKhwan}</size>"
                    : CardData.familiarHealth.ToString();
                khwanLabel.color = CardData.familiarHealth < CardData.maxKhwan ? khwanDamagedColor : faceTextColor;
            }
            RefreshStatusTags();
            if (descriptionLabel != null)
            {
                descriptionLabel.text = KeywordGlossarySO.WrapKeywords(Description(CardData));
                if (descriptionLabel.GetComponent<TawanOS.UI.KeywordLinkTrigger>() == null)
                    descriptionLabel.gameObject.AddComponent<TawanOS.UI.KeywordLinkTrigger>();
            }
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

        // Positions come from CardFaceLayout (shared with the Card Data inspector preview)
        private void BuildFaceText()
        {
            if (nameLabel == null) return;

            if (costLabel == null) costLabel = CloneLabel("CostLabel");
            if (typeLabel == null) typeLabel = CloneLabel("TypeLabel");
            if (attackLabel == null) attackLabel = CloneLabel("AttackLabel");
            if (khwanLabel == null) khwanLabel = CloneLabel("KhwanLabel");
            if (descriptionLabel == null) descriptionLabel = CloneLabel("DescriptionLabel");

            // Text sizes come from the Card Data (Face Text Sizes)
            var src = CardData.source;
            PlaceLabel(nameLabel, CardFaceLayout.Name, 1.5f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Name));
            PlaceLabel(typeLabel, CardFaceLayout.Type, 0.7f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Type));
            PlaceLabel(costLabel, CardFaceLayout.Cost, 2f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Cost));
            PlaceLabel(attackLabel, CardFaceLayout.Attack, 1.2f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Stat));
            PlaceLabel(khwanLabel, CardFaceLayout.Khwan, 1.2f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Stat));
            PlaceLabel(descriptionLabel, CardFaceLayout.Description, 0.4f, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Description), fixedBox: true);
            descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
            // Statuses are symbols now; a row of text tags an older version saved into a scene stays hidden
            var oldTags = nameLabel.transform.parent != null ? nameLabel.transform.parent.Find("StatusLabel") : null;
            if (oldTags != null) oldTags.gameObject.SetActive(false);
            shownStatuses = "?"; // not a real status set: the next refresh applies the current one
            if (Application.isPlaying && GetComponent<CardStatusTags>() == null) gameObject.AddComponent<CardStatusTags>();

            bool onArt = FaceBackground() != null || ArtCarriesFrame;
            Color main = onArt ? textOnArt : (CardData.magicSchool == MagicSchool.WhiteMagic ? textOnPlainWhite : textOnPlainBlack);
            faceTextColor = main;
            foreach (var label in new[] { nameLabel, costLabel, attackLabel, khwanLabel, descriptionLabel }) label.color = main;
            typeLabel.color = onArt ? typeTextOnArt : main;
            foreach (var label in new[] { nameLabel, costLabel, typeLabel, attackLabel, khwanLabel, descriptionLabel }) CardFaceLayout.MakeReadable(label);
        }

        // Reuses a label built earlier (e.g. by the edit-mode preview), else copies the name label
        private TMP_Text CloneLabel(string name)
        {
            var existing = nameLabel.transform.parent != null ? nameLabel.transform.parent.Find(name) : null;
            if (existing != null && existing.GetComponent<TMP_Text>() != null) return existing.GetComponent<TMP_Text>();

            var copy = Instantiate(nameLabel.gameObject, nameLabel.transform.parent);
            copy.name = name;
            return copy.GetComponent<TMP_Text>();
        }

        // Autosized into its box so it fits whatever the font's scale is; maxSize caps short texts.
        // sizeMul grows the text: single-line labels get a box that much bigger (same centre), the
        // description keeps its box (so it still wraps inside the card) and only its size cap grows.
        private static void PlaceLabel(TMP_Text label, CardFaceLayout.Box box, float maxSize, float sizeMul, bool fixedBox = false)
        {
            Vector2 facePos = box.center, faceSize = fixedBox ? box.size : box.Scaled(sizeMul).size;
            maxSize *= sizeMul;
            var t = label.transform;
            float z = t.localPosition.z;
            t.localPosition = new Vector3(facePos.x, facePos.y, z);

            // The face spans 1 x 1 in the card's local units; the label's own scale is undone so the box
            // covers exactly that fraction of the face
            var s = t.localScale;
            label.rectTransform.sizeDelta = new Vector2(
                faceSize.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                faceSize.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)));

            label.alignment = TextAlignmentOptions.Center;
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

        private bool isDying;
        public bool IsDying => isDying;

        // Board cards, the enemy's cards (disabled views), held cards and dying cards never react to the mouse.
        // A hovered board card lifts off its collider and loops enter/exit (jitter), and its DOKill stops the clash tweens.
        // While a tutorial explanation is up the hand stays down, so no card rises over the box (Game Designer, 25 Oct)
        public bool IgnoresMouse => !enabled || isPlacedOnBoard || isHeld || isDying || CardTargeting3D.BlocksInput
            || CombatCameraRig3D.BlocksInput || TawanOS.UI.TutorialCoach.Blocking;

        private void OnMouseEnter()
        {
            if (IgnoresMouse) return;
            if (PinnedHover != null && PinnedHover != this) return;
            // No sound on mouse-over (Game Designer, 25 Oct: the quick ticks while sweeping the hand were too much)
            Hover();
        }

        public void Hover()
        {
            isHovered = true;
            HoveredInHand = this;
            Vector2 offset = HoverOffsetShowingWholeCard();
            float lift = offset.y;
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition + new Vector3(offset.x, lift, -hoverPullToCamera), 0.15f);
            transform.DOLocalRotateQuaternion(Quaternion.identity, 0.15f);
            transform.DOScale(baseLocalScale * hoverScale, 0.15f);

            // The raised card still answers the mouse where it rested, so a pointer near the bottom of the hand does not
            // see it rise away, leave it and start the hover over again
            if (box != null && baseLocalScale.y > 0f)
            {
                float reach = lift / (baseLocalScale.y * hoverScale);
                box.size = new Vector3(boxSize.x, boxSize.y + reach, boxSize.z);
                box.center = new Vector3(boxCenter.x, boxCenter.y - reach * 0.5f, boxCenter.z);
            }
        }

        // Where a hovered hand card goes, from its resting spot (x sideways, y up): up by hoverLift, or more until its
        // bottom edge is on screen but never so far that its top edge leaves it; and sideways, a card at the end of a
        // long hand, until both side edges are on screen
        private Vector2 HoverOffsetShowingWholeCard()
        {
            var cam = Camera.main;
            var parent = transform.parent;
            if (cam == null || parent == null) return new Vector2(0f, hoverLift);

            // The card mesh is a unit cube
            float halfH = 0.5f * Mathf.Abs(baseLocalScale.y) * hoverScale;
            float halfW = 0.5f * Mathf.Abs(baseLocalScale.x) * hoverScale;
            float ViewY(Vector3 local) => cam.WorldToViewportPoint(parent.TransformPoint(local)).y;
            float ViewX(Vector3 local) => cam.WorldToViewportPoint(parent.TransformPoint(local)).x;

            float lift = hoverLift;
            for (int i = 0; i < 120; i++, lift += 0.05f)
            {
                Vector3 centre = originalLocalPosition + new Vector3(0f, lift, -hoverPullToCamera);
                if (ViewY(centre + new Vector3(0f, halfH, 0f)) > 1f - hoverScreenMargin) { lift = Mathf.Max(hoverLift, lift - 0.05f); break; }
                if (ViewY(centre - new Vector3(0f, halfH, 0f)) >= hoverScreenMargin) break;
            }

            Vector3 at = originalLocalPosition + new Vector3(0f, lift, -hoverPullToCamera);
            float dx = 0f;
            for (int i = 0; i < 120 && ViewX(at + new Vector3(dx - halfW, 0f, 0f)) < hoverScreenMargin; i++) dx += 0.05f;
            for (int i = 0; i < 120 && ViewX(at + new Vector3(dx + halfW, 0f, 0f)) > 1f - hoverScreenMargin; i++) dx -= 0.05f;
            return new Vector2(dx, lift);
        }

        private void ResetHoverCollider()
        {
            if (HoveredInHand == this) HoveredInHand = null;
            if (box == null) return;
            box.size = boxSize;
            box.center = boxCenter;
        }

        private void OnMouseExit()
        {
            if (IgnoresMouse) return;
            if (PinnedHover == this) return;
            Unhover();
        }

        public void Unhover(bool instant = false)
        {
            isHovered = false;
            ResetHoverCollider();
            transform.DOKill();
            if (instant)
            {
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
                transform.localScale = baseLocalScale;
            }
            else
            {
                transform.DOLocalMove(originalLocalPosition, 0.15f);
                transform.DOLocalRotateQuaternion(originalLocalRotation, 0.15f);
                transform.DOScale(baseLocalScale, 0.15f);
            }
        }

        private void OnDestroy()
        {
            if (HoveredInHand == this) HoveredInHand = null;
        }

        private void Update()
        {
            if (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver && isHovered)
            {
                Unhover();
            }
            bool ignoring = IgnoresMouse;
            if (wasIgnoringMouse && !ignoring)
            {
                ResyncHover();
            }
            wasIgnoringMouse = ignoring;
            if (isHovered && !ignoring && IsInHand) TawanOS.UI.PointerCursor.Hover(); // a hand card can be picked up
        }

        // ---------------------------------------------------------------- status symbols

        // Redraws the symbols when the card's statuses, their stacks or the face change. The full list with turns left
        // is in the card book. Cheap when nothing changed: a short string compare, and nothing at all for a card
        // without statuses.
        private int shownCost = int.MinValue;
        private static int ShownCost(CardInstance card) => card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : card.corruptionGain;

        public void RefreshStatusTags()
        {
            if (CardData == null || nameLabel == null) return;
            // A cost changed after the face was drawn (กุมารทอง+ makes the card it draws cheaper)
            if (costLabel != null && ShownCost(CardData) != shownCost) RefreshLabel();
            bool life = ShowsLife;
            if (CardData.statuses.Count == 0 && shownStatuses.Length == 0 && !life) return;

            var b = new System.Text.StringBuilder();
            foreach (var s in CardData.statuses)
            {
                if (s == null || s.stacks <= 0) continue;
                b.Append((int)s.type).Append('x').Append(s.stacks).Append(' ');
            }
            if (life) b.Append("life").Append(CardData.currentDurability).Append('u').Append(EffectResolver.UsesLeft(CardData)).Append(' ');
            if (b.Length > 0 && !faceVisible) b.Append("hidden");
            string key = b.ToString();
            // The icons are also redrawn when the card's shape on screen changes (it was moved onto a board slot)
            bool reshaped = key.Length > 0 && Mathf.Abs(WorldFaceAspect() - iconAspect) > 0.01f;
            if (key == shownStatuses && !reshaped) return;

            shownStatuses = key;
            ApplyStatusSymbols();
        }

        // The face's real width over its height in the world. On a board slot the card's own scale is set to make up
        // for the slot's (BoardSlotView.FitCardScale), so the card's local scale alone does not give its shape.
        private float WorldFaceAspect()
        {
            float w = transform.TransformVector(Vector3.right).magnitude;
            float h = transform.TransformVector(Vector3.up).magnitude;
            return h > 0f ? w / h : 0.716f;
        }

        private float iconAspect = -1f; // the face shape the icons were last laid out for

        // An icon per status the cards give (ผวา, ผีบังตา, ยั่วยุ, เกราะคุ้มภัย; ผ้ายันต์กลับด้าน only swaps among these),
        // in a row from the face's bottom-right corner leftwards (StatusSymbols). Places are fractions of the face
        // (the cube's local -0.5..0.5 square), sizes of the face's height so the icons stay round.
        private readonly System.Collections.Generic.Dictionary<string, SpriteRenderer> symbols =
            new System.Collections.Generic.Dictionary<string, SpriteRenderer>();

        private static readonly CardStatusType[] IconOrder =
            { CardStatusType.DoneKhong, CardStatusType.Protect, CardStatusType.Taunt, CardStatusType.Blinded, CardStatusType.Phawa };
        private static readonly Vector2 IconCorner = new Vector2(0.35f, -0.4f);  // the right-most icon's centre
        private const float IconSize = 0.1f, IconGap = 0.012f;

        private void ApplyStatusSymbols()
        {
            float faceAspect = iconAspect = WorldFaceAspect();
            float step = (IconSize + IconGap) / faceAspect; // one icon's width plus the gap, in face widths
            int shown = 0;
            foreach (var type in IconOrder)
            {
                bool on = faceVisible && CardData.statuses.Exists(x => x != null && x.type == type && x.stacks > 0);
                var center = IconCorner - new Vector2(step * shown, 0f);
                Symbol(type.ToString(), on, StatusSymbols.For(type), center, IconSize, faceAspect);
                if (type == CardStatusType.DoneKhong)
                {
                    var curse = on ? CardData.statuses.Find(x => x != null && x.type == type && x.stacks > 0) : null;
                    BadgeNumber("CurseStacks", on, curse != null ? curse.stacks : 0, center, faceAspect);
                }
                if (on) shown++;
            }

            // An amulet on the board: an hourglass at the lower left with the turns it has left (Game Designer, 25 Oct)
            bool life = faceVisible && ShowsLife;
            Symbol(LifeSymbol, life, StatusSymbols.Hourglass, LifeCorner, LifeIconSize, faceAspect);
            BadgeNumber("LifeTurns", life, CardData.currentDurability, LifeCorner, faceAspect, LifeIconSize);

            // One that breaks after a few uses (ตะกรุดดำ, ผ้ายันต์มหาอุด): tally marks with the uses left, right of the hourglass
            int uses = life ? EffectResolver.UsesLeft(CardData) : 0;
            var usesCorner = LifeCorner + new Vector2(LifeIconSize * 1.5f / faceAspect, 0f); // clear of the hourglass's number
            Symbol(UsesSymbol, uses > 0, StatusSymbols.Tally, usesCorner, LifeIconSize, faceAspect);
            BadgeNumber("UsesLeft", uses > 0, uses, usesCorner, faceAspect, LifeIconSize);
        }

        private const string LifeSymbol = "AmuletLife", UsesSymbol = "AmuletUses";
        // A little bigger than the status icons: an amulet on a board slot is small, and its turns left matter (Game Designer, 25 Oct; made smaller later the same day)
        private const float LifeIconSize = 0.12f;
        private static readonly Vector2 LifeCorner = new Vector2(-0.33f, -0.39f);
        private bool ShowsLife => isPlacedOnBoard && CardData != null && CardData.cardType == CardType.Amulet && CardData.currentDurability > 0;

        /// <summary>True when the mouse is on an amulet's hourglass (its turns left).</summary>
        public bool LifeUnderPointer() => SymbolUnderPointer(LifeSymbol);

        /// <summary>True when the mouse is on an amulet's tally marks (its uses left).</summary>
        public bool UsesUnderPointer() => SymbolUnderPointer(UsesSymbol);

        private bool SymbolUnderPointer(string name)
        {
            if (CardData == null || !faceVisible || !symbols.TryGetValue(name, out var sr) || sr == null || !sr.gameObject.activeSelf) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(transform.forward, transform.TransformPoint(new Vector3(0f, 0f, FaceZ)));
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(enter));
            var p = sr.transform.localPosition;
            var s = sr.transform.localScale;
            if (Mathf.Abs(local.x - p.x) > s.x * 0.5f || Mathf.Abs(local.y - p.y) > s.y * 0.5f) return false;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && events.IsPointerOverGameObject()) return false;
            return IsPointerOverCard(Input.mousePosition);
        }

        // The status whose icon is under the mouse, or null. The mouse ray meets the face's plane and the hit is tested
        // against each icon's square; the card must also be the first thing the ray hits (not under another card) and
        // the mouse must not be over the UI.
        public CardStatus StatusUnderPointer() => StatusAt(Input.mousePosition, checkUi: true);

        public CardStatus StatusAt(Vector2 screenPosition, bool checkUi = false)
        {
            if (CardData == null || !faceVisible) return null;
            var cam = Camera.main;
            if (cam == null) return null;

            CardStatusType? hit = null;
            var ray = cam.ScreenPointToRay(screenPosition);
            var plane = new Plane(transform.forward, transform.TransformPoint(new Vector3(0f, 0f, FaceZ)));
            if (!plane.Raycast(ray, out float enter)) return null;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(enter));
            foreach (var type in IconOrder)
            {
                if (!symbols.TryGetValue(type.ToString(), out var sr) || sr == null || !sr.gameObject.activeSelf) continue;
                var p = sr.transform.localPosition;
                var s = sr.transform.localScale;
                if (Mathf.Abs(local.x - p.x) <= s.x * 0.5f && Mathf.Abs(local.y - p.y) <= s.y * 0.5f) { hit = type; break; }
            }
            if (hit == null) return null;

            var events = UnityEngine.EventSystems.EventSystem.current;
            if (checkUi && events != null && events.IsPointerOverGameObject()) return null;
            if (!IsPointerOverCard(screenPosition)) return null;
            return CardData.statuses.Find(x => x != null && x.type == hit.Value && x.stacks > 0);
        }

        private readonly System.Collections.Generic.Dictionary<string, TMP_Text> badgeNumbers =
            new System.Collections.Generic.Dictionary<string, TMP_Text>();

        // A number hanging at the lower right of an icon: สาป's stacks, an amulet's turns left (Game Designer, 25 Oct)
        private void BadgeNumber(string name, bool on, int value, Vector2 iconCenter, float faceAspect, float iconSize = IconSize)
        {
            badgeNumbers.TryGetValue(name, out var label);
            if (!on)
            {
                if (label != null) label.gameObject.SetActive(false);
                return;
            }
            if (label == null)
            {
                label = CloneLabel(name);
                var r = label.GetComponent<Renderer>();
                if (r != null) r.sortingOrder = SymbolOrder + 1; // over the icon
                CardFaceLayout.MakeReadable(label);
                badgeNumbers[name] = label;
            }
            label.gameObject.SetActive(true);
            label.text = value.ToString();
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            PlaceLabel(label, new CardFaceLayout.Box(iconCenter.x + iconSize * 0.45f / faceAspect, iconCenter.y - iconSize * 0.42f, 0.14f, 0.1f), 2.4f, 1f);
            label.enableAutoSizing = false; // one or two digits: a fixed size, about the stat numbers
            label.fontSize = 0.5f * iconSize / IconSize;
            var t = label.transform;
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, FaceZ - 0.004f);
        }

        // height: a fraction of the face's height. The square icon keeps its shape (its width follows the card's aspect).
        private void Symbol(string name, bool on, Sprite sprite, Vector2 center, float height, float faceAspect)
        {
            symbols.TryGetValue(name, out var sr);
            if (!on)
            {
                if (sr != null && sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                return;
            }
            if (sr == null)
            {
                var go = new GameObject("Status" + name);
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SymbolOrder;
                symbols[name] = sr;
            }
            sr.sprite = sprite;
            sr.gameObject.SetActive(true);

            var t = sr.transform;
            t.localPosition = new Vector3(center.x, center.y, FaceZ - 0.003f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(height / faceAspect, height, 1f);
        }

        public void ResyncHover()
        {
            if (PinnedHover == this) return; // the tutorial holds it up
            if (isHovered || Vector3.Distance(transform.localScale, baseLocalScale) > 0.01f)
            {
                if (!IsPointerOverCard())
                {
                    Unhover();
                }
            }
        }

        public bool IsPointerOverCard() => IsPointerOverCard(Input.mousePosition);

        public bool IsPointerOverCard(Vector2 screenPosition)
        {
            var cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return false;

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPosition), 200f);
            if (hits == null || hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits[0].collider.GetComponentInParent<CardView3D>() == this;
        }

        // Click a hand card to pick it up (it waits at the side of the screen until played or cancelled)
        private void OnMouseDown()
        {
            if (IgnoresMouse || isPlacedOnBoard) return;
            CardPlayController3D.Ensure().Select(this);
        }

        public void ReturnToHand()
        {
            isHovered = false;
            ResetHoverCollider();
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            transform.DOScale(baseLocalScale, 0.25f);
        }

        // Called once the engine has put the card in its column
        /// <summary>An enemy card set in a board slot by EnemyBoardView3D: it shows what a board card shows (an amulet's hourglass).</summary>
        public void MarkOnBoard() => isPlacedOnBoard = true;

        public void PlaceOnBoard()
        {
            // Auras already in play (amulets) changed this card's Khwan / attack the moment it was played,
            // while it was still in the hand where the board refresh does not look, so show them now
            RefreshLabel();
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
            ResetHoverCollider();
            transform.DOKill();
            transform.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.localPosition = Vector3.zero);
            // Local rotation identity: the card inherits the slot's tilt and lies flat on the table.
            transform.DOLocalRotateQuaternion(Quaternion.identity, 0.3f)
                .OnComplete(() => transform.localRotation = Quaternion.identity);
            transform.DOScale(targetSlot.FitCardScale(baseLocalScale), 0.3f);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            originalLocalPosition = Vector3.zero;
            originalLocalRotation = Quaternion.identity;
            baseLocalScale = targetSlot.FitCardScale(baseLocalScale);
            isPlacedOnBoard = true;
            isHovered = false;
            // Black smoke or streaks of light from under the card as it lands
            TawanOS.VFX.CardLandVfx.Play(transform, CardData.magicSchool == MagicSchool.BlackMagic);

            // The card has been committed to the board; it no longer hovers like a hand card
            enabled = false;
        }

        /// <summary>
        /// A card sent back to the hand (CardInstance.returnedFrom): puts `handView` where the board card lies, at its
        /// angle and size, and removes the board card without a death, so the hand's layout flies the card up from the
        /// table instead of it burning away and a new one coming off the deck. False when the board card is not on the table.
        /// </summary>
        public static bool TakeOverFromBoard(CardInstance boardCard, CardView3D handView)
        {
            if (boardCard == null || handView == null) return false;
            foreach (var view in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
            {
                if (view == handView || view.CardData != boardCard || view.isDying) continue;
                var from = view.transform;
                var to = handView.transform;
                to.SetPositionAndRotation(from.position, from.rotation);
                var handSize = to.localScale;
                var parent = to.parent != null ? to.parent.lossyScale : Vector3.one;
                var size = from.lossyScale;
                to.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
                // Back to hand size on the way up; targeted at the view, not the transform, so the hand's layout
                // (which kills the transform's tweens) leaves it running
                to.DOScale(handSize, 0.4f).SetEase(Ease.OutCubic).SetTarget(handView);
                // Gone from the table at once; the slot that clears it later finds it dying and leaves it alone
                view.isDying = true;
                from.DOKill();
                Destroy(view.gameObject);
                return true;
            }
            return false;
        }

        private static float lastDeathFireSfxTime = -1f;

        public float PlayDeathAnimation(BoardSlotView slot = null)
        {
            if (isDying) return 0f;
            isDying = true;
            isHovered = false;

            transform.DOKill();

            if (slot == null) slot = GetComponentInParent<BoardSlotView>();
            Vector3 facing = Vector3.forward;
            if (BoardClashView3D.Instance != null && slot != null)
            {
                facing = BoardClashView3D.Instance.FacingOf(slot);
            }
            else if (slot != null)
            {
                facing = slot.side == BoardSlotView.SlotSide.Player ? Vector3.forward : Vector3.back;
            }

            Transform parentForRig = slot != null ? slot.transform : transform.parent;
            var rig = new CardClashRig(transform, parentForRig != null ? parentForRig : transform, facing);

            var config = CardDeathFxConfigSO.Load();
            float length = CardBurnEffect.Burn(transform, config);
            if (length > 0f)
            {
                if (Time.time - lastDeathFireSfxTime > 0.1f)
                {
                    lastDeathFireSfxTime = Time.time;
                    TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on");
                }
            }
            else
            {
                length = rig.Play(CardClashRig.Die);
            }

            rig.DestroyWithCard(length);
            return length;
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
