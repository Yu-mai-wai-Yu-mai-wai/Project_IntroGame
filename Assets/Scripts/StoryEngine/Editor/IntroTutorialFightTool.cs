using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.StoryEngine
{
    /// <summary>
    /// Puts the tutorial fight into the intro story: after the dream page (the shadows attacking the house), the
    /// player fights in the dream, then the story goes on with "ขวัญสะดุ้งตื่น...".
    /// - The enemy is a scripted bot (TutorialShadowProfile, built here from PraiGhostProfile when missing): it plays
    ///   one weak familiar on turn 1, a bigger one on turn 2, another small one on turn 3, then nothing.
    /// - The player's opening hand is fixed so turn 1 can play two familiars and an incantation:
    ///   s_wf05 (1 Merit), s_bf07 (1 Corruption, shows Corruption) and s_wi01 (1 Merit, buffs a familiar on the board).
    /// Re-running it moves the fight back to the dream page; any other page's fight is cleared. An existing
    /// TutorialShadowProfile is kept as the team left it. Everything can also be edited by hand in the inspector.
    /// Batch: -executeMethod TawanOS.StoryEngine.IntroTutorialFightTool.HookUpTutorialFight
    /// </summary>
    public static class IntroTutorialFightTool
    {
        private const string IntroStoryPath = "Assets/StoryEngineData/Story_Intro.asset";
        private const string BaseEnemyPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";
        private const string TutorialEnemyPath = "Assets/CardEngineData/Enemies/TutorialShadowProfile.asset";
        private const string CardFolder = "Assets/CardEngineData/Cards/Sheet/";
        private const string DreamPageMarker = "เงาดำ";

        // Turn 1 Merit is 2: one 1-Merit familiar + one 1-Merit incantation, and a Black Magic familiar on Corruption
        private static readonly string[] PlayerOpeningHand = { "s_wf05", "s_bf07", "s_wi01" };

        // The bot: one card drawn and played per turn, in this order
        private static readonly string[] BotDeck = { "s_wf05", "s_wf01", "s_wf05" };
        private const int BotKhwan = 10; // tutorial only: a few clashes end the dream

        [MenuItem("Tools/TawanOS/Story/Hook Up Intro Tutorial Fight")]
        public static void HookUpTutorialFight()
        {
            if (!TryHookUp(out string error))
            {
                Debug.LogError("[IntroTutorialFightTool] " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static bool TryHookUp(out string error)
        {
            var story = AssetDatabase.LoadAssetAtPath<StoryDataSO>(IntroStoryPath);
            if (story == null) { error = $"No story at {IntroStoryPath}."; return false; }

            var enemy = CreateOrGetBot(out error);
            if (enemy == null) return false;
            var hand = LoadCards(PlayerOpeningHand, out error);
            if (hand == null) return false;

            int dreamPage = story.pages.FindIndex(p => p.text != null && p.text.Contains(DreamPageMarker));
            if (dreamPage < 0) { error = $"No intro page mentions '{DreamPageMarker}' (the dream)."; return false; }
            if (dreamPage == story.pages.Count - 1) { error = "The dream is the last page: the story would have nothing to go back to."; return false; }

            Undo.RecordObject(story, "Hook Up Intro Tutorial Fight");
            for (int i = 0; i < story.pages.Count; i++)
            {
                bool fight = i == dreamPage;
                story.pages[i].tutorialFightEnemy = fight ? enemy : null;
                story.pages[i].tutorialOpeningHand = fight ? new List<CardDataSO>(hand) : new List<CardDataSO>();
            }
            EditorUtility.SetDirty(story);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IntroTutorialFightTool] Tutorial fight against {enemy.name} after intro page {dreamPage + 1} of {story.pages.Count}, " +
                      $"opening hand {string.Join(", ", PlayerOpeningHand)}.");
            error = null;
            return true;
        }

        private static EnemyProfileSO CreateOrGetBot(out string error)
        {
            error = null;
            var existing = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(TutorialEnemyPath);
            if (existing != null) return existing;

            // A copy of a normal minor enemy keeps its portrait and AI states; only the deck and Khwan change
            var baseEnemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BaseEnemyPath);
            if (baseEnemy == null) { error = $"No enemy profile at {BaseEnemyPath}."; return null; }
            var deck = LoadCards(BotDeck, out error);
            if (deck == null) return null;

            var bot = Object.Instantiate(baseEnemy);
            bot.enemyId = "enemy_tutorial_shadow";
            bot.enemyName = "เงาดำในฝัน";
            bot.maxKhwan = BotKhwan;
            bot.deck = deck;
            bot.scriptedDeck = true;
            bot.cardsPerTurn = new List<int>();
            foreach (var _ in BotDeck) bot.cardsPerTurn.Add(1);
            bot.isBoss = false;
            bot.phase2Profile = null;
            AssetDatabase.CreateAsset(bot, TutorialEnemyPath);
            return bot;
        }

        private static List<CardDataSO> LoadCards(string[] ids, out string error)
        {
            var cards = new List<CardDataSO>();
            foreach (var id in ids)
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>($"{CardFolder}Card_{id}.asset");
                if (card == null) { error = $"No card {id} at {CardFolder}Card_{id}.asset."; return null; }
                cards.Add(card);
            }
            error = null;
            return cards;
        }
    }
}
