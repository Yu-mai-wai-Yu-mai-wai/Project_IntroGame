using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.StoryEngine
{
    /// <summary>
    /// Puts the tutorial fight into the intro story: after the dream page (the shadows attacking the house), the
    /// player fights in the dream, then the story goes on with "ขวัญสะดุ้งตื่น...". Re-running it moves the fight
    /// back to that page; any other page's fight is cleared. The team can also set
    /// <see cref="StoryPage.tutorialFightEnemy"/> by hand in the inspector.
    /// Batch: -executeMethod TawanOS.StoryEngine.IntroTutorialFightTool.HookUpTutorialFight
    /// </summary>
    public static class IntroTutorialFightTool
    {
        private const string IntroStoryPath = "Assets/StoryEngineData/Story_Intro.asset";
        private const string EnemyPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset"; // a minor enemy that has a deck
        private const string DreamPageMarker = "เงาดำ";

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
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(EnemyPath);
            if (enemy == null) { error = $"No enemy profile at {EnemyPath}."; return false; }

            int dreamPage = story.pages.FindIndex(p => p.text != null && p.text.Contains(DreamPageMarker));
            if (dreamPage < 0) { error = $"No intro page mentions '{DreamPageMarker}' (the dream)."; return false; }
            if (dreamPage == story.pages.Count - 1) { error = "The dream is the last page: the story would have nothing to go back to."; return false; }

            Undo.RecordObject(story, "Hook Up Intro Tutorial Fight");
            for (int i = 0; i < story.pages.Count; i++) story.pages[i].tutorialFightEnemy = i == dreamPage ? enemy : null;
            EditorUtility.SetDirty(story);
            AssetDatabase.SaveAssets();
            Debug.Log($"[IntroTutorialFightTool] Tutorial fight against {enemy.name} after intro page {dreamPage + 1} of {story.pages.Count}.");
            error = null;
            return true;
        }
    }
}
