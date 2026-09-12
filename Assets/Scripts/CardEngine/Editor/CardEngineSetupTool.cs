#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace TawanOS.CardEngine
{
    [InitializeOnLoad]
    public class CardEngineSetupTool
    {
        static CardEngineSetupTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Directory.Exists("Assets/TextMesh Pro"))
                {
                    ImportTMPEssentialsSilently();
                }

                if (!File.Exists("Assets/Fonts/Charm-Bold SDF.asset"))
                {
                    SetupThaiFonts();
                }

                if (!File.Exists("Assets/Scenes/CombatTestScene.unity") && !Application.isPlaying)
                {
                    Debug.Log("[CardEngineSetupTool] Auto-initializing CombatTestScene and Starter Data...");
                    SetupTestSceneAndCards(false);
                }
            };
        }

        [MenuItem("Tools/TawanOS/Card Engine/Import TMP Essential Resources Silently")]
        public static void ImportTMPEssentialsSilently()
        {
            string pkgPath = "Library/PackageCache/com.unity.ugui@b95364aab964/Package Resources/TMP Essential Resources.unitypackage";
            if (File.Exists(pkgPath))
            {
                AssetDatabase.ImportPackage(pkgPath, false);
                AssetDatabase.Refresh();
                Debug.Log("<color=green>[CardEngineSetupTool] TMP Essential Resources successfully imported silently!</color>");
            }
        }

        [MenuItem("Tools/TawanOS/Card Engine/Setup Thai Fonts & Fallbacks")]
        public static void SetupThaiFonts()
        {
            Font charmFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Charm-Bold.ttf");
            Font sarabunFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Sarabun-Regular.ttf");

            if (charmFont == null || sarabunFont == null)
            {
                Debug.LogError("[CardEngineSetupTool] TTF Fonts not found in Assets/Fonts!");
                return;
            }

            TMP_FontAsset charmSdf = GetOrCreateTMPFontAsset(charmFont, "Assets/Fonts/Charm-Bold SDF.asset");
            TMP_FontAsset sarabunSdf = GetOrCreateTMPFontAsset(sarabunFont, "Assets/Fonts/Sarabun-Regular SDF.asset");

            if (charmSdf == null || sarabunSdf == null)
            {
                Debug.LogError("[CardEngineSetupTool] Failed to create TMP Font Assets!");
                return;
            }

            // 1. Ingest into TMP Settings Fallback list
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty fallbacks = so.FindProperty("m_fallbackFontAssets");
                if (fallbacks != null)
                {
                    AddFallbackIfMissing(fallbacks, sarabunSdf);
                    AddFallbackIfMissing(fallbacks, charmSdf);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                }
            }

            // 2. Ingest into default LiberationSans SDF Fallback table
            TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (defaultFont != null)
            {
                if (defaultFont.fallbackFontAssetTable == null) defaultFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!defaultFont.fallbackFontAssetTable.Contains(sarabunSdf)) defaultFont.fallbackFontAssetTable.Add(sarabunSdf);
                if (!defaultFont.fallbackFontAssetTable.Contains(charmSdf)) defaultFont.fallbackFontAssetTable.Add(charmSdf);
                EditorUtility.SetDirty(defaultFont);
            }

            // 3. Update CardViewPrefab directly
            string prefabPath = "Assets/CardEngineData/Prefabs/CardViewPrefab.prefab";
            GameObject prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabGo != null)
            {
                CardView view = prefabGo.GetComponent<CardView>();
                if (view != null)
                {
                    if (view.nameThaiText != null)
                    {
                        view.nameThaiText.font = charmSdf;
                        view.nameThaiText.fontSize = 15;
                    }
                    if (view.descText != null)
                    {
                        view.descText.font = sarabunSdf;
                        view.descText.fontSize = 11.5f;
                    }
                    EditorUtility.SetDirty(prefabGo);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[CardEngineSetupTool] Thai Fonts (Charm & Sarabun) successfully created and assigned!</color>");
        }

        private static void AddFallbackIfMissing(SerializedProperty listProp, TMP_FontAsset fontAsset)
        {
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                    return;
            }
            int index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);
            listProp.GetArrayElementAtIndex(index).objectReferenceValue = fontAsset;
        }

        private static TMP_FontAsset GetOrCreateTMPFontAsset(Font font, string path)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            UnityEngine.TextCore.LowLevel.FontEngine.InitializeFontEngine();
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            if (fontAsset == null) return null;

            string fileName = Path.GetFileNameWithoutExtension(path);
            fontAsset.name = fileName;
            AssetDatabase.CreateAsset(fontAsset, path);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fileName + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = fileName + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        [MenuItem("Tools/TawanOS/Card Engine/Setup Test Scene & Cards")]
        [MenuItem("Window/TawanOS Card Engine Setup")]
        public static void MenuSetup()
        {
            SetupTestSceneAndCards(true);
        }

        public static void SetupTestSceneAndCards(bool interactive = false)
        {
            if (Application.isPlaying)
            {
                if (interactive) EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            string baseFolder = "Assets/CardEngineData";
            EnsureFolder("Assets", "CardEngineData");
            EnsureFolder(baseFolder, "Cards");
            EnsureFolder(baseFolder, "Enemies");
            EnsureFolder(baseFolder, "Decks");
            EnsureFolder(baseFolder, "StatusEffects");

            // 1. Create Starter Cards
            var c1 = CreateOrGetCard(baseFolder + "/Cards/Card_ExorcistKnife.asset", "c_knife", "มีดหมอปราบมาร", "Exorcist Knife", MagicSchool.WhiteMagic, CardType.Incantation, 1, 0, 8, 0, 0, 0, TargetType.SingleEnemy, "สร้างความเสียหายสะเทือนขวัญ {0} หน่วย");
            var c2 = CreateOrGetCard(baseFolder + "/Cards/Card_HolyWater.asset", "c_water", "น้ำมนต์ธรณีสาร", "Holy Water", MagicSchool.WhiteMagic, CardType.Incantation, 1, 0, 6, 0, 0, 0, TargetType.Self, "ได้รับเกราะคุ้มภัย {0} หน่วย");
            var c3 = CreateOrGetCard(baseFolder + "/Cards/Card_HolyThread.asset", "c_thread", "สายสิญจน์มัดวิญญาณ", "Holy Thread", MagicSchool.WhiteMagic, CardType.Incantation, 2, 0, 14, 0, 0, 0, TargetType.SingleEnemy, "สะกดวิญญาณรุนแรง {0} หน่วย");
            var c4 = CreateOrGetCard(baseFolder + "/Cards/Card_CorpseOil.asset", "c_oil", "น้ำมันพรายมนต์ดำ", "Corpse Oil", MagicSchool.BlackMagic, CardType.Incantation, 0, 2, 12, 0, 0, 0, TargetType.SingleEnemy, "มนต์ดำ! สร้างความเสียหาย {0} หน่วย (เพิ่มมลทิน +2)");
            var c5 = CreateOrGetCard(baseFolder + "/Cards/Card_SoulCurse.asset", "c_curse", "คำสาปมัดตราสัง", "Soul Curse", MagicSchool.BlackMagic, CardType.Incantation, 0, 3, 16, 0, 0, 0, TargetType.SingleEnemy, "มนต์ดำรุนแรง! สร้างความเสียหาย {0} หน่วย (เพิ่มมลทิน +3)");
            var c6 = CreateOrGetCard(baseFolder + "/Cards/Card_PraiGrasipAmulet.asset", "c_amulet", "พรายกระซิบเตือนภัย", "Whispering Ghost Amulet", MagicSchool.WhiteMagic, CardType.Amulet, 1, 0, 0, 3, 0, 0, TargetType.Self, "เครื่องรางคุ้มภัย ทนทาน {0} ครั้ง");
            var c7 = CreateOrGetCard(baseFolder + "/Cards/Card_KumanThongFamiliar.asset", "c_kuman", "กุมารทองเรียกทรัพย์", "Kuman Thong", MagicSchool.WhiteMagic, CardType.Familiar, 2, 0, 0, 0, 10, 4, TargetType.Self, "อัญเชิญกุมารทอง (HP 10, โจมตี 4 ต่อเทิร์น)");

            // 2. Create Starter Enemy Profiles
            var enemyPrai = CreateOrGetEnemy(baseFolder + "/Enemies/PraiGhostProfile.asset", "enemy_prai", "ผีพรายน้ำนอง", 30, false, new List<EnemyMove>
            {
                new EnemyMove { intent = EnemyIntent.Attack, baseValue = 6, moveDescription = "กรงเล็บพราย (โจมตี 6)", weight = 2 },
                new EnemyMove { intent = EnemyIntent.DebuffCurse, baseValue = 0, statusEffect = StatusEffectType.KhwanPhawa, moveDescription = "เสียงกระซิบหลอน (ขวัญผวา)", weight = 1 },
                new EnemyMove { intent = EnemyIntent.HeavyAttack, baseValue = 10, moveDescription = "ลากลงน้ำ (โจมตีหนัก 10)", weight = 1 }
            });

            var enemyBoss = CreateOrGetEnemy(baseFolder + "/Enemies/PhiTaiHongBossProfile.asset", "enemy_boss", "ผีตายโหง (บอสใหญ่)", 60, true, new List<EnemyMove>
            {
                new EnemyMove { intent = EnemyIntent.Attack, baseValue = 8, moveDescription = "ตวาดวิญญาณ (โจมตี 8)", weight = 2 },
                new EnemyMove { intent = EnemyIntent.HeavyAttack, baseValue = 14, moveDescription = "หักคอสังหาร (โจมตีหนัก 14)", weight = 1 },
                new EnemyMove { intent = EnemyIntent.DebuffCurse, baseValue = 0, statusEffect = StatusEffectType.BleedingCurse, moveDescription = "เลือดอาบพื้น (โดนของ)", weight = 1 }
            });

            // 3. Create Starter Deck Config
            string deckPath = baseFolder + "/Decks/StarterDeckConfig.asset";
            DeckConfigSO deck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(deckPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<DeckConfigSO>();
                deck.deckId = "starter_deck";
                deck.deckName = "สำรับหมอธรรมพื้นฐาน";
                deck.defaultDrawCount = 5;
                deck.maxHandSize = 10;
                deck.startingCards = new List<CardDataSO> { c1, c1, c1, c2, c2, c3, c4, c5, c6, c7 };
                AssetDatabase.CreateAsset(deck, deckPath);
            }

            // 4. Create Card Prefab
            string prefabFolder = baseFolder + "/Prefabs";
            EnsureFolder(baseFolder, "Prefabs");
            string cardPrefabPath = prefabFolder + "/CardViewPrefab.prefab";
            CardView cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(cardPrefabPath);
            if (cardPrefab == null)
            {
                cardPrefab = BuildCardPrefab(cardPrefabPath);
            }

            // 5. Build Combat Scene
            EnsureFolder("Assets", "Scenes");
            string scenePath = "Assets/Scenes/CombatTestScene.unity";
            Scene combatScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light & Camera
            GameObject camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.08f); // Thai occult dark atmosphere
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            GameObject lightGo = new GameObject("Candle Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.85f, 0.6f); // Warm candle glow
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Core Managers
            GameObject managersGo = new GameObject("CombatManagers");
            var cardMgr = managersGo.AddComponent<CardManager>();
            cardMgr.defaultDeckConfig = deck;
            cardMgr.defaultDrawCount = 5;
            cardMgr.maxHandSize = 10;

            var combatMgr = managersGo.AddComponent<CombatManager>();
            combatMgr.currentEnemyProfile = enemyPrai;

            var effectRes = managersGo.AddComponent<EffectResolver>();

            // UI Canvas & EventSystem
            GameObject canvasGo = new GameObject("CombatCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // HUD
            var hud = BuildCombatHUD(canvasGo.transform);

            // Hand Layout Controller
            GameObject handContainer = new GameObject("HandContainer");
            handContainer.transform.SetParent(canvasGo.transform, false);
            var handRect = handContainer.AddComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.5f, 0f);
            handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.pivot = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0, 30);
            var handCtrl = handContainer.AddComponent<HandLayoutController>();
            handCtrl.cardPrefab = cardPrefab;
            handCtrl.handContainer = handContainer.transform;

            // Board Slots Container (Amulets on left flank, Familiars in front line)
            BuildBoardSlots(canvasGo.transform);

            // Enemy Combat View
            BuildEnemyView(canvasGo.transform, enemyPrai);

            EditorSceneManager.SaveScene(combatScene, scenePath);
            RegisterSceneInBuild(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[CardEngineSetupTool] CombatTestScene successfully created and registered in Build Settings at {scenePath}!</color>");
        }

        private static void RegisterSceneInBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static CardDataSO CreateOrGetCard(string path, string id, string nameTh, string nameEn, MagicSchool school, CardType type, int merit, int corrupt, int baseVal, int dura, int famHp, int famAtk, TargetType target, string desc)
        {
            CardDataSO card = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDataSO>();
                card.cardId = id;
                card.cardNameThai = nameTh;
                card.cardNameEng = nameEn;
                card.magicSchool = school;
                card.cardType = type;
                card.meritCost = merit;
                card.corruptionGain = corrupt;
                card.baseValue = baseVal;
                card.durability = dura;
                card.familiarHealth = famHp;
                card.familiarDamage = famAtk;
                card.targetType = target;
                card.descriptionFormat = desc;
                AssetDatabase.CreateAsset(card, path);
            }
            return card;
        }

        private static EnemyProfileSO CreateOrGetEnemy(string path, string id, string name, int hp, bool isBoss, List<EnemyMove> moves)
        {
            EnemyProfileSO enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(path);
            if (enemy == null)
            {
                enemy = ScriptableObject.CreateInstance<EnemyProfileSO>();
                enemy.enemyId = id;
                enemy.enemyName = name;
                enemy.maxKhwan = hp;
                enemy.isBoss = isBoss;
                enemy.moves = moves;
                AssetDatabase.CreateAsset(enemy, path);
            }
            return enemy;
        }

        private static CardView BuildCardPrefab(string path)
        {
            GameObject cardGo = new GameObject("CardViewPrefab");
            var rt = cardGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140, 200);

            var bgImg = cardGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.12f, 0.18f);

            var view = cardGo.AddComponent<CardView>();

            // Title Thai (Sacred Occult / Talisman Calligraphy)
            GameObject titleGo = new GameObject("NameThaiText");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            var charmFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            if (charmFont != null) titleText.font = charmFont;
            titleText.fontSize = 15;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0.75f);
            titleRt.anchorMax = new Vector2(1, 0.95f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            view.nameThaiText = titleText;

            // Cost Text
            GameObject costGo = new GameObject("CostText");
            costGo.transform.SetParent(cardGo.transform, false);
            var costText = costGo.AddComponent<TextMeshProUGUI>();
            costText.fontSize = 18;
            costText.fontStyle = FontStyles.Bold;
            costText.alignment = TextAlignmentOptions.TopLeft;
            var costRt = costGo.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0.05f, 0.8f);
            costRt.anchorMax = new Vector2(0.35f, 0.98f);
            view.costText = costText;

            // Description Text (Clean Readable Thai)
            GameObject descGo = new GameObject("DescText");
            descGo.transform.SetParent(cardGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            var sarabunFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            if (sarabunFont != null) descText.font = sarabunFont;
            descText.fontSize = 11.5f;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = new Color(0.85f, 0.85f, 0.85f);
            var descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.05f, 0.05f);
            descRt.anchorMax = new Vector2(0.95f, 0.45f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
            view.descText = descText;

            var prefab = PrefabUtility.SaveAsPrefabAsset(cardGo, path);
            Object.DestroyImmediate(cardGo);
            return prefab.GetComponent<CardView>();
        }

        private static CombatHUD BuildCombatHUD(Transform canvasParent)
        {
            GameObject hudGo = new GameObject("CombatHUD");
            hudGo.transform.SetParent(canvasParent, false);
            var hud = hudGo.AddComponent<CombatHUD>();

            // Top Status Panel: Khwan & Energy
            GameObject topPanel = new GameObject("TopPanel");
            topPanel.transform.SetParent(hudGo.transform, false);
            var topRt = topPanel.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0, 0.85f);
            topRt.anchorMax = new Vector2(1, 1);
            topRt.offsetMin = Vector2.zero;
            topRt.offsetMax = Vector2.zero;

            // Khwan Text
            GameObject khwanGo = new GameObject("PlayerKhwanText");
            khwanGo.transform.SetParent(topPanel.transform, false);
            var khwanTxt = khwanGo.AddComponent<TextMeshProUGUI>();
            khwanTxt.fontSize = 20;
            khwanTxt.text = "ขวัญ: 50 / 50";
            khwanTxt.color = new Color(0.3f, 0.9f, 0.4f);
            var khwanRt = khwanGo.GetComponent<RectTransform>();
            khwanRt.anchoredPosition = new Vector2(150, -30);
            hud.playerKhwanText = khwanTxt;

            // Merit Text
            GameObject meritGo = new GameObject("MeritText");
            meritGo.transform.SetParent(topPanel.transform, false);
            var meritTxt = meritGo.AddComponent<TextMeshProUGUI>();
            meritTxt.fontSize = 20;
            meritTxt.text = "กุศล: 1 / 6";
            meritTxt.color = new Color(1f, 0.85f, 0.2f);
            var meritRt = meritGo.GetComponent<RectTransform>();
            meritRt.anchoredPosition = new Vector2(350, -30);
            hud.meritValueText = meritTxt;

            // Corruption Text
            GameObject corrGo = new GameObject("CorruptionText");
            corrGo.transform.SetParent(topPanel.transform, false);
            var corrTxt = corrGo.AddComponent<TextMeshProUGUI>();
            corrTxt.fontSize = 20;
            corrTxt.text = "มลทิน: 0 / 9";
            corrTxt.color = new Color(0.9f, 0.25f, 0.25f);
            var corrRt = corrGo.GetComponent<RectTransform>();
            corrRt.anchoredPosition = new Vector2(550, -30);
            hud.corruptionText = corrTxt;

            // End Turn Button
            GameObject btnGo = new GameObject("EndTurnButton");
            btnGo.transform.SetParent(hudGo.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.6f, 0.15f, 0.15f);
            var btn = btnGo.AddComponent<Button>();
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 0.2f);
            btnRt.anchorMax = new Vector2(1, 0.2f);
            btnRt.sizeDelta = new Vector2(160, 50);
            btnRt.anchoredPosition = new Vector2(-110, 0);

            GameObject btnTxtGo = new GameObject("Label");
            btnTxtGo.transform.SetParent(btnGo.transform, false);
            var btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
            btnTxt.text = "สิ้นสุดเทิร์น";
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.fontSize = 18;
            hud.endTurnButton = btn;

            // Piles
            GameObject drawPileGo = new GameObject("DrawCountText");
            drawPileGo.transform.SetParent(hudGo.transform, false);
            var drawTxt = drawPileGo.AddComponent<TextMeshProUGUI>();
            drawTxt.text = "สำรับ: 10";
            var drawRt = drawPileGo.GetComponent<RectTransform>();
            drawRt.anchorMin = new Vector2(0, 0);
            drawRt.anchoredPosition = new Vector2(60, 40);
            hud.drawCountText = drawTxt;

            return hud;
        }

        private static void BuildBoardSlots(Transform canvasParent)
        {
            GameObject slotsRoot = new GameObject("BoardSlots");
            slotsRoot.transform.SetParent(canvasParent, false);

            // 3 Amulet Slots on Left Flank
            for (int i = 0; i < 3; i++)
            {
                GameObject slotGo = new GameObject($"AmuletSlot_{i}");
                slotGo.transform.SetParent(slotsRoot.transform, false);
                var img = slotGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.3f, 0.4f, 0.5f);
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.sizeDelta = new Vector2(80, 80);
                rt.anchoredPosition = new Vector2(60, (i - 1) * 95);

                var slotView = slotGo.AddComponent<BoardSlotView>();
                slotView.slotType = BoardSlotView.SlotType.AmuletSlot;
                slotView.slotIndex = i;
            }

            // 3 Familiar Slots in Front Line
            for (int i = 0; i < 3; i++)
            {
                GameObject slotGo = new GameObject($"FamiliarSlot_{i}");
                slotGo.transform.SetParent(slotsRoot.transform, false);
                var img = slotGo.AddComponent<Image>();
                img.color = new Color(0.4f, 0.25f, 0.2f, 0.5f);
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.35f);
                rt.anchorMax = new Vector2(0.5f, 0.35f);
                rt.sizeDelta = new Vector2(100, 110);
                rt.anchoredPosition = new Vector2((i - 1) * 125, 0);

                var slotView = slotGo.AddComponent<BoardSlotView>();
                slotView.slotType = BoardSlotView.SlotType.FamiliarSlot;
                slotView.slotIndex = i;
            }
        }

        private static void BuildEnemyView(Transform canvasParent, EnemyProfileSO profile)
        {
            GameObject enemyGo = new GameObject("EnemyCombatView");
            enemyGo.transform.SetParent(canvasParent, false);
            var enemyView = enemyGo.AddComponent<EnemyCombatView>();
            var rt = enemyGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.65f);
            rt.anchorMax = new Vector2(0.5f, 0.65f);
            rt.sizeDelta = new Vector2(220, 220);
            rt.anchoredPosition = Vector2.zero;

            // Portrait BG
            var bg = enemyGo.AddComponent<Image>();
            bg.color = new Color(0.4f, 0.1f, 0.15f, 0.8f);

            // Name
            GameObject nameGo = new GameObject("EnemyName");
            nameGo.transform.SetParent(enemyGo.transform, false);
            var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
            nameTxt.text = profile != null ? profile.enemyName : "ผีพราย";
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.fontSize = 18;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0, 125);
            enemyView.nameText = nameTxt;

            // Health Text
            GameObject hpGo = new GameObject("EnemyKhwanText");
            hpGo.transform.SetParent(enemyGo.transform, false);
            var hpTxt = hpGo.AddComponent<TextMeshProUGUI>();
            hpTxt.text = "30 / 30";
            hpTxt.alignment = TextAlignmentOptions.Center;
            hpTxt.fontSize = 16;
            var hpRt = hpGo.GetComponent<RectTransform>();
            hpRt.anchoredPosition = new Vector2(0, -125);
            enemyView.khwanText = hpTxt;

            // Intent Root
            GameObject intentGo = new GameObject("IntentTelegraph");
            intentGo.transform.SetParent(enemyGo.transform, false);
            var intentRt = intentGo.AddComponent<RectTransform>();
            intentRt.anchoredPosition = new Vector2(0, 160);
            enemyView.intentRoot = intentGo;

            GameObject descGo = new GameObject("IntentDesc");
            descGo.transform.SetParent(intentGo.transform, false);
            var descTxt = descGo.AddComponent<TextMeshProUGUI>();
            descTxt.text = "เตรียมโจมตี 6 หน่วย";
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.fontSize = 14;
            descTxt.color = Color.yellow;
            enemyView.intentDescText = descTxt;
        }
    }
}
#endif
