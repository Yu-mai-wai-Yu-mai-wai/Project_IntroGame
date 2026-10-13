using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.CardEngine;
using TawanOS.Settings;

namespace TawanOS.UI
{
    /// <summary>
    /// What the whole screen does when มลทิน breaks (Game Designer, 13 Oct); the ring itself bursts in FlameGaugeHUD.
    /// The player's break: the screen flashes dark red, a lightning bolt strikes the มลทิน ring, the camera shakes, and
    /// the khwan lost floats up red from the player's khwan bar while the bar shakes and flushes red. The enemy's
    /// break is smaller, only there: a red bolt strikes the enemy with a red glow on it, its khwan bar shakes and
    /// flushes and the khwan it lost floats up from the bar. Reduce Motion drops the flash, the bolts, the glow and
    /// the shaking; the khwan lost still shows. Strengths come from FlameGaugeStyleSO. Bootstraps itself in any scene
    /// that has a CombatManager.
    /// </summary>
    public class CorruptionBreakFx : MonoBehaviour
    {
        private const int SortingOrder = 800;   // over the HUDs (50) and the card screens (450-500), under the vignette (950)
        private static readonly Color FlashColor = new Color(0.45f, 0f, 0.02f);
        private static readonly Color PlayerBolt = new Color(1f, 0.55f, 0.45f);
        private static readonly Color EnemyBolt = new Color(1f, 0.25f, 0.2f);

        private CombatManager combat;
        private FlameGaugeStyleSO style;
        private RectTransform canvasRect;
        private Image flash;
        private static Texture2D glowTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<CorruptionBreakFx>() != null) return;
            new GameObject("CorruptionBreakFx").AddComponent<CorruptionBreakFx>();
        }

        private void Start()
        {
            style = FlameGaugeStyleSO.Load();
            var canvas = UiFactory.CreateOverlayCanvas("CorruptionBreakCanvas", SortingOrder, transform);
            var group = canvas.GetComponent<CanvasGroup>();
            if (group != null) { group.blocksRaycasts = false; group.interactable = false; }
            canvasRect = (RectTransform)canvas.transform;

            flash = UiFactory.CreateImage("Flash", canvas.transform, FlashColor);
            UiFactory.Stretch(flash.rectTransform, 0f);
            flash.raycastTarget = false;
            SetAlpha(flash, 0f);

            combat = CombatManager.Instance;
            if (combat == null) return;
            combat.OnCurseBackfireTriggered += HandlePlayerBreak;
            combat.OnEnemyCurseBackfireTriggered += HandleEnemyBreak;
        }

        private void OnDestroy()
        {
            if (combat == null) return;
            combat.OnCurseBackfireTriggered -= HandlePlayerBreak;
            combat.OnEnemyCurseBackfireTriggered -= HandleEnemyBreak;
        }

        // The events come before the khwan is taken, so the loss is read a frame later
        private void HandlePlayerBreak() => StartCoroutine(PlayerBreak(combat.State.playerKhwan));
        private void HandleEnemyBreak() => StartCoroutine(EnemyBreak(combat.State.enemyKhwan));

        private IEnumerator PlayerBreak(int before)
        {
            bool still = GameSettings.ReduceMotion;
            if (!still)
            {
                if (style.breakFlash > 0f)
                {
                    flash.DOKill();
                    DOTween.Sequence().SetLink(gameObject)
                        .Append(flash.DOFade(style.breakFlash, 0.06f))
                        .Append(flash.DOFade(0f, 0.45f).SetEase(Ease.OutQuad));
                }
                if (style.breakLightning && TryScreenPoint("CorruptionGauge", out Vector2 ring))
                    Strike(new Vector2(ring.x + Random.Range(180f, 320f), Screen.height + 20f), ring, PlayerBolt, 46f);
                ShakeCamera(style.breakShake);
            }

            yield return null;
            int lost = before - combat.State.playerKhwan;
            HitBar("PlayerPanel", lost, still);
        }

        private IEnumerator EnemyBreak(int before)
        {
            bool still = GameSettings.ReduceMotion;
            if (!still && style.breakLightning)
            {
                Vector2 enemy = EnemyScreenPoint();
                Strike(new Vector2(enemy.x + Random.Range(-120f, 120f), Screen.height + 20f), enemy, EnemyBolt, 34f);
                Glow(enemy, EnemyBolt, 420f);
            }

            yield return null;
            int lost = before - combat.State.enemyKhwan;
            HitBar("EnemyPanel", lost, still: false);
        }

        // ---------------------------------------------------------------- pieces

        // A khwan bar (CombatStatusHUD's panel) shakes and flushes red, and the khwan lost floats up from it
        private void HitBar(string panelName, int lost, bool still)
        {
            var panelGo = GameObject.Find(panelName);
            if (panelGo == null) return;
            var panel = (RectTransform)panelGo.transform;

            if (!still)
            {
                panel.DOComplete();
                panel.DOShakeAnchorPos(0.4f, 14f, 22);
            }
            var bg = panel.GetComponent<Image>();
            if (bg != null)
            {
                var normal = bg.color;
                var red = new Color(0.55f, 0.04f, 0.04f, Mathf.Max(normal.a, 0.85f));
                bg.DOKill();
                DOTween.Sequence().SetLink(panelGo)
                    .Append(bg.DOColor(red, 0.08f))
                    .Append(bg.DOColor(normal, 0.6f));
            }

            if (lost <= 0) return;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local)) return;

            var theme = UIThemeSO.Current;
            var text = UiFactory.CreateText(canvasRect, "KhwanLost", $"−{lost}", 56f, style.lossNumber, TextAlignmentOptions.Center,
                theme != null && theme.titleFont != null ? theme.titleFont : theme != null ? theme.bodyFont : null);
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(20, 0, 0, 255);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 80f);
            rt.anchoredPosition = local + new Vector2(0f, 30f);
            rt.localScale = Vector3.one * 1.4f;
            DOTween.Sequence().SetLink(text.gameObject)
                .Append(rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .Join(rt.DOAnchorPosY(local.y + 110f, 1.2f).SetEase(Ease.OutCubic))
                .Insert(0.6f, text.DOFade(0f, 0.6f))
                .OnComplete(() => Destroy(text.gameObject));
        }

        // A jagged bolt from one screen point to another that flickers twice and fades
        private void Strike(Vector2 fromScreen, Vector2 toScreen, Color color, float width)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, fromScreen, null, out Vector2 from)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, toScreen, null, out Vector2 to)) return;

            var go = new GameObject("Lightning", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.color = color;
            img.texture = BoltTexture();
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            Vector2 d = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(width * 4f, d.magnitude);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg);

            DOTween.Sequence().SetLink(go)
                .AppendInterval(0.07f)
                .AppendCallback(() => SetAlpha(img, 0f))
                .AppendInterval(0.05f)
                .AppendCallback(() => { img.texture = BoltTexture(); SetAlpha(img, 1f); })
                .Append(img.DOFade(0f, 0.25f))
                .OnComplete(() => { Destroy(img.texture); Destroy(go); });
        }

        // A soft round glow that flares and fades (on the enemy)
        private void Glow(Vector2 screen, Color color, float size)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local)) return;
            var go = new GameObject("Glow", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            go.transform.SetAsFirstSibling();
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.texture = GlowTexture();
            img.color = new Color(color.r, color.g, color.b, 0f);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
            rt.sizeDelta = Vector2.one * size;
            DOTween.Sequence().SetLink(go)
                .Append(img.DOFade(0.65f, 0.06f))
                .Append(img.DOFade(0f, 0.5f).SetEase(Ease.OutQuad))
                .OnComplete(() => Destroy(go));
        }

        private static void ShakeCamera(float strength)
        {
            var cam = Camera.main;
            if (cam == null || strength <= 0f || DOTween.IsTweening(cam.transform)) return; // not while the view moves
            cam.transform.DOShakePosition(0.35f, strength, 25, 90f, false, true);
        }

        // Where a HUD element named so sits on the screen
        private static bool TryScreenPoint(string name, out Vector2 screen)
        {
            screen = default;
            var go = GameObject.Find(name);
            if (go == null || !(go.transform is RectTransform rt)) return false;
            screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            return true;
        }

        // The enemy's chest: its hand of cards hangs in front of it
        private static Vector2 EnemyScreenPoint()
        {
            var cam = Camera.main;
            var hand = FindFirstObjectByType<EnemyHandView3D>();
            if (cam != null && hand != null)
            {
                Vector3 p = cam.WorldToScreenPoint(hand.transform.position);
                if (p.z > 0f) return new Vector2(p.x, p.y);
            }
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.72f);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        // A fresh jagged bolt each time: a white core with a soft edge, running from the top of the texture to its
        // bottom, kinked at random, with a short branch or two
        private static Texture2D BoltTexture()
        {
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Lightning" };
            var a = new float[w * h];
            float x = w * 0.5f;
            int segments = 9;
            float lastY = h - 1;
            for (int s = 1; s <= segments; s++)
            {
                float y = h - 1 - (h - 1) * s / (float)segments;
                float nx = s == segments ? w * 0.5f : Mathf.Clamp(x + Random.Range(-14f, 14f), 8f, w - 8f);
                DrawLine(a, w, h, x, lastY, nx, y, 1.6f);
                if (s > 1 && s < segments - 1 && Random.value < 0.35f)
                    DrawLine(a, w, h, nx, y, Mathf.Clamp(nx + Random.Range(-20f, 20f), 4f, w - 4f), y - Random.Range(14f, 30f), 0.9f);
                x = nx;
                lastY = y;
            }
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void DrawLine(float[] a, int w, int h, float x0, float y0, float x1, float y1, float core)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) * 2;
            const int reach = 7;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)Mathf.Max(steps, 1);
                float px = Mathf.Lerp(x0, x1, t), py = Mathf.Lerp(y0, y1, t);
                int cy = Mathf.RoundToInt(py);
                if (cy < 0 || cy >= h) continue;
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int cx = Mathf.RoundToInt(px) + dx;
                    if (cx < 0 || cx >= w) continue;
                    float dist = Mathf.Abs(cx - px);
                    float v = dist <= core ? 1f : Mathf.Exp(-(dist - core) * 0.6f) * 0.55f;
                    int idx = cy * w + cx;
                    if (v > a[idx]) a[idx] = v;
                }
            }
        }

        private static Texture2D GlowTexture()
        {
            if (glowTexture != null) return glowTexture;
            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BreakGlow" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float v = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(v * v * 255f));
                }
            glowTexture.SetPixels32(pixels);
            glowTexture.Apply();
            return glowTexture;
        }
    }
}
