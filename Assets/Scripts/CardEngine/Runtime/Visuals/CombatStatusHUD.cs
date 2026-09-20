using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Self-building status display for scenes without a full HUD:
    //  - top centre: enemy name, Khwan bar, shield, AI state, hand size and the enemy's last action
    //  - bottom left (above Corruption): player Khwan bar and shield
    // Skipped when the scene already has an EnemyCombatView.
    public class CombatStatusHUD : MonoBehaviour
    {
        private static readonly Color PlayerBar = new Color(0.30f, 0.75f, 0.45f);
        private static readonly Color EnemyBar = new Color(0.85f, 0.25f, 0.25f);
        private static readonly Color Panel = new Color(0.05f, 0.04f, 0.03f, 0.75f);

        private struct BarPanel
        {
            public RectTransform root;
            public TMP_Text title;
            public TMP_Text detail;
            public RectTransform fill;
        }

        private BarPanel enemy;
        private BarPanel player;
        private TMP_Text actionText;
        private CombatManager combat;
        private int lastEnemyKhwan = -1;
        private int lastPlayerKhwan = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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
            enemy.title.text = profile != null ? profile.enemyName : "ศัตรู";
            string handInfo = combat.EnemyCards.Active
                ? $"  |  การ์ดในมือ {combat.EnemyCards.Hand.Count}  |  กุศล {s.enemyMerit} / {s.maxMerit}"
                : "";
            string shield = s.enemyShield > 0 ? $"  |  เกราะ {s.enemyShield}" : "";
            enemy.detail.text = $"ขวัญ {s.enemyKhwan} / {s.maxEnemyKhwan}{shield}  |  {combat.EnemyAIStateName}{handInfo}";
            SetFill(enemy.fill, s.maxEnemyKhwan > 0 ? (float)s.enemyKhwan / s.maxEnemyKhwan : 0f, s.enemyKhwan, ref lastEnemyKhwan);

            // Player
            string pShield = s.playerShield > 0 ? $"  |  เกราะ {s.playerShield}" : "";
            player.detail.text = $"ขวัญ {s.playerKhwan} / {s.maxPlayerKhwan}{pShield}";
            SetFill(player.fill, s.maxPlayerKhwan > 0 ? (float)s.playerKhwan / s.maxPlayerKhwan : 0f, s.playerKhwan, ref lastPlayerKhwan);
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

            if (damaged)
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

            enemy = BuildPanel(canvasGo.transform, "EnemyPanel", new Vector2(0.5f, 1f), new Vector2(0, -30),
                new Vector2(760, 120), EnemyBar, titleSize: 32, detailSize: 24);

            var actionGo = new GameObject("EnemyAction", typeof(RectTransform));
            actionGo.transform.SetParent(enemy.root, false);
            actionText = actionGo.AddComponent<TextMeshProUGUI>();
            actionText.fontSize = 24;
            actionText.color = new Color(1f, 0.85f, 0.5f);
            actionText.alignment = TextAlignmentOptions.Center;
            actionText.raycastTarget = false;
            var actionRt = actionText.rectTransform;
            actionRt.anchorMin = new Vector2(0f, 0f);
            actionRt.anchorMax = new Vector2(1f, 0f);
            actionRt.pivot = new Vector2(0.5f, 1f);
            actionRt.anchoredPosition = new Vector2(0, -4);
            actionRt.sizeDelta = new Vector2(0, 36);

            player = BuildPanel(canvasGo.transform, "PlayerPanel", Vector2.zero, new Vector2(40, 240),
                new Vector2(360, 90), PlayerBar, titleSize: 26, detailSize: 22);
            player.title.text = "ผู้เล่น";
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

            p.title = MakeText(rootGo.transform, "Title", titleSize, FontStyles.Bold, Color.white,
                new Vector2(0, 0.62f), new Vector2(1, 1), new Vector2(16, 0), new Vector2(-16, -4));
            p.detail = MakeText(rootGo.transform, "Detail", detailSize, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0, 0.28f), new Vector2(1, 0.62f), new Vector2(16, 0), new Vector2(-16, 0));

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
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return t;
        }
    }
}
