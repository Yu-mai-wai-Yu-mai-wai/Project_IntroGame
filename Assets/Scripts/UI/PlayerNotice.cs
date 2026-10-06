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
            if (!ShouldShow(message, Time.unscaledTime)) return;

            Debug.Log($"[Notice] {message}");
            NoticeHost.EnsureExists();
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

    /// <summary>Draws the notices as toasts under the turn banner. Created on first use and kept across scenes.</summary>
    public class NoticeHost : MonoBehaviour
    {
        private const int MaxVisible = 3;
        private const float FadeIn = 0.15f;
        private const float FadeOut = 0.4f;
        private const float Width = 960f;
        private const float Height = 72f;
        private const float Gap = 10f;
        private const float TopOffset = 190f;

        private class Toast
        {
            public RectTransform root;
            public CanvasGroup group;
            public float age;
            public float life;
        }

        private static NoticeHost instance;

        private readonly List<Toast> toasts = new List<Toast>();
        private RectTransform container;
        private CanvasGroup rootGroup;

        public static void EnsureExists()
        {
            if (instance != null) return;
            if (!Application.isPlaying) return;

            var go = new GameObject("PlayerNoticeHost");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NoticeHost>();
        }

        private void Awake()
        {
            var canvas = UiFactory.CreateOverlayCanvas("NoticeCanvas", 950, transform);
            rootGroup = canvas.GetComponent<CanvasGroup>();
            container = UiFactory.CreateRect("Toasts", canvas.transform);
            container.anchorMin = new Vector2(0.5f, 1f);
            container.anchorMax = new Vector2(0.5f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.sizeDelta = new Vector2(Width, 0f);

            PlayerNotice.OnNotice += Add;
        }

        private void OnDestroy()
        {
            PlayerNotice.OnNotice -= Add;
            if (instance == this) instance = null;
        }

        private void Add(string message, NoticeKind kind)
        {
            while (toasts.Count >= MaxVisible)
            {
                Destroy(toasts[0].root.gameObject);
                toasts.RemoveAt(0);
            }

            var theme = UIThemeSO.Current;
            bool warning = kind == NoticeKind.Warning;
            Color border = warning ? theme.accent : theme.crimson;

            var root = UiFactory.CreateRect("Toast", container);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(Width, Height);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var outer = UiFactory.CreateImage("Border", root, border);
            UiFactory.Stretch(outer.rectTransform, 0f);
            var inner = UiFactory.CreateImage("Panel", root, theme.panel);
            UiFactory.Stretch(inner.rectTransform, 3f);

            var label = UiFactory.CreateText(root, "Message", message, theme.bodySize + 2f,
                warning ? theme.accent : theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(label.rectTransform, 12f);

            toasts.Add(new Toast { root = root, group = group, age = 0f, life = warning ? 3.8f : 3f });
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
                    Destroy(toast.root.gameObject);
                    toasts.RemoveAt(i);
                    continue;
                }

                float alpha = toast.age < FadeIn ? toast.age / FadeIn
                    : toast.age > toast.life ? 1f - (toast.age - toast.life) / FadeOut
                    : 1f;
                toast.group.alpha = Mathf.Clamp01(alpha);
            }

            for (int i = 0; i < toasts.Count; i++)
            {
                toasts[i].root.anchoredPosition = new Vector2(0f, -TopOffset - i * (Height + Gap));
            }
        }
    }
}
