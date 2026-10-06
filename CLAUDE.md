# ขวัญเอ้ย ขวัญมา (There, there, my dear.)

Thai-horror deck-building roguelike, Unity 6 (6000.3.19f1, URP), course project for
06016427 Introduction to Game Design and Development. Presentation: Wed 21 Oct 2026 13:30.
The active work plan is `Docs/Plan/PLAN_to_21Oct.md`. Read it before starting a task, and tick its
checkboxes when a task is done.

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/GameFlow/` | `GameFlowManager` (auto-created singleton, owns every scene switch), `RunState` (HP, deck, incense, relics, resume point; JSON save), `MainMenu/`, `Rewards/` (pick 1 of N after a win), `Meru/` (burn / upgrade a card), `Editor/` (scene setup tools) |
| `Assets/Scripts/CardEngine/` | Combat: `Runtime/Core` (CombatManager, CardManager, EffectResolver*, EnemyCardPlayer, EnemyStateMachine, TurnPhaseController), `Runtime/Data`, `Runtime/ScriptableObjects` (CardDataSO, CardCatalogSO, DeckConfigSO, EnemyProfileSO), `Runtime/Visuals` (3D views, HUDs, Graveyard), `Editor` |
| `Assets/Scripts/MapEngine/` | Map graph, save, node views. `NodeType`: MinorEnemy, EliteEnemy, RestSite (= เมรุ), Treasure (= กองของเซ่น), Store (= ศาล), Boss, Event (= หมอกดำ) |
| `Assets/Scripts/EventEngine/` | หมอกดำ events: `EventManager`, `EventDataSO` pages + choices + effects, `EventCatalogSO` |
| `Assets/Scripts/ShopEngine/` | Shop: `ShopManager`, `ShopConfigSO` (cards, heal blessing, card removal, amulets/relics) |
| `Assets/Scripts/Editor/` | `AutomatedCombatFlowTest` (batch-mode smoke test), `CombatSceneDressingTool` |
| `Assets/CardEngineData/Cards/Sheet/Card_s_*.asset` | 42 cards. `cardId` = `s_` + school (w/b) + type (i=incantation, a=amulet, f=familiar) + 2-digit index, e.g. `s_bf05` |
| `Assets/EventEngineData/`, `Assets/ShopEngineData/`, `Assets/GameFlowData/` | Event assets, shop config, reward config |
| `Assets/Scenes/` | Build order: `MainMenu`, `MapTestScene`, `CombatTestScene`, `EventScene`, `ShopScene`, `RewardScene` (+ `MeruScene` once built). `SampleScene` is unused |
| `Tools/Trello/` | PM script, not part of the game |

Namespaces: `TawanOS.GameFlow`, `TawanOS.CardEngine`, `TawanOS.MapEngine`, `TawanOS.EventEngine`,
`TawanOS.ShopEngine`, `TawanOS.EditorTools`. New code: `TawanOS.Audio`, `TawanOS.UI`, `TawanOS.VFX`.

## Game terms (code name = Thai)

khwan / Hp = ขวัญ (HP) · merit = กุศล (white-magic cost, max 6) · corruption = มลทิน (black-magic cost) ·
incense = ธูป (currency) · familiar = บริวาร · amulet = เครื่องราง · incantation = อาคม ·
relic = เครื่องรางติดตัว bought in the shop or found in events

## Card images

`CardDataSO` has two image slots:
- `artwork`: illustration only. The card face (frame, name, cost, text) is drawn by Unity around it.
- `cardImage`: a finished full-card PNG. Unity only draws attack and khwan on top.
The Google Drive set (`s_<cardId>_<ชื่อไทย>.png`) is illustration-only and goes to `artwork`.
Bind by the `s_xxxx` prefix, never by the Thai part.

Audio files: `bgm_*`, `amb_*`, `sfx_*`. Variants end in `_01`, `_02` and are picked at random.

Source assets live outside the repo at `D:\Tawanagent\GameProject_Asset` (Art, Sounds). Task B0 in the plan copies
them into `Assets/Audio`, `Assets/Art`, `Assets/Video`. 13 cards have no art yet and use a placeholder (task B7).

## Unity command line (Windows)

The editor must be closed for batch mode (project lock). Set once per shell:

```powershell
$UNITY = "D:\Unity_Editor\6000.3.19f1\Editor\Unity.exe"   # เครื่องของตะวัน (ตรวจแล้ว 6 ต.ค.) เครื่องอื่นแก้ path ให้ตรงกับที่ติดตั้ง
$PROJ  = "D:\Unity2026PJ\Project_IntroGame"
```

| Purpose | Command |
|---|---|
| Compile check | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -logFile compile.log` then search `compile.log` for `error CS` |
| Run an editor method | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -executeMethod <Namespace.Class.Method> -logFile run.log` |
| Combat smoke test | `& $UNITY -batchmode -projectPath $PROJ -executeMethod TawanOS.EditorTools.AutomatedCombatFlowTest.Run -logFile combat.log` (exits itself; check exit code) |
| EditMode tests | `& $UNITY -batchmode -nographics -projectPath $PROJ -runTests -testPlatform EditMode -testResults tests.xml -logFile tests.log` (no `-quit` with `-runTests`) |
| Windows build | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -executeMethod TawanOS.EditorTools.BuildScript.BuildWindows -logFile build.log` (BuildScript is created in task B0) |

Existing setup tools callable this way: `TawanOS.GameFlow.MeruSetupTool.SetupMeruScene`,
`TawanOS.GameFlow.RewardSetupTool.SetupRewardScene`, `TawanOS.GameFlow.MainMenuSetupTool.SetupMainMenu`,
`TawanOS.EventEngine.EventEngineSetupTool.SetupEventScene`, `TawanOS.ShopEngine.ShopEngineSetupTool.SetupShopScene`.
**Re-running a setup tool rebuilds its scene.** Check with the team before re-running one on a scene someone has dressed by hand.

## Rules

- Runtime code must not use `UnityEditor` (`AssetDatabase` etc.). Anything a build needs is a serialized
  reference or lives under `Resources/`. A `#if UNITY_EDITOR` branch that returns null in a build is a bug.
- `RunState.Current` is the single source of truth for HP, deck, incense and relics across scenes.
  Combat reads it at start and writes back at the end.
- Every editor tool gets both a `[MenuItem("Tools/TawanOS/...")]` and a static method callable with `-executeMethod`.
  Batch methods call `EditorApplication.Exit(code)` on failure.
- Hook into existing C# events (`CombatManager.OnCombatEnded`, `OnMeritChanged`, `OnCorruptionChanged`,
  `OnCurseBackfireTriggered`, `CardManager.OnCardDrawn/OnCardPlayed/OnDeckReshuffled`, `MapManager.On*NodeEntered`)
  instead of polling or editing combat rules.
- Do not change game numbers (corruption cap, max khwan, prices, card stats) without the team's decision.
- Scenes and prefabs are YAML: build or modify them from an editor script, never by hand-editing YAML.
- After each task: compile check, combat smoke test, then commit with a conventional message
  (`fix(game-flow): ...`). One task per commit.
- The repo is mirrored in Unity Version Control (`ignore.conf`) and git. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/`.
