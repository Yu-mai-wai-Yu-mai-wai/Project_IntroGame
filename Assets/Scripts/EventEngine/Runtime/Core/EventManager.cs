using System;
using System.Collections.Generic;
using System.Text;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using UnityEngine;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// Runs one story event: shows its pages, pays choice costs, rolls weighted outcomes and applies
    /// their effects to the <see cref="RunState"/>. Raises <see cref="OnEventFinished"/> when the player
    /// leaves, <see cref="OnCombatRequested"/> when an outcome starts a fight, or
    /// <see cref="OnCardRewardRequested"/> when an outcome gives a card (the reward screen picks it).
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        [Header("Data")]
        public EventCatalogSO catalog;
        [Tooltip("Played when the scene is opened directly (no event requested by the map). Empty = random from catalog.")]
        public EventDataSO testEvent;

        [Header("View")]
        public EventViewUI view;

        public EventDataSO CurrentEvent { get; private set; }

#if UNITY_EDITOR
        /// <summary>SessionState key holding the GUID of an event the inspector's "Test play" button wants to run.</summary>
        public const string EditorTestEventKey = "TawanOS.EventEngine.TestEventGuid";
#endif

        public event Action OnEventFinished;
        public event Action<EnemyProfileSO> OnCombatRequested;
        public event Action OnCardRewardRequested;

        private bool hasBegun;
        private EnemyProfileSO pendingCombatEnemy;
        private bool pendingCardReward;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // GameFlowManager calls BeginRandomEvent from sceneLoaded (before Start) when coming from the map.
            if (!hasBegun)
            {
                var editorTest = TakeEditorTestEvent();
                if (editorTest != null) BeginEvent(editorTest);
                else if (testEvent != null) BeginEvent(testEvent);
                else BeginRandomEvent(int.MaxValue);
            }
        }

        private static EventDataSO TakeEditorTestEvent()
        {
#if UNITY_EDITOR
            string guid = UnityEditor.SessionState.GetString(EditorTestEventKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;
            UnityEditor.SessionState.EraseString(EditorTestEventKey);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<EventDataSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#else
            return null;
#endif
        }

        public void BeginRandomEvent(int floor)
        {
            var evt = catalog != null ? catalog.PickRandom(floor, RunState.Current.SeenEventIds) : null;
            if (evt == null)
            {
                Debug.LogError("[EventManager] No event available - assign an EventCatalogSO with events.");
                return;
            }
            BeginEvent(evt);
        }

        // กองของเซ่น (the map's offering-pile node): its own story, not rolled from the random pool
        public void BeginOfferingEvent()
        {
            if (catalog != null && catalog.offeringEvent != null) BeginEvent(catalog.offeringEvent);
            else
            {
                Debug.LogError("[EventManager] EventCatalog has no Offering Event - playing a random event instead.");
                BeginRandomEvent(int.MaxValue);
            }
        }

        public void BeginEvent(EventDataSO evt)
        {
            hasBegun = true;
            CurrentEvent = evt;
            pendingCombatEnemy = null;
            pendingCardReward = false;
            RunState.Current.MarkEventSeen(evt.eventId);

            if (view != null) view.ShowEventHeader(evt);
            ShowPage(evt.FirstPage, null, null);
        }

        private void ShowPage(EventPage page, string prefixText, string effectSummary)
        {
            if (page == null)
            {
                Debug.LogError($"[EventManager] Event '{CurrentEvent.eventId}' has a missing page.");
                ShowEnding(prefixText, effectSummary);
                return;
            }

            string body = string.IsNullOrEmpty(prefixText) ? page.body : prefixText + "\n\n" + page.body;
            string speaker = string.IsNullOrEmpty(page.speakerOverride) ? CurrentEvent.speakerName : page.speakerOverride;

            if (page.choices.Count == 0)
            {
                ShowEnding(body, effectSummary);
                return;
            }

            var options = new List<EventViewUI.ChoiceOption>();
            for (int i = 0; i < page.choices.Count; i++)
            {
                var choice = page.choices[i];
                int index = i;
                options.Add(new EventViewUI.ChoiceOption
                {
                    label = choice.label,
                    hint = choice.hint,
                    costText = BuildCostText(choice),
                    interactable = CanChoose(choice),
                    onSelected = () => HandleChoice(page, index),
                });
            }

            if (view != null) view.ShowPage(speaker, body, effectSummary, options);
        }

        private void HandleChoice(EventPage page, int choiceIndex)
        {
            var choice = page.choices[choiceIndex];
            var run = RunState.Current;
            var summary = new List<string>();

            if (choice.incenseCost > 0)
            {
                run.AddIncense(-choice.incenseCost);
                summary.Add($"-{choice.incenseCost} ธูป");
            }
            if (choice.hpCost > 0)
            {
                run.TakeDamage(choice.hpCost);
                summary.Add($"-{choice.hpCost} HP");
            }

            var outcome = RollOutcome(choice.outcomes);
            if (outcome == null)
            {
                ShowEnding(null, JoinSummary(summary));
                return;
            }

            foreach (var effect in outcome.effects)
            {
                string line = ApplyEffect(effect);
                if (!string.IsNullOrEmpty(line)) summary.Add(line);
            }

            // A fight or a card reward always ends the event, so it skips any follow-up page.
            var nextPage = string.IsNullOrEmpty(outcome.nextPageId) ? null : CurrentEvent.GetPage(outcome.nextPageId);
            if (nextPage != null && pendingCombatEnemy == null && !pendingCardReward)
            {
                ShowPage(nextPage, outcome.resultText, JoinSummary(summary));
            }
            else
            {
                ShowEnding(outcome.resultText, JoinSummary(summary));
            }
        }

        private void ShowEnding(string body, string effectSummary)
        {
            string speaker = CurrentEvent != null ? CurrentEvent.speakerName : null;
            bool fight = pendingCombatEnemy != null;
            var options = new List<EventViewUI.ChoiceOption>
            {
                new EventViewUI.ChoiceOption
                {
                    label = fight ? "เข้าสู่การต่อสู้" : pendingCardReward ? "เลือกของเซ่น" : "จากไป",
                    hint = fight ? pendingCombatEnemy.enemyName : pendingCardReward ? "รับการ์ด 1 ใบ" : null,
                    interactable = true,
                    onSelected = fight ? (Action)FinishWithCombat : pendingCardReward ? (Action)FinishWithCardReward : FinishEvent,
                }
            };
            if (view != null) view.ShowPage(speaker, body, effectSummary, options);
        }

        private void FinishEvent()
        {
            if (OnEventFinished != null)
            {
                OnEventFinished.Invoke();
                return;
            }
            // Scene opened on its own: roll the next event so designers can keep testing.
            Debug.Log("[EventManager] Event finished (no GameFlow listener) - starting another event.");
            BeginRandomEvent(int.MaxValue);
        }

        private void FinishWithCombat()
        {
            var enemy = pendingCombatEnemy;
            pendingCombatEnemy = null;
            if (OnCombatRequested != null)
            {
                OnCombatRequested.Invoke(enemy);
                return;
            }
            Debug.Log($"[EventManager] Combat requested vs {enemy.enemyName} (no GameFlow listener).");
            FinishEvent();
        }

        private void FinishWithCardReward()
        {
            pendingCardReward = false;
            if (OnCardRewardRequested != null)
            {
                OnCardRewardRequested.Invoke();
                return;
            }
            Debug.Log("[EventManager] Card reward requested (no GameFlow listener).");
            FinishEvent();
        }

        private string ApplyEffect(EventEffect effect)
        {
            var run = RunState.Current;
            switch (effect.type)
            {
                case EventEffectType.Heal:
                    run.Heal(effect.amount);
                    return $"+{effect.amount} HP";
                case EventEffectType.TakeDamage:
                    run.TakeDamage(effect.amount);
                    return $"-{effect.amount} HP";
                case EventEffectType.ChangeMaxHp:
                    run.ChangeMaxHp(effect.amount);
                    return $"{(effect.amount >= 0 ? "+" : "")}{effect.amount} HP สูงสุด";
                case EventEffectType.GainIncense:
                    run.AddIncense(effect.amount);
                    return $"+{effect.amount} ธูป";
                case EventEffectType.LoseIncense:
                    run.AddIncense(-effect.amount);
                    return $"-{effect.amount} ธูป";
                case EventEffectType.GainRelic:
                    run.AddRelic(effect.relicId);
                    return $"ได้รับ: {effect.relicId}";
                case EventEffectType.StartCombat:
                    if (effect.enemy != null) pendingCombatEnemy = effect.enemy;
                    else Debug.LogError($"[EventManager] StartCombat effect in '{CurrentEvent.eventId}' has no enemy.");
                    return null;
                case EventEffectType.CardReward:
                    pendingCardReward = true;
                    return "ได้รับการ์ด 1 ใบ";
                default:
                    return null;
            }
        }

        private static EventOutcome RollOutcome(List<EventOutcome> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0) return null;
            int total = 0;
            foreach (var o in outcomes) total += Mathf.Max(0, o.weight);
            if (total <= 0) return outcomes[0];

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var o in outcomes)
            {
                roll -= Mathf.Max(0, o.weight);
                if (roll < 0) return o;
            }
            return outcomes[outcomes.Count - 1];
        }

        private static bool CanChoose(EventChoice choice)
        {
            var run = RunState.Current;
            return run.CanAfford(choice.incenseCost) && run.CurrentHp > choice.hpCost;
        }

        private static string BuildCostText(EventChoice choice)
        {
            var sb = new StringBuilder();
            if (choice.incenseCost > 0) sb.Append($"ธูป {choice.incenseCost}");
            if (choice.hpCost > 0)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"HP {choice.hpCost}");
            }
            return sb.ToString();
        }

        private static string JoinSummary(List<string> lines)
        {
            return lines.Count > 0 ? string.Join("   ", lines) : null;
        }
    }
}
