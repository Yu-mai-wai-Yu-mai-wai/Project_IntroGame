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

Use the Unity CLI (`unity`, docs: https://docs.unity.com/en-us/unity-cli/use-unity-cli), run from the repo root.
It reads the editor version from `ProjectVersion.txt` and the project path from the current directory.
Install it with `winget install Unity.CLI` if `unity --version` fails. The project already has `com.unity.pipeline`,
which lets the CLI drive an editor that is open.

**Editor open** (no project lock, preferred while working):

| Purpose | Command |
|---|---|
| Is an editor connected? | `unity status` |
| Compile check | `unity recompile` (reports compile errors; non-zero exit on failure) |
| List / run editor tools | `unity command` lists them; `unity command editor_play`, `unity command eval "return 1+1;"` |

**Editor closed** (batch mode):

| Purpose | Command |
|---|---|
| Compile check | `unity run . --no-tail -l Logs/compile.log -- -nographics` then search `Logs/compile.log` for `error CS` |
| Run an editor method | `unity run . --no-tail -l Logs/run.log -- -nographics -executeMethod <Namespace.Class.Method>` |
| EditMode tests | `unity test . --mode EditMode --output tests.xml` |
| Windows build | `unity build . --target StandaloneWindows64 --execute-method TawanOS.EditorTools.BuildScript.BuildWindows -o Build/Windows` (BuildScript is created in task T10) |

`unity run` already adds `-batchmode -quit -projectPath -logFile`; passing `-quit` after `--` is an error (exit 6).
Write logs under `Logs/` (git-ignored): Unity logs the Hub access token in the command-line section.

**Fallback: raw `Unity.exe`.** Use it for the combat smoke test, because that test enters Play Mode from
`EditorApplication.update` and calls `Exit` itself, and the `-quit` that `unity run` forces would end it early.
Also use it if the CLI (still beta) misbehaves. The editor must be closed.

```powershell
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
$PROJ  = (Get-Location).Path   # run from the repo root
```

| Purpose | Command |
|---|---|
| Combat smoke test | `& $UNITY -batchmode -projectPath $PROJ -executeMethod TawanOS.EditorTools.AutomatedCombatFlowTest.Run -logFile Logs/combat.log` (exits itself; check exit code) |
| Compile check | `& $UNITY -batchmode -nographics -quit -projectPath $PROJ -logFile Logs/compile.log` then search for `error CS` |
| EditMode tests | `& $UNITY -batchmode -nographics -projectPath $PROJ -runTests -testPlatform EditMode -testResults tests.xml -logFile Logs/tests.log` (no `-quit` with `-runTests`) |

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
