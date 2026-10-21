using UnityEngine;
using TawanOS.UI;

namespace TawanOS.CardEngine
{
    // Keeps a card's status symbols (CardView3D) up to date. Statuses come and go at round end, from auras and from flips
    // without a damage event, so they are checked every frame; a card on the enemy's board has its CardView3D
    // switched off, so this check lives in a component of its own. Added by CardView3D when it builds its face.
    // Pointing at a status icon shows what the status does (KeywordTooltipView), with its stacks and how long it lasts.
    public class CardStatusTags : MonoBehaviour
    {
        private CardView3D view;

        // The card whose icon the tooltip is showing for (one tooltip for the whole table)
        private static CardStatusTags tooltipOwner;
        private CardStatus shownStatus;

        private void Awake()
        {
            view = GetComponent<CardView3D>();
        }

        private void OnDisable()
        {
            if (tooltipOwner == this) HideTooltip();
        }

        private void LateUpdate()
        {
            if (view == null) return;
            view.RefreshStatusTags();

            var status = view.CardData != null && view.CardData.statuses.Count > 0 ? view.StatusUnderPointer() : null;
            if (status != null)
            {
                var tip = KeywordTooltipView.Instance;
                if (tip == null) return;
                if (tooltipOwner != this || shownStatus != status)
                {
                    tip.Show(Title(status), Describe(status), Input.mousePosition);
                    tooltipOwner = this;
                    shownStatus = status;
                }
                else tip.UpdatePosition(Input.mousePosition);
            }
            else if (tooltipOwner == this) HideTooltip();
        }

        private void HideTooltip()
        {
            KeywordTooltipView.Instance?.Hide();
            tooltipOwner = null;
            shownStatus = null;
        }

        private static string Title(CardStatus s)
        {
            string name = Name(s.type);
            return s.stacks > 1 ? $"{name} ×{s.stacks}" : name;
        }

        // The glossary's definition, then how long this one lasts. The glossary writes the length as "X เทิร์น";
        // that part goes, since the line under it gives this status's own turns left.
        private static string Describe(CardStatus s)
        {
            var glossary = KeywordGlossarySO.Current;
            string def = glossary != null ? glossary.GetDefinition(Name(s.type)) : null;
            if (!string.IsNullOrEmpty(def)) def = def.Replace(" เป็นเวลา X เทิร์น", "").Replace("X เทิร์น", "").Trim();
            string lasts = s.fromAura ? "มาจากออร่าของการ์ดบนสนาม อยู่จนกว่าการ์ดนั้นจะออกจากสนาม"
                : s.duration >= 99 ? "อยู่จนกว่าจะถูกใช้"
                : $"เหลืออีก {s.duration} เทิร์น";
            return string.IsNullOrEmpty(def) ? lasts : def + "\n" + lasts;
        }

        private static string Name(CardStatusType type)
        {
            switch (type)
            {
                case CardStatusType.Phawa: return "ผวา";
                case CardStatusType.Blinded: return "ผีบังตา";
                case CardStatusType.Protect: return "เกราะคุ้มภัย";
                case CardStatusType.Taunt: return "คุ้มกัน";
                case CardStatusType.DoneKhong: return "สาป";
                case CardStatusType.PhiAm: return "ผีอำ";
                default: return type.ToString();
            }
        }
    }
}
