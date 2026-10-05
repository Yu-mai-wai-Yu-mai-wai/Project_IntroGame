# แผนงานถึงวันนำเสนอ 21 ต.ค. 2569

เป้าหมาย: เกมเล่นจบลูปได้ (Win/Lose) และผ่านข้อบังคับ Unity ครบ 8 ข้อภายใน **อังคาร 14 ต.ค.**
เพื่อให้มีเวลา Playtest 15 ต.ค. และแก้ตามผลก่อนเขียน Testing Log

ลำดับ task เรียงตามคะแนนที่ได้ต่อเวลาที่ใช้ ทำตามลำดับ ยกเว้นที่ระบุว่าทำคู่ขนานได้
แต่ละ task มี **Done when** ที่ตรวจได้จาก CLI ห้ามปิด task ถ้ายังไม่ผ่าน

---

## Phase 0 · ก่อนเริ่ม (จันทร์ 5 – พุธ 7 ต.ค.)

### T0.1 ยืนยัน asset ที่ import แล้ว
- หาไฟล์ใน `Assets/` ที่ชื่อขึ้นต้น `s_b`, `s_w`, `bgm_`, `amb_`, `sfx_` แล้วรายงานว่าอยู่ path ไหน
- ถ้ายังกระจายอยู่หลายที่ ย้ายเข้าโครงนี้ผ่าน `AssetDatabase.MoveAsset` (ห้ามย้ายด้วย file system เพราะ .meta จะหลุด):
  - `Assets/Art/Cards/` ภาพการ์ด
  - `Assets/Audio/BGM/`, `Assets/Audio/Ambience/`, `Assets/Audio/SFX/`
- **Done when:** รายงานจำนวนไฟล์ต่อโฟลเดอร์ ภาพการ์ด 29 ไฟล์, BGM 5, Ambience 4, SFX 18

### T0.2 รอผลตัดสินจากทีม (PM เป็นคนตัดสิน ห้ามเดา)
| เรื่อง | ค่าในโค้ดตอนนี้ | ที่ต้องรู้ |
|---|---|---|
| มลทินสูงสุด | 7 (`CombatStateData.corruptionThreshold`) | 7 / 9 / 10 |
| ขวัญสูงสุดผู้เล่น | 50 (`maxPlayerKhwan`) | Sheet เขียน 20 |
| หมอกดำ, เมรุ | ไม่มีใน `NodeType` | ทำหรือตัด (แผนนี้ถือว่าตัด) |
| ชุดการ์ด | 42 ใบ | ยืนยัน 42 |

ระหว่างรอ ทำ Phase 1 ได้เลย เพราะไม่ขึ้นกับค่าเหล่านี้

---

## Phase 1 · ผูก asset เข้าเกม (พุธ 8 ต.ค.)

### T1 CardArtBinder (ข้อ Prefab/การ์ดมีภาพ)
- ไฟล์ใหม่: `Assets/Scripts/CardEngine/Editor/CardArtBinder.cs`
- สำหรับทุก PNG ใน `Assets/Art/Cards/`:
  1. ตั้ง importer เป็น `TextureImporterType.Sprite`, `SpriteImportMode.Single`, max size 1024
  2. ตัดชื่อไฟล์เอา 6 ตัวแรกหลัง `s_` มาเป็น `cardId` เช่น `s_ba05_...` → `s_ba05`
  3. หา `CardDataSO` ที่ `cardId` ตรงกัน แล้วตั้ง `artwork` + `EditorUtility.SetDirty`
- รายงาน: ผูกได้กี่ใบ, การ์ดไหนยังไม่มีภาพ (ควรได้ 13 ใบ), ไฟล์ไหนหาการ์ดไม่เจอ
- MenuItem `Tools/TawanOS/Card Engine/Bind Card Art` + `public static void BindFromCli()`
- **Done when:** รันจาก CLI แล้ว log บอก `bound=29 missing=13 orphan=0` และ `CardView3D` แสดงภาพใน CombatTestScene

### T2 AudioManager (ข้อ Sound + Player Preferences)
- ไฟล์ใหม่ใน `Assets/Scripts/Audio/` namespace `TawanOS.Audio`:
  - `AudioLibrarySO`: list ของ `{ key, AudioClip[] variants, volume }` key = ชื่อไฟล์ตัดเลข variant ออก เช่น `sfx_card_hover`
  - `AudioManager`: singleton + `DontDestroyOnLoad` สร้างอัตโนมัติแบบเดียวกับ `GameFlowManager`
    - 3 channel: BGM (loop, crossfade 1 วินาที), Ambience (loop), SFX (`PlayOneShot`, สุ่ม variant)
    - `AudioMixer` ใหม่ `Assets/Audio/MainMixer.mixer` group Master/BGM/SFX expose parameter `MasterVol`, `BgmVol`, `SfxVol`
    - `SetVolume(channel, 0..1)` แปลงเป็น dB (`Mathf.Log10(v) * 20`, v=0 → -80) แล้ว **บันทึก PlayerPrefs** key `vol_master`, `vol_bgm`, `vol_sfx` และโหลดตอนเริ่ม
  - Editor tool `Tools/TawanOS/Audio/Build Audio Library` สแกน `Assets/Audio/**` สร้าง `AudioLibrary.asset` ใน `Resources/` อัตโนมัติ
- การเล่นตามฉาก:
  | ฉาก | BGM | Ambience |
  |---|---|---|
  | MainMenu | `bgm_title` | `amb_map` |
  | MapTestScene | `bgm_map` | `amb_map` |
  | CombatTestScene ศัตรูทั่วไป | `bgm_combat` | `amb_combat` |
  | CombatTestScene เมื่อ `NodeType.Boss` | `bgm_boss` | `amb_boss` |
- การผูก SFX ผ่าน event ที่มีอยู่แล้ว (ห้ามแก้ logic การต่อสู้):
  | Event | SFX |
  |---|---|
  | `CardManager.OnCardDrawn` | `sfx_card_draw` |
  | `CardManager.OnCardPlayed` | `sfx_card_play` |
  | `CardManager.OnDeckReshuffled` | `sfx_card_shuffle` |
  | hover การ์ดใน `CardPlayController3D` | `sfx_card_hover` |
  | `CombatManager.OnMeritChanged` เมื่อค่าเพิ่ม | `sfx_merit_gain` |
  | `CombatManager.OnCurseBackfireTriggered` | `sfx_corruption_break` + `sfx_corruption_thunder` |
  | `TurnPhaseController` จบเทิร์นผู้เล่น | `sfx_turn_end` |
  | ขวัญผู้เล่นลด (`TakeDamage` toPlayer) | `sfx_hit_taken` + `sfx_khwan_shake` |
  | บริวารโจมตี (`BoardClashView3D`) | `sfx_attack` |
  | เริ่มต่อสู้ / ขวัญหมด | `sfx_fire_on` / `sfx_fire_off` |
  | ศัตรูตาย / ชนะ | `sfx_scream` |
  ถ้า event ที่ต้องการยังไม่มี ให้เพิ่ม `event` ใน class ต้นทางแค่บรรทัดเดียวแล้ว invoke ตรงจุดเดิม
- **Done when:** compile ผ่าน, smoke test ผ่าน, EditMode test `AudioLibraryTests` ยืนยันว่า key ทุกตัวในตารางข้างบนหาเจอ และ PlayerPrefs เก็บ/อ่านค่า volume ได้

---

## Phase 2 · ปิด Core Loop (พฤหัส 9 – จันทร์ 12 ต.ค.)

### T3 RunState: ข้อมูลข้ามฉาก (ข้อ Serialization)
- ไฟล์ใหม่ `Assets/Scripts/Run/RunState.cs` namespace `TawanOS.Run` เก็บ: `playerKhwan`, `maxPlayerKhwan`, `incense`, `deckCardIds` (List<string>), `floorReached`, `bossDefeated`
- `RunStateService`: singleton, `NewRun(DeckConfigSO starter)`, `Save()` / `Load()` เป็น JSON ด้วย `JsonUtility` ที่ `Application.persistentDataPath/run_save.json` (แยกจาก `map_save.json` เดิม)
- แก้ `CombatManager.StartCombat`: อ่านขวัญจาก RunState แทน `state.playerKhwan = state.maxPlayerKhwan` (บรรทัด ~68) เมื่อ RunState มีอยู่ ถ้าไม่มี (เปิด CombatTestScene ตรงๆ) ใช้ค่าเดิม เพื่อไม่ให้ smoke test พัง
- เมื่อ `OnCombatEnded(true)`: เขียนขวัญที่เหลือกลับ RunState, ให้ธูปรางวัล (ค่าเริ่ม 10 ธูป, Elite 20, ใส่เป็น `[SerializeField]`), Save
- **Done when:** EditMode test `RunStateTests` ยืนยัน save → load ได้ค่าเดิม และ smoke test ยังผ่าน

### T4 Win / Lose Flow
- ตอนนี้ `CombatHUD.HandleCombatEnded` เปิด `victoryPanel` / `defeatPanel` ในฉาก Combat ส่วน `GameFlowManager` ข้ามกรณีแพ้ (`if (!isVictory) return;`)
- `GameFlowManager.HandleCombatEnded` (เก็บ `NodeType` ที่เข้ามาไว้ใน field `pendingNodeType` ตอน `HandleCombatNodeEntered`):
  - แพ้ → โหลด GameOver (T6) แทนการค้างที่ Defeat panel
  - ชนะ Boss (`pendingNodeType == NodeType.Boss`) → `RunState.bossDefeated = true` → หน้า Victory
  - ชนะทั่วไป → กลับแผนที่ (เดิม)
- Restart: `RunStateService.NewRun` + `MapManager.ResetAndRegenerate` (มีอยู่แล้ว) + ลบ save
- **Done when:** เขียน batch method ใหม่ `AutomatedRunFlowTest` ที่จำลอง: เริ่ม run → combat แพ้ → ได้ GameOver; เริ่มใหม่ → ชนะ boss → ได้ Victory (สั่งผลแพ้/ชนะผ่าน `CombatManager.EndCombat(bool)` ได้เลย)

### T5 โหนดที่ไม่ใช่การต่อสู้
- `MapManager`: เพิ่ม `event Action<NodeType> OnNodeEntered` ยิงทุกครั้งที่เลือกโหนด (คง `OnCombatNodeEntered` ไว้เพื่อไม่ให้ของเดิมพัง)
- ไฟล์ใหม่ `Assets/Scripts/GameFlow/NodeEventPanels.cs` เป็น overlay บนฉากแผนที่ (ไม่ต้องสร้างฉากใหม่):
  | NodeType | ชื่อในเกม | ทำอะไร |
  |---|---|---|
  | `RestSite` | วัด | ฟื้นขวัญ 30% ของสูงสุด (`[SerializeField]`) |
  | `Store` | ศาลตายาย | แสดงการ์ดสุ่ม 3 ใบ ใช้ธูปซื้อ เข้าเด็คใน RunState ราคา: `CardDataSO` ยังไม่มีช่องราคา ให้เพิ่ม `public int incensePrice` (ค่าเริ่ม = `(meritCost + corruptionGain) * 5`) แล้วให้ Game Designer ปรับใน asset ตามสูตรมูลค่าการ์ดใน Sheet |
  | `Treasure` | กองของเซ่น | เลือกการ์ด 1 จาก 3 ฟรี |
- ปิด panel แล้วโหนดถัดไปเปิดตามปกติ (ใช้ระบบ NodeStatus เดิม)
- **Done when:** RunState เปลี่ยนตามที่ตาราง และ map save/load ยังทำงาน

---

## Phase 3 · ข้อบังคับ Unity ที่เหลือ (อังคาร 13 – อังคาร 14 ต.ค.)

T6, T8, T9 ทำคู่ขนานได้ (คนละไฟล์กัน)

### T6 UI: Main Menu, Pause, Game Over, Victory, Settings
- ฉากใหม่ `Assets/Scenes/MainMenu.unity` สร้างผ่าน editor script `Tools/TawanOS/UI/Build Menu Scenes` (ห้ามเขียน YAML ฉากเอง)
  - ปุ่ม: เริ่มเกม, เล่นต่อ (แสดงเมื่อมี `run_save.json`), ตั้งค่า, เครดิต, ออก
- Pause: prefab overlay ใช้ได้ทั้ง Map และ Combat กด `Esc` (Input System) → `Time.timeScale = 0` ปุ่ม: เล่นต่อ, ตั้งค่า, กลับเมนูหลัก
- Settings: slider Master/BGM/SFX เรียก `AudioManager.SetVolume` (ค่าถูกเก็บ PlayerPrefs จาก T2)
- GameOver และ Victory: overlay หรือฉากเล็ก ปุ่ม เริ่มใหม่ / เมนูหลัก
- ใช้ฟอนต์ Sarabun/Charm ที่มีอยู่ (TMP) ข้อความภาษาไทย ปุ่มขนาดอย่างน้อย 44×44 px และ contrast ข้อความ 4.5:1 (WCAG AA)
- **Done when:** กด Esc ในทั้งสองฉากแล้วเกมหยุด, ปรับ slider แล้วปิดเปิดเกมใหม่ค่ายังอยู่

### T7 Scene transition + loading screen (ข้อ Scene Management)
- `Assets/Scripts/GameFlow/SceneLoader.cs`: fade out → `SceneManager.LoadSceneAsync` แสดง progress → fade in
- แทนที่ `SceneManager.LoadScene` ทุกจุดใน `GameFlowManager` ด้วย `SceneLoader.Load(name)`
- **Done when:** ไม่มี `SceneManager.LoadScene(` เหลือใน `Assets/Scripts` นอกจากใน SceneLoader (เช็คด้วย grep)

### T8 Particle / VFX
- prefab ใน `Assets/VFX/` ใช้ `ParticleSystem` built-in:
  - `VFX_IncenseSmoke` ควันธูปลอยช้า วางบนโต๊ะ CombatTestScene
  - `VFX_CandleFlame` เปลวเทียน วางบนเทียนที่มีอยู่ (`CandleMat`)
  - `VFX_CardPlayBurst` ระเบิดแสงสั้นๆ ตอน `OnCardPlayed` สีทองสำหรับมนต์ขาว สีแดงคล้ำสำหรับมนต์ดำ
  - `VFX_CorruptionBurst` ตอน `OnCurseBackfireTriggered`
- สร้าง prefab ผ่าน editor script เพื่อให้รันซ้ำได้
- **Done when:** prefab 4 ตัวอยู่ใน `Assets/VFX/` และถูกเรียกใช้ในฉาก/โค้ดจริง

### T9 Animator Controller (ข้อ Animation)
- โจทย์ต้องการ Animator Controller / State Machine จริง DOTween อย่างเดียวไม่นับ
- ทำ `Assets/Animation/EnemyPresence.controller` สำหรับศัตรูฝั่งตรงข้ามโต๊ะ (หรือ placeholder ที่มีอยู่):
  states `Idle` → `Attack` → `Hit` → `Death` ด้วย trigger `attack`, `hit`, `die` และ clip ที่สร้างจาก transform (ขยับ/สั่น/จางหาย) ได้ ไม่ต้องมี rig
- ตัวขับ: `EnemyAnimatorDriver.cs` subscribe `CombatManager.OnEnemyAction`, `TakeDamage` ฝั่งศัตรู, `OnCombatEnded`
- ทางเลือกเพิ่ม: Animator ของปุ่มเมนูหรือมือผู้เล่น
- **Done when:** controller มีอย่างน้อย 4 state + transition ด้วย parameter และเห็นทำงานตอนต่อสู้

### T10 Build settings + BuildScript
- Build scenes ตามลำดับ: `MainMenu`, `MapTestScene`, `CombatTestScene` และเอา `SampleScene` ออก
- `Assets/Scripts/Editor/BuildScript.cs` namespace `TawanOS.EditorTools` method `BuildWindows()` → `Build/Windows/KhwanEuyKhwanMa.exe` exit code ≠ 0 ถ้า build ล้ม
- **Done when:** build จาก CLI สำเร็จ และเปิด exe แล้วเริ่มจาก Main Menu

---

## Phase 4 · ทดสอบและเอกสาร (พุธ 15 – จันทร์ 19 ต.ค.)

### T11 Test ที่ต้องผ่านก่อน Playtest (พุธ 14 ต.ค. เย็น)
- [ ] compile ไม่มี `error CS`
- [ ] `AutomatedCombatFlowTest` ผ่าน
- [ ] `AutomatedRunFlowTest` ผ่าน (T4)
- [ ] EditMode tests ผ่านทั้งหมด (`AudioLibraryTests`, `RunStateTests`, `CardArtBinderTests`)
- [ ] Build Windows สำเร็จ

### T12 Playtest support
- สร้าง `Docs/Testing/playtest_log.md` เป็นแบบฟอร์ม: ผู้เล่น, วันที่, เล่นถึงชั้นไหน, ชนะ/แพ้, เวลาที่ใช้, บั๊ก, ความเห็น, สิ่งที่แก้ (ก่อน/หลัง)
- (เสริม) `RunTelemetry`: บันทึก CSV ลง `persistentDataPath` ทุกจบการต่อสู้: ชั้น, เทิร์น, ขวัญที่เหลือ, การ์ดที่ใช้ ใช้เป็นตัวเลขในรายงานส่วน Testing

### T13 ข้อมูลสำหรับรายงานส่วน 7.4 Technical Documentation
- `Docs/Report/unity_features.md` ตาราง: ข้อบังคับ → class/ไฟล์/ฉากที่ใช้ → อธิบาย 1 บรรทัด
- `Docs/Report/class_diagram.md` Mermaid class diagram ของ class หลัก: GameFlowManager, SceneLoader, RunStateService, MapManager, CombatManager, CardManager, EffectResolver, EnemyCardPlayer, EnemyStateMachine, AudioManager
- `Docs/Report/enemy_fsm.md` อธิบาย `EnemyStateMachine` เป็น state diagram (Mermaid `stateDiagram-v2`) สำหรับ bonus AI +6 ไม่ต้องอธิบายโค้ด
- `Docs/Report/tech_issues.md` ปัญหาทางเทคนิคและวิธีแก้ ดึงจาก git log (เช่น turn loop ซ้อนกันใน GameFlowManager, การ flip กล้องแผนที่)

---

## ลำดับเวลาโดยสรุป

| วัน | Task |
|---|---|
| จ. 5 – พ. 7 | T0.1, T0.2 (รอทีมตัดสิน) |
| พ. 8 | T1, T2 |
| พฤ. 9 – จ. 12 | T3, T4, T5 |
| อ. 13 – อ. 14 | T6, T7, T8, T9, T10, T11 |
| พ. 15 | Playtest รอบ 1 |
| พฤ. 16 – จ. 19 | แก้จาก playtest, T12, T13 |
| อ. 20 | build สุดท้ายขึ้น Drive (`Build/`, `Source/`), ซ้อมนำเสนอ |

## ไม่อยู่ใน scope (ใส่รายงานส่วน Future Work)
- โหนดหมอกดำ (Visual Novel event) และเมรุ (อัปเกรด/ทิ้งการ์ด) ถ้าทีมยืนยันว่าตัด
- ภาพการ์ดที่ยังขาด 13 ใบ ใช้ภาพ placeholder จาก `Card_Template_Base.png` ไปก่อน
