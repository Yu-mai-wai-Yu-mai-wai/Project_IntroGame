using System.Collections;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The ghost speaks during a fight: a line typed out under the enemy's bar at the top of the screen, then gone.
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
            line.maxVisibleCharacters = 0;
            line.ForceMeshUpdate();
            int total = line.textInfo.characterCount;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.15f)
            {
                group.alpha = Mathf.Max(group.alpha, t);
                yield return null;
            }
            group.alpha = 1f;

            float perSecond = GameSettings.CharactersPerSecond(lettersPerSecond);
            float shown = 0f;
            while (shown < total)
            {
                shown += perSecond * Time.unscaledDeltaTime;
                line.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            line.maxVisibleCharacters = total;

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

        /// <summary>
        /// Builds the speech box under this object: a dark smoky strip under the enemy's bar with the ghost's name
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

            var back = UiFactory.CreateImage("Back", box, new Color(0.04f, 0.02f, 0.03f, 0.82f));
            UiFactory.Stretch(back.rectTransform, 0f);
            back.raycastTarget = false;

            var useFont = font != null ? font : theme != null ? theme.bodyFont : null;
            Color accent = theme != null ? theme.accent : new Color(0.94f, 0.68f, 0.48f);
            speaker = UiFactory.CreateText(box, "Speaker", "", 24f, accent, TextAlignmentOptions.TopLeft, useFont);
            var sr = speaker.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = new Vector2(28f, -40f);
            sr.offsetMax = new Vector2(-28f, -8f);

            line = UiFactory.CreateText(box, "Line", "", 34f, new Color(0.92f, 0.86f, 0.86f), TextAlignmentOptions.Center, useFont);
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
