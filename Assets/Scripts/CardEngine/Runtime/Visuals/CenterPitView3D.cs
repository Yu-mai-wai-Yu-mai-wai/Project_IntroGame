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
        [Tooltip("Card size relative to a card on the board; made smaller when the gap between the rows is too short")]
        public float cardScale = 0.8f;
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
        }

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

        // True for a card lying in the pit (its details can be opened)
        public static bool Shows(CardView3D view)
        {
            return Instance != null && view != null && Instance.SlotOf(view) != null;
        }

        // Where a card taken from the pit starts flying to its hand: its slot, else the old pit's spot
        public static bool TryGetSpawnPosition(CardInstance card, out Vector3 position)
        {
            if (Instance != null && card != null && Instance.takenFrom.TryGetValue(card, out position))
            {
                Instance.takenFrom.Remove(card);
                return true;
            }
            position = Vector3.zero;
            if (DrawPitView3D.Instance == null) return false;
            position = DrawPitView3D.Instance.SpawnPosition;
            return true;
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
            || CardDetailPanelUI.BlocksInput || CombatCameraRig3D.BlocksInput
            || (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver);

        private void Subscribe(CenterPit centerPit)
        {
            subscribed = centerPit;
            subscribed.OnDealt += HandleDealt;
            subscribed.OnTaken += HandleTaken;
            subscribed.OnCleared += HandleCleared;
            if (subscribed.Offer.Count > 0) HandleDealt();
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.OnDealt -= HandleDealt;
            subscribed.OnTaken -= HandleTaken;
            subscribed.OnCleared -= HandleCleared;
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
                // Column i + 1: the familiar columns, left to right
                if (!TryFindColumn(EffectResolver.FirstFamiliarSlot + i, out var player, out var enemy))
                {
                    Debug.LogWarning($"[CenterPitView3D] No board column {EffectResolver.FirstFamiliarSlot + i} on both sides; card {i + 1} of the pit has no slot.");
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
                    rest = (player.transform.position + enemy.transform.position) * 0.5f + Vector3.up * height,
                };
                slot.root.transform.SetParent(transform, false);
                slot.root.transform.position = slot.rest;

                view.transform.rotation = player.transform.rotation;
                view.transform.position = slot.rest;
                view.transform.localScale = slot.scale * 0.2f;
                Vector2 size = CardSize(view, slot.scale);

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

        // ---------------------------------------------------------------- hover

        private void Lift(PitSlot slot)
        {
            if (slot.card == null) return;
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_card_hover");
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
