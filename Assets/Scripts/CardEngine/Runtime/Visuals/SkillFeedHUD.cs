using System.Collections.Generic;
using DG.Tweening;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// The skill feed (Game Designer, 24 Oct): whenever a card's skill does something, a slim strip slides in at the
    /// right edge of the screen, half way up: the card's picture, its name and what the skill just did ("ฮีล +2",
    /// "สาป 1 → กระสือ"). A gold edge is the player's card, a red edge the enemy's. A strip stays about three seconds and
    /// fades; at most three show, the newest at the bottom; the same card doing the same thing again counts up (×2).
    /// The card on the board gives a short pulse so the strip and the card read together. Pointing at a strip shows
    /// the card's whole skill text. Hidden while a hand card is raised or held, or the enemy's card is on show. Listens to EffectResolver.OnSkillShown;
    /// created by itself in a fight.
    /// </summary>
    public class SkillFeedHUD : MonoBehaviour
    {
        private const int MaxShown = 3;
        private const float ShowSeconds = 3f;
        private const float StripWidth = 540f, StripHeight = 104f, Gap = 10f;
        private static readonly Color PlayerEdge = new Color(0.95f, 0.75f, 0.35f);
        private static readonly Color EnemyEdge = new Color(0.85f, 0.2f, 0.18f);

        private class Strip
        {
            public RectTransform root;
            public CanvasGroup group;
            public TextMeshProUGUI name, result;
            public CardInstance card;
            public string text, fullText;
            public int count = 1;
            public float until;
            public bool hovered;
        }

        private readonly List<Strip> strips = new List<Strip>();
        private RectTransform column;
        private CanvasGroup columnGroup;
        private EffectResolver resolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (EffectResolver.Instance == null || FindFirstObjectByType<SkillFeedHUD>() != null) return;
            new GameObject("SkillFeedHUD").AddComponent<SkillFeedHUD>();
        }

        private void Start()
        {
            resolver = EffectResolver.Instance;
            if (resolver == null) { Destroy(gameObject); return; }
            resolver.OnSkillShown += OnSkillShown;
            Build();
        }

        private void OnDestroy()
        {
            if (resolver != null) resolver.OnSkillShown -= OnSkillShown;
        }

        private void Build()
        {
            var canvas = UiFactory.CreateOverlayCanvas("SkillFeedCanvas", 60, transform);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            column = UiFactory.CreateRect("Column", canvas.transform);
            column.anchorMin = column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(1f, 1f);
            column.sizeDelta = new Vector2(StripWidth, (StripHeight + Gap) * MaxShown);
            column.anchoredPosition = new Vector2(0f, -16f); // top-right corner, out of the board's way
            columnGroup = column.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnSkillShown(CardInstance card, bool ownerIsPlayer, CardAbility ability, string result,
            IReadOnlyList<CardInstance> targets)
        {
            if (column == null) return;
            string text = result ?? Words(ability);
            if (string.IsNullOrEmpty(text)) return;
            text += OnWhom(card, targets);

            // The same card doing the same thing again, while its strip is still up: count it
            var last = strips.Count > 0 ? strips[strips.Count - 1] : null;
            if (last != null && last.card == card && last.text == text && Time.time < last.until)
            {
                last.count++;
                last.name.text = $"{card.cardNameThai}  <size=75%><color=#B8AFA0>×{last.count}</color></size>";
                last.until = Time.time + ShowSeconds;
                last.root.DOKill(true);
                last.root.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6).SetLink(last.root.gameObject);
                Pulse(card);
                return;
            }

            if (strips.Count >= MaxShown) Remove(strips[0], instant: true);
            var strip = MakeStrip(card, ownerIsPlayer, text);
            strips.Add(strip);
            Layout();
            Pulse(card);
        }

        private Strip MakeStrip(CardInstance card, bool ownerIsPlayer, string text)
        {
            var theme = UIThemeSO.Current;
            var s = new Strip { card = card, text = text, until = Time.time + ShowSeconds };
            try { s.fullText = string.Format(card.descriptionFormat ?? "", card.baseValue); }
            catch (System.FormatException) { s.fullText = card.descriptionFormat; }
            if (string.IsNullOrEmpty(s.fullText)) s.fullText = text;

            var bg = UiFactory.CreateImage("Strip", column, new Color(0.04f, 0.03f, 0.03f, 0.88f));
            bg.raycastTarget = true; // pointing at it shows the whole skill
            s.root = bg.rectTransform;
            s.root.anchorMin = s.root.anchorMax = new Vector2(1f, 1f);
            s.root.pivot = new Vector2(1f, 1f);
            s.root.sizeDelta = new Vector2(StripWidth, StripHeight);
            s.group = bg.gameObject.AddComponent<CanvasGroup>();
            s.group.blocksRaycasts = true;

            var edge = UiFactory.CreateImage("Edge", s.root, ownerIsPlayer ? PlayerEdge : EnemyEdge);
            var ert = edge.rectTransform;
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = new Vector2(0f, 1f);
            ert.sizeDelta = new Vector2(7f, 0f);
            ert.anchoredPosition = new Vector2(3.5f, 0f);

            var art = UiFactory.CreateImage("Art", s.root, Color.white);
            art.sprite = card.source != null ? (card.source.artwork != null ? card.source.artwork : card.source.cardImage) : null;
            art.preserveAspect = true;
            if (art.sprite == null) art.color = ownerIsPlayer ? PlayerEdge * 0.5f : EnemyEdge * 0.5f;
            var art_rt = art.rectTransform;
            art_rt.anchorMin = art_rt.anchorMax = new Vector2(0f, 0.5f);
            art_rt.pivot = new Vector2(0f, 0.5f);
            art_rt.sizeDelta = new Vector2(StripHeight - 12f, StripHeight - 12f);
            art_rt.anchoredPosition = new Vector2(14f, 0f);

            float textLeft = 14f + StripHeight - 12f + 10f;
            s.name = UiFactory.CreateText(s.root, "Name", card.cardNameThai, 32f, ownerIsPlayer ? PlayerEdge : new Color(1f, 0.55f, 0.5f),
                TextAlignmentOptions.BottomLeft, theme != null ? theme.titleFont : null);
            Place(s.name.rectTransform, textLeft, 0.5f, 1f);
            s.result = UiFactory.CreateText(s.root, "Result", text, 28f, new Color(0.93f, 0.9f, 0.84f),
                TextAlignmentOptions.TopLeft, theme != null ? theme.bodyFont : null);
            s.result.textWrappingMode = TextWrappingModes.NoWrap;
            s.result.overflowMode = TextOverflowModes.Ellipsis;
            Place(s.result.rectTransform, textLeft, 0f, 0.5f);

            var trigger = bg.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => SetHover(s, true));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetHover(s, false));

            // Slides in from the right edge
            s.root.anchoredPosition = new Vector2(StripWidth + 20f, 0f);
            s.group.alpha = 0f;
            s.group.DOFade(1f, 0.2f).SetLink(bg.gameObject);
            return s;
        }

        private static void Place(RectTransform rt, float left, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(left, 4f);
            rt.offsetMax = new Vector2(-12f, -8f);
        }

        // Pointing at a strip: it stays, and shows the whole skill text
        private void SetHover(Strip s, bool on)
        {
            s.hovered = on;
            s.result.textWrappingMode = on ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            s.result.text = on ? s.fullText : s.text;
            s.root.sizeDelta = new Vector2(StripWidth, on ? Mathf.Max(StripHeight, 56f + s.result.GetPreferredValues(s.fullText, StripWidth - 130f, 0f).y) : StripHeight);
            Layout();
        }

        // Newest at the top, older ones pushed down; each strip slides to its place
        private void Layout()
        {
            float y = 0f;
            for (int i = strips.Count - 1; i >= 0; i--)
            {
                var s = strips[i];
                s.root.DOKill();
                s.root.DOAnchorPos(new Vector2(0f, y), 0.25f).SetEase(Ease.OutCubic).SetLink(s.root.gameObject);
                y -= s.root.sizeDelta.y + Gap;
            }
        }

        private void Remove(Strip s, bool instant)
        {
            strips.Remove(s);
            if (instant || s.root == null)
            {
                if (s.root != null) Destroy(s.root.gameObject);
                return;
            }
            // Fades where it is (Game Designer, 25 Oct): the other strips stay put rather than sliding up into its place;
            // the gap closes when the next strip comes in at the top
            s.root.DOKill();
            s.group.DOFade(0f, 0.7f).SetEase(Ease.InQuad).SetLink(s.root.gameObject).OnComplete(() => Destroy(s.root.gameObject));
        }

        private void Update()
        {
            if (columnGroup == null) return;
            // Out of the way of a raised or held card, like the other HUD
            var hovered = CardView3D.HoveredInHand;
            // ... and of the enemy's card on show, which stands at the same side
            bool away = (hovered != null && hovered.IsHovered)
                        || (CardPlayController3D.Instance != null && CardPlayController3D.Instance.IsHoldingAny)
                        || Time.time < EnemyHandView3D.ShowcaseUntil;
            columnGroup.alpha = Mathf.MoveTowards(columnGroup.alpha, away ? 0f : 1f, Time.unscaledDeltaTime * 6f);

            for (int i = strips.Count - 1; i >= 0; i--)
            {
                var s = strips[i];
                if (s.hovered) { s.until = Time.time + 1f; continue; }
                // Out of the way (an enemy card on show plays its skills right then): the strip waits to be seen
                if (away) { s.until += Time.deltaTime; continue; }
                if (Time.time >= s.until) Remove(s, instant: false);
            }
        }

        // The card on the board gives a short pulse (a card in the hand is left alone: it has its own hover)
        private static void Pulse(CardInstance card)
        {
            foreach (var view in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
            {
                if (view.CardData != card || view.IsInHand) continue;
                view.transform.DOPunchScale(view.transform.localScale * 0.12f, 0.35f, 5, 0.6f).SetLink(view.gameObject);
                return;
            }
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // Which card a skill was used on (Game Designer, 24 Oct): " → name", or how many when it hit several; nothing
        // when it was only the card itself
        private static string OnWhom(CardInstance card, IReadOnlyList<CardInstance> targets)
        {
            if (targets == null || targets.Count == 0) return "";
            if (targets.Count == 1) return targets[0] == card ? "" : $" → {targets[0].cardNameThai}";
            return $" → {targets.Count} ใบ";
        }

        // What a triggered ability does, in a few words
        private static string Words(CardAbility a)
        {
            if (a == null) return null;
            int v = a.value;
            string related = a.relatedCard != null ? a.relatedCard.cardNameThai : "การ์ด";
            switch (a.effect)
            {
                case AbilityEffect.HealKhwan:
                case AbilityEffect.HealPlayer: return $"ฟื้นฟูขวัญ +{v}";
                case AbilityEffect.HealPerAlly: return $"ฟื้นฟูขวัญ +{v} ต่อบริวาร 1 ตัว";
                case AbilityEffect.BuffKhwan: return $"ขวัญ +{v}";
                case AbilityEffect.BuffAttack: return v >= 0 ? $"ตี +{v}" : $"ตี {v}";
                case AbilityEffect.DamageCard:
                case AbilityEffect.DamageHealOnKill: return $"ทำดาเมจ {v}";
                case AbilityEffect.ApplyStatus: return $"{CardStatusTags.Name(a.status)} {v}";
                case AbilityEffect.CleanseAll: return "ล้างสถานะร้าย";
                case AbilityEffect.DrawCards: return $"จั่ว {v} ใบ";
                case AbilityEffect.DrawCheaper: return $"จั่ว {v} ใบ ลดราคา 1";
                case AbilityEffect.GiveCardToHand: return $"ได้ {related} ขึ้นมือ";
                case AbilityEffect.SacrificeForAttack: return "สังเวย รับพลังตีของบริวาร";
                case AbilityEffect.SacrificeForHeal: return "สังเวย ฟื้นฟูขวัญเท่ากับขวัญบริวาร";
                case AbilityEffect.ReviveLast: return "คืนชีพบริวารที่ตายล่าสุด";
                case AbilityEffect.ReturnToHand: return "ส่งบริวารกลับขึ้นมือ";
                case AbilityEffect.MoveCurses: return "ย้ายสาปไปฝั่งศัตรู";
                case AbilityEffect.DestroyEnemyAmulet: return "ทำลายเครื่องรางศัตรู";
                case AbilityEffect.SummonRelated: return $"เรียก {related} ลงสนาม";
                case AbilityEffect.CursedCannotAttack: return "บริวารที่ติดสาปตีไม่ได้เทิร์นนี้";
                case AbilityEffect.LinkLane: return "ผูกเลน";
                default: return null; // auras and keywords: nothing to show when played
            }
        }
    }
}
