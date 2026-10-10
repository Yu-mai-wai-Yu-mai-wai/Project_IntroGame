using System;
using System.Collections;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Victory reward screen: shows the incense earned and offers a few cards; the one picked is
    /// added to the run deck (<see cref="RunState"/>). Raises <see cref="OnRewardFinished"/> afterwards.
    /// </summary>
    public class RewardManager : MonoBehaviour
    {
        public static RewardManager Instance { get; private set; }

        public CardRewardConfigSO config;
        public RewardViewUI view;
        [Tooltip("Pause after picking a card so the player sees it join the deck.")]
        [Min(0f)] public float finishDelay = 0.9f;

        public event Action OnRewardFinished;

        public IReadOnlyList<CardDataSO> Choices => choices;

        private readonly List<CardDataSO> choices = new List<CardDataSO>();
        private bool hasBegun;
        private bool resolved;

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
            // GameFlowManager calls BeginReward from sceneLoaded (before Start) after a won fight.
            if (!hasBegun) BeginReward(0, RewardTier.Minor);
        }

        public void BeginReward(int incenseEarned, RewardTier tier)
        {
            hasBegun = true;
            resolved = false;
            if (config == null)
            {
                Debug.LogError("[RewardManager] CardRewardConfigSO is not assigned.");
                return;
            }

            RunState.Current.EnsureDeck(config.starterDeck);
            RollChoices(config.ChoicesFor(tier));
            if (view != null) view.Show(this, incenseEarned, tier);
        }

        private void RollChoices(int count)
        {
            choices.Clear();
            var pool = config.GetPool();
            while (choices.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                choices.Add(pool[i]);
                pool.RemoveAt(i);
            }
        }

        public void Choose(CardDataSO card)
        {
            if (resolved || card == null) return;
            resolved = true;
            RunState.Current.AddCard(card);
            if (view != null) view.ShowPicked(card);
            StartCoroutine(FinishAfterDelay());
        }

        // With allowSkip off a card must be taken; "ไปต่อ" still works when there was no card to offer
        public void Skip()
        {
            bool nothingOffered = choices.Count == 0;
            if (resolved || (!config.allowSkip && !nothingOffered)) return;
            resolved = true;
            if (config.allowSkip && !nothingOffered && config.skipIncense > 0) RunState.Current.AddIncense(config.skipIncense);
            Finish();
        }

        private IEnumerator FinishAfterDelay()
        {
            // The cards not taken burn away first (RewardFxConfigSO), so wait for whichever is longer
            float wait = Mathf.Max(finishDelay, view != null ? view.PickSequenceSeconds : 0f);
            yield return new WaitForSeconds(wait);
            Finish();
        }

        private void Finish()
        {
            if (OnRewardFinished != null)
            {
                OnRewardFinished.Invoke();
                return;
            }
            // Scene opened on its own: roll another reward so designers can keep testing.
            Debug.Log($"[RewardManager] Reward finished (no GameFlow listener). Run deck: {RunState.Current.Deck.Count} cards.");
            BeginReward(0, RewardTier.Minor);
        }
    }
}
