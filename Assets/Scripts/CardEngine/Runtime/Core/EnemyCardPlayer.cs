using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Gives an enemy its own deck / hand / discard and lets it play cards with the same rules as the
    // player: Merit refills each turn (firstTurnMerit, +1 per turn), White Magic costs Merit, Black
    // Magic builds Corruption. Active only when the EnemyProfileSO has a non-empty deck.
    public class EnemyCardPlayer
    {
        private const float DelayBeforePlay = 0.6f;
        private const float DelayAfterPlay = 0.4f;
        private const float DrawAnimationBase = 0.45f;
        private const float DrawAnimationStagger = 0.12f;

        private readonly List<CardInstance> drawPile = new List<CardInstance>();
        private readonly List<CardInstance> hand = new List<CardInstance>();
        private readonly List<CardInstance> discardPile = new List<CardInstance>();
        private EnemyProfileSO profile;

        public event System.Action<CardInstance> OnCardDrawn;
        public event System.Action<CardInstance> OnCardPlayed;

        // True while the opening hand is being drawn, so views can fly the cards in all at once
        public bool IsOpeningDraw { get; private set; }

        public bool Active => profile != null && profile.deck != null && profile.deck.Count > 0;
        public IReadOnlyList<CardInstance> Hand => hand;
        public int DrawCount => drawPile.Count;
        public int DiscardCount => discardPile.Count;

        public void Init(EnemyProfileSO enemyProfile)
        {
            profile = enemyProfile;
            drawPile.Clear();
            hand.Clear();
            discardPile.Clear();

            if (!Active) return;

            foreach (var template in profile.deck)
            {
                if (template != null) drawPile.Add(new CardInstance(template));
            }
            Shuffle(drawPile);
        }

        // Draws the whole opening hand in one go. Returns false when the enemy has no deck.
        public bool DrawOpeningHand()
        {
            if (!Active) return false;

            IsOpeningDraw = true;
            Draw(profile.startingHandSize);
            IsOpeningDraw = false;
            return true;
        }

        public IEnumerator PlayTurn(CombatManager combat, int turn, EnemyCardPlayStyle style)
        {
            var state = combat.State;
            var resolver = EffectResolver.Instance;
            if (resolver == null) yield break;

            // Merit was already refilled for this turn by CombatManager.RefillMeritForTurn, same as the player
            int drawn = Draw(profile.drawPerTurn);
            if (drawn > 0)
            {
                // Let the draw fly-in play out before the enemy starts acting
                yield return new WaitForSeconds(DrawAnimationBase + (drawn - 1) * DrawAnimationStagger);
            }

            bool costsEnabled = CardManager.Instance != null && CardManager.Instance.costSystemEnabled;
            int played = 0;

            while (played < profile.maxCardsPerTurn && !combat.IsCombatOver)
            {
                var card = ChooseCard(state, style, costsEnabled);
                if (card == null) break;

                yield return new WaitForSeconds(DelayBeforePlay);
                if (combat.IsCombatOver) break;

                // White Magic spends Merit exactly like the player's (checked again in case it changed)
                if (costsEnabled && card.magicSchool == MagicSchool.WhiteMagic && !combat.SpendEnemyMerit(card.meritCost))
                {
                    break;
                }

                hand.Remove(card);
                OnCardPlayed?.Invoke(card);

                combat.ReportEnemyAction($"ศัตรูใช้ {card.cardNameThai}");
                resolver.ResolveCardEffect(card, null, casterIsPlayer: false);

                if (costsEnabled && card.magicSchool == MagicSchool.BlackMagic && card.corruptionGain > 0)
                {
                    combat.AddEnemyCorruption(card.corruptionGain);
                }

                if (card.cardType == CardType.Incantation) discardPile.Add(card);
                played++;

                yield return new WaitForSeconds(DelayAfterPlay);
            }

            if (played == 0) combat.ReportEnemyAction("ศัตรูไม่มีการ์ดที่ใช้ได้");
        }

        // Returns how many cards were actually drawn
        private int Draw(int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= profile.maxHandSize) break;

                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break;
                    drawPile.AddRange(discardPile);
                    discardPile.Clear();
                    Shuffle(drawPile);
                }

                var card = drawPile[0];
                hand.Add(card);
                drawPile.RemoveAt(0);
                drawn++;
                OnCardDrawn?.Invoke(card);
            }
            return drawn;
        }

        private CardInstance ChooseCard(CombatStateData s, EnemyCardPlayStyle style, bool costsEnabled)
        {
            CardInstance best = null;
            float bestScore = float.MinValue;

            foreach (var card in hand)
            {
                bool affordable = !costsEnabled || card.magicSchool != MagicSchool.WhiteMagic || card.meritCost <= s.enemyMerit;
                if (!affordable) continue;

                // Small bonus for spending more Merit so ties use the turn's budget fully
                float score = Score(card, s, style) + card.meritCost * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = card;
                }
            }
            return best;
        }

        private float Score(CardInstance card, CombatStateData s, EnemyCardPlayStyle style)
        {
            if (style == EnemyCardPlayStyle.Random) return Random.value;

            float score;
            switch (card.cardType)
            {
                case CardType.Incantation:
                    if (card.targetType == TargetType.SingleEnemy || card.targetType == TargetType.AllEnemies)
                    {
                        score = card.baseValue;
                        if (card.baseValue >= s.playerKhwan + s.playerShield) score += 100f; // lethal
                        if (style == EnemyCardPlayStyle.Aggressive) score *= 1.5f;
                        else if (style == EnemyCardPlayStyle.Defensive) score *= 0.7f;
                    }
                    else if (card.targetType == TargetType.Self)
                    {
                        score = card.baseValue;
                        if (style == EnemyCardPlayStyle.Defensive) score *= 1.5f;
                        else if (style == EnemyCardPlayStyle.Aggressive) score *= 0.7f;
                        if (s.maxEnemyKhwan > 0 && (float)s.enemyKhwan / s.maxEnemyKhwan < 0.3f) score *= 1.3f;
                        if (s.enemyShield >= 10) score *= 0.3f; // already well shielded
                    }
                    else
                    {
                        score = 1f;
                    }
                    break;

                case CardType.Familiar:
                    score = 4f + card.familiarDamage;
                    if (style == EnemyCardPlayStyle.Aggressive) score *= 1.2f;
                    break;

                default: // Amulet
                    score = 3f;
                    break;
            }
            return score;
        }

        private static void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
