# ขวัญเอ้ย ขวัญมา (There, there, my dear.)

Thai-horror deck-building roguelike, Unity 6 (6000.3.19f1, URP), course project for
06016427 Introduction to Game Design and Development. Presentation: Wed 21 Oct 2026 13:30.
The active work plan is `Docs/Plan/PLAN_to_21Oct.md`. Read it before starting a task.

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/CardEngine/` | Combat: `Runtime/Core` (CombatManager, CardManager, EffectResolver*, EnemyCardPlayer, EnemyStateMachine, TurnPhaseController), `Runtime/Data` (CombatStateData, CardInstance), `Runtime/ScriptableObjects` (CardDataSO, DeckConfigSO, EnemyProfileSO), `Runtime/Visuals` (3D views, HUDs), `Editor` (setup tools) |
| `Assets/Scripts/MapEngine/` | Map: `Runtime/Core` (MapManager, MapGraphGenerator, MapSaveManager), `Runtime/Data` (NodeType enum), `Runtime/Visuals` |
| `Assets/Scripts/GameFlow/GameFlowManager.cs` | Scene switching Map <-> Combat (auto-created before scene load) |
| `Assets/Scripts/Editor/` | `AutomatedCombatFlowTest` (batch-mode smoke test), `CombatSceneDressingTool` |
| `Assets/CardEngineData/Cards/Sheet/Card_s_*.asset` | 42 cards. `cardId` = `s_` + school (w/b) + type (i=incantation, a=amulet, f=familiar) + 2-digit index, e.g. `s_bf05` |
| `Assets/Scenes/` | `MapTestScene`, `CombatTestScene` (SampleScene is unused) |
| `Tools/Trello/` | PM script, not part of the game |

Namespaces: `TawanOS.CardEngine`, `TawanOS.MapEngine`, `TawanOS.GameFlow`. New code follows the same pattern
(`TawanOS.Audio`, `TawanOS.UI`, `TawanOS.Run`).

## Game terms (code name = Thai)

khwan = ขวัญ (HP) · merit = กุศล (white-magic cost, max 6) · corruption = มลทิน (black-magic cost) ·
incense = ธูป (currency) · familiar = บริวาร · amulet = เครื่องราง · incantation = อาคม

## Asset naming (Google Drive and Unity use the same names)

- Card art: `s_<cardId>_<ชื่อไทย>.png` e.g. `s_ba05_หัวกะโหลกอาถรรพ์.png`. Bind by the `s_xxxx` prefix, never by the Thai part.
- Audio: `bgm_*`, `amb_*`, `sfx_*`. Variants end in `_01`, `_02` and are picked at random.

## Unity command line (Windows)

The editor must be closed for batch mode (project lock). Set once per shell:

```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
$PROJ  = "D:\Unity2026PJ\Project_IntroGame"
```

| Purpose | Command |
|---|---|
| Compile check | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -logFile compile.log` then search `compile.log` for `error CS` |
| Run an editor method | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -executeMethod <Namespace.Class.Method> -logFile run.log` |
| Combat smoke test | `& $UNITY -batchmode -projectPath $PROJ -executeMethod TawanOS.EditorTools.AutomatedCombatFlowTest.Run -logFile combat.log` (exits itself; check exit code) |
| EditMode tests | `& $UNITY -batchmode -nographics -projectPath $PROJ -runTests -testPlatform EditMode -testResults tests.xml -logFile tests.log` (no `-quit` with `-runTests`) |
| Windows build | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -executeMethod TawanOS.EditorTools.BuildScript.BuildWindows -logFile build.log` (BuildScript is created in task T10) |

## Rules

- Every editor tool gets both a `[MenuItem("Tools/TawanOS/...")]` and a static method callable with `-executeMethod`,
  so it works in the editor and from the CLI. Batch methods call `EditorApplication.Exit(code)` on failure.
- Hook into existing C# events (`CombatManager.OnCombatEnded`, `OnMeritChanged`, `OnCorruptionChanged`,
  `OnCurseBackfireTriggered`, `MapManager.OnCombatNodeEntered`) instead of polling or editing combat rules.
- Do not change game numbers (corruption cap, card stats) without the team's decision; they live in
  `CombatStateData` and the card assets.
- Scenes and prefabs are YAML: prefer building them from an editor script over hand-editing YAML.
- After each task: compile check, run the combat smoke test, then commit with a conventional message
  (`feat(audio): ...`). One task per commit.
- The repo is mirrored in Unity Version Control (`ignore.conf`) and git. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/`.
