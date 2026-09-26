using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Shows the enemy's Amulet / Familiar cards as physical 3D cards on the EnemySlot_* board slots,
    // mirroring CombatManager.State.enemyBoardCards. Reuses the card prefab of the player's hand and
    // bootstraps itself in scenes that have enemy BoardSlotViews.
    public class EnemyBoardView3D : MonoBehaviour
    {
        [Tooltip("Auto-found from the scene's HandLayoutController3D when empty.")]
        public CardView3D cardPrefab;
        public float placeDuration = 0.4f;
        public float spawnHeight = 1.5f;

        // Board cards (Amulet / Familiar) the enemy just played from its hand. EnemyHandView3D parks the
        // physical card here and this view flies it from the hand straight into its slot.
        internal static readonly Dictionary<CardInstance, CardView3D> PendingFromHand = new Dictionary<CardInstance, CardView3D>();

        private readonly Dictionary<int, BoardSlotView> slots = new Dictionary<int, BoardSlotView>();
        private readonly Dictionary<int, CardView3D> views = new Dictionary<int, CardView3D>();
        private Transform spawnOrigin;
        private EffectResolver resolver;

        // AfterSceneLoad fires only for the first scene played; the combat scene is usually loaded later from the map.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; // no double hook without domain reload
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Bootstrap();

        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<EnemyBoardView3D>() != null) return;

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (IsEnemyBoardSlot(slot))
                {
                    new GameObject("EnemyBoardView3D").AddComponent<EnemyBoardView3D>();
                    return;
                }
            }
        }

        internal static bool IsEnemyBoardSlot(BoardSlotView slot)
        {
            return slot.side == BoardSlotView.SlotSide.Enemy
                && slot.GetComponent<DeckPileView3D>() == null
                && !slot.name.Contains("Deck");
        }

        private void Start()
        {
            if (cardPrefab == null)
            {
                var hand = FindFirstObjectByType<HandLayoutController3D>();
                if (hand != null) cardPrefab = hand.cardPrefab;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning("[EnemyBoardView3D] No card prefab found; enemy board cards will not be shown.");
                return;
            }

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (IsEnemyBoardSlot(slot)) slots[slot.slotIndex] = slot;
                else if (slot.side == BoardSlotView.SlotSide.Enemy) spawnOrigin = slot.transform; // enemy deck slot
            }

            resolver = EffectResolver.Instance;
            if (resolver == null) return;
            resolver.OnEnemySlotOccupied += HandleOccupied;
            resolver.OnEnemySlotCleared += HandleCleared;
        }

        private void OnDestroy()
        {
            if (resolver == null) return;
            resolver.OnEnemySlotOccupied -= HandleOccupied;
            resolver.OnEnemySlotCleared -= HandleCleared;
        }

        private void HandleOccupied(CardInstance card, int index)
        {
            if (!slots.TryGetValue(index, out var slot)) return;

            // The board is resynced as a whole; only touch slots whose card actually changed
            if (views.TryGetValue(index, out var existing) && existing != null && existing.CardData == card) return;

            RemoveView(index);

            CardView3D view = TakeExistingView(card);
            if (view == null)
            {
                view = Instantiate(cardPrefab, slot.transform);
                view.Bind(card);
                view.enabled = false; // no hover/drag on the enemy's cards

                Vector3 start = (spawnOrigin != null ? spawnOrigin.position : slot.transform.position) + Vector3.up * spawnHeight;
                view.transform.position = start;
                view.transform.localRotation = Quaternion.identity;
            }

            // Fly the card into the slot, then snap exactly onto its centre
            var t = view.transform;
            t.SetParent(slot.transform, worldPositionStays: true);
            t.DOKill();
            t.DOLocalMove(Vector3.zero, placeDuration).SetEase(Ease.OutQuad)
                .OnComplete(() => t.localPosition = Vector3.zero);
            t.DOLocalRotateQuaternion(Quaternion.identity, placeDuration)
                .OnComplete(() => t.localRotation = Quaternion.identity);
            t.DOScale(cardPrefab.transform.localScale, placeDuration);

            views[index] = view;
        }

        // Reuses a card that is already physically on the table: the one just played from the enemy's
        // hand, or one that shifted slots because the board was full and the oldest card was replaced.
        private CardView3D TakeExistingView(CardInstance card)
        {
            if (PendingFromHand.TryGetValue(card, out var fromHand))
            {
                PendingFromHand.Remove(card);
                if (fromHand != null) return fromHand;
            }

            foreach (var pair in views)
            {
                if (pair.Value != null && pair.Value.CardData == card)
                {
                    views.Remove(pair.Key);
                    return pair.Value;
                }
            }
            return null;
        }

        private void HandleCleared(int index)
        {
            RemoveView(index);
        }

        private void RemoveView(int index)
        {
            if (!views.TryGetValue(index, out var view)) return;
            views.Remove(index);
            if (view == null) return;

            view.transform.DOKill();
            Destroy(view.gameObject);
        }
    }
}
