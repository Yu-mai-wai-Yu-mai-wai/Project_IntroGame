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

        private Vector3 baseLocalScale;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private bool isPlacedOnBoard;
        private bool isHeld;
        private bool isHovered;
        private bool wasIgnoringMouse;

        public bool IsHovered => isHovered;

        private SpriteRenderer backgroundFace;
        private SpriteRenderer artworkFace;
        private SpriteRenderer backFace;
        private MeshRenderer emberFace; // upgraded cards smoulder at the edge (CardEmber)
        private bool faceVisible = true;

        // Face text laid out like the card design: cost top-left, name + type top-right,
        // attack / Khwan in the middle band, ability text in the bottom box. nameLabel is the name.
        private TMP_Text costLabel, typeLabel, attackLabel, khwanLabel, descriptionLabel;

        // The statuses on a card in play are shown as symbols (CardStatusTags refreshes them): the set last drawn
        private string shownStatuses = "";
        private Color faceTextColor = Color.white; // the face's normal text colour (khwanLabel goes back to it when healed)

        public Vector3 BaseScale => baseLocalScale;
        public bool IsInHand => !isPlacedOnBoard && enabled;

        private void Awake()
        {
            baseLocalScale = transform.localScale;
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
                costLabel.text = CardData.magicSchool == MagicSchool.WhiteMagic
                    ? CardData.meritCost.ToString()
                    : CardData.corruptionGain.ToString();
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
                attackLabel.text = attack.ToString();
                attackLabel.color = attack > CardData.familiarDamage ? attackRaisedColor
                    : attack < CardData.familiarDamage ? attackLoweredColor : faceTextColor;
            }
            if (khwanLabel != null)
            {
                khwanLabel.gameObject.SetActive(hasKhwan && faceVisible);
                khwanLabel.text = CardData.familiarHealth.ToString();
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
        public bool IgnoresMouse => !enabled || isPlacedOnBoard || isHeld || isDying || CardTargeting3D.BlocksInput
            || CombatCameraRig3D.BlocksInput;

        private void OnMouseEnter()
        {
            if (IgnoresMouse) return;
            // Only a real mouse-over sounds; Hover() is also called to resync after the card book closes
            if (!isHovered) TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_card_hover");
            Hover();
        }

        public void Hover()
        {
            isHovered = true;
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition + new Vector3(0, hoverLift, -hoverPullToCamera), 0.15f);
            transform.DOScale(baseLocalScale * hoverScale, 0.15f);
        }

        private void OnMouseExit()
        {
            if (IgnoresMouse) return;
            Unhover();
        }

        public void Unhover(bool instant = false)
        {
            isHovered = false;
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
        }

        // ---------------------------------------------------------------- status symbols

        // Redraws the symbols when the card's statuses, their stacks or the face change. The full list with turns left
        // is in the card book. Cheap when nothing changed: a short string compare, and nothing at all for a card
        // without statuses.
        public void RefreshStatusTags()
        {
            if (CardData == null || nameLabel == null) return;
            if (CardData.statuses.Count == 0 && shownStatuses.Length == 0) return;

            var b = new System.Text.StringBuilder();
            foreach (var s in CardData.statuses)
            {
                if (s == null || s.stacks <= 0) continue;
                b.Append((int)s.type).Append('x').Append(s.stacks).Append(' ');
            }
            if (b.Length > 0 && !faceVisible) b.Append("hidden");
            string key = b.ToString();
            if (key == shownStatuses) return;

            shownStatuses = key;
            ApplyStatusSymbols();
        }

        // A picture per status the cards give (ผวา, ผีบังตา, ยั่วยุ, เกราะคุ้มภัย; ผ้ายันต์กลับด้าน only swaps among these)
        // that tells what it does at a glance (StatusSymbols): sizes and places are fractions of
        // the face (the cube's local -0.5..0.5 square), sizes of the face's height so the pictures stay square
        private readonly System.Collections.Generic.Dictionary<string, SpriteRenderer> symbols =
            new System.Collections.Generic.Dictionary<string, SpriteRenderer>();

        private void ApplyStatusSymbols()
        {
            bool Has(CardStatusType t) => faceVisible && CardData.statuses.Exists(x => x != null && x.type == t && x.stacks > 0);

            // เกราะคุ้มภัย: a see-through shield in the middle of the picture, with a halo behind it and a ring that
            // flashes out from it (ShieldShimmer makes them pulse)
            bool prot = Has(CardStatusType.Protect);
            Symbol("ShieldRipple", prot, StatusSymbols.ShieldGlow, ShieldCenter, ShieldSize * ShieldShimmer.GlowScale, 0f, Color.clear, 0f, order: SymbolOrder);
            Symbol("ShieldGlow", prot, StatusSymbols.ShieldGlow, ShieldCenter, ShieldSize * ShieldShimmer.GlowScale, 0f, Color.clear, 0f, order: SymbolOrder);
            Symbol("Shield", prot, StatusSymbols.Shield, ShieldCenter, ShieldSize, ShieldSize, ShieldShimmer.Base, 0f, order: SymbolOrder + 1);
            if (prot && symbols["Shield"].GetComponent<ShieldShimmer>() == null)
            {
                var shimmer = symbols["Shield"].gameObject.AddComponent<ShieldShimmer>();
                shimmer.shield = symbols["Shield"];
                shimmer.glow = symbols["ShieldGlow"];
                shimmer.ripple = symbols["ShieldRipple"];
            }
            // ยั่วยุ: the whole card glows gold at its edge
            Symbol("TauntFrame", Has(CardStatusType.Taunt), StatusSymbols.Frame, Vector2.zero, 1.03f, 1.02f,
                new Color(1f, 0.78f, 0.2f, 0.95f), 0f, stretch: true);
            // ผีบังตา: its ability text is crossed out
            var desc = CardFaceLayout.Description;
            Symbol("BlindCross", Has(CardStatusType.Blinded), StatusSymbols.Cross, desc.center, desc.size.y * 1.15f, desc.size.y * 1.15f,
                new Color(0.9f, 0.1f, 0.1f, 0.9f), 0f, stretch: true, widthFraction: desc.size.x * 0.75f);
            // ผวา: wisps of purple smoke sinking down the picture, more of them the more stacks (PhawaSmoke)
            var phawa = faceVisible ? CardData.statuses.Find(x => x != null && x.type == CardStatusType.Phawa && x.stacks > 0) : null;
            if (phawa != null && phawaSmoke == null)
            {
                var go = new GameObject("StatusPhawaSmoke");
                go.transform.SetParent(transform, false);
                phawaSmoke = go.AddComponent<PhawaSmoke>();
                phawaSmoke.faceZ = FaceZ - 0.003f;
                phawaSmoke.sortingOrder = SymbolOrder;
            }
            if (phawaSmoke != null)
            {
                phawaSmoke.gameObject.SetActive(phawa != null);
                if (phawa != null) phawaSmoke.SetStacks(phawa.stacks);
            }
        }

        private PhawaSmoke phawaSmoke;

        // height: a fraction of the face's height. A square picture keeps its shape (its width follows the card's aspect);
        // stretch spreads it over widthFraction of the face's width (or as wide as it is high in face units)
        private void Symbol(string name, bool on, Sprite sprite, Vector2 center, float height, float width, Color tint, float angle,
            bool stretch = false, float widthFraction = -1f, int order = SymbolOrder)
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
                sr.sortingOrder = order;
                symbols[name] = sr;
            }
            sr.sprite = sprite;
            sr.color = tint;
            sr.gameObject.SetActive(true);

            float faceAspect = baseLocalScale.y != 0f ? baseLocalScale.x / baseLocalScale.y : 0.716f;
            float x = stretch ? (widthFraction > 0f ? widthFraction : width) : height / faceAspect;
            var t = sr.transform;
            t.localPosition = new Vector3(center.x, center.y, FaceZ - 0.003f);
            t.localRotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(x, height, 1f);
        }

        // The เกราะคุ้มภัย shield: its place and size on the face
        private static readonly Vector2 ShieldCenter = new Vector2(0f, 0.12f);
        private const float ShieldSize = 0.3f;

        public void ResyncHover()
        {
            if (isHovered || Vector3.Distance(transform.localScale, baseLocalScale) > 0.01f)
            {
                if (!IsPointerOverCard())
                {
                    Unhover();
                }
            }
        }

        public bool IsPointerOverCard()
        {
            var cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return false;

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 200f);
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
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            transform.DOScale(baseLocalScale, 0.25f);
        }

        // Called once the engine has put the card in its column
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
