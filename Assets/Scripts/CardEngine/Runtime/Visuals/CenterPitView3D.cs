using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // หลุมกลาง on the table: while the pit is open, 4 slots with a grey frame and a thin mist lie in the middle of
    // the table, between the two board rows and lined up with the familiar columns, each holding one face-up card
    // (CenterPit). While the player has to pick, hovering lifts a card and a left click takes it (right click
    // shows its details, CombatCameraRig3D). A taken card flies from its slot to the hand; when the picking is
    // over the slots fade away with the cards left in them. Bootstraps itself in any scene with a TurnPhaseController.
    public class CenterPitView3D : MonoBehaviour
    {
        public static CenterPitView3D Instance { get; private set; }

        [Header("Cards")]
        [Tooltip("Card size relative to a card on the board (1 = the same size, Game Designer 22 Oct); made smaller only when the gap between the rows is too short")]
        public float cardScale = 1f;
        [Tooltip("Height above the table")]
        public float height = 0.03f;
        public float hoverLift = 0.25f;
        public float hoverScale = 1.12f;
        public float dealTime = 0.35f;
        public float dealStagger = 0.08f;
        public float fadeTime = 0.35f;

        [Header("Slots")]
        [Tooltip("Frame size against the card in it")]
        public float frameScale = 1.08f;
        [ColorUsage(true, true)] public Color frameColor = new Color(0.55f, 0.55f, 0.58f, 0.55f);
        [Range(0f, 1f)] public float frameFill = 0.06f;

        [Header("Mist")]
        public Color mistColor = new Color(0.75f, 0.75f, 0.8f, 0.12f);
        public float mistRate = 6f;
        [Tooltip("Mist puff size against the card's width")]
        public float mistSize = 0.6f;

        private const string SlotGlowShader = "Shaders/SlotGlow";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int FrameStrengthId = Shader.PropertyToID("_FrameStrength");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int CornerId = Shader.PropertyToID("_Corner");

        private class PitSlot
        {
            public GameObject root;
            public Renderer frame;
            public ParticleSystem mist;
            public CardView3D card;
            public Vector3 rest;
            public Vector3 scale;
            public Vector2 size;      // the card's width and length on the table
            public Vector3 toEnemy;   // flat, from the player's row toward the enemy's
            public GameObject hand;   // the enemy's shadow hand on a claimed card
        }

        [Header("Enemy claim")]
        [ColorUsage(true, true)] public Color claimColor = new Color(0.95f, 0.15f, 0.12f, 0.9f);
        [Range(0f, 1f)] public float handAlpha = 0.92f;
        private static Texture2D handTexture;

        private CenterPit subscribed;
        private CardView3D cardPrefab;
        private Material frameMaterial, mistMaterial;
        private readonly List<PitSlot> slots = new List<PitSlot>();
        private readonly Dictionary<CardInstance, Vector3> takenFrom = new Dictionary<CardInstance, Vector3>();
        private PitSlot hovered;
        private bool wasWaiting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // no double hook without domain reload
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindFirstObjectByType<TurnPhaseController>() == null || FindFirstObjectByType<CenterPitView3D>() != null) return;
            new GameObject("CenterPit3D").AddComponent<CenterPitView3D>();
        }

        /// <summary>Where the pit's cards lie (world), empty when the pit shows none; the camera zooms onto them.</summary>
        public static List<Vector3> CardSpots()
        {
            var spots = new List<Vector3>();
            if (Instance == null) return spots;
            foreach (var slot in Instance.slots)
                if (slot.root != null) spots.Add(slot.rest);
            return spots;
        }

        // True for a card lying in the pit (its details can be opened)
        public static bool Shows(CardView3D view)
        {
            return Instance != null && view != null && Instance.SlotOf(view) != null;
        }

        // Where a card taken from the pit starts flying to its hand: its slot
        public static bool TryGetSpawnPosition(CardInstance card, out Vector3 position)
        {
            if (Instance != null && card != null && Instance.takenFrom.TryGetValue(card, out position))
            {
                Instance.takenFrom.Remove(card);
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
            if (frameMaterial != null) Destroy(frameMaterial);
            if (mistMaterial != null)
            {
                Destroy(mistMaterial.mainTexture);
                Destroy(mistMaterial);
            }
        }

        private void Update()
        {
            // CenterPit is added by TurnPhaseController; hook up once it exists
            if (subscribed == null && CenterPit.Instance != null) Subscribe(CenterPit.Instance);
            if (subscribed == null) return;

            bool waiting = subscribed.WaitingForPlayer && !BlocksInput;
            if (waiting && !wasWaiting) TawanOS.UI.PlayerNotice.Show(PlayBlockReasons.HintFor(TurnPhase.PitPick), TawanOS.UI.NoticeKind.Info);
            wasWaiting = waiting;

            var under = waiting ? SlotUnderMouse() : null;
            if (under != null && under.card != null && under.card.CardData == subscribed.ReservedForEnemy) under = null; // the enemy's
            if (under != null) TawanOS.UI.PointerCursor.Hover();
            if (under != hovered)
            {
                if (hovered != null) Rest(hovered);
                hovered = under;
                if (hovered != null) Lift(hovered);
            }

            if (waiting && hovered != null && Input.GetMouseButtonDown(0))
            {
                var card = hovered.card.CardData;
                hovered = null;
                subscribed.TakeForPlayer(card);
            }
        }

        private static bool BlocksInput => TawanOS.UI.PauseMenu.IsPaused || TawanOS.UI.TutorialCoach.Blocking
            || !TawanOS.UI.TutorialCoach.AllowsPitPick
            || CardDetailPanelUI.BlocksInput || CombatCameraRig3D.BlocksInput
            || (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver);

        private void Subscribe(CenterPit centerPit)
        {
            subscribed = centerPit;
            subscribed.OnDealt += HandleDealt;
            subscribed.OnTaken += HandleTaken;
            subscribed.OnCleared += HandleCleared;
            subscribed.OnReserved += HandleReserved;
            if (subscribed.Offer.Count > 0) HandleDealt();
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.OnDealt -= HandleDealt;
            subscribed.OnTaken -= HandleTaken;
            subscribed.OnCleared -= HandleCleared;
            subscribed.OnReserved -= HandleReserved;
            subscribed = null;
        }

        // ---------------------------------------------------------------- dealing

        private void HandleDealt()
        {
            RemoveSlots(fade: false);
            takenFrom.Clear();
            if (cardPrefab == null)
            {
                var hand = FindFirstObjectByType<HandLayoutController3D>();
                if (hand != null) cardPrefab = hand.cardPrefab;
            }
            if (cardPrefab == null) return;

            var offer = subscribed.Offer;
            for (int i = 0; i < offer.Count; i++)
            {
                // Card i sits in the gap between board columns i and i + 1, halfway between the rows, so the four pit
                // cards are staggered against the board's five columns and spread over the whole table (Game Designer, 22 Oct)
                if (!TryFindColumn(i, out var player, out var enemy) || !TryFindColumn(i + 1, out var player2, out var enemy2))
                {
                    Debug.LogWarning($"[CenterPitView3D] No board columns {i} and {i + 1} on both sides; card {i + 1} of the pit has no slot.");
                    continue;
                }

                var view = Instantiate(cardPrefab, transform);
                view.Bind(offer[i]);
                view.enabled = false; // not a hand card: no hand hover or drag
                view.SetFaceVisible(true);

                // A board card's pose (the slot's tilt, the slot's width), smaller, halfway between the rows
                Vector3 boardScale = Vector3.Scale(player.FitCardScale(view.transform.localScale), player.transform.lossyScale);
                float rowGap = Vector3.Distance(Flat(player.transform.position), Flat(enemy.transform.position));
                float boardLength = Mathf.Abs(boardScale.y) * MeshLength(view);
                float factor = cardScale;
                if (boardLength > 0f) factor = Mathf.Min(factor, Mathf.Max(0.3f, (rowGap - boardLength) / (boardLength * frameScale)));

                var slot = new PitSlot
                {
                    root = new GameObject($"PitSlot{i + 1}"),
                    card = view,
                    scale = boardScale * factor,
                    rest = (player.transform.position + enemy.transform.position + player2.transform.position + enemy2.transform.position) * 0.25f
                        + Vector3.up * height,
                };
                slot.root.transform.SetParent(transform, false);
                slot.root.transform.position = slot.rest;

                view.transform.rotation = player.transform.rotation;
                view.transform.position = slot.rest;
                view.transform.localScale = slot.scale * 0.2f;
                Vector2 size = CardSize(view, slot.scale);
                slot.size = size;
                slot.toEnemy = (Flat(enemy.transform.position) - Flat(player.transform.position)).normalized;

                Vector3 along = Vector3.ProjectOnPlane(player.transform.up, Vector3.up);
                if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
                slot.frame = MakeFrame(slot.root.transform, size, along.normalized);
                slot.mist = MakeMist(slot.root.transform, size);
                slots.Add(slot);

                float delay = i * dealStagger;
                view.transform.DOScale(slot.scale, dealTime).SetDelay(delay).SetEase(Ease.OutBack);
            }
        }

        private void HandleTaken(CardInstance card, bool byPlayer)
        {
            var slot = slots.Find(s => s.card != null && s.card.CardData == card);
            if (slot == null) return;
            takenFrom[card] = slot.card.transform.position;
            if (slot == hovered) hovered = null;
            if (slot.hand != null) FadeHand(slot);
            slot.card.transform.DOKill();
            Destroy(slot.card.gameObject);
            slot.card = null; // the empty slot stays until the picking is over
        }

        private void HandleCleared() => RemoveSlots(fade: true);

        private void RemoveSlots(bool fade)
        {
            foreach (var slot in slots)
            {
                var root = slot.root;
                var card = slot.card;
                if (card != null) card.transform.DOKill();
                if (slot.hand != null) FadeHand(slot);
                if (!fade)
                {
                    if (card != null) Destroy(card.gameObject);
                    Destroy(root);
                    continue;
                }

                // The slot, its mist and the card left in it fade into the table
                if (slot.mist != null) slot.mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (slot.frame != null)
                {
                    var mat = slot.frame.material;
                    var c = frameColor;
                    DOTween.To(() => c.a, a => { c.a = a; mat.SetColor(ColorId, c); }, 0f, fadeTime).SetTarget(root);
                }
                if (card != null)
                {
                    card.transform.DOScale(Vector3.zero, fadeTime).SetEase(Ease.InBack)
                        .OnComplete(() => { if (card != null) Destroy(card.gameObject); });
                }
                Destroy(root, fadeTime + 1f); // lets the last mist drift away
            }
            slots.Clear();
            hovered = null;
        }

        // ---------------------------------------------------------------- the enemy's claim

        // The enemy picks first this turn: its card lights red and a shadow hand reaches across the table to it, the
        // fingertips at the card's edge so the card stays readable (Game Designer, 25 Oct)
        private void HandleReserved(CardInstance card)
        {
            var slot = slots.Find(s => s.card != null && s.card.CardData == card);
            if (slot == null) return;
            if (slot == hovered) { Rest(slot); hovered = null; }
            if (slot.frame != null)
            {
                var mat = slot.frame.material;
                DOTween.Kill(slot.frame.gameObject);
                mat.SetColor(ColorId, claimColor);
            }

            float length = slot.size.y * 1.35f, width = slot.size.x * 0.85f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "EnemyHand";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(slot.root.transform, true);
            // Lying on the table, the fingertips (the texture's top) toward the card
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, -slot.toEnemy);
            go.transform.localScale = new Vector3(width, length, 1f);
            Vector3 edge = Flat(slot.rest) + slot.toEnemy * (slot.size.y * 0.5f * frameScale);
            Vector3 target = edge + slot.toEnemy * (length * 0.5f - length * 0.06f) + Vector3.up * (slot.rest.y - height * 0.6f);
            go.transform.position = target + slot.toEnemy * length * 0.5f;

            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mat2 = new Material(Shader.Find("Sprites/Default")) { mainTexture = HandTexture() };
            mat2.color = new Color(1f, 1f, 1f, 0f); // the colour is in the texture: black with a dark red rim
            r.material = mat2;
            slot.hand = go;

            go.transform.DOMove(target, 0.6f).SetEase(Ease.OutCubic).SetTarget(go)
                .OnComplete(() => go.transform.DOMove(target - slot.toEnemy * length * 0.03f, 0.9f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(go));
            DOTween.To(() => 0f, a => mat2.color = new Color(1f, 1f, 1f, a), handAlpha, 0.6f).SetTarget(go);
        }

        private void FadeHand(PitSlot slot)
        {
            var go = slot.hand;
            slot.hand = null;
            if (go == null) return;
            DOTween.Kill(go);
            var mat = go.GetComponent<Renderer>().material;
            var c = mat.color;
            DOTween.To(() => c.a, a => mat.color = new Color(c.r, c.g, c.b, a), 0f, fadeTime).SetTarget(go)
                .OnComplete(() => { if (go != null) Destroy(go); });
        }

        // A soft dark hand from the wrist up: an arm fading out at the bottom, a palm, four fingers (drawn once in code)
        private static Texture2D HandTexture()
        {
            if (handTexture != null) return handTexture;
            const int W = 128, H = 256;
            handTexture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EnemyHand" };
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
            {
                for (int i = 0; i < W; i++)
                {
                    float u = (i + 0.5f) / W, v = (j + 0.5f) / H;
                    float arm = Mathf.Min(0.17f - Mathf.Abs(u - 0.5f), 0.55f - v);
                    float pu = (u - 0.5f) / 0.3f, pv = (v - 0.58f) / 0.17f;
                    float palm = (1f - Mathf.Sqrt(pu * pu + pv * pv)) * 0.15f;
                    float fingers = -1f;
                    float[] xs = { 0.29f, 0.43f, 0.57f, 0.71f };
                    float[] tips = { 0.86f, 0.95f, 0.93f, 0.84f };
                    for (int f = 0; f < 4; f++)
                    {
                        float top = tips[f] - 0.045f;
                        float dy = v > top ? v - top : v < 0.6f ? 0.6f - v : 0f;
                        float d = Mathf.Sqrt((u - xs[f]) * (u - xs[f]) + dy * dy);
                        fingers = Mathf.Max(fingers, 0.045f - d);
                    }
                    float inside = Mathf.Max(arm, Mathf.Max(palm, fingers));
                    float a = Mathf.Clamp01(inside / 0.035f + 0.5f);
                    a *= Mathf.Clamp01(v / 0.3f); // the arm comes out of the dark
                    // Black inside, a dark red rim so it reads on the dark wood
                    float rim = 1f - Mathf.Clamp01(inside / 0.03f);
                    px[j * W + i] = new Color32((byte)(150 * rim), (byte)(14 * rim), (byte)(12 * rim), (byte)(a * 255));
                }
            }
            handTexture.SetPixels32(px);
            handTexture.Apply(false, true);
            return handTexture;
        }

        // ---------------------------------------------------------------- hover

        private void Lift(PitSlot slot)
        {
            if (slot.card == null) return; // silent, like a hand card's hover (Game Designer, 25 Oct)
            slot.card.transform.DOKill();
            slot.card.transform.DOMove(slot.rest + Vector3.up * hoverLift, 0.15f);
            slot.card.transform.DOScale(slot.scale * hoverScale, 0.15f);
        }

        private void Rest(PitSlot slot)
        {
            if (slot.card == null) return;
            slot.card.transform.DOKill();
            slot.card.transform.DOMove(slot.rest, 0.15f);
            slot.card.transform.DOScale(slot.scale, 0.15f);
        }

        private PitSlot SlotUnderMouse()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 200f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<CardView3D>();
                if (view != null) return SlotOf(view);
            }
            return null;
        }

        private PitSlot SlotOf(CardView3D view) => slots.Find(s => s.card != null && s.card == view);

        // ---------------------------------------------------------------- looks

        // A grey frame lying flat under the card, drawn with the slot light's shader (CardPlayController3D)
        private Renderer MakeFrame(Transform parent, Vector2 size, Vector3 along)
        {
            if (frameMaterial == null)
            {
                var shader = Resources.Load<Shader>(SlotGlowShader);
                frameMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                frameMaterial.SetFloat(FrameStrengthId, 1f);
                frameMaterial.SetFloat(FillId, frameFill);
                frameMaterial.SetFloat(CornerId, 0.04f);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Frame";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = parent.position - Vector3.up * (height * 0.5f);
            // A quad's face looks down its -Z: face up, its length along the board's columns
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, along);
            go.transform.localScale = new Vector3(size.x, size.y, 1f) * frameScale;

            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = frameMaterial;
            r.material.SetFloat(AspectId, size.y > 0f ? size.x / size.y : 0.6f);
            var c = frameColor;
            var mat = r.material;
            DOTween.To(() => 0f, a => { c.a = a; mat.SetColor(ColorId, c); }, frameColor.a, dealTime).SetTarget(go);
            return r;
        }

        // A few faint puffs drifting low over the slot; thin enough to see the card and the board through
        private ParticleSystem MakeMist(Transform parent, Vector2 size)
        {
            if (mistMaterial == null)
            {
                mistMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot(32) };
            }

            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x * mistSize * 0.7f, size.x * mistSize * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = mistColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = mistRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(size.x * frameScale, size.y * frameScale, 1f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mistMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private static Texture2D SoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(new Vector2(x - half, y - half).magnitude / half);
                    float a = 1f - d * d;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- board

        private static bool TryFindColumn(int column, out BoardSlotView player, out BoardSlotView enemy)
        {
            player = enemy = null;
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.slotIndex != column || slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;
                if (slot.side == BoardSlotView.SlotSide.Player) player = slot;
                else enemy = slot;
            }
            return player != null && enemy != null;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // Length of the card's mesh along its local Y (the card's long side)
        private static float MeshLength(CardView3D view)
        {
            var mf = view.cardRenderer != null ? view.cardRenderer.GetComponent<MeshFilter>() : null;
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.y : 1f;
        }

        // World width and length of the card at its resting scale
        private static Vector2 CardSize(CardView3D view, Vector3 scale)
        {
            float length = Mathf.Abs(scale.y) * MeshLength(view);
            float width = length * Mathf.Abs(scale.x) / Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            var mf = view.cardRenderer != null ? view.cardRenderer.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null) width = Mathf.Abs(scale.x) * mf.sharedMesh.bounds.size.x;
            return new Vector2(width, length);
        }
    }
}
