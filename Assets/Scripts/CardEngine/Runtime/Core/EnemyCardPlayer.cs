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

        // Cards played so far this turn (maxCardsPerTurn counts over every play phase of the turn)
        private int playedThisTurn;

        // หลุมจั่ว: the enemy weighs pulling from the shared pit against the Corruption it costs
        // (see PitDrawScore). It never pulls when that would set off its own curse backfire.
        private const int MaxPitDrawsPerTurn = 3;
        private const float PitDrawDelay = 0.6f;
        private const float PitDrawScoreNeeded = 2.5f;
        private int pitDrawsThisTurn;

        public IEnumerator PlayTurn(CombatManager combat, int turn, EnemyCardPlayStyle style)
        {
            yield return DrawForTurn();
            yield return PlayCards(combat, style, null);
        }

        // Start of the enemy's turn: its draw. Merit was already refilled by CombatManager.RefillMeritForTurn.
        public IEnumerator DrawForTurn()
        {
            playedThisTurn = 0;
            pitDrawsThisTurn = 0;
            if (!Active) yield break;

            int drawn = Draw(profile.drawPerTurn);
            if (drawn > 0)
            {
                // Let the draw fly-in play out before the enemy starts acting
                yield return new WaitForSeconds(DrawAnimationBase + (drawn - 1) * DrawAnimationStagger);
            }
        }

        // Plays cards one by one while it has something worth playing. filter = which cards this play
        // phase allows (familiars/amulets, or incantations); null = any card.
        public IEnumerator PlayCards(CombatManager combat, EnemyCardPlayStyle style, System.Predicate<CardInstance> filter)
        {
            var state = combat.State;
            var resolver = EffectResolver.Instance;
            if (resolver == null || !Active) yield break;

            bool costsEnabled = CardManager.Instance != null && CardManager.Instance.costSystemEnabled;
            int played = 0;

            // A thin or losing hand may be worth topping up from the pit before playing
            if (PitDrawScore(state, style, costsEnabled, nothingToPlay: false) >= PitDrawScoreNeeded && TryDrawFromPit(combat))
            {
                yield return new WaitForSeconds(PitDrawDelay);
            }

            while (playedThisTurn < profile.maxCardsPerTurn && !combat.IsCombatOver)
            {
                var card = ChooseCard(state, style, costsEnabled, filter);
                if (card == null)
                {
                    // Nothing to play in this phase: the pit is worth more now, then look again
                    if (PitDrawScore(state, style, costsEnabled, nothingToPlay: true) < PitDrawScoreNeeded) break;
                    if (!TryDrawFromPit(combat)) break;
                    yield return new WaitForSeconds(PitDrawDelay);
                    continue;
                }

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
                playedThisTurn++;

                yield return new WaitForSeconds(DelayAfterPlay);
            }

            if (played == 0 && filter == null) combat.ReportEnemyAction("ศัตรูไม่มีการ์ดที่ใช้ได้");
        }

        // How much a pit pull is worth right now. Higher when the enemy has nothing to play, a small hand,
        // unspent Merit or is losing the board / Khwan race; lower the closer the pull takes it to its
        // curse backfire (keeping room for the Black Magic already in hand) and for every pull this turn.
        private float PitDrawScore(CombatStateData s, EnemyCardPlayStyle style, bool costsEnabled, bool nothingToPlay)
        {
            if (hand.Count >= profile.maxHandSize || pitDrawsThisTurn >= MaxPitDrawsPerTurn) return float.MinValue;

            int corruptionAfter = s.enemyCorruption + CardManager.PitCorruptionGain;
            if (corruptionAfter >= s.enemyCorruptionThreshold) return float.MinValue;

            float score = 0f;
            if (nothingToPlay) score += 3f;
            if (hand.Count < 4) score += (4 - hand.Count) * 0.75f;
            if (costsEnabled) score += Mathf.Min(s.enemyMerit, 3) * 0.5f; // Merit that would otherwise go unused

            // Behind on the board or in Khwan: dig for answers
            int ownBoard = s.enemyBoardCards.FindAll(c => c != null && !c.IsDead).Count;
            int foeBoard = s.activeBoardCards.FindAll(c => c != null && !c.IsDead).Count;
            score += Mathf.Max(0, foeBoard - ownBoard) * 1f;
            float ownRatio = s.maxEnemyKhwan > 0 ? (float)s.enemyKhwan / s.maxEnemyKhwan : 1f;
            float foeRatio = s.maxPlayerKhwan > 0 ? (float)s.playerKhwan / s.maxPlayerKhwan : 1f;
            if (ownRatio < foeRatio) score += 1.5f;

            // Corruption risk: keep room for the costliest Black Magic card still in hand
            int reserve = 0;
            if (costsEnabled)
            {
                foreach (var c in hand)
                {
                    if (c.magicSchool == MagicSchool.BlackMagic) reserve = Mathf.Max(reserve, c.corruptionGain);
                }
            }
            int headroom = s.enemyCorruptionThreshold - corruptionAfter - reserve;
            score -= headroom > 0 ? 3f / headroom : 2f + (-headroom);

            score -= pitDrawsThisTurn * 1.5f;

            if (style == EnemyCardPlayStyle.Aggressive) score += 0.5f;
            else if (style == EnemyCardPlayStyle.Defensive) score -= 0.5f;
            else if (style == EnemyCardPlayStyle.Random) score = Random.Range(0f, 5f);

            Debug.Log($"[EnemyAI] pit score {score:0.0} (hand {hand.Count}, corruption {s.enemyCorruption}/{s.enemyCorruptionThreshold}, nothing to play: {nothingToPlay})");
            return score;
        }

        private bool TryDrawFromPit(CombatManager combat)
        {
            var state = combat.State;
            if (pitDrawsThisTurn >= MaxPitDrawsPerTurn || hand.Count >= profile.maxHandSize) return false;
            if (state.enemyCorruption + CardManager.PitCorruptionGain >= state.enemyCorruptionThreshold) return false;

            var template = CardManager.PickPitCard();
            if (template == null) return false;

            pitDrawsThisTurn++;
            var card = new CardInstance(template) { fromPit = true };
            hand.Add(card);
            OnCardDrawn?.Invoke(card);
            DrawPitView3D.Instance?.PlayUseEffect();

            combat.ReportEnemyAction("ศัตรูจั่วการ์ดจากหลุมจั่ว");
            combat.AddEnemyCorruption(CardManager.PitCorruptionGain);
            return true;
        }

        // Extra draw from a card ability (outside the normal draw step)
        public int DrawExtra(int count)
        {
            return Active ? Draw(count) : 0;
        }

        public void AddToHand(CardInstance card)
        {
            if (!Active || card == null) return;

            if (hand.Count >= profile.maxHandSize)
            {
                discardPile.Add(card);
                return;
            }
            hand.Add(card);
            OnCardDrawn?.Invoke(card);
        }

        public void SendToGraveyard(CardInstance card)
        {
            if (!Active || card == null) return;
            discardPile.Add(card.source != null ? new CardInstance(card.source) : card);
        }

        public bool ReturnRandomFromDiscard()
        {
            if (!Active || discardPile.Count == 0) return false;

            int index = Random.Range(0, discardPile.Count);
            var card = discardPile[index];
            discardPile.RemoveAt(index);
            AddToHand(card);
            return true;
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

        private CardInstance ChooseCard(CombatStateData s, EnemyCardPlayStyle style, bool costsEnabled,
            System.Predicate<CardInstance> filter = null)
        {
            CardInstance best = null;
            float bestScore = float.MinValue;

            foreach (var card in hand)
            {
                if (filter != null && !filter(card)) continue;
                if (EffectResolver.Instance != null && !EffectResolver.Instance.CanResolve(card, casterIsPlayer: false)) continue;

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

            // Cards built from abilities are valued by what their abilities do on the current board
            if (card.abilities != null && card.abilities.Count > 0) return ScoreAbilities(card, s, style);

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

        // Values an ability card for the enemy. Offense (hurting/hindering the player's board), defense
        // (protecting its own board) and utility are summed, then weighted by the play style.
        private float ScoreAbilities(CardInstance card, CombatStateData s, EnemyCardPlayStyle style)
        {
            var own = s.enemyBoardCards;
            var foe = s.activeBoardCards;
            var ownCards = own.FindAll(c => c != null && !c.IsDead && (c.maxKhwan > 0 || c.cardType == CardType.Familiar));
            var foeCards = foe.FindAll(c => c != null && !c.IsDead && (c.maxKhwan > 0 || c.cardType == CardType.Familiar));
            var foeFamiliars = foe.FindAll(c => EffectResolver.IsAliveFamiliar(c));

            float offense = 0f, defense = 0f, utility = 0f;

            foreach (var a in card.abilities)
            {
                switch (a.trigger)
                {
                    case AbilityTrigger.Passive:
                        ScorePassive(a, card, s, foeFamiliars, ref offense, ref defense);
                        continue;
                    case AbilityTrigger.OnDeath:
                    case AbilityTrigger.OnKill:
                        utility += 1.5f; // a rider that only matters later
                        continue;
                    case AbilityTrigger.OnTurnStart:
                    case AbilityTrigger.OnRoundEnd:
                        utility += 3f;   // pays off every turn it stays on the board
                        continue;
                }

                bool allTargets = a.target == AbilityTarget.AllFriendly || a.target == AbilityTarget.AllFriendlyExceptSelf
                    || a.target == AbilityTarget.AllEnemy || a.target == AbilityTarget.AllEnemyFamiliars;
                int friendlyCount = ownCards.Count;
                int foeCount = a.target == AbilityTarget.AllEnemyFamiliars ? foeFamiliars.Count : foeCards.Count;

                switch (a.effect)
                {
                    case AbilityEffect.BuffKhwan:
                        if (a.value >= 0) defense += 1.5f * a.value * (allTargets ? friendlyCount : Mathf.Min(1, friendlyCount));
                        else offense += 2f * -a.value * (allTargets ? foeCount : Mathf.Min(1, foeCount)); // ผ้าประเจียดดำ
                        break;

                    case AbilityEffect.HealKhwan:
                        int missing = 0;
                        foreach (var c in ownCards) missing = Mathf.Max(missing, c.maxKhwan - c.familiarHealth);
                        defense += Mathf.Min(a.value, missing) * 1.5f + 0.3f;
                        break;

                    case AbilityEffect.GrantArmor:
                        defense += ownCards.Count > 0 ? a.value * 1.2f : 0f;
                        break;

                    case AbilityEffect.BuffAttack:
                        if (a.value >= 0) offense += 3f * a.value * (allTargets ? friendlyCount : Mathf.Min(1, friendlyCount));
                        else offense += 3f * -a.value * (allTargets ? foeCount : Mathf.Min(1, foeCount));
                        break;

                    case AbilityEffect.DamageCard:
                        offense += ScoreDamage(a, foeCards, foeFamiliars, allTargets);
                        break;

                    case AbilityEffect.DestroyBelowKhwan:
                        float best = 0f;
                        foreach (var c in foeCards) if (c.familiarHealth < a.value) best = Mathf.Max(best, 6f + c.familiarDamage);
                        offense += best;
                        break;

                    case AbilityEffect.ApplyStatus:
                        if (CardStatus.IsDebuff(a.status))
                        {
                            float top = 0f;
                            foreach (var c in foeCards) top = Mathf.Max(top, 3f + c.familiarDamage * 1.5f);
                            offense += top * (a.status == CardStatusType.DoneKhong ? 1.4f : 1f);
                        }
                        else
                        {
                            defense += ownCards.Count > 0 ? 3f : 0f; // taunt / blessing
                        }
                        break;

                    case AbilityEffect.CleanseLatest:
                    case AbilityEffect.CleanseAll:
                        int debuffs = 0;
                        foreach (var c in ownCards) debuffs += c.statuses.FindAll(st => CardStatus.IsDebuff(st.type)).Count;
                        defense += 2f + debuffs * 3f;
                        break;

                    case AbilityEffect.Purify:
                        defense += 2f;
                        break;

                    case AbilityEffect.FlipOmens:
                        defense += 4f;
                        break;

                    case AbilityEffect.DrawCards:
                        utility += 4f * a.value;
                        break;

                    case AbilityEffect.GainMerit:
                        utility += 3f * a.value;
                        break;

                    case AbilityEffect.SummonRandomFamiliar:
                    case AbilityEffect.ReturnFromGraveyard:
                    case AbilityEffect.GiveCardToHand:
                        utility += 5f;
                        break;

                    case AbilityEffect.ThreadFormation:
                        int copies = own.FindAll(c => c != null && c.cardId == card.cardId).Count;
                        utility += copies >= 1 ? 6f : 2f; // this card would be the second copy
                        break;

                    case AbilityEffect.DetonateWithOpposite:
                        int slot = EffectResolver.FreeSlot(own);
                        var facing = EffectResolver.CardAt(foe, slot);
                        utility += facing != null && !facing.IsDead ? 8f : 1f;
                        break;
                }
            }

            // Board cards are also worth their body
            if (card.cardType == CardType.Familiar)
            {
                if (card.familiarHealth <= 0) return 0.1f; // would die at once
                utility += 4f + card.familiarDamage + card.familiarHealth * 0.4f;
                if (style == EnemyCardPlayStyle.Aggressive) offense += card.familiarDamage;
            }
            else if (card.cardType == CardType.Amulet)
            {
                utility += 2f;
            }

            float aggressive = style == EnemyCardPlayStyle.Aggressive ? 1.4f : style == EnemyCardPlayStyle.Defensive ? 0.7f : 1f;
            float defensive = style == EnemyCardPlayStyle.Defensive ? 1.4f : style == EnemyCardPlayStyle.Aggressive ? 0.7f : 1f;
            float score = offense * aggressive + defense * defensive + utility;

            // Black Magic that would tip Corruption over the threshold triggers a backfire on the enemy
            if (card.magicSchool == MagicSchool.BlackMagic && card.corruptionGain > 0
                && s.enemyCorruption + card.corruptionGain >= s.enemyCorruptionThreshold)
            {
                score *= style == EnemyCardPlayStyle.Aggressive ? 0.8f : 0.5f;
            }
            return score;
        }

        private static float ScoreDamage(CardAbility a, List<CardInstance> foeCards, List<CardInstance> foeFamiliars, bool allTargets)
        {
            if (allTargets)
            {
                float total = 0f;
                foreach (var c in foeFamiliars) total += a.value * 2f + (c.familiarHealth <= a.value ? 5f : 0f);
                return total;
            }

            float best = 0f;
            foreach (var c in foeCards)
            {
                float value = a.value * 2f + (c.familiarHealth <= a.value ? 5f + c.familiarDamage : 0f);
                best = Mathf.Max(best, value);
            }
            return best;
        }

        private static void ScorePassive(CardAbility a, CardInstance card, CombatStateData s, List<CardInstance> foeFamiliars,
            ref float offense, ref float defense)
        {
            switch (a.effect)
            {
                case AbilityEffect.Taunt:
                    defense += 5f;
                    break;
                case AbilityEffect.Overhead:
                    offense += 2f + card.familiarDamage * 0.5f;
                    break;
                case AbilityEffect.OverheadMagnet:
                    defense += foeFamiliars.Exists(c => EffectResolver.HasKeyword(c, AbilityEffect.Overhead)) ? 4f : 1f;
                    break;
                case AbilityEffect.MultiStrike:
                    offense += 1.5f * Mathf.Min(a.value, Mathf.Max(1, foeFamiliars.Count));
                    break;
                case AbilityEffect.AdjacentAttackAura:
                    defense += 2f * a.value * Mathf.Min(2, s.enemyBoardCards.Count);
                    break;
            }
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
