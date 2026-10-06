# แผนงานถึงวันนำเสนอ 21 ต.ค. 2569

อัปเดต 6 ต.ค. 2569 หลัง merge `dcb2b57` (Main Menu, Event, Shop, Reward, Meru, RunState, Graveyard)

เป้าหมาย: **build .exe ที่เล่นจบได้ (ชนะ Boss = จบเกม, ขวัญหมด = Game Over)** และผ่านข้อบังคับ Unity ครบ 8 ข้อ
ภายใน **อังคาร 14 ต.ค.** เพื่อ Playtest วันพุธ 15 ต.ค.

กติกา: ทำตามลำดับ task, ทุก task มี **Done when** ที่ตรวจได้ ห้ามติ๊กถ้ายังไม่ผ่าน, หนึ่ง task หนึ่ง commit
ถ้า task ต้องการคำตอบจากหัวข้อ "รอทีมตัดสิน" ให้ทำด้วยค่า default ที่เขียนไว้ แล้วระบุใน commit message

---

## สถานะตอนนี้

| ระบบ | สถานะ |
|---|---|
| Main Menu (เริ่ม / เล่นต่อ / ออก) | มีแล้ว |
| แผนที่ → ต่อสู้ / หมอกดำ / กองของเซ่น / ศาล / เมรุ | มีแล้ว ยกเว้นเมรุ (ไม่มีฉาก) |
| ชนะ → เลือกการ์ด + ได้ธูป | มีแล้ว |
| แพ้ → ลบ save → Main Menu | มีแล้ว (panel 3 วินาที ไม่มีปุ่ม) |
| Save / Continue (`RunState` JSON) | มีแล้ว |
| ชนะ Boss → จบเกม | **ยังไม่มี** |
| ขวัญต่อเนื่องข้ามการต่อสู้ | **ยังไม่มี** |
| ศัตรูใน build | **พัง** (คืนค่า null นอก Editor) |
| Sound, PlayerPrefs (volume), Particle, Animator, Pause, Settings, Loading screen | **ยังไม่มี** |

## รอทีมตัดสิน (PM เป็นคนตัดสิน)

| # | เรื่อง | ค่าในโค้ด | default ถ้ายังไม่ได้คำตอบ |
|---|---|---|---|
| D1 | มลทินสูงสุด | 7 | คงไว้ 7 |
| D2 | ขวัญสูงสุดผู้เล่น | 50 (`RunState.DefaultMaxHp`) | คงไว้ 50 |
| D3 | เครื่องรางติดตัว (relic) ในร้าน/อีเวนต์ | เก็บไว้แต่ไม่มีผล | ซ่อนจากร้านและอีเวนต์ (A6 ทางเลือก ก) |
| D4 | ภาพการ์ด: ภาพประกอบ (`artwork`) หรือภาพสำเร็จ (`cardImage`) | ปนกัน 5 ใบเป็น `cardImage` | ใช้ `artwork` จากชุด Drive, 5 ใบเดิมคงไว้ |
| D5 | ชื่อร้าน: ศาลตายาย (Doc) หรือ ศาลพระภูมิ (เกม) | ศาลพระภูมิ | คงตามเกม แล้วแก้ Doc |
| D6 | โหนดวัด (ฟื้นขวัญ) ที่ Doc เขียนไว้ | ไม่มี ฟื้นขวัญอยู่ในร้าน | ไม่ทำ แก้ Doc |

---

## Phase A · แก้บั๊กระบบ (อังคาร 6 – พฤหัส 8 ต.ค.)

ทำก่อนทุกอย่าง เพราะกระทบเกณฑ์ "เล่นจนจบ Win/Lose ได้จริง" (15%)

### [ ] A0 BuildScript (ทำก่อนเพื่อจับบั๊กที่เกิดเฉพาะใน build)
- `Assets/Scripts/Editor/BuildScript.cs` namespace `TawanOS.EditorTools`, method `BuildWindows()`
- ใช้ scene จาก `EditorBuildSettings` ที่ enabled, เอา `SampleScene` ออกจาก Build Settings
- output `Build/Windows/KhwanEuyKhwanMa.exe`, `EditorApplication.Exit(1)` ถ้า `BuildReport.summary.result != Succeeded`
- **Done when:** build จาก CLI สำเร็จ, Build Settings ไม่มี `SampleScene`

### [ ] A1 ศัตรูโหลดได้ใน build
- ปัญหา: `GameFlowManager.ResolveEnemyProfile` ใช้ `AssetDatabase` ใน `#if UNITY_EDITOR` ส่วน build คืน `null` ทำให้ทุกการต่อสู้ได้ศัตรูเปล่าขวัญ 30
- แก้: สร้าง `EncounterTableSO` (`TawanOS.GameFlow`) มี list `minor`, `elite`, `boss` เป็น `EnemyProfileSO` เก็บที่ `Assets/Resources/EncounterTable.asset` โหลดด้วย `Resources.Load` แล้วสุ่มจาก list ตาม `NodeType` ลบ path string ทั้งหมดออก
- ใส่ค่าเริ่ม: minor = PraiGhost, elite = PraiGhost (จนกว่าจะมีศัตรูใหม่), boss = PhiTaiHongBoss
- `EventManager` ส่งศัตรูจากอีเวนต์เป็น reference อยู่แล้ว ส่วน `TakeEditorTestEvent` ที่ใช้ `AssetDatabase` เป็นตัวช่วยทดสอบใน Editor ที่คืน null ใน build โดยตั้งใจ ไม่ต้องแก้
- **Done when:** ไม่มีโค้ด runtime ที่ต้องพึ่ง `AssetDatabase` เพื่อให้เกมทำงาน (ยกเว้นตัวช่วยทดสอบใน Editor) และใน build การต่อสู้ Boss แสดงชื่อผีตายโหง

### [ ] A2 ขวัญต่อเนื่องข้ามการต่อสู้
- ปัญหา: `CombatManager.StartCombat` ตั้ง `playerKhwan = maxPlayerKhwan` (50) ทุกครั้ง และไม่เขียนค่ากลับ `RunState` ทำให้ฟื้นขวัญที่ร้าน หรือเสียขวัญจากอีเวนต์ไม่มีผล
- แก้:
  - ตอนเริ่มต่อสู้ (ใน `GameFlowManager.HandleSceneLoaded` ฝั่ง combat หรือใน `StartCombat`): ถ้า `RunState.Current.IsPersistent` ให้ `maxPlayerKhwan = RunState.MaxHp`, `playerKhwan = RunState.CurrentHp` ถ้าไม่ใช่ (เปิด CombatTestScene ตรงๆ) ใช้ค่าเดิม เพื่อไม่ให้ smoke test พัง
  - ตอน `OnCombatEnded(true)`: เขียนขวัญที่เหลือกลับ `RunState` แล้ว save
  - HUD ขวัญในฉากต่อสู้ต้องแสดงค่าที่อ่านมาจาก RunState
- **Done when:** EditMode test `RunStateCombatSyncTests`: ตั้ง RunState HP 23/50 → เริ่ม combat → `playerKhwan == 23` และจบ combat ที่ 17 → `RunState.CurrentHp == 17`; smoke test ยังผ่าน

### [ ] A3 ชนะ Boss = จบเกม
- ปัญหา: ชนะ Boss → RewardScene → กลับแผนที่
- แก้ใน `GameFlowManager.HandleCombatEnded`: ถ้า node ที่เข้ามาคือ `NodeType.Boss` → `RunState.EndRun()` + ลบ map save → เปิดหน้าจบเกม (Victory) ที่มีสรุป run (ชั้นที่ไป, ขวัญที่เหลือ, จำนวนการ์ด, ธูป) และปุ่ม "เมนูหลัก" (ใช้ร่วมกับ C3)
- **Done when:** batch test `AutomatedRunFlowTest` (สร้างใหม่ ใน `Assets/Scripts/Editor/`): จำลองเข้า Boss แล้ว `CombatManager.EndCombat(true)` → active scene เป็นหน้า Victory และ `RunState.HasSave == false`; จำลองแพ้ → ได้หน้า Game Over

### [ ] A4 เมรุใช้งานได้
- รัน `TawanOS.GameFlow.MeruSetupTool.SetupMeruScene` สร้าง `MeruScene` แล้วใส่ Build Settings ต่อท้าย
- ไม่มีการ์ดใบไหนตั้ง `upgradedCard` เลย (0/42) ปุ่ม "อัพเกรด" จึงถูกปิดพร้อมข้อความ "ไม่มีการ์ดที่อัพเกรดได้" อยู่แล้ว (`MeruViewUI.ShowChoice`) ไม่ต้องแก้โค้ด แค่บันทึกใน Future Work จนกว่าทีม Design จะทำการ์ดอัปเกรด
- **Done when:** เข้าโหนดเมรุแล้วเปิดฉาก เผาการ์ดได้ จำนวนการ์ดในเด็คลด กลับแผนที่ได้

### [ ] A5 อีเวนต์หมอกดำครบ
- `EventCatalog.asset` อ้างแค่ `what` (วอดส์) ให้ใส่ `Event_GhostGamble`, `Event_SpiritHouse`, `Event_WanderingShaman`, `Event_WellVoice` และ `what` ผ่าน editor script (ห้ามรัน `SetupEventScene` ซ้ำ เพราะจะสร้างฉากใหม่ทับ)
- แก้ชื่อ `what.asset` เป็น `Event_Wods.asset` และ title "วอดส์่" เป็น "วอดส์" (มีวรรณยุกต์เกิน)
- ลบ `Event_New.asset` (placeholder) ถ้าไม่มีอะไรอ้างถึง
- เปลี่ยนชื่อโหนดใน `MapEngineData/Profiles/EventProfile.asset` จาก `Unknown Omen` เป็น `หมอกดำ`
- **Done when:** catalog มี 5 อีเวนต์ และเล่นโหนดหมอกดำ 5 ครั้งไม่เจอเรื่องซ้ำ (catalog เลือกจากที่ยังไม่เคยเจอ)

### [ ] A6 เครื่องรางติดตัว (relic)
- ปัญหา: `RunState.AddRelic` เก็บ id แต่ไม่มีโค้ดใช้ ผู้เล่นเสียธูปฟรี
- ทางเลือก ก (default ตาม D3): ซ่อน amulet ใน `ShopConfig` และเอา effect `GainRelic` ออกจากอีเวนต์ที่ใช้ แทนด้วย `GainIncense` หรือ `CardReward`
- ทางเลือก ข (ถ้าทีมเลือกทำ): `RelicEffects` ใน `TawanOS.GameFlow` อ่าน `RunState.RelicIds` ตอนเริ่ม combat แล้วใช้ effect ที่มีอยู่แล้ว (`AddShield`, `AddMerit`, `RaiseCorruptionThreshold`, `ApplyStatus`) map จาก relicId ทีละตัว
- **Done when:** ไม่มีทางซื้อหรือได้ของที่ไม่มีผลในเกม

### [ ] A7 ศัตรู Elite
- ใน `EncounterTable` (A1) ใส่ศัตรู Elite ที่ต่างจาก minor: ถ้ายังไม่มี ให้ duplicate `PraiGhostProfile` เป็น `PraiGhostEliteProfile` เพิ่ม `maxKhwan` 1.5 เท่า แล้วแจ้งทีม Game Design ให้ปรับ
- **Done when:** Elite กับ minor ใช้ profile คนละตัว

---

## Phase B · ข้อบังคับ Unity ที่ยังขาด (ศุกร์ 9 – อังคาร 14 ต.ค.)

B1 → B2 ต้องทำตามลำดับ (Settings ใช้ AudioManager) ส่วน B4, B5, B6, B7 ทำคู่ขนานได้

### [ ] B1 AudioManager (ข้อ Sound)
- ไฟล์เสียงต้องอยู่ที่ `Assets/Audio/BGM/`, `Assets/Audio/Ambience/`, `Assets/Audio/SFX/` (ถ้ายังไม่มี ให้หยุดแล้วแจ้ง PM ให้ import จาก Drive ก่อน)
- `Assets/Scripts/Audio/` namespace `TawanOS.Audio`:
  - `AudioLibrarySO`: `{ key, AudioClip[] variants }` key = ชื่อไฟล์ตัด `_01`/`_02` ออก
  - Editor tool `Tools/TawanOS/Audio/Build Audio Library` สแกน `Assets/Audio/**` สร้าง `Assets/Resources/AudioLibrary.asset`
  - `AudioManager`: singleton + `DontDestroyOnLoad` สร้างแบบเดียวกับ `GameFlowManager`, channel BGM (loop, crossfade 1 วิ), Ambience (loop), SFX (`PlayOneShot`, สุ่ม variant)
  - `Assets/Audio/MainMixer.mixer` group Master/BGM/SFX expose `MasterVol`, `BgmVol`, `SfxVol`
- เพลงตามฉาก (เปลี่ยนใน `SceneManager.sceneLoaded`):
  | ฉาก | BGM | Ambience |
  |---|---|---|
  | MainMenu | `bgm_title` | `amb_map` |
  | MapTestScene, ShopScene, RewardScene, MeruScene | `bgm_map` | `amb_map` |
  | EventScene | `bgm_event` | `amb_event` |
  | CombatTestScene ศัตรูทั่วไป/Elite | `bgm_combat` | `amb_combat` |
  | CombatTestScene Boss | `bgm_boss` | `amb_boss` |
- SFX ผ่าน event ที่มีอยู่ (ถ้า event ที่ต้องการยังไม่มี ให้เพิ่ม `event` 1 บรรทัดในคลาสต้นทาง ห้ามแก้ logic):
  | Event | SFX |
  |---|---|
  | `CardManager.OnCardDrawn` / `OnCardPlayed` / `OnDeckReshuffled` | `sfx_card_draw` / `sfx_card_play` / `sfx_card_shuffle` |
  | hover การ์ดใน `CardPlayController3D` | `sfx_card_hover` |
  | `CombatManager.OnMeritChanged` เมื่อค่าเพิ่ม | `sfx_merit_gain` |
  | `CombatManager.OnCurseBackfireTriggered` | `sfx_corruption_break` + `sfx_corruption_thunder` |
  | `TurnPhaseController` เข้า `TurnPhase.End` | `sfx_turn_end` |
  | ขวัญผู้เล่นลด (เพิ่ม event `OnPlayerDamaged` ใน `TakeDamageInternal`) | `sfx_hit_taken` + `sfx_khwan_shake` |
  | บริวารโจมตี (`BoardClashView3D`) | `sfx_attack` |
  | เริ่มต่อสู้ / แพ้ | `sfx_fire_on` / `sfx_fire_off` |
  | ศัตรูตาย, ชนะ | `sfx_scream` |
- **Done when:** EditMode test `AudioLibraryTests` ยืนยันว่า key ทุกตัวในสองตารางนี้หาเจอ

### [ ] B2 Settings + PlayerPrefs (ข้อ Player Preferences)
- `AudioManager.SetVolume(channel, 0..1)` แปลง dB (`v <= 0.0001 ? -80 : Mathf.Log10(v) * 20`) บันทึก PlayerPrefs key `vol_master`, `vol_bgm`, `vol_sfx` และโหลดตอนเริ่ม
- Settings panel (prefab ใช้ร่วมกันใน Main Menu และ Pause): slider 3 ตัว + toggle fullscreen (`Screen.fullScreen` เก็บใน PlayerPrefs `fullscreen`)
- เพิ่มปุ่ม "ตั้งค่า" ใน `MainMenuUI`
- **Done when:** EditMode test ตั้ง volume → อ่านจาก PlayerPrefs ได้ค่าเดิม; ปิดเปิดเกมแล้วค่า slider ยังอยู่

### [ ] B3 Pause, Game Over, Victory (ข้อ UI/UX)
- Pause: prefab overlay สร้างโดย `PauseMenu` ที่ auto-create แบบ `GameFlowManager` ทำงานทุกฉากยกเว้น MainMenu กด `Esc` (Input System) → `Time.timeScale = 0` ปุ่ม: เล่นต่อ, ตั้งค่า, กลับเมนูหลัก (save ไว้ก่อนออก)
- Game Over: แทน panel 3 วินาทีเดิม เป็นหน้าที่มีข้อความ, สรุป run และปุ่ม "เริ่มใหม่" / "เมนูหลัก" ไม่เด้งกลับเอง
- Victory: หน้าจาก A3
- ฟอนต์ Sarabun/Charm (TMP), ปุ่มอย่างน้อย 44×44 px, contrast ข้อความ 4.5:1 (WCAG AA), ทุกปุ่มกดด้วยคีย์บอร์ดได้
- **Done when:** กด Esc ในทุกฉากเกมแล้วเกมหยุดและเล่นต่อได้; แพ้แล้วเห็นปุ่มทั้งสอง

### [ ] B4 Loading / transition (ข้อ Scene Management)
- `Assets/Scripts/GameFlow/SceneLoader.cs`: overlay fade (CanvasGroup) → `SceneManager.LoadSceneAsync` แสดง progress → fade in
- แทน `SceneManager.LoadScene(` ทุกจุดใน `GameFlowManager`, `MainMenuUI`, Event/Shop/Reward/Meru ด้วย `SceneLoader.Load(name)`
- **Done when:** `grep -rn "SceneManager.LoadScene(" Assets/Scripts --include=*.cs` เหลือแค่ใน SceneLoader และ Editor scripts

### [ ] B5 Particle / VFX
- prefab ใน `Assets/VFX/` (ParticleSystem built-in) สร้างจาก editor script:
  - `VFX_IncenseSmoke` บนโต๊ะ CombatTestScene และในฉากศาล
  - `VFX_CandleFlame` บนเทียน (`CandleMat`)
  - `VFX_CardPlayBurst` ตอน `OnCardPlayed` สีทอง = มนต์ขาว, แดงคล้ำ = มนต์ดำ
  - `VFX_CorruptionBurst` ตอน `OnCurseBackfireTriggered`
  - `VFX_BurnCard` ตอนเผาการ์ดที่เมรุ
- **Done when:** prefab 5 ตัวมีอยู่และถูกใช้ในฉาก/โค้ดจริง

### [ ] B6 Animator Controller (ข้อ Animation)
- DOTween อย่างเดียวไม่นับ ต้องเป็น Animator Controller ที่มี state + transition
- `Assets/Animation/EnemyPresence.controller`: `Idle` → `Attack` → `Hit` → `Death` ด้วย trigger `attack`, `hit`, `die` clip สร้างจาก transform (ขยับ/สั่น/จาง) ไม่ต้องมี rig
- `EnemyAnimatorDriver.cs` subscribe `CombatManager.OnEnemyAction`, ศัตรูโดนดาเมจ, `OnCombatEnded`
- ทางเลือกเพิ่ม: Animator ปุ่ม Main Menu (Normal/Highlighted/Pressed)
- **Done when:** controller มีอย่างน้อย 4 state + transition ด้วย parameter และทำงานตอนต่อสู้

### [ ] B7 ผูกภาพการ์ดจาก Drive
- ภาพอยู่ที่ `Assets/Art/Cards/` ชื่อ `s_<cardId>_<ชื่อไทย>.png` (ถ้ายังไม่มี ให้แจ้ง PM ให้ import)
- `Assets/Scripts/CardEngine/Editor/CardArtBinder.cs`: ตั้ง importer เป็น Sprite, ตัด `s_xxxx` จากชื่อไฟล์, ใส่ `artwork` ของ `CardDataSO` ที่ `cardId` ตรงกัน **ไม่แตะ `cardImage`** ของ 5 ใบที่ตั้งไว้แล้ว
- MenuItem `Tools/TawanOS/Card Engine/Bind Card Art` + `BindFromCli()`
- **Done when:** log `bound=29 missing=<จำนวน> orphan=0` และการ์ดในเกมแสดงภาพ

---

## Phase C · ทดสอบและเอกสาร (พุธ 15 – จันทร์ 19 ต.ค.)

### [ ] C1 Gate ก่อน Playtest (อังคาร 14 ต.ค. เย็น)
- [ ] compile ไม่มี `error CS`
- [ ] `AutomatedCombatFlowTest` ผ่าน
- [ ] `AutomatedRunFlowTest` ผ่าน (A3)
- [ ] EditMode tests ผ่านทั้งหมด
- [ ] Build Windows สำเร็จ และเล่นใน .exe จาก Main Menu → ชนะ Boss ได้ 1 รอบ

### [ ] C2 Playtest support
- `Docs/Testing/playtest_log.md` แบบฟอร์ม: ผู้เล่น, วันที่, ชั้นที่ไปถึง, ชนะ/แพ้, เวลา, บั๊ก, ความเห็น, สิ่งที่แก้ (ก่อน/หลัง)
- (เสริม) `RunTelemetry`: เขียน CSV ที่ `persistentDataPath` ทุกจบต่อสู้: ชั้น, ประเภทศัตรู, จำนวนเทิร์น, ขวัญที่เหลือ, การ์ดที่ใช้

### [ ] C3 ข้อมูลสำหรับรายงานส่วน 7.4 Technical Documentation
- `Docs/Report/unity_features.md`: ข้อบังคับ 8 ข้อ → class/ไฟล์/ฉาก → หน้าที่ 1 บรรทัด
- `Docs/Report/class_diagram.md`: Mermaid class diagram ของ GameFlowManager, RunState, SceneLoader, MapManager, CombatManager, CardManager, EffectResolver, EnemyCardPlayer, EnemyStateMachine, EventManager, ShopManager, RewardManager, MeruManager, AudioManager
- `Docs/Report/enemy_fsm.md`: `EnemyStateMachine` เป็น `stateDiagram-v2` สำหรับ bonus AI (+6) ไม่ต้องอธิบายโค้ด
- `Docs/Report/game_flow.md`: `flowchart` ของฉากทั้งหมดตามโค้ดจริง ใช้แทน Diagram เดิมในรายงาน
- `Docs/Report/tech_issues.md`: ปัญหาทางเทคนิคและวิธีแก้ จาก git log (turn loop ซ้อน, flip กล้องแผนที่, AssetDatabase ใน build, HP ไม่ sync)

---

## ลำดับเวลา

| วัน | งาน |
|---|---|
| อ. 6 – พฤ. 8 | A0 – A7 |
| ศ. 9 – จ. 12 | B1, B2, B3, B7 |
| อ. 13 – อ. 14 | B4, B5, B6, C1 |
| พ. 15 | Playtest รอบ 1 |
| พฤ. 16 – จ. 19 | แก้จาก playtest, C2, C3 |
| อ. 20 | build สุดท้ายขึ้น Drive (`Build/`, `Source/`), ซ้อมนำเสนอ |

## ไม่อยู่ใน scope (ใส่รายงานส่วน Future Work)
- อัปเกรดการ์ดที่เมรุ (ยังไม่มีการ์ดเวอร์ชันอัปเกรด)
- โหนดวัด และจุดพักตอนเริ่มเกม ตาม Diagram
- ลำดับการลงการ์ดและการทำงานของอาคมตาม Diagram (โค้ดให้ผู้เล่นลงก่อน และอาคมทำงานทันที)
