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
    /// Simulated-Universe style event screen: illustration on the left, narration panel on the right
    /// with a typewriter body and the choice list below it. Click the text (or press Space) to finish
    /// typing; number keys pick a choice.
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
        public Image illustration;
        [Tooltip("Box behind the illustration. Empty = the illustration's parent. Hidden while a picture is shown so the picture blends into the background.")]
        public Image illustrationFrame;
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
            SetupSoftEdge();
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
                ShowFrame(evt.illustration == null);
                illustration.sprite = evt.illustration;
                illustration.enabled = evt.illustration != null;
                // Fit comes from the Image's own Preserve Aspect box in EventScene, so Play looks like edit mode
                illustration.color = new Color(1f, 1f, 1f, 0f);
                illustration.DOFade(1f, 0.6f);
            }
        }

        public const float EventIllustrationFeather = 0.3f;

        // The picture's edge fades into the scene background instead of ending at a hard rectangle.
        // A SoftEdgeImage added in the scene (Tools/TawanOS/UI/Soften Edge) keeps its own sides.
        private void SetupSoftEdge()
        {
            if (illustration == null) return;
            if (illustrationFrame == null && illustration.transform.parent != null)
            {
                illustrationFrame = illustration.transform.parent.GetComponent<Image>();
            }
            // Only the right edge, which faces the narration panel
            if (illustration.GetComponent<SoftEdgeImage>() == null) illustration.gameObject.AddComponent<SoftEdgeImage>().SetSides(right: EventIllustrationFeather);
        }

        // The frame (box, gold outline, "?") is only for an event without a picture
        private void ShowFrame(bool show)
        {
            if (illustrationFrame == null || illustrationFrame == illustration) return;
            illustrationFrame.enabled = show;
            var outline = illustrationFrame.GetComponent<Outline>();
            if (outline != null) outline.enabled = show;
            foreach (var label in illustrationFrame.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.enabled = show;
            }
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
            HideChoices();

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

        private void HideChoices()
        {
            foreach (var b in choiceButtons) b.gameObject.SetActive(false);
            if (choiceGroup != null)
            {
                choiceGroup.DOKill();
                choiceGroup.alpha = 0f;
                choiceGroup.interactable = false;
            }
        }

        private void ShowChoices()
        {
            if (pendingOptions == null) return;

            while (choiceButtons.Count < pendingOptions.Count)
            {
                var b = Instantiate(choiceTemplate, choiceContainer);
                choiceButtons.Add(b);
            }

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool used = i < pendingOptions.Count;
                choiceButtons[i].gameObject.SetActive(used);
                if (used) choiceButtons[i].Setup(i, pendingOptions[i]);
            }

            if (choiceGroup != null)
            {
                choiceGroup.interactable = true;
                choiceGroup.DOFade(1f, 0.25f);
            }
        }

        private void RefreshRunStatus()
        {
            var run = RunState.Current;
            if (hpText != null) hpText.text = $"HP {run.CurrentHp}/{run.MaxHp}";
            if (incenseText != null) incenseText.text = $"ธูป {run.Incense}";
        }
    }
}
