using System.Collections;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The ghost speaks during a fight: a faint red line that seeps in letter by letter over a soft black haze under
    /// the enemy's bar at the top of the screen, then fades (Game Designer, 24 Oct: no hard box; warnings are red
    /// toasts at the bottom, so the two do not mix).
    /// Lines come from the enemy profile (EnemyProfileSO.lines, or EnemyLines.Defaults). It speaks when the fight
    /// starts, enters its next phase, first runs low on khwan, wins or loses, and now and then when it plays a card,
    /// is hurt or hurts the player. Text only for now; a voice can hook into <see cref="OnLine"/>.
    /// Placed in the combat scene by Tools > TawanOS > UI > Add Enemy Speech; parts left empty are built at runtime.
    /// </summary>
    public class EnemySpeech : MonoBehaviour
    {
        [Header("Parts (Add Enemy Speech builds them)")]
        public CanvasGroup group;
        public TMP_Text speaker;
        public TMP_Text line;
        [Tooltip("Font for the lines. Empty = the UI theme's body font.")]
        public TMP_FontAsset font;

        [Header("Timing")]
        [Tooltip("Letters per second at the normal text speed (the Settings text speed scales it).")]
        public float lettersPerSecond = 28f;
        [Tooltip("Seconds each letter takes to fade in; several are fading at once, so the line seeps in.")]
        public float letterFadeSeconds = 0.35f;
        [Tooltip("Extra pause, in letters, at a space and at each dot of \"...\".")]
        public float pauseLetters = 3f;
        [Tooltip("How strong the line is once fully in (below 1 = faint).")]
        [Range(0.2f, 1f)] public float lineAlpha = 1f; // full: at 0.85 the red sank into the dark (Game Designer, 25 Oct)
        [Tooltip("Seconds a line stays after it is fully typed.")]
        public float holdSeconds = 2.4f;
        [Tooltip("Seconds after a line before a line that is not important may be said.")]
        public float quietSeconds = 5f;

        [Header("How often (0..1) for the moments that happen a lot")]
        [Range(0f, 1f)] public float playCardChance = 0.3f;
        [Range(0f, 1f)] public float hurtChance = 0.4f;
        [Range(0f, 1f)] public float playerHurtChance = 0.4f;
        [Tooltip("Khwan share under which the ghost says its low-khwan line (once a fight).")]
        [Range(0f, 1f)] public float lowKhwanRatio = 0.3f;

        /// <summary>A line starts: the speaker's name, the line and the moment.</summary>
        public event System.Action<string, string, EnemyLines.Moment> OnLine;

        private CombatManager combat;
        private EffectResolver resolver;
        private EnemyProfileSO spokenFor;
        private bool saidLow;
        private float quietUntil;
        private Coroutine speaking;

        private void Start()
        {
            if (group == null || line == null) BuildDefaultUI();
            group.alpha = 0f;
            // The haze is made in code, so a scene built by the editor tool saves the Back image without it
            var back = group.transform.Find("Back") != null ? group.transform.Find("Back").GetComponent<Image>() : null;
            if (back != null && back.sprite == null) back.sprite = Haze();

            combat = CombatManager.Instance;
            resolver = EffectResolver.Instance;
            if (combat != null)
            {
                combat.OnEnemyPhaseChanged += HandlePhase;
                combat.OnCombatEnded += HandleEnded;
                combat.EnemyCards.OnCardPlayed += HandlePlayed;
            }
            if (resolver != null) resolver.OnDamageDealt += HandleDamage;
        }

        private void OnDestroy()
        {
            if (combat != null)
            {
                combat.OnEnemyPhaseChanged -= HandlePhase;
                combat.OnCombatEnded -= HandleEnded;
                combat.EnemyCards.OnCardPlayed -= HandlePlayed;
            }
            if (resolver != null) resolver.OnDamageDealt -= HandleDamage;
        }

        // A new fight (or a new enemy): it greets the player once it is under way
        private void Update()
        {
            if (combat == null || combat.currentEnemyProfile == null || combat.IsCombatOver) return;
            if (spokenFor == combat.currentEnemyProfile || combat.EnemyPhase > 1) return;
            spokenFor = combat.currentEnemyProfile;
            saidLow = false;
            StartCoroutine(SayAfter(1f, EnemyLines.Moment.CombatStart));
        }

        private IEnumerator SayAfter(float seconds, EnemyLines.Moment moment)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Say(moment, important: true);
        }

        private void HandlePhase(int phase, EnemyProfileSO profile)
        {
            spokenFor = profile;
            saidLow = false; // a new khwan bar: it can cry out again when that one runs low
            Say(EnemyLines.Moment.PhaseChange, important: true);
        }

        private void HandleEnded(bool playerWon)
        {
            Say(playerWon ? EnemyLines.Moment.EnemyLoses : EnemyLines.Moment.EnemyWins, important: true);
        }

        private void HandlePlayed(CardInstance card)
        {
            if (Random.value < playCardChance) Say(EnemyLines.Moment.PlayCard, important: false);
        }

        private void HandleDamage(int amount, bool toPlayer)
        {
            if (combat == null || amount <= 0 || combat.IsCombatOver) return;
            if (toPlayer)
            {
                if (Random.value < playerHurtChance) Say(EnemyLines.Moment.PlayerHurt, important: false);
                return;
            }

            var s = combat.State;
            if (!saidLow && s.maxEnemyKhwan > 0 && s.enemyKhwan > 0 && s.enemyKhwan <= s.maxEnemyKhwan * lowKhwanRatio)
            {
                saidLow = true;
                Say(EnemyLines.Moment.LowKhwan, important: true);
            }
            else if (Random.value < hurtChance)
            {
                Say(EnemyLines.Moment.Hurt, important: false);
            }
        }

        /// <summary>Says a line for this moment. A line that is not important waits its turn and never cuts one off.</summary>
        public void Say(EnemyLines.Moment moment, bool important)
        {
            if (!important && (speaking != null || Time.unscaledTime < quietUntil)) return;
            var profile = combat != null ? combat.currentEnemyProfile : null;
            string text = EnemyLines.Pick(profile != null ? profile.lines : null, moment);
            if (string.IsNullOrEmpty(text)) return;

            string name = profile != null ? profile.enemyName : "";
            if (speaking != null) StopCoroutine(speaking);
            speaking = StartCoroutine(Speak(name, text));
            OnLine?.Invoke(name, text, moment);
        }

        // Real time throughout, so a hit-stop or the pause menu's time scale does not freeze a line mid-word
        private IEnumerator Speak(string name, string text)
        {
            if (speaker != null) speaker.text = name;
            line.text = text;
            line.maxVisibleCharacters = int.MaxValue;
            line.ForceMeshUpdate();
            int total = line.textInfo.characterCount;

            // When each letter starts to fade in: one after another, with a pause at spaces and dots
            float perSecond = GameSettings.CharactersPerSecond(lettersPerSecond);
            var starts = new float[total];
            float clock = 0f;
            for (int i = 0; i < total; i++)
            {
                starts[i] = clock / Mathf.Max(1f, perSecond);
                char c = line.textInfo.characterInfo[i].character;
                clock += c == ' ' || c == '.' || c == '…' ? 1f + pauseLetters : 1f;
            }
            float done = (total > 0 ? starts[total - 1] : 0f) + letterFadeSeconds;
            SetLetterAlphas(starts, 0f);

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.15f)
            {
                group.alpha = Mathf.Max(group.alpha, t);
                yield return null;
            }
            group.alpha = 1f;

            for (float t = 0f; t < done; t += Time.unscaledDeltaTime)
            {
                SetLetterAlphas(starts, t);
                yield return null;
            }
            SetLetterAlphas(starts, float.MaxValue);

            yield return new WaitForSecondsRealtime(holdSeconds);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f)
            {
                group.alpha = t;
                yield return null;
            }
            group.alpha = 0f;
            quietUntil = Time.unscaledTime + quietSeconds;
            speaking = null;
        }

        // Each letter's alpha from when it started (TMP rebuilds the mesh at full alpha, so it is set every frame)
        private void SetLetterAlphas(float[] starts, float time)
        {
            line.ForceMeshUpdate();
            var info = line.textInfo;
            for (int i = 0; i < info.characterCount && i < starts.Length; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                float k = letterFadeSeconds > 0f ? Mathf.Clamp01((time - starts[i]) / letterFadeSeconds) : (time >= starts[i] ? 1f : 0f);
                byte a = (byte)(255f * lineAlpha * k * k * (3f - 2f * k));
                var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                for (int v = 0; v < 4; v++) colors[ch.vertexIndex + v].a = a;
            }
            line.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        // 0 below a, 1 above b, smooth between (Mathf.SmoothStep interpolates a..b instead, which is not this)
        private static float Ramp(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // A soft black haze: dark in the middle, fading out to nothing at every edge (no box, no border)
        private static Sprite haze;
        private static Sprite Haze()
        {
            if (haze != null) return haze;
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f), dy = Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float ex = 1f - Ramp(0.55f, 1f, dx), ey = 1f - Ramp(0.25f, 1f, dy);
                    px[y * w + x] = new Color32(0, 0, 0, (byte)(255f * ex * ey));
                }
            tex.SetPixels32(px);
            tex.Apply();
            haze = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return haze;
        }

        /// <summary>
        /// Builds the speech box under this object: a soft black haze under the enemy's bar with the ghost's name
        /// and its line. Used by the editor tool (so the parts can be tuned in the scene) and for parts left empty.
        /// </summary>
        public void BuildDefaultUI()
        {
            var theme = UIThemeSO.Current;
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) canvas = UiFactory.CreateOverlayCanvas("EnemySpeechCanvas", 55, transform);

            var box = UiFactory.CreateRect("SpeechBox", canvas.transform);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -215f);
            box.sizeDelta = new Vector2(980f, 120f);
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var back = UiFactory.CreateImage("Back", box, new Color(1f, 1f, 1f, 0.92f));
            back.sprite = Haze();
            UiFactory.Stretch(back.rectTransform, 0f);
            back.rectTransform.offsetMin = new Vector2(-160f, -30f); // the haze reaches past the text and fades out
            back.rectTransform.offsetMax = new Vector2(160f, 30f);
            back.raycastTarget = false;

            var useFont = font != null ? font : theme != null ? theme.bodyFont : null;
            speaker = UiFactory.CreateText(box, "Speaker", "", 24f, new Color(0.95f, 0.5f, 0.45f, 0.9f), TextAlignmentOptions.Top, useFont);
            var sr = speaker.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = new Vector2(28f, -40f);
            sr.offsetMax = new Vector2(-28f, -8f);

            line = UiFactory.CreateText(box, "Line", "", 34f, new Color(1f, 0.38f, 0.32f), TextAlignmentOptions.Center, useFont);
            var lr = line.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(28f, 10f);
            lr.offsetMax = new Vector2(-28f, -38f);
            line.enableAutoSizing = true;
            line.fontSizeMin = 18f;
            line.fontSizeMax = 34f;
            line.outlineWidth = 0.2f;
            line.outlineColor = new Color32(0, 0, 0, 255);
        }
    }
}
