using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.CardEngine
{
    // StatusFxTestScene (Tools/TawanOS/Test/Status FX Test Scene): on Play it loads the real CombatTestScene, so the
    // fight is the game's own (hand, enemy AI, clashes, sounds), and keeps this panel on the right of the screen to set
    // the scene up: give or take each status on any card on the board (through EffectResolver.AddCardStatus, as a
    // card would), draw a familiar to play or put one on the enemy's side, draw the cards that give statuses, refill
    // merit, clear corruption.
    // Test only: StatusFxTestScene is not in the build's scene list.
    public class StatusTestPanel : MonoBehaviour
    {
        public const string CombatSceneName = "CombatTestScene";

        [Tooltip("The cards that give statuses (drawn into the hand by the panel).")]
        public List<CardDataSO> statusCards = new List<CardDataSO>();
        [Tooltip("Familiars the panel can put on the board.")]
        public List<CardDataSO> familiars = new List<CardDataSO>();

        private static readonly CardStatusType[] Statuses =
            { CardStatusType.Protect, CardStatusType.Taunt, CardStatusType.Blinded, CardStatusType.Phawa };

        private bool shown = true;
        private Vector2 scroll;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (SceneManager.GetActiveScene().name != CombatSceneName) SceneManager.LoadScene(CombatSceneName);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) shown = !shown;
        }

        private void OnGUI()
        {
            var combat = CombatManager.Instance;
            var resolver = EffectResolver.Instance;
            if (combat == null || resolver == null || combat.State == null) return;

            float scale = Screen.height / 1080f;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = 420f, screenW = Screen.width / scale, screenH = Screen.height / scale;
            int labelSize = GUI.skin.label.fontSize, buttonSize = GUI.skin.button.fontSize;
            GUI.skin.label.fontSize = 17;
            GUI.skin.button.fontSize = 16;

            if (!shown)
            {
                if (GUI.Button(new Rect(screenW - 150f, 70f, 140f, 34f), "แผงเทส (F2)")) shown = true;
                GUI.skin.label.fontSize = labelSize;
                GUI.skin.button.fontSize = buttonSize;
                GUI.matrix = matrix;
                return;
            }

            var area = new Rect(screenW - width - 10f, 70f, width, screenH - 160f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>แผงเทสสถานะ</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 20 });
            if (GUILayout.Button("ซ่อน (F2)", GUILayout.Width(90f))) shown = false;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("จั่วการ์ดสถานะ")) DrawStatusCards();
            if (GUILayout.Button("เติมกุศล")) combat.AddMerit(99);
            if (GUILayout.Button("ล้างมลทิน")) ClearCorruption(combat);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("จั่วบริวารเข้ามือ")) DrawFamiliar();
            if (GUILayout.Button("ลงบริวาร ฝั่งศัตรู")) PlaceEnemyFamiliar(resolver);
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            CardRows("ฝั่งศัตรู", combat.State.enemyBoardCards, resolver);
            CardRows("ฝั่งเรา", combat.State.activeBoardCards, resolver);
            GUILayout.Label("คลิกสถานะเพื่อใส่ / ถอด  (เกราะ, ผวา: คลิกซ้ำ = เพิ่มชั้น, x = ถอด)",
                new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true });
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.label.fontSize = labelSize;
            GUI.skin.button.fontSize = buttonSize;
            GUI.matrix = matrix;
        }

        // One block per card on that side: its name and Khwan, then a toggle per status
        private void CardRows(string side, List<CardInstance> cards, EffectResolver resolver)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"<b>{side}</b>", new GUIStyle(GUI.skin.label) { richText = true });
            if (cards.Count == 0) GUILayout.Label("  (ไม่มีการ์ด)");
            foreach (var card in cards.ToArray())
            {
                if (card == null || card.IsDead) continue;
                GUILayout.Label($"ช่อง {card.boardSlot + 1}: {card.cardNameThai}  (ขวัญ {card.familiarHealth}/{card.maxKhwan})");
                GUILayout.BeginHorizontal();
                foreach (var type in Statuses)
                {
                    var status = card.statuses.Find(x => x != null && x.type == type && x.stacks > 0);
                    bool on = status != null;
                    GUI.backgroundColor = on ? new Color(0.35f, 1f, 0.45f) : new Color(0.6f, 0.6f, 0.6f);
                    string label = ShortName(type) + (status != null && status.stacks > 1 ? $" ×{status.stacks}" : "");
                    bool stacks = type == CardStatusType.Phawa || type == CardStatusType.Protect;
                    if (GUILayout.Button(label))
                    {
                        // Off: put it on. On: ผวา / เกราะคุ้มภัย get one more stack, the others come off.
                        if (on && !stacks) Remove(card, type, resolver);
                        else resolver.AddCardStatus(card, type, 1, type == CardStatusType.Protect ? 99 : 0);
                    }
                    GUI.backgroundColor = Color.white;
                    if (on && stacks && GUILayout.Button("x", GUILayout.Width(24f))) Remove(card, type, resolver);
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void Remove(CardInstance card, CardStatusType type, EffectResolver resolver)
        {
            card.statuses.RemoveAll(x => x != null && x.type == type);
            resolver.RefreshAuras(); // ยั่วยุ and the attack shown (ผวา) follow at once
        }

        private void DrawStatusCards()
        {
            var cards = CardManager.Instance;
            if (cards == null) return;
            foreach (var so in statusCards) if (so != null) cards.AddToHand(new CardInstance(so));
        }

        // The player's familiar goes to the hand, to be played as usual (the board shows the card played from the hand)
        private void DrawFamiliar()
        {
            if (familiars.Count == 0 || CardManager.Instance == null) return;
            CardManager.Instance.AddToHand(new CardInstance(familiars[Random.Range(0, familiars.Count)]));
        }

        // The enemy's lands straight on its board, the way the enemy plays one (its board view brings the card in)
        private void PlaceEnemyFamiliar(EffectResolver resolver)
        {
            if (familiars.Count == 0) return;
            resolver.ResolveCardEffect(new CardInstance(familiars[Random.Range(0, familiars.Count)]), null, false);
        }

        private static void ClearCorruption(CombatManager combat)
        {
            combat.State.currentCorruption = 0;
            combat.AddCorruption(0); // tells the gauges
        }

        private static string ShortName(CardStatusType type)
        {
            switch (type)
            {
                case CardStatusType.Protect: return "เกราะ";
                case CardStatusType.Taunt: return "ยั่วยุ";
                case CardStatusType.Blinded: return "บังตา";
                case CardStatusType.Phawa: return "ผวา";
                default: return type.ToString();
            }
        }
    }
}
