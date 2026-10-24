using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// เมรุ (the map's rest node): the player upgrades one card of the run deck (it becomes its
    /// CardDataSO.upgradedCard), then leaves (Game Designer, 24 Oct: upgrade only, no burning).
    /// Raises <see cref="OnMeruFinished"/> when the player leaves.
    /// </summary>
    public class MeruManager : MonoBehaviour
    {
        public static MeruManager Instance { get; private set; }

        [Tooltip("Seeds the run deck if the scene is opened before any combat (testing).")]
        public DeckConfigSO starterDeck;
        public MeruViewUI view;

        public event Action OnMeruFinished;

        // One upgrade per visit
        public bool Used { get; private set; }

        public IReadOnlyList<CardDataSO> Deck => RunState.Current.Deck;

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
            Begin();
        }

        private void Begin()
        {
            Used = RunState.Current.MeruUsed;
            RunState.Current.EnsureDeck(starterDeck);
            if (view != null) view.Show(this);
        }

        public static bool CanUpgrade(CardDataSO card) => card != null && card.upgradedCard != null;

        public bool AnyUpgradable()
        {
            foreach (var card in Deck) if (CanUpgrade(card)) return true;
            return false;
        }

        public bool CanApply(CardDataSO card) => !Used && CanUpgrade(card);

        public void Apply(CardDataSO card)
        {
            if (!CanApply(card)) return;
            if (!RunState.Current.UpgradeCard(card)) return;

            Used = true;
            RunState.Current.SetMeruUsed(true);
            if (view != null) view.ShowResult(card);
        }

        public void Leave()
        {
            RunState.Current.ClearMeruUsed();
            RunState.Current.ClearResume();
            if (OnMeruFinished != null)
            {
                OnMeruFinished.Invoke();
                return;
            }
            // Scene opened on its own: start over so designers can keep testing
            Debug.Log($"[MeruManager] Left the Meru (no GameFlow listener). Run deck: {Deck.Count} cards.");
            if (Application.isPlaying) UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
