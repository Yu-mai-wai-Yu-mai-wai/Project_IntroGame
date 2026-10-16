using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.GameFlow;
using TawanOS.Settings;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// Simulated-Universe style event screen: the illustration fills the left half of the screen edge to edge (cropped,
    /// never stretched, no frame), and a black narration panel on the right meets it with a smoky edge down the middle
    /// (Game Designer, 12 Oct; layout by Tools/TawanOS/Event Engine/Split Layout With Smoke). An event without a
    /// picture leaves the left half dark. The narration has a typewriter body and the choice list below it. Click the
    /// text (or press Space) to finish typing; number keys pick a choice.
    /// </summary>
    public class EventViewUI : MonoBehaviour
    {
        public struct ChoiceOption
        {
            public string label;
            public string hint;
            public string costText;
            public bool interactable;
            public Action onSelected;
        }

        [Header("Header")]
        [Tooltip("Fills its parent (the left half) like a cover: an AspectRatioFitter set to Envelope Parent keeps the picture's shape and the parent's mask crops what spills over.")]
        public Image illustration;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI speakerText;

        [Header("Narration")]
        public TextMeshProUGUI bodyText;
        public TextMeshProUGUI effectText;
        public Button skipTypingButton;
        [Min(1f)] public float charactersPerSecond = 60f;

        [Header("Choices")]
        public RectTransform choiceContainer;
        public CanvasGroup choiceGroup;
        public EventChoiceButton choiceTemplate;

        [Header("Run Status")]
        public TextMeshProUGUI hpText;
        [FormerlySerializedAs("offeringsText")]
        public TextMeshProUGUI incenseText;

        private readonly List<EventChoiceButton> choiceButtons = new List<EventChoiceButton>();
        private List<ChoiceOption> pendingOptions;
        private Coroutine typingRoutine;
        private bool isTyping;

        private void Awake()
        {
            SetupIllustration();
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);
            if (skipTypingButton != null) skipTypingButton.onClick.AddListener(CompleteTyping);
        }

        private void OnEnable()
        {
            RunState.Current.OnChanged += RefreshRunStatus;
            RefreshRunStatus();
        }

        private void OnDisable()
        {
            RunState.Current.OnChanged -= RefreshRunStatus;
        }

        private void Update()
        {
            if (isTyping)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) CompleteTyping();
                return;
            }

            if (choiceGroup != null && !choiceGroup.interactable) return;
            for (int i = 0; i < choiceButtons.Count && i < 9; i++)
            {
                if (choiceButtons[i].gameObject.activeSelf && Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    choiceButtons[i].Select();
                    break;
                }
            }
        }

        public void ShowEventHeader(EventDataSO evt)
        {
            if (titleText != null) titleText.text = evt.title;
            if (illustration != null)
            {
                // No picture: the left half stays dark (no frame, no "?")
                illustration.sprite = evt.illustration;
                illustration.enabled = evt.illustration != null;
                if (evt.illustration != null && illustrationFit != null)
                {
                    var r = evt.illustration.rect;
                    illustrationFit.aspectRatio = r.height > 0f ? r.width / r.height : 1f;
                }
                illustration.color = new Color(1f, 1f, 1f, 0f);
                illustration.DOFade(1f, 0.6f);
            }
        }

        // Feather used by Tools/TawanOS/UI/Soften Edge on the old framed layout
        public const float EventIllustrationFeather = 0.3f;

        private AspectRatioFitter illustrationFit;

        // The picture covers the left half: it keeps its shape (Envelope Parent) and the half's mask crops the spill.
        // The smoke of the narration panel hides the seam, so the picture no longer fades out at its right edge.
        private void SetupIllustration()
        {
            if (illustration == null) return;
            illustration.preserveAspect = false;
            var soft = illustration.GetComponent<SoftEdgeImage>();
            if (soft != null) soft.enabled = false;
            illustrationFit = illustration.GetComponent<AspectRatioFitter>();
            if (illustrationFit == null) illustrationFit = illustration.gameObject.AddComponent<AspectRatioFitter>();
            illustrationFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var parent = illustration.transform.parent;
            if (parent != null && parent.GetComponent<RectMask2D>() == null) parent.gameObject.AddComponent<RectMask2D>();
        }

        public void ShowPage(string speaker, string body, string effectSummary, List<ChoiceOption> options)
        {
            if (speakerText != null)
            {
                speakerText.text = speaker;
                speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }
            SetEffectSummary(effectSummary);

            pendingOptions = options;
            // The choices take their place (hidden) before the text types, so the text's box is final from the start
            LayoutChoices();

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = StartCoroutine(TypeBody(body ?? string.Empty));
        }

        public void SetEffectSummary(string summary)
        {
            if (effectText == null) return;
            effectText.text = summary;
            effectText.gameObject.SetActive(!string.IsNullOrEmpty(summary));
            if (!string.IsNullOrEmpty(summary))
            {
                effectText.transform.DOKill();
                effectText.transform.localScale = Vector3.one * 1.15f;
                effectText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
            }
        }

        private IEnumerator TypeBody(string body)
        {
            isTyping = true;
            bodyText.text = body;
            bodyText.maxVisibleCharacters = 0;
            FitBodyText();
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;

            float shown = 0f;
            while (shown < total)
            {
                shown += GameSettings.CharactersPerSecond(charactersPerSecond) * Time.deltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            FinishTyping();
        }

        private void CompleteTyping()
        {
            if (!isTyping) return;
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            FinishTyping();
        }

        private void FinishTyping()
        {
            typingRoutine = null;
            isTyping = false;
            bodyText.maxVisibleCharacters = int.MaxValue;
            ShowChoices();
        }

        // Builds the page's choices, invisible and not clickable until the text has finished typing. Each one is as
        // tall as its text needs (EventChoiceButton), so the list's height is known before the text lays out.
        private void LayoutChoices()
        {
            if (choiceGroup != null)
            {
                choiceGroup.DOKill();
                choiceGroup.alpha = 0f;
                choiceGroup.interactable = false;
                choiceGroup.blocksRaycasts = false;
            }

            int count = pendingOptions != null ? pendingOptions.Count : 0;
            while (choiceButtons.Count < count)
            {
                var b = Instantiate(choiceTemplate, choiceContainer);
                choiceButtons.Add(b);
            }

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool used = i < count;
                choiceButtons[i].gameObject.SetActive(used);
                if (used) choiceButtons[i].Setup(i, pendingOptions[i]);
            }
        }

        private void ShowChoices()
        {
            if (pendingOptions == null || choiceGroup == null) return;
            choiceGroup.interactable = true;
            choiceGroup.blocksRaycasts = true;
            choiceGroup.DOFade(1f, 0.25f);
        }

        // The text's box is what the choices leave of the panel: a long story shrinks its letters to fit the box
        // instead of running down over the choices. The scene's font size is the largest it gets.
        private float bodyMaxSize = -1f;
        private const float BodyMinSize = 20f;

        private void FitBodyText()
        {
            if (bodyText == null) return;
            if (bodyMaxSize < 0f) bodyMaxSize = bodyText.fontSize;
            var panel = bodyText.transform.parent != null ? bodyText.transform.parent.parent as RectTransform : null;
            if (panel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = BodyMinSize;
            bodyText.fontSizeMax = bodyMaxSize;
        }

        private void RefreshRunStatus()
        {
            var run = RunState.Current;
            if (hpText != null) hpText.text = $"ขวัญ {KhwanDisplay.Text(run.CurrentHp, run.MaxHp)}";
            if (incenseText != null) incenseText.text = $"ธูป {run.Incense}";
        }
    }
}
