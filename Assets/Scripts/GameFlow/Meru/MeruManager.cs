using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    public enum MeruAction
    {
        Burn,    // เผาการ์ดทิ้ง: the card leaves the run deck
        Upgrade  // ชำระการ์ด: the card becomes its CardDataSO.upgradedCard
    }

    /// <summary>
    /// เมรุ (the map's rest node): the player does one of two things to the run deck, burn a card out of
    /// it or upgrade a card, then leaves. Raises <see cref="OnMeruFinished"/> when the player leaves.
    /// </summary>
    public class MeruManager : MonoBehaviour
    {
        public static MeruManager Instance { get; private set; }

        [Tooltip("Seeds the run deck if the scene is opened before any combat (testing).")]
        public DeckConfigSO starterDeck;
        public MeruViewUI view;

        public event Action OnMeruFinished;

        // One action per visit
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
            Used = false;
            RunState.Current.EnsureDeck(starterDeck);
            if (view != null) view.Show(this);
        }

        public static bool CanUpgrade(CardDataSO card) => card != null && card.upgradedCard != null;

        public bool AnyUpgradable()
        {
            foreach (var card in Deck) if (CanUpgrade(card)) return true;
            return false;
        }

        public bool CanApply(MeruAction action, CardDataSO card)
        {
            if (Used || card == null) return false;
            return action == MeruAction.Burn ? Deck.Count > 1 : CanUpgrade(card); // never burn the last card
        }

        public void Apply(MeruAction action, CardDataSO card)
        {
            if (!CanApply(action, card)) return;

            var run = RunState.Current;
            bool done = action == MeruAction.Burn ? run.RemoveCard(card, countsAsPurchase: false) : run.UpgradeCard(card);
            if (!done) return;

            Used = true;
            if (view != null) view.ShowResult(action, card);
        }

        public void Leave()
        {
            if (OnMeruFinished != null)
            {
                OnMeruFinished.Invoke();
                return;
            }
            // Scene opened on its own: start over so designers can keep testing
            Debug.Log($"[MeruManager] Left the Meru (no GameFlow listener). Run deck: {Deck.Count} cards.");
            Begin();
        }
    }
}
