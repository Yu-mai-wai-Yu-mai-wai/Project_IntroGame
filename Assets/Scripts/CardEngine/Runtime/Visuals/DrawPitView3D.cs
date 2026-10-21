using UnityEngine;
using TawanOS.VFX;

namespace TawanOS.CardEngine
{
    // หลุมจั่ว: the smoking pit at the middle-right of the table, between the two board rows. Only scenery now:
    // the center pit's cards lie in their own slots (CenterPitView3D). Bootstraps itself.
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

        private Renderer pitRenderer;
        private DrawPitSmoke smoke; // black smoke over the pit when Resources/DrawPit/DarkSmoke.mat exists

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
            // The scene's DrawPitSmoke object marks where the pit goes; without one it is worked out from the board
            var sceneSmoke = FindFirstObjectByType<DrawPitSmoke>();
            if (sceneSmoke != null) go.transform.position = sceneSmoke.transform.position;
            else if (!pit.TryPlaceMiddleRight())
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

        private bool TryPlaceMiddleRight()
        {
            if (!TryFindMiddleRight(gapRightOfBoard, out var position)) return false;
            transform.position = position;
            return true;
        }

        // Halfway between the player's and the enemy's board rows, gap to the right of the rightmost column
        // (also used by the graveyard, GraveyardView3D)
        public static bool TryFindMiddleRight(float gap, out Vector3 position)
        {
            position = Vector3.zero;
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
            position = new Vector3(maxX + gap, middle.y, middle.z);
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
            // Smoke covers the pit; the cylinder is not drawn. The scene's
            // DrawPitSmoke object (Tools > TawanOS > VFX > Use Scene Draw Pit Smoke) is used when there is one
            // (the pit was placed on it in Bootstrap) and sets the pit's width; otherwise the prefab is placed
            smoke = FindFirstObjectByType<DrawPitSmoke>();
            Vector3 size = pitSize;
            if (smoke != null)
            {
                size.x = smoke.pitSize.x;
                size.z = smoke.pitSize.y;
                smoke.Follow(transform);
            }
            else smoke = DrawPitSmoke.Attach(transform);

            transform.localScale = size; // a unit cylinder: x and z are the pit's width and depth
            pitRenderer = GetComponent<Renderer>();
            if (pitRenderer != null) pitRenderer.material.color = pitColor;
            if (smoke != null && pitRenderer != null) pitRenderer.enabled = false;
        }
    }
}
