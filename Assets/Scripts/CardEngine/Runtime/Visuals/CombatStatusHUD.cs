using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TawanOS.Settings;
using TawanOS.UI;
using TawanOS.VFX;

namespace TawanOS.CardEngine
{
    // Self-building status display for scenes without a full HUD:
    //  - top centre: enemy name, Khwan bar, shield, AI state, hand size and the enemy's last action
    //  - left, clear of the hand (where the flame rings' default layout leaves room under them): the player's Khwan
    //    bar and shield. Its place is its own; it does not follow FlameGaugeStyle (Game Designer, 13 Oct)
    // Skipped when the scene already has an EnemyCombatView.
    public class CombatStatusHUD : MonoBehaviour
    {
        private static readonly Color PlayerBar = KhwanDisplay.BarColour;
        private static readonly Color EnemyBar = new Color(0.85f, 0.25f, 0.25f); // the enemy stays red (Game Designer, 15 Oct)
        private static readonly Color Panel = new Color(0.05f, 0.04f, 0.03f, 0.75f);

        private struct BarPanel
        {
            public RectTransform root;
            public TMP_Text title;
            public TMP_Text detail;
            public RectTransform fill;
            public RectTransform tempStrip; // ขวัญชั่วคราว over the fill (player only)
            public Button button;
            public TMP_Text buttonLabel;
        }

        private BarPanel enemy;
        private BarPanel player;
        private TMP_Text actionText;
        private CombatManager combat;
        private int lastEnemyKhwan = -1;
        private int lastPlayerKhwan = -1;

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
            if (FindFirstObjectByType<CombatStatusHUD>() != null) return;
            if (FindFirstObjectByType<EnemyCombatView>() != null) return;

            new GameObject("CombatStatusHUD").AddComponent<CombatStatusHUD>();
        }

        private void Start()
        {
            BuildUI();
            combat = CombatManager.Instance;
            if (combat != null) combat.OnEnemyAction += HandleEnemyAction;
        }

        private void OnDestroy()
        {
            if (combat != null) combat.OnEnemyAction -= HandleEnemyAction;
        }



        private void HandleEnemyAction(string description)
        {
            actionText.text = description;
            actionText.DOKill();
            actionText.transform.localScale = Vector3.one;
            actionText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6);
        }

        private void Update()
        {
            if (combat == null) return;

            var s = combat.State;
            var profile = combat.currentEnemyProfile;

            // Enemy
            var theme = UIThemeSO.Current;
            string enemyName = profile != null ? profile.enemyName : "ศัตรู";
            string compactStats = $"  <size=75%><color=#E0D6B8>กุศล {s.enemyMerit}/{s.maxMerit} · มลทิน {s.enemyCorruption}/{s.enemyCorruptionThreshold}</color></size>";
            enemy.title.text = $"{enemyName}{compactStats}";
            string handInfo = combat.EnemyCards.Active
                ? $"  |  การ์ดในมือ {combat.EnemyCards.Hand.Count}"
                : "";
            string shield = s.enemyShield > 0 ? $"  |  เกราะ {s.enemyShield}" : "";
            enemy.detail.text = $"ขวัญ {s.enemyKhwan} / {s.maxEnemyKhwan}{shield}  |  {combat.EnemyAIStateName}{handInfo}";
            SetFill(enemy.fill, s.maxEnemyKhwan > 0 ? (float)s.enemyKhwan / s.maxEnemyKhwan : 0f, s.enemyKhwan, ref lastEnemyKhwan);

            // Player
            string pShield = s.playerShield > 0 ? $"  |  เกราะ {s.playerShield}" : "";
            player.detail.text = $"ขวัญ {KhwanDisplay.Text(s.playerKhwan, s.maxPlayerKhwan, " / ")}{pShield}";
            SetFill(player.fill, KhwanDisplay.Fill(s.playerKhwan, s.maxPlayerKhwan), s.playerKhwan, ref lastPlayerKhwan);
            KhwanDisplay.SetTempStrip(player.tempStrip, s.playerKhwan, s.maxPlayerKhwan);

            var turns = TurnPhaseController.Instance;
            bool isCombatOver = combat.IsCombatOver;
            bool isPlayerTurn = !isCombatOver;
            string btnText = "จบเทิร์น";

            if (turns != null && turns.isActiveAndEnabled)
            {
                isPlayerTurn = !isCombatOver && (turns.CurrentPhase == TurnPhase.PlayerBoard || turns.CurrentPhase == TurnPhase.PlayerSpell);
                btnText = turns.CurrentPhase == TurnPhase.PlayerBoard ? "จบช่วงลงบริวาร" : "จบเทิร์น";
            }
            else
            {
                isPlayerTurn = !isCombatOver && (combat.State.currentPhase == CombatPhase.PlayerTurn);
            }

            if (player.button != null)
            {
                player.button.interactable = isPlayerTurn;
            }
            if (player.buttonLabel != null)
            {
                player.buttonLabel.text = btnText;
            }
        }

        private void SetFill(RectTransform fill, float t, int value, ref int last)
        {
            if (value == last) return;

            bool first = last < 0;
            bool damaged = !first && value < last;
            last = value;

            fill.DOKill();
            var target = new Vector2(Mathf.Clamp01(t), 1f);
            if (first) fill.anchorMax = target;
            else fill.DOAnchorMax(target, 0.25f);

            if (damaged && !GameSettings.ReduceMotion)
            {
                var root = (RectTransform)fill.parent.parent;
                root.DOComplete();
                root.DOShakeAnchorPos(0.3f, 12f, 20);
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("CombatStatusCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            enemy = BuildPanel(canvasGo.transform, "EnemyPanel", new Vector2(0.5f, 1f), new Vector2(0, -30),
                new Vector2(760, 120), EnemyBar, titleSize: 32, detailSize: 24);

            var actionGo = new GameObject("EnemyAction", typeof(RectTransform));
            actionGo.transform.SetParent(enemy.root, false);
            actionText = actionGo.AddComponent<TextMeshProUGUI>();
            actionText.fontSize = 24;
            actionText.color = new Color(1f, 0.85f, 0.5f);
            actionText.alignment = TextAlignmentOptions.Center;
            actionText.raycastTarget = false;
            UiFactory.EnableThaiMarks(actionText);
            var actionRt = actionText.rectTransform;
            actionRt.anchorMin = new Vector2(0f, 0f);
            actionRt.anchorMax = new Vector2(1f, 0f);
            actionRt.pivot = new Vector2(0.5f, 1f);
            actionRt.anchoredPosition = new Vector2(0, -4);
            actionRt.sizeDelta = new Vector2(0, 36);
            var theme = UIThemeSO.Current;
            if (theme != null && theme.bodyFont != null) actionText.font = theme.bodyFont;

            // Left, clear of the hand, under flame rings
            player = BuildPlayerPanel(canvasGo.transform);
        }

        private BarPanel BuildPlayerPanel(Transform parent)
        {
            var p = new BarPanel();
            var theme = UIThemeSO.Current;

            var rootGo = new GameObject("PlayerPanel", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            p.root = (RectTransform)rootGo.transform;
            p.root.anchorMin = p.root.anchorMax = Vector2.zero;
            p.root.pivot = Vector2.zero;
            p.root.anchoredPosition = new Vector2(40, 220);
            p.root.sizeDelta = new Vector2(360, 120);

            var bg = rootGo.AddComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;

            var soft = rootGo.AddComponent<SoftEdgeImage>();
            soft.SetSides(0.08f, 0.08f, 0.08f, 0.08f);

            // Title: Player name
            p.title = MakeText(rootGo.transform, "PlayerName", 24, FontStyles.Normal, theme != null ? theme.accent : Color.white,
                new Vector2(0, 0.68f), new Vector2(0.45f, 0.98f), new Vector2(16, 0), Vector2.zero);
            if (theme != null && theme.titleFont != null) p.title.font = theme.titleFont;
            p.title.text = "ผู้เล่น";

            // Detail: Khwan numbers
            p.detail = MakeText(rootGo.transform, "KhwanDetail", 20, FontStyles.Normal, theme != null ? theme.text : new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0.35f, 0.68f), new Vector2(1f, 0.98f), Vector2.zero, new Vector2(-16, 0));
            p.detail.alignment = TextAlignmentOptions.Right;
            if (theme != null && theme.bodyFont != null) p.detail.font = theme.bodyFont;

            // Khwan Bar Track
            var trackGo = new GameObject("BarTrack", typeof(RectTransform));
            trackGo.transform.SetParent(rootGo.transform, false);
            var trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = new Vector2(0, 0.44f);
            trackRt.anchorMax = new Vector2(1, 0.62f);
            trackRt.offsetMin = new Vector2(16, 0);
            trackRt.offsetMax = new Vector2(-16, 0);
            var track = trackGo.AddComponent<Image>();
            track.color = new Color(0.12f, 0.10f, 0.10f, 0.9f);
            track.raycastTarget = false;

            // Khwan Bar Fill
            var fillGo = new GameObject("BarFill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);
            p.fill = (RectTransform)fillGo.transform;
            p.fill.anchorMin = Vector2.zero;
            p.fill.anchorMax = new Vector2(1f, 1f);
            p.fill.offsetMin = p.fill.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = PlayerBar;
            fillImg.raycastTarget = false;
            p.tempStrip = KhwanDisplay.AddTempStrip(trackRt);

            // End Turn Button
            var btnGo = new GameObject("EndTurnButton", typeof(RectTransform));
            btnGo.transform.SetParent(rootGo.transform, false);
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(0, 0.06f);
            btnRt.anchorMax = new Vector2(1, 0.38f);
            btnRt.offsetMin = new Vector2(16, 0);
            btnRt.offsetMax = new Vector2(-16, 0);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = Color.white;
            p.button = btnGo.AddComponent<Button>();
            p.button.targetGraphic = btnImg;

            var smokeBtn = btnGo.AddComponent<UiSmokeButton>();

            p.buttonLabel = MakeText(btnGo.transform, "Label", 20, FontStyles.Normal, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            p.buttonLabel.alignment = TextAlignmentOptions.Center;
            if (theme != null && theme.titleFont != null) p.buttonLabel.font = theme.titleFont;
            p.buttonLabel.text = "จบช่วงลงบริวาร";

            p.button.onClick.AddListener(() =>
            {
                var turns = TurnPhaseController.Instance;
                if (turns != null && turns.isActiveAndEnabled)
                {
                    turns.RequestEndTurn();
                }
                else if (CombatManager.Instance != null && !CombatManager.Instance.IsCombatOver)
                {
                    CombatManager.Instance.EndPlayerTurn();
                }
            });

            return p;
        }

        private BarPanel BuildPanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            Color barColor, int titleSize, int detailSize)
        {
            var p = new BarPanel();

            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            p.root = (RectTransform)rootGo.transform;
            p.root.anchorMin = p.root.anchorMax = anchor;
            p.root.pivot = anchor;
            p.root.anchoredPosition = pos;
            p.root.sizeDelta = size;
            var bg = rootGo.AddComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;

            var soft = rootGo.AddComponent<SoftEdgeImage>();
            soft.SetSides(0.08f, 0.08f, 0.08f, 0.08f);

            var theme = UIThemeSO.Current;
            p.title = MakeText(rootGo.transform, "Title", titleSize, FontStyles.Normal, Color.white,
                new Vector2(0, 0.62f), new Vector2(1, 1), new Vector2(16, 0), new Vector2(-16, -4));
            if (theme != null && theme.titleFont != null) p.title.font = theme.titleFont;

            p.detail = MakeText(rootGo.transform, "Detail", detailSize, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0, 0.28f), new Vector2(1, 0.62f), new Vector2(16, 0), new Vector2(-16, 0));
            if (theme != null && theme.bodyFont != null) p.detail.font = theme.bodyFont;

            var trackGo = new GameObject("BarTrack", typeof(RectTransform));
            trackGo.transform.SetParent(rootGo.transform, false);
            var trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = new Vector2(0, 0);
            trackRt.anchorMax = new Vector2(1, 0.28f);
            trackRt.offsetMin = new Vector2(16, 8);
            trackRt.offsetMax = new Vector2(-16, 0);
            var track = trackGo.AddComponent<Image>();
            track.color = new Color(0.15f, 0.12f, 0.12f, 0.9f);
            track.raycastTarget = false;

            var fillGo = new GameObject("BarFill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);
            p.fill = (RectTransform)fillGo.transform;
            p.fill.anchorMin = Vector2.zero;
            p.fill.anchorMax = new Vector2(1f, 1f);
            p.fill.offsetMin = p.fill.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = barColor;
            fillImg.raycastTarget = false;

            return p;
        }

        private static TMP_Text MakeText(Transform parent, string name, int size, FontStyles style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.raycastTarget = false;
            UiFactory.EnableThaiMarks(t);
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return t;
        }
    }
}
