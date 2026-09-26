using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // หลุมจั่ว: a shared pit at the middle-right of the table, between the two board rows. Each click
    // during the player's turn draws one random card from every card in the game and adds Corruption
    // (see CardManager.DrawFromPit); the enemy uses it too (EnemyCardPlayer). Bootstraps itself.
    public class DrawPitView3D : MonoBehaviour
    {
        public static DrawPitView3D Instance { get; private set; }

        [Header("Placement")]
        [Tooltip("Distance to the right of the rightmost board column.")]
        public float gapRightOfBoard = 2f;
        [Tooltip("Used only when there are no board slots to line up with.")]
        public Vector3 offsetFromDeck = new Vector3(1.3f, 0f, 0f);

        [Header("Look")]
        public Vector3 pitSize = new Vector3(1.3f, 0.05f, 1.3f);
        public Color pitColor = new Color(0.05f, 0.02f, 0.08f);
        public Color hoverColor = new Color(0.35f, 0.1f, 0.45f);

        private Renderer pitRenderer;
        private Vector3 baseScale;

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
            if (FindFirstObjectByType<CardManager>() == null) return;
            if (FindFirstObjectByType<DrawPitView3D>() != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "DrawPit3D";
            var pit = go.AddComponent<DrawPitView3D>();
            if (!pit.TryPlaceMiddleRight())
            {
                var deck = FindFirstObjectByType<DeckPileView3D>();
                if (deck == null)
                {
                    Destroy(go);
                    return;
                }
                go.transform.position = deck.transform.position + pit.offsetFromDeck;
            }
        }

        // Halfway between the player's and the enemy's board rows, right of the rightmost column
        private bool TryPlaceMiddleRight()
        {
            Vector3 playerSum = Vector3.zero, enemySum = Vector3.zero;
            int playerCount = 0, enemyCount = 0;
            float maxX = float.MinValue;

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;

                Vector3 p = slot.transform.position;
                maxX = Mathf.Max(maxX, p.x);
                if (slot.side == BoardSlotView.SlotSide.Player)
                {
                    playerSum += p;
                    playerCount++;
                }
                else
                {
                    enemySum += p;
                    enemyCount++;
                }
            }
            if (playerCount == 0 || enemyCount == 0) return false;

            Vector3 middle = (playerSum / playerCount + enemySum / enemyCount) * 0.5f;
            transform.position = new Vector3(maxX + gapRightOfBoard, middle.y, middle.z);
            return true;
        }

        // Where cards pulled from the pit start flying from
        public Vector3 SpawnPosition => transform.position + Vector3.up * 0.1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            transform.localScale = new Vector3(pitSize.x, pitSize.y, pitSize.z);
            baseScale = transform.localScale;
            pitRenderer = GetComponent<Renderer>();
            if (pitRenderer != null) pitRenderer.material.color = pitColor;
        }

        private void OnMouseEnter()
        {
            if (pitRenderer != null) pitRenderer.material.color = hoverColor;
        }

        private void OnMouseExit()
        {
            if (pitRenderer != null) pitRenderer.material.color = pitColor;
        }

        private void OnMouseDown()
        {
            if (CardTargeting3D.BlocksInput || CardPlayController3D.BlocksInput || CombatCameraRig3D.BlocksInput) return;
            CardManager.Instance?.DrawFromPit();
        }

        // Pulse when either side pulls a card out
        public void PlayUseEffect()
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            transform.DOKill();
            transform.localScale = baseScale;
            transform.DOPunchScale(baseScale * 0.15f, 0.25f, 6);
        }
    }
}
