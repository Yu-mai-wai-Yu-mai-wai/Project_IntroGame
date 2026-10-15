using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Shows the enemy's hand as 3D cards floating at the top of the camera view, mirroring the
    // player's camera-anchored hand at the bottom, plus a draw pile on Enemy_DeckSlot. Cards fly from
    // the pile into the hand when drawn and fly to the middle of the table (face up) when played.
    // Bootstraps itself and reuses the player's card prefab and fan layout settings.
    public class EnemyHandView3D : MonoBehaviour
    {
        [Tooltip("Auto-found from the scene's HandLayoutController3D when empty.")]
        public CardView3D cardPrefab;
        [Tooltip("Off = the enemy's hand is shown as face-down card backs.")]
        public bool showFaces = false;
        public Color backColor = new Color(0.25f, 0.1f, 0.15f);

        [Header("Hand Position")]
        [Tooltip("Scene object that marks where the enemy hand sits. Cards fan out along its right (X) axis, facing its forward (Z) axis, centred on its position. When empty, the hand is anchored to the camera using the offset below.")]
        public Transform handAnchor;

        [Header("Camera Anchor (used only when Hand Anchor is empty)")]
        [Tooltip("Offset from the camera (right, up, forward). Auto-mirrored from the player's hand anchor when Auto Mirror is on.")]
        public Vector3 offset = new Vector3(0f, 1.5f, 3f);
        public bool autoMirrorPlayerAnchor = true;
        [Tooltip("Scale of the cards in the enemy's hand (board cards are the card prefab's full size).")]
        public float handCardScale = 0.5f;

        [Header("Fan Layout (copied from the player's hand when found)")]
        public float cardSpacing = 0.9f;
        public float maxTotalWidth = 6f;
        public float arcAngle = 5f;
        public float curveDepth = 0.15f;

        [Header("Animation")]
        public float drawFlyDuration = 0.45f;
        public float drawStagger = 0.12f;
        [Tooltip("Seconds a card made in the hand out of nothing (a boss's per-turn card) takes to appear in fire, bottom to top.")]
        public float conjureRevealTime = 1f;
        [Tooltip("Incantations: time to fly from the showcase into the graveyard.")]
        public float playFlyDuration = 0.4f;
        [Tooltip("How far (in hand units) a card the enemy is weighing up is pulled out of the hand, toward the table.")]
        public float considerPull = 0.45f;

        [Header("Card Showcase")]
        [Tooltip("A cast incantation waits where the player's card waits while being played (same spot and size), for this long before it takes effect. Durations are divided by CombatManager's Enemy Play Speed.")]
        public float showcaseHoldTime = 1.6f;
        public float showcaseInDuration = 0.3f;
        [Tooltip("A familiar or amulet with abilities holds in showcase for this long.")]
        public float abilityCardHoldTime = 1.2f;

        [Header("Draw Pile")]
        public Vector2 pileCardSize = new Vector2(0.7f, 1f);
        public float thicknessPerCard = 0.02f;
        [Tooltip("Card-back material for the enemy pile. Empty = borrows from player pile or URP Unlit.")]
        public Material pileMaterial;

        private readonly List<CardView3D> views = new List<CardView3D>();
        private readonly List<CardView3D> newlyDrawn = new List<CardView3D>();
        private EnemyCardPlayer enemyCards;
        private Camera cam;
        private Transform container;
        private Transform playerHandParent; // the player's hand cards sit under it

        // The object the enemy's hand cards sit under (CombatCameraRig3D scales it with the camera zoom)
        public Transform Container => container;
        private Transform deckSlot;
        private Transform pile;
        private Vector3 tableCenter;
        private Quaternion tableRotation = Quaternion.identity;
        private bool layoutDirty;
        private bool simultaneousDraw;

        // AfterSceneLoad fires only for the first scene played; the combat scene is usually loaded later from the map.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; // no double hook without domain reload
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Bootstrap();

        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<EnemyHandView3D>() != null) return;

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (EnemyBoardView3D.IsEnemyBoardSlot(slot))
                {
                    new GameObject("EnemyHandView3D").AddComponent<EnemyHandView3D>();
                    return;
                }
            }
        }

        private void Start()
        {
            cam = Camera.main;

            var playerHand = FindFirstObjectByType<HandLayoutController3D>();
            if (playerHand != null)
            {
                if (cardPrefab == null) cardPrefab = playerHand.cardPrefab;
                playerHandParent = playerHand.handContainer != null ? playerHand.handContainer : playerHand.transform;
                cardSpacing = playerHand.cardSpacing;
                maxTotalWidth = playerHand.maxTotalWidth;
                arcAngle = playerHand.arcAngle;
                curveDepth = playerHand.curveDepth;

                var anchor = playerHand.GetComponent<CameraAnchoredHand>();
                if (autoMirrorPlayerAnchor && anchor != null && handAnchor == null)
                {
                    offset = new Vector3(anchor.offset.x, -anchor.offset.y, anchor.offset.z);
                }
            }

            if (cardPrefab == null)
            {
                Debug.LogWarning("[EnemyHandView3D] No card prefab found; the enemy hand will not be shown.");
                return;
            }

            FindTable();

            container = new GameObject("EnemyHandCards").transform;
            container.SetParent(transform, false);
            container.localScale = Vector3.one * handCardScale;
            AnchorToCamera();
            if (pileMaterial == null)
            {
                var playerPile = FindFirstObjectByType<DeckPileView3D>();
                if (playerPile != null && playerPile.pileMaterial != null)
                {
                    pileMaterial = playerPile.pileMaterial;
                }
            }
            BuildPile();

            var combat = CombatManager.Instance;
            if (combat == null) return;
            enemyCards = combat.EnemyCards;
            enemyCards.OnCardDrawn += HandleDrawn;
            enemyCards.OnCardPlayed += HandlePlayed;
            enemyCards.OnConsidering += HandleConsidering;
            enemyCards.RevealTime = RevealTime;
        }

        private void OnDestroy()
        {
            if (enemyCards == null) return;
            enemyCards.OnCardDrawn -= HandleDrawn;
            enemyCards.RevealTime = null;
            enemyCards.OnCardPlayed -= HandlePlayed;
            enemyCards.OnConsidering -= HandleConsidering;
        }

        // The card the enemy is weighing up is pulled out of the fan a little; the rest settle back
        private CardView3D considering;

        private void HandleConsidering(CardInstance card)
        {
            considering = card != null ? views.Find(v => v != null && v.CardData == card) : null;
            layoutDirty = true;
        }

        // Finds the enemy deck slot and the middle of the table (where played cards are shown)
        private void FindTable()
        {
            var enemyPositions = new List<Vector3>();
            var playerPositions = new List<Vector3>();

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (EnemyBoardView3D.IsEnemyBoardSlot(slot))
                {
                    enemyPositions.Add(slot.transform.position);
                    tableRotation = slot.transform.rotation;
                }
                else if (slot.side == BoardSlotView.SlotSide.Enemy) deckSlot = slot.transform;
                else if (slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck")) playerPositions.Add(slot.transform.position);
            }

            tableCenter = Average(enemyPositions);
            if (playerPositions.Count > 0) tableCenter = (tableCenter + Average(playerPositions)) * 0.5f;
            tableCenter += Vector3.up * 0.6f;
        }

        private static Vector3 Average(List<Vector3> points)
        {
            if (points.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }

        private void AnchorToCamera()
        {
            if (container == null) return;

            if (handAnchor != null)
            {
                container.SetPositionAndRotation(handAnchor.position, handAnchor.rotation);
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null || container == null) return;

            Transform camT = cam.transform;
            container.position = camT.position
                + camT.right * offset.x
                + camT.up * offset.y
                + camT.forward * offset.z;
            container.rotation = camT.rotation;
        }

        private void BuildPile()
        {
            if (deckSlot == null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "EnemyDeckPile3D";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            var r = go.GetComponent<Renderer>();
            if (pileMaterial != null)
            {
                r.sharedMaterial = pileMaterial;
            }
            else
            {
                var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = new Material(unlitShader);
                mat.color = backColor;
                r.material = mat;
            }
            pile = go.transform;
            pile.SetParent(transform, true);
            SetPileThickness(0f);
        }

        private void SetPileThickness(float thickness)
        {
            pile.gameObject.SetActive(thickness > 0.0001f);
            pile.localScale = new Vector3(pileCardSize.x, Mathf.Max(thickness, 0.0001f), pileCardSize.y);
            pile.rotation = Quaternion.identity;
            pile.position = deckSlot.position + Vector3.up * (thickness * 0.5f);
        }

        private Vector3 PileTop()
        {
            if (deckSlot == null) return container.position;
            float thickness = enemyCards != null ? enemyCards.DrawCount * thicknessPerCard : 0f;
            return deckSlot.position + Vector3.up * (thickness + 0.05f);
        }

        private void HandleDrawn(CardInstance card)
        {
            if (container == null) return;

            var view = Instantiate(cardPrefab, container);
            view.Bind(card);
            view.enabled = false; // no hover/drag on the enemy's cards
            SetFace(view, card, faceUp: showFaces);

            // Start lying flat on top of the enemy's draw pile (or the pit); the layout pass flies it into the hand
            bool fromPit = card.fromPit && DrawPitView3D.Instance != null;
            card.fromPit = false;
            if (card.conjured)
            {
                // Made out of nothing: it appears in fire right in the hand, from its bottom edge up
                card.conjured = false;
                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                TawanOS.VFX.CardBurnEffect.Reveal(view.transform, TawanOS.VFX.CardDeathFxConfigSO.Load(), conjureRevealTime);
            }
            else
            {
                view.transform.position = fromPit ? DrawPitView3D.Instance.SpawnPosition : PileTop();
                view.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (fromPit) TawanOS.VFX.PitCardTrail.Spawn(view.transform); // red smoke trailing it to the hand

            views.Add(view);
            newlyDrawn.Add(view);
            if (enemyCards.IsOpeningDraw) simultaneousDraw = true;
            layoutDirty = true;
        }

        public static bool ShouldShowcase(CardInstance card)
        {
            if (card == null) return false;
            if (card.cardType == CardType.Incantation) return true;
            return card.abilities != null && card.abilities.Count > 0;
        }

        private void HandlePlayed(CardInstance card)
        {
            int idx = views.FindIndex(v => v != null && v.CardData == card);
            if (idx < 0) return;

            var view = views[idx];
            views.RemoveAt(idx);
            newlyDrawn.Remove(view);

            SetFace(view, card, faceUp: true); // reveal what was played

            // Leave the camera-anchored hand so it stays on the table instead of following the camera
            var t = view.transform;
            t.SetParent(transform, worldPositionStays: true);
            t.DOKill();

            bool showcase = ShouldShowcase(card);

            if (card.cardType != CardType.Incantation)
            {
                // Amulet / Familiar: hand the physical card to the board view so it flies into its slot.
                // If nothing adopts it (no board view), clean it up.
                EnemyBoardView3D.PendingFromHand[card] = view;
                // Wait out the showcase first: at a slow enemy play speed it can run past a fixed 2-3 s
                DOVirtual.DelayedCall(RevealTime(card) + 2f, () =>
                {
                    if (EnemyBoardView3D.PendingFromHand.Remove(card) && view != null) Destroy(view.gameObject);
                });

                if (showcase && TryGetHeldPose(out var heldPos, out var heldRot, out var heldScale))
                {
                    if (enemyCards != null && enemyCards.PlayingTarget != null)
                    {
                        CardTargeting3D.Ensure().PointAt(enemyCards.PlayingTarget, RevealTime(card), enemy: true);
                    }

                    float speed = Speed;
                    float showIn = showcaseInDuration / speed;
                    float hold = abilityCardHoldTime / speed;

                    var seq = DOTween.Sequence().SetTarget(t);
                    seq.Append(t.DOMove(heldPos, showIn).SetEase(Ease.OutQuad))
                       .Join(t.DORotateQuaternion(heldRot, showIn))
                       .Join(t.DOScale(heldScale, showIn))
                       .AppendInterval(hold);
                }

                layoutDirty = true;
                return;
            }

            // Incantation: wait where the player's card waits while being played, then go to the graveyard.
            // The arrow shows which card it acts on while it waits.
            if (enemyCards.PlayingTarget != null)
            {
                CardTargeting3D.Ensure().PointAt(enemyCards.PlayingTarget, RevealTime(card), enemy: true);
            }

            float speed2 = Speed;
            float fly = playFlyDuration / speed2;

            var seq2 = DOTween.Sequence().SetTarget(t);
            if (TryGetHeldPose(out var heldPos2, out var heldRot2, out var heldScale2))
            {
                float showIn = showcaseInDuration / speed2;
                seq2.Append(t.DOMove(heldPos2, showIn).SetEase(Ease.OutQuad))
                   .Join(t.DORotateQuaternion(heldRot2, showIn))
                   .Join(t.DOScale(heldScale2, showIn))
                   .AppendInterval(showcaseHoldTime / speed2);
            }
            Vector3 grave = GraveyardView3D.Instance != null ? GraveyardView3D.Instance.DropPoint : tableCenter;
            seq2.Append(t.DOMove(grave, fly).SetEase(Ease.InCubic))
               .Join(t.DORotateQuaternion(tableRotation, fly))
               .Join(t.DOScale(Vector3.zero, fly).SetEase(Ease.InQuad))
               .OnComplete(() => { if (view != null) Destroy(view.gameObject); });

            layoutDirty = true;
        }

        private static float Speed => CombatManager.Instance != null ? CombatManager.Instance.EnemyPlaySpeed : 1f;

        // How long EnemyCardPlayer waits before an incantation lands or ability card is showcased: until its showcase has been read
        public float RevealTime(CardInstance card)
        {
            if (card == null || cardPrefab == null) return 0f;
            if (!ShouldShowcase(card)) return 0f;
            float hold = card.cardType == CardType.Incantation ? showcaseHoldTime : abilityCardHoldTime;
            return (showcaseInDuration + hold) / Speed;
        }

        // Same spot, facing and size as a card the player is about to play (CardPlayController3D)
        private bool TryGetHeldPose(out Vector3 position, out Quaternion rotation, out Vector3 localScale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            localScale = Vector3.one;

            var play = CardPlayController3D.Ensure();
            if (!play.TryGetHeldWorldPoint(playerHandParent, out position)) return false;

            rotation = playerHandParent.rotation;
            Vector3 world = Vector3.Scale(playerHandParent.lossyScale, cardPrefab.transform.localScale * play.heldScale);
            Vector3 parent = transform.lossyScale;
            localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
            return true;
        }

        private void SetFace(CardView3D view, CardInstance card, bool faceUp)
        {
            if (view.nameLabel != null) view.nameLabel.gameObject.SetActive(faceUp);
            view.SetFaceVisible(faceUp);
            if (view.cardRenderer != null)
            {
                view.cardRenderer.material.color = faceUp
                    ? (card.magicSchool == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f))
                    : backColor;
            }
        }

        private void LateUpdate()
        {
            AnchorToCamera();

            if (pile != null && deckSlot != null && enemyCards != null)
            {
                float target = enemyCards.DrawCount > 0 ? Mathf.Max(0.02f, enemyCards.DrawCount * thicknessPerCard) : 0f;
                SetPileThickness(Mathf.MoveTowards(pile.localScale.y, target, 10f * Time.deltaTime));
            }

            if (!layoutDirty) return;
            layoutDirty = false;
            Layout();
        }

        // Same fan as the player's hand: middle card highest, ends lower and tilted outward
        private void Layout()
        {
            views.RemoveAll(v => v == null);
            int count = views.Count;
            if (count == 0) return;

            float spacing = cardSpacing;
            if (count * cardSpacing > maxTotalWidth) spacing = maxTotalWidth / count;

            float startX = -(count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f; // -0.5 to +0.5
                Vector3 pos = new Vector3(startX + i * spacing, -Mathf.Abs(t) * curveDepth * 2f, -i * 0.005f);
                Quaternion rot = Quaternion.Euler(0f, 0f, -t * arcAngle * count);
                if (views[i] == considering)
                {
                    // Down out of the fan (toward the table) and a little in front of its neighbours, held straight
                    pos += new Vector3(0f, -considerPull, -0.05f);
                    rot = Quaternion.identity;
                }

                var tr = views[i].transform;
                tr.DOKill();

                int order = newlyDrawn.IndexOf(views[i]);
                if (order >= 0)
                {
                    float delay = simultaneousDraw ? 0f : order * drawStagger;
                    tr.DOLocalMove(pos, drawFlyDuration).SetDelay(delay).SetEase(Ease.OutCubic);
                    tr.DOLocalRotateQuaternion(rot, drawFlyDuration).SetDelay(delay).SetEase(Ease.OutCubic);
                }
                else
                {
                    tr.DOLocalMove(pos, 0.2f).SetEase(Ease.OutQuad);
                    tr.DOLocalRotateQuaternion(rot, 0.2f);
                }
            }
            simultaneousDraw = false;
            newlyDrawn.Clear();
        }
    }
}
