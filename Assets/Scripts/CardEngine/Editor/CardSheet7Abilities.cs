using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using T = TawanOS.CardEngine.AbilityTrigger;
using E = TawanOS.CardEngine.AbilityEffect;
using G = TawanOS.CardEngine.AbilityTarget;

namespace TawanOS.CardEngine
{
    // Phase 2 of the sheet 7 port: the abilities of the 42 cards and their upgrades, as the sheet's skill text
    // says (Docs/Design/card_skills_th_en.csv), plus the token cards some of them make. Run by CardSheet7Apply.
    public static class CardSheet7Abilities
    {
        private const string TokenFolder = "Assets/CardEngineData/Cards/Tokens";

        public static bool Apply(Dictionary<string, CardDataSO> cards)
        {
            bool ok = true;
            var puppet = Token(cards, "tok_hunphayon", "s_ba02", "หุ่นพยนต์", 1, 1);
            var puppetUp = Token(cards, "tok_hunphayon_up", "s_ba02", "หุ่นพยนต์", 2, 2);
            var headless = Token(cards, "tok_phihuakhad", "s_bf03", null, -1, -1);
            var kuman = Token(cards, "tok_kumanthong", "s_wf05", null, -1, -1);
            cards.TryGetValue("s_wf05", out var kumanFull);

            // id, base abilities, upgraded abilities
            ok &= Set(cards, "s_ba01", L(P(E.CurseOnAllyEnter, 2)), L(P(E.CurseOnAllyEnter, 3)));
            ok &= Set(cards, "s_ba02", L(P(E.AllyDeathSummon, 0, related: puppet)), L(P(E.AllyDeathSummon, 0, related: puppetUp)));
            ok &= Set(cards, "s_ba03", L(P(E.AllyDeathHurtsEnemy, 1, 2)), L(P(E.AllyDeathHurtsEnemy, 2, 2)));
            ok &= Set(cards, "s_ba04", L(Aura(E.AttackIfSturdy, 2, 4)), L(Aura(E.AttackIfSturdy, 2, 3)));
            ok &= Set(cards, "s_ba05", L(P(E.CurseNoDecay, 0)), L(P(E.CurseNoDecay, 1)));
            ok &= Set(cards, "s_ba06", L(Aura(E.AttackPerCorruption, 1, 3)), L(Aura(E.AttackPerCorruption, 1, 2)));
            ok &= Set(cards, "s_ba07", L(Play(E.LinkLane, 0)), L(Play(E.LinkLane, 1)));

            ok &= Set(cards, "s_bf01", L(P(E.CursedBonusDamage, 1)), L(P(E.CursedBonusDamage, 1, 1)));
            ok &= Set(cards, "s_bf02", L(P(E.Overhead, 1)), L(P(E.Overhead, 1)));
            ok &= Set(cards, "s_bf03", L(A(T.OnDeath, E.DamageCard, G.Opposite, 1)), L(A(T.OnDeath, E.DamageCard, G.Opposite, 2)));
            ok &= Set(cards, "s_bf04", L(Play(E.GiveCardToHand, 1, related: kuman)), L(Play(E.GiveCardToHand, 1, related: kumanFull)));
            ok &= Set(cards, "s_bf05", L(Play(E.SacrificeForAttack, 0)), L(Play(E.SacrificeForAttack, 1)));
            ok &= Set(cards, "s_bf06", L(P(E.OverheadMagnet, 1), P(E.AllyDeathBuff, 1, 0)), L(P(E.OverheadMagnet, 1), P(E.AllyDeathBuff, 1, 1)));
            ok &= Set(cards, "s_bf07", L(Curse(T.OnHit, G.StruckCard, 1)), L(Curse(T.OnHit, G.StruckCard, 2)));

            ok &= Set(cards, "s_bi01", L(Curse(T.OnPlay, G.EnemyCard, 2)), L(Curse(T.OnPlay, G.EnemyCard, 3)));
            ok &= Set(cards, "s_bi02", L(Curse(T.OnPlay, G.AllEnemyFamiliars, 1)), L(Curse(T.OnPlay, G.AllEnemyFamiliars, 2)));
            ok &= Set(cards, "s_bi03", L(Play(E.DamageHealOnKill, 2, 2)), L(Play(E.DamageHealOnKill, 2, 3)));
            ok &= Set(cards, "s_bi04", L(Play(E.SummonRelated, 1, related: headless)), L(Play(E.SummonRelated, 3, related: headless)));
            ok &= Set(cards, "s_bi05", L(Curse(T.OnPlay, G.EnemyCard, 1)), L(Curse(T.OnPlay, G.EnemyCard, 1), Play(E.CursedCannotAttack, 1)));
            ok &= Set(cards, "s_bi06", L(Play(E.SacrificeForHeal, 0)), L(Play(E.SacrificeForHeal, 1)));
            ok &= Set(cards, "s_bi07", L(Play(E.ReviveLast, 2, 2)), L(Play(E.ReviveLast, 3, 2)));

            ok &= Set(cards, "s_wa01", L(Aura(E.BuffAttack, 1, 0, G.AllFriendlyExceptSelf)),
                                       L(Aura(E.BuffAttack, 1, 0, G.AllFriendlyExceptSelf), Aura(E.BuffKhwan, 1, 0, G.AllFriendlyExceptSelf)));
            ok &= Set(cards, "s_wa02", L(P(E.EndRoundHealOrDraw, 1, 0)), L(P(E.EndRoundHealOrDraw, 1, 1)));
            ok &= Set(cards, "s_wa03", L(P(E.HealHurtsEnemy, 3)), L(P(E.HealHurtsEnemy, 5)));
            ok &= Set(cards, "s_wa04", L(P(E.HealBonus, 1)), L(P(E.HealBonus, 2)));
            ok &= Set(cards, "s_wa05", L(P(E.BlockFlying, 1), P(E.Thorns, 1)), L(P(E.BlockFlying, 1), P(E.Thorns, 2)));
            ok &= Set(cards, "s_wa06", L(P(E.CurseImmunity, 1)), L(P(E.CurseImmunity, 1), A(T.OnPlay, E.CleanseAll, G.AllFriendly, 1)));
            ok &= Set(cards, "s_wa07", L(P(E.DeathSave, 2)), L(P(E.DeathSave, 3)));

            ok &= Set(cards, "s_wf01", L(P(E.Taunt, 1), P(E.GrowOnAllyPlayed, 1, 0)), L(P(E.Taunt, 1), P(E.GrowOnAllyPlayed, 1, 1)));
            ok &= Set(cards, "s_wf02", L(Play(E.HealPlayer, 2), A(T.OnPlay, E.HealKhwan, G.AllFriendly, 1)),
                                       L(Play(E.HealPlayer, 3), A(T.OnPlay, E.HealKhwan, G.AllFriendly, 1)));
            ok &= Set(cards, "s_wf03", L(Aura(E.AttackPerAlly, 1, 0, G.Self)), L(Aura(E.AttackPerAlly, 1, 0, G.Self), P(E.Pierce, 1)));
            ok &= Set(cards, "s_wf04", L(P(E.OverheadMagnet, 1), Aura(E.BuffKhwan, 1, 0, G.Adjacent)),
                                       L(P(E.OverheadMagnet, 1), Aura(E.BuffKhwan, 1, 0, G.Adjacent), P(E.AdjacentAttackAura, 1)));
            ok &= Set(cards, "s_wf05", L(A(T.OnDeath, E.DrawCards, G.Self, 1)), L(A(T.OnDeath, E.DrawCheaper, G.Self, 1)));
            ok &= Set(cards, "s_wf06", L(P(E.Overhead, 1)), L(P(E.Overhead, 1)));
            ok &= Set(cards, "s_wf07", L(P(E.AttackOnHeal, 1)), L(P(E.AttackOnHeal, 2)));

            ok &= Set(cards, "s_wi01", L(A(T.OnPlay, E.BuffKhwan, G.AllFriendly, 1), Play(E.HealPerAlly, 1)),
                                       L(A(T.OnPlay, E.BuffKhwan, G.AllFriendly, 1), A(T.OnPlay, E.BuffAttack, G.AllFriendly, 1), Play(E.HealPerAlly, 1)));
            ok &= Set(cards, "s_wi02", L(Play(E.HealPlayer, 2), Play(E.DrawCards, 1)), L(Play(E.HealPlayer, 2), Play(E.HealPlayer, 2), Play(E.DrawCards, 1)));
            ok &= Set(cards, "s_wi03", L(Status(G.EnemyCard, CardStatusType.Phawa, 2, 1)), L(Status(G.EnemyCard, CardStatusType.Phawa, 3, 1))); // redesign sheet, 23 Oct: 2, upgraded 3
            ok &= Set(cards, "s_wi04", L(Status(G.FriendlyCard, CardStatusType.Protect, 1, 0), Status(G.FriendlyCard, CardStatusType.Taunt, 1, 1)),
                                       L(Status(G.FriendlyCard, CardStatusType.Protect, 1, 0), Status(G.FriendlyCard, CardStatusType.Taunt, 1, 1)));
            ok &= Set(cards, "s_wi05", L(Play(E.MoveCurses, 0)), L(Play(E.MoveCurses, 1)));
            ok &= Set(cards, "s_wi06", L(Play(E.DestroyEnemyAmulet, 1)), L(Play(E.DestroyEnemyAmulet, 1), Play(E.HealPlayer, 2)));
            ok &= Set(cards, "s_wi07", L(A(T.OnPlay, E.ReturnToHand, G.EnemyCard, 1)), L(A(T.OnPlay, E.ReturnToHand, G.AnyCard, 1)));
            return ok;
        }

        // ---------------------------------------------------------------- builders

        private static List<CardAbility> L(params CardAbility[] a) => new List<CardAbility>(a);

        private static CardAbility A(T trigger, E effect, G target, int value, int duration = 0) =>
            new CardAbility { trigger = trigger, effect = effect, target = target, value = value, duration = duration };

        private static CardAbility P(E effect, int value, int duration = 0, CardDataSO related = null) =>
            new CardAbility { trigger = T.Passive, effect = effect, target = G.Self, value = value, duration = duration, relatedCard = related };

        private static CardAbility Play(E effect, int value, int duration = 0, CardDataSO related = null) =>
            new CardAbility { trigger = T.OnPlay, effect = effect, target = G.Self, value = value, duration = duration, relatedCard = related };

        private static CardAbility Aura(E effect, int value, int duration, G target = G.Self) =>
            new CardAbility { trigger = T.Aura, effect = effect, target = target, value = value, duration = duration };

        private static CardAbility Curse(T trigger, G target, int stacks) =>
            new CardAbility { trigger = trigger, effect = E.ApplyStatus, target = target, status = CardStatusType.DoneKhong, value = stacks };

        private static CardAbility Status(G target, CardStatusType status, int value, int duration) =>
            new CardAbility { trigger = T.OnPlay, effect = E.ApplyStatus, target = target, status = status, value = value, duration = duration };

        private static bool Set(Dictionary<string, CardDataSO> cards, string id, List<CardAbility> baseAbilities, List<CardAbility> upAbilities)
        {
            if (!cards.TryGetValue(id, out var card))
            {
                Debug.LogError($"[CardSheet7Abilities] No CardDataSO with cardId {id}");
                return false;
            }
            Write(card, baseAbilities);
            var up = card.upgradedCard != null ? card.upgradedCard : (cards.TryGetValue(id + "_up", out var u) ? u : null);
            if (up != null) Write(up, upAbilities);
            return true;
        }

        private static void Write(CardDataSO card, List<CardAbility> abilities)
        {
            Undo.RecordObject(card, "Apply sheet 7 abilities");
            card.abilities = abilities;
            EditorUtility.SetDirty(card);
        }

        // A card a skill makes (not in the catalog, so never a reward or shop card): a copy of `fromId`'s face with no
        // skill, with its own name and stats when given (-1 = keep the original's)
        private static CardDataSO Token(Dictionary<string, CardDataSO> cards, string id, string fromId, string name, int khwan, int attack)
        {
            if (!AssetDatabase.IsValidFolder(TokenFolder)) AssetDatabase.CreateFolder("Assets/CardEngineData/Cards", "Tokens");
            string path = $"{TokenFolder}/Card_{id}.asset";
            var token = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
            if (token == null)
            {
                token = ScriptableObject.CreateInstance<CardDataSO>();
                AssetDatabase.CreateAsset(token, path);
            }
            cards.TryGetValue(fromId, out var from);

            Undo.RecordObject(token, "Sheet 7 token");
            token.cardId = id;
            token.cardType = CardType.Familiar;
            token.magicSchool = from != null ? from.magicSchool : MagicSchool.BlackMagic;
            token.cardNameThai = name ?? (from != null ? from.cardNameThai : id);
            token.cardNameEng = from != null ? from.cardNameEng : id;
            token.familiarHealth = khwan >= 0 ? khwan : (from != null ? from.familiarHealth : 1);
            token.familiarDamage = attack >= 0 ? attack : (from != null ? from.familiarDamage : 1);
            token.meritCost = from != null ? from.meritCost : 0;
            token.corruptionGain = from != null ? from.corruptionGain : 0;
            token.descriptionFormat = "ไม่มีความสามารถ";
            token.abilities = new List<CardAbility>();
            if (from != null)
            {
                token.artwork = from.artwork;
                token.cardImage = null; // the original's finished face would show its skill text
                token.frameBorder = from.frameBorder;
                token.cardBackground = from.cardBackground;
            }
            cards[id] = token;
            EditorUtility.SetDirty(token);
            return token;
        }
    }
}
