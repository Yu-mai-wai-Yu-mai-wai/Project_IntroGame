using System.Collections;
using TawanOS.CardEngine;
using TawanOS.ShopEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Fills the สมุดการ์ด (<see cref="CardCollection"/>). A card counts as seen when:
    /// - it is in the run's deck (the starter deck, a reward, a shop buy, an upgrade),
    /// - it comes into the player's hand in combat (draw, the pit, a card ability),
    /// - the enemy plays it,
    /// - it is offered on the card reward screen or for sale in the shop.
    /// Hooks into the existing events only. Bootstraps itself before the first scene, like GameFlowManager.
    /// </summary>
    public class CardCollectionTracker : MonoBehaviour
    {
        private static CardCollectionTracker instance;

        private CardManager hookedCards;
        private EnemyCardPlayer hookedEnemy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CardCollectionTracker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CardCollectionTracker>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RunState.Current.OnChanged += RecordDeck;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            RunState.Current.OnChanged -= RecordDeck;
            Unhook();
        }

        private void RecordDeck()
        {
            CardCollection.Current.AddAll(RunState.Current.Deck);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Unhook();
            RecordDeck();

            if (CardManager.Instance != null)
            {
                hookedCards = CardManager.Instance;
                hookedCards.OnCardDrawn += RecordInstance;
            }
            if (CombatManager.Instance != null)
            {
                hookedEnemy = CombatManager.Instance.EnemyCards;
                hookedEnemy.OnCardPlayed += RecordInstance;
            }

            // The reward choices and the shop stock are rolled by sceneLoaded / Start: read them a frame later
            if (RewardManager.Instance != null || ShopManager.Instance != null) StartCoroutine(RecordOffersNextFrame());
        }

        private IEnumerator RecordOffersNextFrame()
        {
            yield return null;
            var collection = CardCollection.Current;
            if (RewardManager.Instance != null) collection.AddAll(RewardManager.Instance.Choices);
            if (ShopManager.Instance != null)
                foreach (var offer in ShopManager.Instance.CardOffers)
                    if (offer != null) collection.Add(offer.card);
        }

        private void RecordInstance(CardInstance card)
        {
            if (card != null && card.source != null) CardCollection.Current.Add(card.source);
        }

        private void Unhook()
        {
            if (hookedCards != null) hookedCards.OnCardDrawn -= RecordInstance;
            if (hookedEnemy != null) hookedEnemy.OnCardPlayed -= RecordInstance;
            hookedCards = null;
            hookedEnemy = null;
        }
    }
}
