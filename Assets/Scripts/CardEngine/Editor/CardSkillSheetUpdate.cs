using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Brings the cards whose skills changed in the team's card sheet (Google Sheet, Oct 2026) in line with it:
    // ตุ๊กตาคุณไสย, ผีตายโหง, คาถาสาปแช่ง, คาถาคุณไสย, คาถาเรียกผี, คาถากันภัย. The other 36 cards already match.
    // อัปมงคล = ผีบังตา / ผวา, สิริมงคล = ยั่วยุ / เกราะคุ้มภัย (CardStatus).
    // Idempotent. Batch: -executeMethod TawanOS.CardEngine.CardSkillSheetUpdate.ApplyFromCli
    public static class CardSkillSheetUpdate
    {
        [MenuItem("Tools/TawanOS/Card Engine/Apply Card Sheet Skills (Oct 2026)")]
        public static void ApplyFromMenu() => Apply();

        public static void ApplyFromCli()
        {
            if (!Apply()) EditorApplication.Exit(1);
        }

        public static bool Apply()
        {
            var cards = new Dictionary<string, CardDataSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && !string.IsNullOrEmpty(card.cardId)) cards[card.cardId] = card;
            }

            bool ok = true;
            ok &= Set(cards, "s_ba01", c =>
            {
                c.familiarHealth = 1;
                c.descriptionFormat = "มอบสถานะผีบังตาให้บริวารฝั่งตรงข้าม";
                c.abilities = new List<CardAbility>
                {
                    Status(AbilityTrigger.Aura, AbilityTarget.AllEnemyFamiliars, CardStatusType.Blinded, 1, 0),
                };
            });
            ok &= Set(cards, "s_bf01", c =>
            {
                c.descriptionFormat = "เมื่อตี จะมอบสถานะผวาให้ศัตรูที่ถูกตี 1 หน่วย";
                c.abilities = new List<CardAbility>
                {
                    Status(AbilityTrigger.OnHit, AbilityTarget.StruckCard, CardStatusType.Phawa, 1, 2),
                };
            });
            ok &= Set(cards, "s_bi01", c =>
            {
                c.descriptionFormat = "มอบสถานะผวาให้บริวาร 1 ใบ";
                c.abilities = new List<CardAbility>
                {
                    Status(AbilityTrigger.OnPlay, AbilityTarget.EnemyCard, CardStatusType.Phawa, 1, 2),
                };
            });
            ok &= Set(cards, "s_bi02", c =>
            {
                c.corruptionGain = 3;
                c.descriptionFormat = "ทำดาเมจ 1 หน่วย ใส่บริวารหรือเครื่องราง ทุกใบบนสนาม";
                c.abilities = new List<CardAbility>
                {
                    new CardAbility { trigger = AbilityTrigger.OnPlay, effect = AbilityEffect.DamageCard, target = AbilityTarget.AllOnBoard, value = 1 },
                };
            });
            ok &= Set(cards, "s_bi04", c =>
            {
                c.corruptionGain = 4;
                c.descriptionFormat = "สร้างบริวารมนต์ดำแบบสุ่มบนช่องที่ว่างอยู่ บริวารนั้นไม่สามารถโจมตีได้ในเทิร์นนี้";
                c.abilities = new List<CardAbility>
                {
                    new CardAbility { trigger = AbilityTrigger.OnPlay, effect = AbilityEffect.SummonToBoard, target = AbilityTarget.Self, value = 1 },
                };
            });
            ok &= Set(cards, "s_wi04", c =>
            {
                c.descriptionFormat = "สร้างเกราะคุ้มภัยให้การ์ดฝั่งเรา 1 ใบ";
                c.abilities = new List<CardAbility>
                {
                    Status(AbilityTrigger.OnPlay, AbilityTarget.FriendlyCard, CardStatusType.Protect, 1, 0),
                };
            });

            AssetDatabase.SaveAssets();
            Debug.Log(ok ? "<color=green>[CardSkillSheetUpdate] 6 cards updated from the card sheet.</color>"
                         : "[CardSkillSheetUpdate] Some cards were not found (see errors above).");
            return ok;
        }

        private static CardAbility Status(AbilityTrigger trigger, AbilityTarget target, CardStatusType status, int value, int duration)
        {
            return new CardAbility { trigger = trigger, effect = AbilityEffect.ApplyStatus, target = target, status = status, value = value, duration = duration };
        }

        private static bool Set(Dictionary<string, CardDataSO> cards, string id, System.Action<CardDataSO> apply)
        {
            if (!cards.TryGetValue(id, out var card))
            {
                Debug.LogError($"[CardSkillSheetUpdate] No CardDataSO with cardId {id}");
                return false;
            }
            Undo.RecordObject(card, "Apply card sheet skills");
            apply(card);
            EditorUtility.SetDirty(card);
            return true;
        }
    }
}
