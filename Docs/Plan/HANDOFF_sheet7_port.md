# Handoff: port card sheet 7 into Unity

For a Claude Code session on the team's Windows machine. Read `CLAUDE.md` first (rules, Unity CLI commands).
Written 21 Oct 2026 at the end of a design + port thread with Narathorn (team member, game designer for the cards).

## Where things stand

- **Branch:** `claude/project-thread-kxtjq1`, open as **draft PR #4 into `GotDev`**.
- **Done on that branch** (compiles, combat smoke test passes):
  1. `bb4326f` Amulet slot: column 0 of each side is the amulet slot, familiars use columns 1-4.
  2. `10c2ec1` + `4867303` Old pit removed: the click-to-draw pit that cost Corruption (player and enemy) and its tutorial hint.
  3. `27c9fd4` Center pit: 4 face-up cards per turn, each side takes 1.
- **Left uncommitted on Narathorn's laptop** (`C:\Users\Acer\Desktop\TTMD\Project_IntroGame`, on the PR branch):
  - `Assets/Scenes/CombatTestScene.unity`: result of running `TawanOS.EditorTools.AmuletSlotIconTool.Apply` (the ตะกรุด picture beside both amulet slots). **Commit this and push to PR #4.**
  - `LiberationSans SDF - Fallback.asset`: Unity rewrote the TMP font atlas by itself. **Discard** (`git checkout --`).
  - `stash@{0}`: two files Narathorn had uncommitted on GotDev before (TMP fallback atlas, `ProjectSettings/EntitiesClientSettings.asset`). Narathorn chose to discard them; the stash is only a backup. Drop it when sure.
- **Known failure not caused by PR #4:** the smoke test logs `Failed to set the cursor because the specified texture ('Eyes') was not CPU accessible` and exits 1 (the flow itself reports `RESULT: PASS, errorsLogged=1`). It comes from the default cursor added on GotDev in `1a7dc6b`. Fix: Read/Write enabled (or Texture Type = Cursor) on the Eyes texture's importer, done through an editor script or the Inspector, in its own commit.

## Design source of truth

- `Docs/Design/Cards_Redesign_7_final.csv`: Narathorn's final sheet 7 (Thai, white cards left, black cards right).
- `Docs/Design/card_skills_th_en.csv`: the same 42 cards as one row per card: `card_id` (matches `CardDataSO.cardId`, e.g. `s_bf05`), Thai/English name, rarity, school, type, cost, cost resource, `hp` (= ขวัญ), `atk` (= สะเทือนขวัญ), skill and upgraded skill in EN and TH.
- Bind cards by `card_id` / the `s_xxxx` prefix, never by the Thai name.

## Rules decided by Narathorn (treat as fixed)

| Topic | Decision |
|---|---|
| Board | 5 slots per side: slot 0 = amulet, slots 1-4 = familiars. **Done.** |
| Amulets | One per side. Placing a new one sends the old one to the graveyard. Effect applies to the owner's side only. **Every amulet lasts 3 turns.** Amulets with a use count **break as soon as the count is used up**: ด้ายแดงผูกจิต 1 use, ตะกรุดดำ 2 uses, ผ้ายันต์มหาอุด 2 uses. |
| ตะกรุดดำ | Whenever an ally dies: the enemy loses 1 Khwan (max 2 times, then it breaks). |
| Heal school rework | คาถาเรียกขวัญ: heal 2 and draw 1. ผีแม่ย่านาง: attacks on every heal, once per turn. พระเครื่อง: the enemy loses Khwan equal to each heal, max 3 per turn. |
| Draw | Draw 1 from your deck every turn (free) **and** take 1 from the center pit. Turn 1 has no draw and no pit (opening hand 3). |
| Center pit | **Done.** From turn 2: 4 face-up cards (2 familiars, 1 incantation, 1 amulet), max card cost 1 on turns 2-3, 2 on turns 4-5, 3 after. The enemy picks first on turn 2, then the first pick alternates. Leftovers are thrown away; 4 new cards every turn. Shown as 4 grey-framed misty slots between the board rows that vanish after the picks. |
| Enemy cards per turn | Normal enemies play **2** per turn, the boss **1** (both phases). |
| Difficulty | Normal enemies **+2 Khwan** (sim run win 52%). |
| Opening hand | The **player's** opening hand always gets 1 amulet first, then the rest at random (not the enemy's). |
| Curse | "สาป X": at end of turn the familiar loses X HP, then Curse −1. Use the existing โดนของ status (`DoneKhong`), shown as "สาป". |
| Rewards | Pick 1 of 3 after a win, from 3 different schools. |
| Skill text | Plain English, trigger first (`Play:`, `Death:`, `Start of turn:`, `End of turn:`, `Whenever …:`). Thai text in the CSV. |

`CLAUDE.md` says game numbers need the team's decision: Narathorn made these decisions for the team in this thread; mention them in each PR.

## Work left, in order

### Phase 1: data (fastest, makes the sheet playable)

Write an editor tool (`[MenuItem("Tools/TawanOS/Cards/Apply Sheet 7")]` + static batch method that calls `EditorApplication.Exit(1)` on failure; pattern: `CardSkillSheetUpdate.cs`) that reads `Docs/Design/card_skills_th_en.csv` and updates:
- The 42 `CardDataSO` assets in `Assets/CardEngineData/Cards/Sheet/` and their upgraded versions in `Cards/Upgraded/` (cost, HP, ATK, description text, abilities that already exist).
- Amulet `durability = 3` on every amulet.
- `EnemyProfileSO.maxCardsPerTurn`: 2 normal, 1 boss (both phases).
- Normal enemies +2 Khwan.
- The opening hand amulet rule (small code change in `CardManager.InitializeDeck` / the opening draw, player only).

Never hand-edit asset YAML.

### Phase 2: new card effects (~15), in groups

Add to `EffectResolver.Abilities.cs` (partial class `EffectResolver`), hooked to existing events, and teach `EnemyCardPlayer.ScoreAbilities` about each one:
1. **Heal triggers:** on-heal trigger for ผีแม่ย่านาง (attack once per turn), พระเครื่อง (enemy loses = heal, max 3/turn), ผ้ายันต์ ("if you healed this turn"); ลูกประคำ: every heal +1.
2. **Death / sacrifice:** on-ally-death trigger for เปรต, ตะกรุดดำ, หุ่นพยนต์ (first per turn); Sacrifice for ผีปอบ and พิธีฝังรูปฝังรอย (heal = the sacrificed familiar's Khwan); พิธีเรียกวิญญาณ revives the last 2, and they are exiled if they die again; บทสวดส่งวิญญาณ returns to hand, or destroys if Cursed.
3. **Curse:** a Cursed familiar that dies makes its owner lose its remaining Curse as Khwan; ผีตายโหง +1 damage to Cursed targets; หัวกะโหลก: Curse does not go down; คาถาถอนคุณไสย moves Curses to the opposite side.
4. **Amulets that break:** use counters for ตะกรุดดำ / ด้ายแดง / มหาอุด, break at 0; ด้ายแดงผูกจิต links a lane (the other dies too); ผ้าประเจียดดำ +1 ATK per 3 Corruption; พิธีสะเดาะเคราะห์ destroys the enemy amulet; บทชินบัญชร: one target deals less damage until next turn.

Existing keywords to reuse: `PhiAm` (cannot attack), `Taunt` (Guard), `Protect`, `Overhead` (Flying), `OverheadMagnet` (Anti-air).

## How to work (from CLAUDE.md)

- After each task: compile (`unity recompile` with the editor open, or batch compile), then the combat smoke test (raw `Unity.exe`, editor closed), then **one commit per task** with a conventional message, e.g. `feat(cards): ...`.
- Runtime code must not use `UnityEditor`. Scenes/prefabs/assets only through editor scripts.
- Hook into existing C# events instead of polling.
- Tick the matching boxes in `Docs/Plan/PLAN_to_21Oct.md`.

## Code map for the parts already done

- `EffectResolver.cs`: `AmuletSlot = 0`, `FirstFamiliarSlot = 1`, `IsFamiliarSlot()`; `FreeSlot` only returns 1-4; `PutOnBoard(list, card, isPlayer)` puts an amulet in slot 0 and sends the old one to the graveyard.
- `CombatManager` clash loop, `EnemyCardPlayer` block logic, `CardPlayController3D` slot markers: familiar columns only.
- `BoardSlotView`: `amuletSlotIcon` picture beside slot 0 (set by `AmuletSlotIconTool`).
- `CenterPit.cs` (Core): deals and tracks the 4 cards, `TakeForPlayer` / `TakeForEnemy`, events `OnDealt/OnTaken/OnCleared`; tunables on the component.
- `TurnPhaseController.PitPicks`: new `TurnPhase.PitPick` after the draw; waits for the player's click.
- `CombatManager.EnemyTakeFromPit` + `EnemyCardPlayer.ChoosePitCard`: enemy pick (same scoring as playing a card). The scripted tutorial enemy and a full hand take nothing.
- `CenterPitView3D.cs` (Visuals): bootstraps itself; 4 slots at the midpoint of familiar columns 1-4, SlotGlow-shader frame + procedural mist; hover lifts, left click takes, right click shows details; `TryGetSpawnPosition` makes taken cards fly from their slot.
- `DrawPitView3D` (old pit's smoke at middle-right) is now scenery only. **Open question for Narathorn:** remove it (and the scene's `DrawPitSmoke`) or keep as decoration.
- Tutorial: new hint `TutorialTextSO.centerPit` on turn 2. `Assets/Resources/TutorialText.asset` still carries the old unused `pit` text; Unity drops it on the next save.

## Balance sim (optional)

A Python simulator lives in the project's shared files (`review/combat_sim/`, not in this repo); `review/redesign3/sim_sheet6.md` has the sheet 6/7 results (sections 6-10). Not needed for the Unity work.
