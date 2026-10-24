using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.CardEngine;

namespace TawanOS.UI
{
    public enum NoticeKind
    {
        Info,
        Warning,
    }

    /// <summary>
    /// Tells the player something in the game window instead of only in the Console: why a card cannot be
    /// played, that the hand is full, and so on (plan task H1). Call PlayerNotice.Show("ข้อความไทย") from any
    /// game code; the same message repeated within a short time is shown once so a held click does not spam.
    /// </summary>
    public static class PlayerNotice
    {
        public const float DuplicateWindowSeconds = 1.5f;

        public static event Action<string, NoticeKind> OnNotice;

        private static string lastMessage;
        private static float lastTime = float.NegativeInfinity;

        public static void Show(string message, NoticeKind kind = NoticeKind.Warning)
        {
            if (string.IsNullOrEmpty(message)) return;

            NoticeHost.EnsureExists();

            if (NoticeHost.Instance != null && NoticeHost.Instance.TryExtend(message))
            {
                lastMessage = message;
                lastTime = Time.unscaledTime;
                return;
            }

            if (!ShouldShow(message, Time.unscaledTime)) return;

            Debug.Log($"[Notice] {message}");
            OnNotice?.Invoke(message, kind);
        }

        // Pure rule, kept apart from Time so it can be tested: the same text twice inside the window is dropped
        public static bool ShouldShow(string message, float now)
        {
            if (string.IsNullOrEmpty(message)) return false;
            if (message == lastMessage && now - lastTime < DuplicateWindowSeconds) return false;

            lastMessage = message;
            lastTime = now;
            return true;
        }

        public static void ResetForTests()
        {
            lastMessage = null;
            lastTime = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// Draws the notices as toasts low on the screen, a little above the bottom edge (Game Designer, 24 Oct: away from
    /// the ghost's lines at the top; warnings red, with a short shake). Created on first use and kept across scenes.
    /// </summary>
    public class NoticeHost : MonoBehaviour
    {
        private const int MaxVisible = 3;
        private const float FadeIn = 0.15f;
        private const float FadeOut = 0.4f;
        private const float Width = 860f;
        private const float Height = 72f;
        private const float Gap = 10f;
        private const float BottomOffset = 150f;
        private const float ShakeSeconds = 0.3f;

        private static readonly Color WarnBorder = new Color(0.78f, 0.13f, 0.12f, 0.95f);
        private static readonly Color WarnPanel = new Color(0.13f, 0.02f, 0.02f, 0.92f);
        private static readonly Color WarnText = new Color(1f, 0.62f, 0.58f);

        public class Toast
        {
            public string message;
            public RectTransform root;
            public CanvasGroup group;
            public TextMeshProUGUI label;
            public float age;
            public float life;
            public bool warning;
        }

        public static NoticeHost Instance => instance;
        private static NoticeHost instance;

        public readonly List<Toast> toasts = new List<Toast>();
        private RectTransform container;
        private CanvasGroup rootGroup;

        public int VisibleCount => toasts.Count;

        public static void EnsureExists()
        {
            if (instance != null) return;
            if (!Application.isPlaying) return;

            var go = new GameObject("PlayerNoticeHost");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NoticeHost>();
        }

        public static NoticeHost CreateHostForTesting(Transform parent = null)
        {
            var go = new GameObject("TestNoticeHost");
            if (parent != null) go.transform.SetParent(parent, false);
            var host = go.AddComponent<NoticeHost>();
            instance = host;
            return host;
        }

        private void Awake()
        {
            instance = this;
            EnsureContainer();
            PlayerNotice.OnNotice -= Add;
            PlayerNotice.OnNotice += Add;
        }

        private void OnDestroy()
        {
            PlayerNotice.OnNotice -= Add;
            if (instance == this) instance = null;
        }

        public void EnsureContainer()
        {
            if (container != null) return;
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                canvas = UiFactory.CreateOverlayCanvas("NoticeCanvas", 950, transform);
            }
            rootGroup = canvas.GetComponent<CanvasGroup>();
            container = UiFactory.CreateRect("Toasts", canvas.transform);
            container.anchorMin = new Vector2(0.5f, 0f);
            container.anchorMax = new Vector2(0.5f, 0f);
            container.pivot = new Vector2(0.5f, 0f);
            container.sizeDelta = new Vector2(Width, 0f);
        }

        /// <summary>
        /// (a) If the same text is already visible, extends its display time and returns true.
        /// </summary>
        public bool TryExtend(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            for (int i = 0; i < toasts.Count; i++)
            {
                if (toasts[i].message == message)
                {
                    toasts[i].age = 0f;
                    return true;
                }
            }
            return false;
        }

        public void Add(string message, NoticeKind kind)
        {
            if (string.IsNullOrEmpty(message)) return;

            // (a) If the same text is already visible, do not add a new row; extend that row's display time instead.
            if (TryExtend(message)) return;

            // (c) At most 3 rows visible.
            while (toasts.Count >= MaxVisible)
            {
                if (toasts[0].root != null)
                {
                    if (Application.isPlaying) Destroy(toasts[0].root.gameObject);
                    else DestroyImmediate(toasts[0].root.gameObject);
                }
                toasts.RemoveAt(0);
            }

            var theme = UIThemeSO.Current;
            bool warning = kind == NoticeKind.Warning;
            Color border = warning ? WarnBorder : theme.crimson;

            EnsureContainer();

            var root = UiFactory.CreateRect("Toast", container);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(Width, Height);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var outer = UiFactory.CreateImage("Border", root, border);
            UiFactory.Stretch(outer.rectTransform, 0f);
            var inner = UiFactory.CreateImage("Panel", root, warning ? WarnPanel : theme.panel);
            UiFactory.Stretch(inner.rectTransform, 3f);

            bool hasKhwanOrThup = message.Contains("ขวัญ") || message.Contains("ธูป");
            TMP_FontAsset fontToUse = (hasKhwanOrThup && theme.titleFont != null) ? theme.titleFont : theme.bodyFont;
            var label = UiFactory.CreateText(root, "Message", message, theme.bodySize + 2f,
                warning ? WarnText : theme.text, TextAlignmentOptions.Center, fontToUse);

            // (b) Text must fit its box: auto-size within limits or wrap with the box growing; no overflow.
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = theme.bodySize + 2f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.Stretch(label.rectTransform, 12f);

            float prefHeight = label.preferredHeight + 24f;
            if (prefHeight > Height)
            {
                root.sizeDelta = new Vector2(Width, prefHeight);
            }

            toasts.Add(new Toast
            {
                message = message,
                root = root,
                group = group,
                label = label,
                age = 0f,
                life = warning ? 3.8f : 3f,
                warning = warning
            });

            Relayout();
        }

        // Newest lowest, older ones pushed up; a new warning shakes for a moment
        public void Relayout()
        {
            float y = BottomOffset;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                if (t.root == null) continue;
                float shake = t.warning && t.age < ShakeSeconds ? Mathf.Sin(t.age * 70f) * 9f * (1f - t.age / ShakeSeconds) : 0f;
                t.root.anchoredPosition = new Vector2(shake, y);
                y += t.root.sizeDelta.y + Gap;
            }
        }

        private void Update()
        {
            // The board-only camera view hides all UI
            rootGroup.alpha = CombatCameraRig3D.HideOverlay ? 0f : 1f;

            float dt = Time.unscaledDeltaTime;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var toast = toasts[i];
                toast.age += dt;

                if (toast.age >= toast.life + FadeOut)
                {
                    if (toast.root != null)
                    {
                        if (Application.isPlaying) Destroy(toast.root.gameObject);
                        else DestroyImmediate(toast.root.gameObject);
                    }
                    toasts.RemoveAt(i);
                    continue;
                }

                float alpha = toast.age < FadeIn ? toast.age / FadeIn
                    : toast.age > toast.life ? 1f - (toast.age - toast.life) / FadeOut
                    : 1f;
                toast.group.alpha = Mathf.Clamp01(alpha);
            }

            Relayout();
        }
    }
}
