using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Creates the enemy deck presets and Assets/Resources/EnemyDeckProgression.asset (EnemyDeckProgressionSO).
    /// Starting point, to be tuned by hand: the presets start from ผีพราย's deck (every enemy shares it), and get
    /// harder by swapping cards for their upgraded versions (CardDataSO.upgradedCard):
    ///   1 from node 0: the deck as it is; 2 from node 3: every other card upgraded; 3 from node 6: all upgraded.
    /// Re-running only creates what is missing, so presets and steps edited by hand are kept. Batch:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.GameFlow.EnemyDeckPresetSetupTool.CreatePresets
    /// </summary>
    public static class EnemyDeckPresetSetupTool
    {
        private const string PresetFolder = "Assets/CardEngineData/Decks/EnemyPresets";
        private const string ProgressionPath = "Assets/Resources/" + EnemyDeckProgressionSO.ResourceName + ".asset";
        private const string BaseDeckSource = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";

        [MenuItem("Tools/TawanOS/Game Flow/Create Enemy Deck Presets")]
        public static void CreatePresetsFromMenu()
        {
            CreatePresets();
        }

        // Entry point for -executeMethod. Exits with code 1 when the base deck cannot be found.
        public static void CreatePresets()
        {
            bool ok = TryCreate();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool TryCreate()
        {
            var source = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BaseDeckSource);
            if (source == null || source.deck == null || source.deck.Count == 0)
            {
                Debug.LogError($"[EnemyDeckPresetSetupTool] No deck to start from at {BaseDeckSource}");
                return false;
            }
            var baseCards = source.deck.FindAll(c => c != null);

            if (!AssetDatabase.IsValidFolder(PresetFolder)) AssetDatabase.CreateFolder("Assets/CardEngineData/Decks", "EnemyPresets");

            var early = LoadOrCreatePreset("EnemyDeck_1_Early", "สำรับผี ช่วงต้น",
                "จาก node 0: สำรับเดิมของผี", baseCards);
            var mid = LoadOrCreatePreset("EnemyDeck_2_Mid", "สำรับผี ช่วงกลาง",
                "จาก node 3: การ์ดเว้นใบอัพเกรดแล้ว", Upgraded(baseCards, everyOther: true));
            var late = LoadOrCreatePreset("EnemyDeck_3_Late", "สำรับผี ช่วงท้าย",
                "จาก node 6 และบอส: การ์ดทุกใบที่อัพเกรดได้อัพเกรดแล้ว", Upgraded(baseCards, everyOther: false));

            var progression = AssetDatabase.LoadAssetAtPath<EnemyDeckProgressionSO>(ProgressionPath);
            if (progression == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                progression = ScriptableObject.CreateInstance<EnemyDeckProgressionSO>();
                progression.minor = Steps((0, early), (3, mid), (6, late));
                progression.boss = Steps((0, late));
                AssetDatabase.CreateAsset(progression, ProgressionPath);
                Debug.Log($"[EnemyDeckPresetSetupTool] Created {ProgressionPath}");
            }
            else
            {
                Debug.Log($"[EnemyDeckPresetSetupTool] {ProgressionPath} already exists, left as it is");
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        private static EnemyDeckPresetSO LoadOrCreatePreset(string fileName, string presetName, string notes, List<CardDataSO> cards)
        {
            string path = $"{PresetFolder}/{fileName}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<EnemyDeckPresetSO>(path);
            if (preset != null) return preset;

            preset = ScriptableObject.CreateInstance<EnemyDeckPresetSO>();
            preset.presetName = presetName;
            preset.notes = notes;
            preset.cards = cards;
            AssetDatabase.CreateAsset(preset, path);
            Debug.Log($"[EnemyDeckPresetSetupTool] Created {path} ({cards.Count} cards)");
            return preset;
        }

        // The cards with their upgraded versions in place: all of them, or every other upgradable one
        private static List<CardDataSO> Upgraded(List<CardDataSO> cards, bool everyOther)
        {
            var result = new List<CardDataSO>();
            int upgradable = 0;
            foreach (var card in cards)
            {
                bool swap = card.upgradedCard != null && (!everyOther || upgradable++ % 2 == 0);
                result.Add(swap ? card.upgradedCard : card);
            }
            return result;
        }

        private static List<EnemyDeckProgressionSO.Step> Steps(params (int from, EnemyDeckPresetSO preset)[] steps)
        {
            var list = new List<EnemyDeckProgressionSO.Step>();
            foreach (var (from, preset) in steps)
                list.Add(new EnemyDeckProgressionSO.Step { fromNodesPassed = from, preset = preset });
            return list;
        }
    }
}
