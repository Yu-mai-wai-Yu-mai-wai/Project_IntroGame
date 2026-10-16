using System.Collections;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // StatusFxTestScene (Tools/TawanOS/Test/Status FX Test Scene): a row of cards, each with one status the cards give,
    // and a last card with all of them, so every status symbol and its effect can be looked at side by side.
    // Buttons under each card turn its statuses on and off. Test only: not in the build's scene list.
    public class StatusFxShowcase : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public CardView3D card;
            public string title;
            public CardStatusType[] statuses;
        }

        public Slot[] slots;

        private static readonly CardStatusType[] Shown = { CardStatusType.Protect, CardStatusType.Taunt, CardStatusType.Blinded, CardStatusType.Phawa };

        // CardView3D binds its Card Data in Start; the statuses go on after that
        private IEnumerator Start()
        {
            yield return null;
            foreach (var s in slots)
            {
                if (s.card == null || s.card.CardData == null) continue;
                s.card.enabled = false; // stay put like a card on the board (no hover)
                foreach (var type in s.statuses) Set(s.card, type, true);
            }
        }

        private static bool Has(CardView3D card, CardStatusType type) =>
            card.CardData.statuses.Exists(x => x != null && x.type == type && x.stacks > 0);

        private static void Set(CardView3D card, CardStatusType type, bool on)
        {
            card.CardData.statuses.RemoveAll(x => x != null && x.type == type);
            if (on) card.CardData.statuses.Add(new CardStatus(type, type == CardStatusType.Phawa ? 2 : 1, 99));
        }

        private static string Name(CardStatusType type)
        {
            switch (type)
            {
                case CardStatusType.Protect: return "เกราะคุ้มภัย";
                case CardStatusType.Taunt: return "ยั่วยุ";
                case CardStatusType.Blinded: return "ผีบังตา";
                case CardStatusType.Phawa: return "ผวา";
                default: return type.ToString();
            }
        }

        private void OnGUI()
        {
            var cam = Camera.main;
            if (cam == null || slots == null) return;
            float scale = Screen.height / 1080f;
            var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * scale) };
            float w = 190f * scale, h = 40f * scale;

            foreach (var s in slots)
            {
                if (s.card == null || s.card.CardData == null) continue;
                var top = cam.WorldToScreenPoint(s.card.transform.position + Vector3.up * (s.card.transform.lossyScale.y * 0.5f + 0.15f));
                var bottom = cam.WorldToScreenPoint(s.card.transform.position - Vector3.up * (s.card.transform.lossyScale.y * 0.5f + 0.1f));
                GUI.Label(new Rect(top.x - w, Screen.height - top.y - h * 1.6f, w * 2f, h * 1.4f), s.title, title);

                float y = Screen.height - bottom.y;
                foreach (var type in Shown)
                {
                    bool on = Has(s.card, type);
                    GUI.backgroundColor = on ? new Color(0.35f, 1f, 0.45f) : new Color(0.55f, 0.55f, 0.55f);
                    bool want = GUI.Toggle(new Rect(bottom.x - w * 0.5f, y, w, h), on, (on ? "เปิด: " : "ปิด: ") + Name(type), button);
                    GUI.backgroundColor = Color.white;
                    if (want != on) Set(s.card, type, want);
                    y += h * 1.15f;
                }
            }
        }
    }
}
