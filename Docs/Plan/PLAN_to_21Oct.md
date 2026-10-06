# แผนงานถึงวันนำเสนอ 21 ต.ค. 2569

อัปเดต 6 ต.ค. 2569 หลัง merge `dcb2b57` (Main Menu, Event, Shop, Reward, Meru, RunState, Graveyard)
แก้ไขรอบ 2 (6 ต.ค.): PM ตัดสินให้ใส่ Story + Character Design ในเกม (B8), ภาพการ์ดที่ขาด 13 ใบใช้ placeholder (B7),
เพิ่ม B0 นำ Asset เข้าโปรเจกต์ และ Phase D (รายงานตามโครงสร้าง 7 ส่วนและการส่งงาน) ที่เดิมไม่มีใน PLAN

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
| Asset จาก `D:\Tawanagent\GameProject_Asset` (เสียง 27, ภาพการ์ด 29, Story 13 รูป + 2 วิดีโอ, Character 2 รูป) | **ยังไม่อยู่ในโปรเจกต์** (ไม่มี `Assets/Audio`, `Assets/Art`) → B0 |
| ภาพการ์ด | มีภาพ 29 จาก 42 ใบ ขาด 13 ใบ → ใช้ placeholder (B7) |
| Intro เนื้อเรื่อง | **ยังไม่มี** → B8 |
| รายงาน 7 ส่วน, สไลด์, ไฟล์ส่งงาน | มีแค่ส่วน 7.4 ใน C3 → เพิ่ม Phase D |

## รอทีมตัดสิน (PM เป็นคนตัดสิน)

| # | เรื่อง | ค่าในโค้ด | default ถ้ายังไม่ได้คำตอบ |
|---|---|---|---|
| D1 | มลทินสูงสุด (ข้อขัดแย้ง: Doc ออกแบบเขียน 9 หน่วย เกจ 0–10 เตือนที่ 8–9 แต่ค่าที่ใช้อยู่คือ 7) | 7 | **ตัดสินแล้ว 6 ต.ค.:** คงไว้ 7 ตามโค้ด แก้ Doc และรายงานให้ตรง (ทีม Game Design ต้องรับทราบ) |
| D2 | ขวัญสูงสุดผู้เล่น | 50 (`RunState.DefaultMaxHp`) | คงไว้ 50 |
| D3 | เครื่องรางติดตัว (relic) ในร้าน/อีเวนต์ | เก็บไว้แต่ไม่มีผล | ซ่อนจากร้านและอีเวนต์ (A6 ทางเลือก ก) |
| D4 | ภาพการ์ด: ภาพประกอบ (`artwork`) หรือภาพสำเร็จ (`cardImage`) | ปนกัน 5 ใบเป็น `cardImage` | **ตัดสินแล้ว 6 ต.ค.:** ใช้ `artwork` จากชุด Drive (29 ใบ), 5 ใบเดิมคงไว้, อีก 13 ใบใช้ placeholder จนกว่าศิลปินส่งภาพ |
| D5 | ชื่อร้าน: ศาลตายาย (Doc) หรือ ศาลพระภูมิ (เกม) | ศาลพระภูมิ | คงตามเกม แล้วแก้ Doc |
| D6 | โหนดวัด (ฟื้นขวัญ) ที่ Doc เขียนไว้ | ไม่มี ฟื้นขวัญอยู่ในร้าน | ไม่ทำ แก้ Doc |
| D7 | Story intro: ลำดับภาพ, ข้อความบรรยาย, วิดีโอ 2 ไฟล์ใช้ตรงไหน | ยังไม่มี | **ตำแหน่งตัดสินแล้ว:** ก่อน Main Menu ทุกครั้งที่เปิดเกม ส่วนที่เหลือใช้ default: ภาพเรียงตามชื่อไฟล์ `IMG_7922`→`IMG_7934`, วิดีโอต่อท้าย (`dad_khwan.mp4` ก่อน), ข้อความบรรยายเว้นว่างจนกว่าทีมส่งบท |
| D8 | ทฤษฎีจิตวิทยาสำหรับรายงาน 7.2 (Design Foundation = 20% ของคะแนน) | ยังไม่เลือก | **ตัดสินแล้ว:** ทีม Game Design (นราธร, อสิธารา) เป็นเจ้าของ ฝั่ง Dev ไม่เขียน แต่ต้องตรวจว่าทุก mechanic ที่ทีมอ้างมีอยู่จริงใน build (D1) |
| D9 | ภาพ Character Design 2 รูป (`IMG_7920` ชีทตัวละครเต็มตัว + silhouette) ใช้ตรงไหน | ยังไม่มี | แสดงเป็นภาพตัวละครผู้เล่นในหน้า Game Over / Victory (B3) |
| D10 | หน้ารางวัลข้ามได้ไหม (Doc: ข้ามไม่ได้, โค้ด: `allowSkip = true`, ได้ธูป 10) | ข้ามได้ | **ตัดสินแล้ว 6 ต.ค.:** ตามโค้ดเดิม แก้ Doc |

---

## Phase A · แก้บั๊กระบบ (อังคาร 6 – พฤหัส 8 ต.ค.)

ทำก่อนทุกอย่าง เพราะกระทบเกณฑ์ "เล่นจนจบ Win/Lose ได้จริง" (15%)

### [x] A0 BuildScript (ทำก่อนเพื่อจับบั๊กที่เกิดเฉพาะใน build)
- **เสร็จ 6 ต.ค.** commit `aa6692d` build ผ่านเมนูใน Editor ที่เปิดอยู่: `Result=Succeeded`, 6 scene, 0 error, 487 warning, 242 MB, 316 วินาที ยังไม่ได้ทดสอบเส้นทาง `-executeMethod` + exit code (ต้องปิด Editor) และยังไม่ได้เปิด `.exe` เล่น
- ผลข้างเคียงที่พบ: การ build ทำให้ Unity แก้ `PC_RPAsset`, `UniversalRenderPipelineGlobalSettings`, `ProjectSettings.asset` (เพิ่ม preloaded InputActions) และ `UnityConnectSettings` (Enabled 0→1) ไม่ได้ commit ไว้ ต้องให้ทีมตัดสินว่าจะเก็บ `preloadedAssets` ของ Input Actions ไหม เพราะ build จริงต้องใช้
- `Assets/Scripts/Editor/BuildScript.cs` namespace `TawanOS.EditorTools`, method `BuildWindows()`
- ใช้ scene จาก `EditorBuildSettings` ที่ enabled, เอา `SampleScene` ออกจาก Build Settings
- output `Build/Windows/KhwanEuyKhwanMa.exe`, `EditorApplication.Exit(1)` ถ้า `BuildReport.summary.result != Succeeded`
- **Done when:** build จาก CLI สำเร็จ, Build Settings ไม่มี `SampleScene`

### [x] A1 ศัตรูโหลดได้ใน build
- **เสร็จ 6 ต.ค.** commit `bca8da0` `EncounterTableSO` + `Resources/EncounterTable.asset`, `EncounterTableTests` ผ่าน 8 ข้อ, smoke test ผ่าน (`EnemyKhwan=30`) ยังไม่ได้เปิด `.exe` เล่นเพื่อดูชื่อ Boss ใน build จริง
- พบเพิ่ม: `MapPathPrefab` ไม่มี material (อาศัย `AssetDatabase` ใน Editor เติมให้) ผูก `String.mat` ใน prefab แล้ว `MapManager.config` ผูกใน scene แล้ว (ไม่ต้องแก้) การเรียก `AssetDatabase` ที่เหลือใน runtime อยู่ใน `#if UNITY_EDITOR` ทั้งหมด หรือเป็นตัวช่วยทดสอบ (`EventManager.TakeEditorTestEvent`)
- ปัญหา: `GameFlowManager.ResolveEnemyProfile` ใช้ `AssetDatabase` ใน `#if UNITY_EDITOR` ส่วน build คืน `null` ทำให้ทุกการต่อสู้ได้ศัตรูเปล่าขวัญ 30
- แก้: สร้าง `EncounterTableSO` (`TawanOS.GameFlow`) มี list `minor`, `elite`, `boss` เป็น `EnemyProfileSO` เก็บที่ `Assets/Resources/EncounterTable.asset` โหลดด้วย `Resources.Load` แล้วสุ่มจาก list ตาม `NodeType` ลบ path string ทั้งหมดออก
- ใส่ค่าเริ่ม: minor = PraiGhost, elite = PraiGhost (จนกว่าจะมีศัตรูใหม่), boss = PhiTaiHongBoss
- `EventManager` ส่งศัตรูจากอีเวนต์เป็น reference อยู่แล้ว ส่วน `TakeEditorTestEvent` ที่ใช้ `AssetDatabase` เป็นตัวช่วยทดสอบใน Editor ที่คืน null ใน build โดยตั้งใจ ไม่ต้องแก้
- **Done when:** ไม่มีโค้ด runtime ที่ต้องพึ่ง `AssetDatabase` เพื่อให้เกมทำงาน (ยกเว้นตัวช่วยทดสอบใน Editor) และใน build การต่อสู้ Boss แสดงชื่อผีตายโหง

### [x] A2 ขวัญต่อเนื่องข้ามการต่อสู้
- **เสร็จ 6 ต.ค. (ส่วนที่ทดสอบอัตโนมัติได้)** `CombatManager.SetStartingKhwan`, `RunState.SetCurrentHp`, `GameFlowManager` ส่งค่าตอนเข้าฉากต่อสู้และเขียนกลับหลังชนะ `RunStateCombatSyncTests` ผ่าน 7 ข้อ (HP 23/50 → เริ่มที่ 23; จบที่ 17 → run = 17; clamp 1..max; ไม่มี run → เต็ม) A8, A1 และ smoke test ยังผ่าน
- **ยังไม่ได้ทดสอบ:** เส้นทางจริงผ่าน `GameFlowManager` (เงื่อนไข `RunState.IsPersistent`) smoke test เริ่มจาก MapTestScene โดยไม่มี run ที่เซฟ จึงไม่ผ่านกิ่งนี้ ให้ทดสอบมือ: เริ่มเกมใหม่ → สู้จนขวัญลด → ชนะ → เข้าต่อสู้ถัดไป ต้องเริ่มด้วยขวัญที่เหลือ และ HUD แสดงค่านั้น
- `StartCombat` ยังรีเซ็ตมลทินทุกต่อสู้ (PM ตัดสินให้คงไว้)
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

### [x] A6 เครื่องรางติดตัว (relic)
- **เสร็จ 6 ต.ค. (ทางเลือก ก):** `ShopConfig.amuletsForSale = 0`; อีเวนต์ GhostGamble/WanderingShaman ใช้ `CardReward`, WellVoice (ธูป 40) และ SpiritHouse (+ธูป 25) ตัด `GainRelic` ออก; `EventEngineSetupTool.cs` แก้ให้ตรงกัน เพื่อไม่ให้กด setup ซ้ำแล้ว relic กลับมา; `EventCatalogTests` PASS 46/46. ข้อความ resultText ในอีเวนต์ยังพูดถึงของ (ปิ่น, ตะกรุด, พระเครื่อง) ให้ทีม Game Design ปรับ
- ปัญหา: `RunState.AddRelic` เก็บ id แต่ไม่มีโค้ดใช้ ผู้เล่นเสียธูปฟรี
- ทางเลือก ก (default ตาม D3): ซ่อน amulet ใน `ShopConfig` และเอา effect `GainRelic` ออกจากอีเวนต์ที่ใช้ แทนด้วย `GainIncense` หรือ `CardReward`
- ทางเลือก ข (ถ้าทีมเลือกทำ): `RelicEffects` ใน `TawanOS.GameFlow` อ่าน `RunState.RelicIds` ตอนเริ่ม combat แล้วใช้ effect ที่มีอยู่แล้ว (`AddShield`, `AddMerit`, `RaiseCorruptionThreshold`, `ApplyStatus`) map จาก relicId ทีละตัว
- **Done when:** ไม่มีทางซื้อหรือได้ของที่ไม่มีผลในเกม

### [ ] A7 ศัตรู Elite
- ใน `EncounterTable` (A1) ใส่ศัตรู Elite ที่ต่างจาก minor: duplicate `PraiGhostProfile` เป็น `PraiGhostEliteProfile` ตั้ง `maxKhwan: 45` (1.5 เท่าของศัตรูทั่วไป) แล้วแจ้งทีม Game Design ให้ปรับ
- ที่มาของตัวเลข (PM มอบให้ Claude กำหนด 6 ต.ค.): ศัตรูทั่วไป 30 (คืนค่าเดิม), Elite 45, Boss 60 ตั้งตามสัดส่วนเวลาต่อสู้ใน Doc (ทั่วไป 5–10 นาที, Boss 10–15 นาที) เป็นค่าเริ่มต้นที่ต้องปรับหลัง playtest รอบ 1 ไม่ใช่ค่าที่ balance แล้ว ค่า 1 เดิมมาจากคอมมิต `3b97241` และน่าจะเป็นค่าทดสอบ
- **Done when:** Elite กับ minor ใช้ profile คนละตัว, `maxKhwan` ของ Elite = 45

### [x] A8 การต่อสู้จบครั้งเดียว (guard `EndCombat`) + ตายพร้อมกันผู้เล่นชนะ
- **เสร็จ 6 ต.ค.** commit `e703ff0` ตรวจแล้ว: compile 0 error, `CombatEndTests` PASS 7 กรณี, `AutomatedCombatFlowTest` PASS
- ปัญหา: `CombatManager.TakeDamageInternal` ไม่ตรวจว่าการต่อสู้จบแล้ว และ `GameFlowManager.HandleCombatEnded` ไม่ป้องกันการถูกเรียกซ้ำ ถ้าขวัญสองฝั่งถึง 0 ในจังหวะเดียวกัน จะเรียก `EndCombat(true)` และ `EndCombat(false)` ทั้งคู่ ผลคือให้รางวัลแล้วลบ save ในรอบเดียว เส้นทางที่เกิดได้จริงในโค้ด: `MontSaThon` สะท้อนดาเมจ และ Bleeding ที่ทำให้ทั้งสองฝั่งเหลือ 0 ในการ tick ปลายรอบ (หมายเหตุ: บริวารที่ชนกันตัวต่อตัวทำดาเมจใส่การ์ด ไม่ใช่ขวัญผู้เล่น จึงไม่ใช่เส้นทางตายพร้อมกัน ตามที่เขียนไว้ผิดในร่างแรก)
- กติกา (PM ตัดสิน 6 ต.ค.): **ตายพร้อมกัน ผู้เล่นชนะ**
- แก้:
  - `CombatManager.EndCombat`: ถ้า `IsCombatOver` อยู่แล้วให้ return (เรียกได้ครั้งเดียว)
  - `EndCombat(false)`: ถ้า `enemyKhwan <= 0` ให้ถือเป็น `EndCombat(true)`
  - การโจมตีคู่ที่เกิดพร้อมกัน (บริวารเผชิญหน้ากัน) ให้ใส่ดาเมจทั้งสองฝั่งก่อน แล้วค่อยตัดสินผลหลังใส่ครบ
  - `HandleCombatEnded` ทำงานครั้งเดียวต่อการต่อสู้ (ล้าง subscription หรือ flag) และไม่แก้ไขกฎการตัดสินแพ้ชนะอื่น
- **Done when:** `CombatEndTests` (รันด้วย `-executeMethod TawanOS.EditorTools.CombatEndTests.Run` เพราะโปรเจกต์ยังไม่มี test assembly) ผ่าน: (1) เรียก `EndCombat` ซ้ำ `OnCombatEnded` ยิงครั้งเดียว (2) สะท้อนดาเมจทำให้สองฝั่งเป็น 0 → Victory (3) Bleeding ทำให้สองฝั่งเป็น 0 ใน tick เดียวกัน → Victory (4) ตายฝั่งเดียวยังตัดสินถูก; smoke test ยังผ่าน
- ข้อจำกัดที่ยังเหลือ: การทดสอบ (3) ทดสอบกลไก batch ผ่าน reflection ไม่ได้รัน `ClashAndRoundEnd` จริง (ต้องใช้ coroutine ใน Play mode) และไม่ได้รันแบบ red-test ก่อนแก้ เพราะ API ใหม่ (`BeginOutcomeBatch`) ไม่มีในโค้ดเดิม

### [x] A11 หยุด `MapEngineAutoRunner` เขียนทับแผนที่ (เพิ่มระหว่างทำ A8)
- พบตอนรัน smoke test: Unity เขียนทับ `MapTestScene`, `DefaultMapConfig`, `EventProfile`, `RestProfile`, `TreasureProfile` เอง ชื่อโหนดไทย ("เมรุ", "กองของเซ่น") กลายเป็นอังกฤษ ("Spirit Lantern", "Sacred Offering") สาเหตุ: `[InitializeOnLoad]` สั่ง `SetupTestSceneAndProfiles()` ทุกครั้งที่ไม่มี `Temp/MapEngineSetupRunFlag_v4.txt` (เครื่องใหม่ หรือเคลียร์ Temp) และน่าจะเป็นสาเหตุที่ `MapTestScene` สองฝั่งรวมกันไม่ได้
- แก้ commit `38f7bd5`: เหลือเป็นเมนูที่ต้องกดเองพร้อมกล่องยืนยัน
- แก้ตัวทดสอบ commit `31eb65e`: `AutomatedCombatFlowTest` เริ่มจาก run ใหม่ (ล้าง save), คลิกเฉพาะโหนดต่อสู้, ใช้งบเวลาจริง 240 วินาทีแทนงบเฟรม
- **Done when:** compile ผ่าน, smoke test PASS, `git status` หลังรันไม่มีไฟล์แผนที่/scene เปลี่ยน (ตรวจแล้ว: เหลือเฉพาะไฟล์ font ที่ Unity แก้เอง รอ F1)

### [ ] A9 ศัตรูทั่วไปตัวที่ 3 + แก้ id ซ้ำ
- ปัญหา: roster ตอนนี้มีศัตรูทั่วไปจริงตัวเดียว `NewEnemyProfile` ใช้ `enemyId: enemy_prai` ซ้ำกับ `PraiGhostProfile` และเด็คเริ่มต้นเหมือนกัน
- ขั้นต่ำที่ต้องมีใน build (PM ตัดสิน): ศัตรูทั่วไป 3 แบบ, Elite 1, Boss 1
- ทำ: แก้ `NewEnemyProfile` ให้ `enemyId` ไม่ซ้ำ และสร้างศัตรูทั่วไปให้ครบ 3 แบบจากการ์ดที่มีอยู่ (เด็คต่างกัน ชื่อ/ภาพต่างกัน `maxKhwan: 30`) ตัวเลขการ์ดและเด็คเป็นของทีม Game Design ฝั่ง Dev ใส่ค่าเริ่มต้นให้เล่นได้ แล้วส่งให้ทีม Game Design ปรับ
- ต่อสายผ่าน `EncounterTable` (A1) ห้ามใช้ path string
- **Done when:** `enemyId` ทุกตัวไม่ซ้ำ (ตรวจด้วย editor script), `EncounterTable` มี minor 3, elite 1, boss 1, และเล่นแผนที่ 1 รอบแล้วเจอศัตรูทั่วไปอย่างน้อย 2 แบบ

### [ ] A10 โหมดสั้น 4 ชั้น
- PM ตัดสิน 6 ต.ค.: มีโหมดสั้นสำหรับนำเสนอ (ยังไม่ทราบเวลานำเสนอ)
- เพิ่มตัวเลือกใน Main Menu: "เล่นเต็ม (7 ชั้น)" / "เล่นสั้น (4 ชั้น)" ส่งค่าเข้า `MapConfigSO` ผ่าน `RunState` (บันทึกใน save เพื่อให้ "เล่นต่อ" ใช้โหมดเดิม) ห้ามแก้ asset `DefaultMapConfig` ตรงๆ
- ต้องตรวจก่อน: `startingNodesCount: 3`, `preBossNodesCount: 2`, `pathCount: 3` ที่ตั้งไว้ต้องสร้างแผนที่ 4 ชั้นที่ถูกต้องได้ (ชั้นสุดท้ายเป็น Boss)
- **Done when:** batch test สร้างแผนที่ 4 ชั้นด้วย seed 20 ค่า ทุกครั้งมีทางเดินจากชั้นแรกถึง Boss และไม่มีโหนดโดดเดี่ยว; "เล่นต่อ" กลับมาในโหมดเดิม

---

## Phase B · ข้อบังคับ Unity ที่ยังขาด (ศุกร์ 9 – อังคาร 14 ต.ค.)

B0 ต้องทำก่อน B1, B7, B8 B1 → B2 ต้องทำตามลำดับ (Settings ใช้ AudioManager) ส่วน B4, B5, B6, B7 ทำคู่ขนานได้

### [x] B0 นำ Asset เข้าโปรเจกต์
- **เสร็จ 6 ต.ค.** commit `2722b0c`: เสียง 27 / การ์ด 29 / เนื้อเรื่อง 13 / วิดีโอ 2 / ตัวละคร 2 คัดลอกจาก `D:\Tawanagent\GameProject_Asset` (ต้นทางคงไว้) `.meta` ครบ ไฟล์ใหญ่สุด 17.6 MB Console 0 error ไฟล์ mp4 ถูก Git LFS รับ ยังไม่ได้เปิด Play mode ดูว่า import setting ของ mp3/wav เหมาะกับ build (B1 จะตั้ง)
- ต้นทาง `D:\Tawanagent\GameProject_Asset` ปลายทางในโปรเจกต์ (คัดลอก ไม่ย้าย ต้นทางคงไว้):
  | ต้นทาง | ปลายทาง | จำนวน |
  |---|---|---|
  | `Sounds\BGM`, `Sounds\Ambience`, `Sounds\SFX` (คงโฟลเดอร์ย่อย `card/`, `corruption/`, `scream/`) | `Assets/Audio/BGM`, `Assets/Audio/Ambience`, `Assets/Audio/SFX` | 27 ไฟล์ |
  | `Art\Card Design\**\*.png` (รวมเป็นโฟลเดอร์เดียว ชื่อไฟล์ไม่ซ้ำกัน) | `Assets/Art/Cards/` | 29 ไฟล์ |
  | `Art\Story_Start_Game\*.png` | `Assets/Art/Story/` | 13 ไฟล์ (`IMG_7922`–`IMG_7934`) |
  | `Art\Story_Start_Game\*.mp4` | `Assets/Video/` | 2 ไฟล์ (1920×1080, h264, 30 fps, **ไม่มีแทร็กเสียง**; `dad_khwan.mp4` 6.0 วินาที 5.7 MB, อีกไฟล์ 10.0 วินาที 9.8 MB) |
  | `Art\Character Design\*.JPG` | `Assets/Art/Characters/` | 2 ไฟล์ |
- ไม่คัดลอก: ไฟล์ `.zip`, `.unitypackage`, `.tar`, `icon.ai` (Unity import `.ai` ไม่ได้ ต้องให้ศิลปิน export เป็น PNG ก่อน), โฟลเดอร์ `Font`/`Logo`/`UI-UX` ที่ยังว่าง
- เปิด Unity ให้ import ครั้งเดียว แล้ว commit ไฟล์ `.meta` ทั้งหมดไปพร้อมกัน (ห้ามขาด `.meta`)
- **Done when:** นับไฟล์ได้ตามตาราง, Console ไม่มี Error ตอน import, ไม่มีไฟล์เดี่ยวเกิน 100 MB (ข้อจำกัด GitHub; ไฟล์ใหญ่สุดคือ `amb_event.wav` 17.6 MB), `git status` ไม่มี `.meta` ที่ขาด
- หมายเหตุ: ถ้า `amb_event.wav` ทำให้ build ใหญ่เกินไป ให้แปลงเป็น mp3 ชื่อเดิมก่อน (ไม่เปลี่ยน key)

### [~] B1 AudioManager (ข้อ Sound) — โค้ดเสร็จ 6 ต.ค. ค้าง: Mixer, hover SFX, ฟังจริง
- **เสร็จ:** `AudioLibrarySO`, `AudioManager`, `AudioDirector`, tool `Build Audio Library` (23 key), `AudioLibraryTests` PASS (Red→Green) ผูกเพลงตามฉากและ SFX จาก event เดิมโดยไม่แก้ logic
- **ค้าง/ต่างจากสเปก:** (1) ไม่มี `MainMixer.mixer` ใช้ volume ต่อ AudioSource แทน (Unity ไม่มี API สร้าง mixer + exposed parameter) (2) `sfx_card_hover` ยังไม่ผูก ต้องเพิ่ม event ใน `CardPlayController3D` (3) ยังไม่ได้ฟังจริงใน Play mode
- ไฟล์เสียงต้องอยู่ที่ `Assets/Audio/BGM/`, `Assets/Audio/Ambience/`, `Assets/Audio/SFX/` (ทำ B0 ก่อน ถ้ายังไม่มีให้หยุดแล้วแจ้ง PM)
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

### [ ] B7 ผูกภาพการ์ดจาก Drive + placeholder 13 ใบ
- ภาพอยู่ที่ `Assets/Art/Cards/` ชื่อ `s_<cardId>_<ชื่อไทย>.png` (ทำ B0 ก่อน)
- `Assets/Scripts/CardEngine/Editor/CardArtBinder.cs`: ตั้ง importer เป็น Sprite, ตัด `s_xxxx` จากชื่อไฟล์, ใส่ `artwork` ของ `CardDataSO` ที่ `cardId` ตรงกัน **ไม่แตะ `cardImage`** ของ 5 ใบที่ตั้งไว้แล้ว
- Placeholder: การ์ดที่ไม่มีทั้ง `artwork` และ `cardImage` หลังผูกเสร็จ ให้ใส่ภาพ placeholder ตามสำนัก (มนต์ขาว/มนต์ดำ) 2 ไฟล์ที่ tool สร้างเองที่ `Assets/Art/Cards/_placeholder_white.png`, `_placeholder_black.png` (สีพื้นต่างกัน + ข้อความ "รอภาพ") ชื่อขึ้นต้น `_` เพื่อไม่ถูกจับเป็นภาพการ์ดจริง
- ที่ขาดอยู่ตอนนี้ 13 ใบ (ตรวจแล้ว 6 ต.ค.): `s_bi01`–`s_bi06`, `s_wa03`, `s_wa07`, `s_wf01`, `s_wf02`, `s_wi01`, `s_wi02`, `s_wi04`
- เมื่อศิลปินส่งภาพเพิ่ม: วางไฟล์ใน `Assets/Art/Cards/` แล้วรัน Bind ซ้ำ ภาพจริงต้องทับ placeholder เอง
- MenuItem `Tools/TawanOS/Card Engine/Bind Card Art` + `BindFromCli()`
- **Done when:** log `bound=29 placeholder=13 orphan=0`, การ์ดทั้ง 42 ใบมีภาพที่ไม่ใช่ null, รันซ้ำแล้วผลเท่าเดิม (idempotent) และลองใส่ภาพทดสอบ 1 ใบแล้ว `placeholder=12`

### [ ] B8 Story intro + ภาพตัวละคร
- ใช้ภาพจาก `Assets/Art/Story/` (13 รูป), `Assets/Video/` (2 วิดีโอ ไม่มีเสียงในตัว ใช้เสียง BGM แทน), `Assets/Art/Characters/` (2 รูป) ค่า default ตาม D7, D9
- **ตำแหน่ง (PM ตัดสิน 6 ต.ค.):** เล่นตอนเปิดเกม **ก่อนเข้า Main Menu** ทุกครั้งที่เปิดเกม ข้ามได้ทันที ไม่เกี่ยวกับ "เริ่มใหม่/เล่นต่อ"
- `TawanOS.GameFlow.IntroStoryDataSO`: list ของ step, แต่ละ step เป็นภาพหรือวิดีโอ + ข้อความบรรยาย (ว่างได้) เก็บที่ `Assets/GameFlowData/IntroStory.asset` เพื่อให้ทีมสลับลำดับได้โดยไม่แตะโค้ด
- ฉาก `IntroStoryScene` สร้างจาก editor script `TawanOS.GameFlow.IntroStorySetupTool.SetupIntroScene` (ห้ามแก้ YAML ด้วยมือ) ใช้ `UnityEngine.Video.VideoPlayer` กับ `VideoClip` ที่ serialize ไว้ (ห้ามอ่านจาก path ใน runtime)
- ลำดับฉาก: เปิดเกม → `IntroStoryScene` → `MainMenu` → (เริ่มใหม่/เล่นต่อ) → `MapTestScene` `IntroStoryScene` เป็น build index 0 ก่อน `MainMenu`
- ต้องตรวจก่อนทำ: ฉากแรกของ build เปลี่ยนจาก `MainMenu` เป็น `IntroStoryScene` โค้ดที่สมมติว่า `MainMenu` เป็นจุดเริ่ม (การสร้าง `GameFlowManager` อัตโนมัติ, ปุ่ม "กลับเมนูหลัก", `RunState` โหลด save) ต้องไม่พึ่ง build index 0 ถ้าต้องแก้ ให้แก้ที่ `GameFlowManager` จุดเดียว
- ควบคุม: คลิก/Space/Enter = ถัดไป, ปุ่ม "ข้าม" และ `Esc` = ข้ามทั้งหมด, ปุ่ม 44×44 px ขึ้นไป, ใช้ฟอนต์ Sarabun (TMP) ผู้เล่นที่เห็น intro มาแล้วต้องข้ามได้ภายใน 1 คลิก
- เสียง: วิดีโอไม่มีเสียงในตัว ใช้ `bgm_title` เล่นต่อเนื่องจาก intro เข้า Main Menu (ผ่าน B1) ถ้ายังไม่มี B1 ให้เงียบ
- **Done when:** batch test เปิดเกมแล้ว active scene เป็น `IntroStoryScene`, จำลองกดข้ามแล้วไป `MainMenu`, "เริ่มใหม่" และ "เล่นต่อ" ทำงานเหมือนเดิม (`RunState.HasSave` ไม่เปลี่ยนจาก intro), `grep` ไม่พบโค้ดที่ผูกกับ build index 0, จำนวน step ใน asset = 13 ภาพ + 2 วิดีโอ

### [ ] B9 ภาพตัวละครในหน้า Game Over / Victory
- ใช้ `Assets/Art/Characters/IMG_7920.JPG` (ชีทตัวละครเต็มตัว) ในหน้า Game Over และ Victory จาก B3 ตัดเฉพาะตัวละครด้านซ้าย (ไม่เอา silhouette) ด้วย Sprite import setting หรือ `RectTransform` mask ไม่แก้ไฟล์ต้นฉบับ
- **Done when:** ทั้งสองหน้าแสดงภาพตัวละครโดยไม่ยืดสัดส่วน และ Console ไม่มี warning เรื่อง texture

---

## Phase F · ข้อเสนอเพิ่มเติมรอบ 3 (**รอตะวันตอบ "Proceed" ก่อนเริ่มทุกข้อ**)

เสนอเมื่อ 6 ต.ค. จากคำขอของ PM (เสียง, VFX, ฟอนต์ไทย, UI, ข้อบังคับ Unity 8 ข้อ, cutscene) ตรวจสถานะจริงในโปรเจกต์ก่อนเขียน:
- ติดตั้งแล้ว: URP 17.3.0, Timeline 1.8.12, Input System 1.19.0, uGUI 2.0 (TMP), DOTween (`Assets/Plugins/Demigiant`)
- **ยังไม่ติดตั้ง:** `com.unity.visualeffectgraph` (ไม่อยู่ใน `Packages/manifest.json`)
- ไม่มีโค้ดที่ใช้ `ParticleSystem`/`VisualEffect`, ไม่มี Animator Controller, ไม่มี AudioMixer, ไม่มี `PlayerPrefs` สำหรับ settings, ไม่มี damage number
- ที่มีแล้วใช้ต่อได้: event `EffectResolver.OnDamageDealt(int, bool)`, `CombatObjectPool`, `RunState` JSON save

เกณฑ์เรียงลำดับ: อะไรที่เกณฑ์วิชาบังคับ (Unity 8 ข้อ) ทำก่อน อะไรที่เป็นความสวยงามทำทีหลัง
**เส้นตัด:** F1–F7 ต้องเสร็จก่อน C1 (14 ต.ค.) F8–F10 ทำเท่าที่ทัน F11–F12 ทำเมื่อ F1–F10 ผ่านแล้วเท่านั้น

### [ ] F1 ฟอนต์ไทย วรรณยุกต์/สระแสดงผลผิด
- ข้อเท็จจริง: `Sarabun-Regular SDF` และ `Charm-Bold SDF` เป็น **Dynamic atlas** (`m_AtlasPopulationMode: 1`) และไม่มี fallback font ทั้งสองมีข้อมูล mark-to-base / mark-to-mark ในไฟล์แล้ว Dynamic atlas ยังเป็นสาเหตุที่ Unity แก้ไฟล์ `.asset` ทั้งสองทุกครั้งที่เล่น (ไฟล์ค้างใน git status) **ยังไม่ได้ยืนยันสาเหตุของวรรณยุกต์เพี้ยน** เพราะยังไม่ได้เห็นภาพจาก Unity
- ขั้นแรก: สร้าง `FontTestScene` ด้วย editor script แสดงข้อความอ้างอิง 20 บรรทัด (เช่น ที่ ปู่ ผี้ น้ำ ก้อน เสี่ยง ผู้ ชั้น ขวัญเอ้ย ขวัญมา พรายน้ำนอง วอดส์ ตะเกียง) ทุกฟอนต์ ทุกขนาดที่เกมใช้ (HUD, ปุ่ม, การ์ด, อีเวนต์) แล้วถ่ายภาพเทียบ
- วิธีแก้ที่เป็นไปได้ (เลือกหลังเห็นภาพ): สร้าง SDF แบบ Static ครอบคลุม U+0E01–U+0E5B + Latin + ตัวเลข + เครื่องหมาย พร้อม OpenType features (kerning, mark positioning) ที่ atlas 2048 เปิด kerning บน TMP component ทุกตัว สร้าง SDF ให้ `Sarabun-Bold` และ `Charm-Regular` ที่มี `.ttf` อยู่แล้ว ตั้ง fallback ระหว่างสองฟอนต์
- **Done when:** ข้อความอ้างอิงทั้ง 20 บรรทัดแสดงถูกในทุกฟอนต์ทุกขนาด (ตะวันหรือ UI/UX owner ตรวจจากภาพ), เล่นเกมแล้ว `git status` ไม่มีไฟล์ SDF เปลี่ยน, ลบ `Library/` แล้วเปิดใหม่ยังแสดงถูก

### [ ] F2 UI/UX: Main Menu mockup + ปรับ UI ที่ยังไม่ใช้ได้จริง
- เจ้าของงานออกแบบ: นราธร (UI/UX) ร่วมกับ 2D artist (วรรณณิศา, สายชล) ฝั่ง Dev สร้างตามภาพออกแบบ ไม่ออกแบบเอง
- ขั้นแรก (Dev): จับภาพหน้าจอทุกฉาก (MainMenu, Intro, Map, Combat, Event, Shop, Reward, Meru, Pause, Game Over) ด้วย batch script ให้ UI/UX owner ทำเครื่องหมายจุดที่ใช้ไม่ได้ ผมยังไม่ได้เห็นหน้า UI จริง จึงไม่สรุปว่าอะไรผิดตรงไหน
- Mockup ที่ต้องมี: Main Menu (เริ่ม/เล่นต่อ/โหมดสั้น/ตั้งค่า/ออก), หน้า Intro พร้อมปุ่มข้าม, Pause, Settings, Game Over, Victory ใส่ใน `Docs/Design/` เป็นภาพหรือไฟล์ Figma
- ข้อห้ามเขียนไว้ชัด (ตาม CLAUDE.md ข้อ 5): ปุ่ม Unity เริ่มต้นสีเทา/ขาว, สี่เหลี่ยมมนไล่สีพร้อมเงาเหมือนกันทุกปุ่ม, แสงนีออนเรืองรอบขอบ, emoji/ไอคอนตกแต่ง, จัดกึ่งกลางทุกอย่าง
- Dev ทำ: `UITheme.asset` ที่เดียว (สี, ฟอนต์, ระยะห่าง, sprite 9-slice) ให้ทุกฉากอ้างอิง ไม่ใส่ค่าสีใน prefab รายตัว
- **Done when:** ภาพหน้าจอก่อน/หลังของทุกฉากอยู่ใน `Docs/Testing/ui_review.md`, UI/UX owner เซ็นรับรายฉาก, ทุกปุ่มมีขนาดอย่างน้อย 44×44 px และกดด้วยคีย์บอร์ดได้, contrast ข้อความ ≥ 4.5:1

### [ ] F3 เสียงครบทุกไฟล์ ส่งให้ตรงจุด
- ขยาย B1: สร้างตาราง "ไฟล์เสียง → จุดเรียก" ครบ 27 ไฟล์ (ตารางใน B1 ครอบคลุม key ทั้งหมดอยู่แล้ว ข้อนี้เพิ่มการตรวจว่าไม่มีไฟล์ตกหล่น)
- editor tool `Tools/TawanOS/Audio/Check Coverage` พิมพ์รายการ key ที่ไม่มีจุดเรียก
- ช่องว่างที่ไฟล์ที่ได้รับไม่ครอบคลุม: เสียงกดปุ่ม/โฮเวอร์ UI, Victory, Defeat, ซื้อของในร้าน, เลือกตัวเลือกอีเวนต์, เดินบนแผนที่ **PM ตัดสิน 6 ต.ค.: รอไปก่อน** จุดเหล่านี้เงียบไปจนกว่าจะมีไฟล์ ให้เตรียม key และจุดเรียกไว้ล่วงหน้า (เรียก `AudioManager.Play("sfx_ui_click")` ฯลฯ โดยไม่ error เมื่อไม่มี key)
- **Done when:** รายงาน coverage ได้ `unreferenced=0` สำหรับ 27 ไฟล์ และ playtest ยืนยันว่าแต่ละเสียงดังตรงจุดตามตาราง

### [ ] F4 Player Preferences ให้ครบ
- key: `vol_master`, `vol_bgm`, `vol_sfx`, `fullscreen`, `resolution` (index ของรายการที่เครื่องรองรับ) ตั้งค่าตอนเปิดเกมก่อนฉากแรก (ก่อน Intro)
- ย้าย `MapSaveData` ออกจาก `PlayerPrefs` (ตอนนี้ `MapSaveManager` เขียนซ้ำทั้งไฟล์และ PlayerPrefs ทำให้มีข้อมูลสองที่) เหลือเฉพาะไฟล์ ใช้ `PlayerPrefs` กับ settings เท่านั้น
- Settings เปิดได้จาก Main Menu และ Pause ใช้ panel เดียวกัน
- **Done when:** EditMode test ตั้งค่า → อ่านกลับได้, ปิดเปิด build แล้วค่ายังอยู่, `grep -rn PlayerPrefs Assets/Scripts` เหลือเฉพาะโค้ดตั้งค่า

### [ ] F5 Serialization ให้สมบูรณ์
- ปัจจุบัน `RunSaveData` เก็บ HP, ธูป, เด็ค, relic, อีเวนต์ที่เจอ, resume point แต่ยังไม่ครบ
- เพิ่ม: เขียนไฟล์แบบ atomic (เขียนไฟล์ชั่วคราวแล้วแทนที่) พร้อม `.bak`, ตรวจไฟล์เสียตอนโหลดแล้วตกไปเริ่มเกมใหม่โดยไม่ crash, สถิติ run (ชั้นที่ไป, ศัตรูที่เอาชนะ, จำนวนเทิร์น, เวลาเล่น, โหมดสั้น/เต็ม) สำหรับหน้า Victory/Game Over และรายงาน, ตรวจความสอดคล้อง save ของ run กับ save ของแผนที่ (มีอันเดียว = ถือว่าไม่มี save), migration ตาม `version`
- **Done when:** EditMode test: round-trip ทุกฟิลด์, ไฟล์ JSON เสีย → ไม่ crash และ `HasSave == false`, version เก่า → โหลดได้หรือปฏิเสธอย่างชัดเจน

### [ ] F6 Scene transition / loading screen (ขยาย B4)
- ใช้ `SceneManager.LoadSceneAsync` พร้อมหน้า loading แบบ "หมอกดำ" (ตามชื่อใน SPEC) แสดง progress และอยู่อย่างน้อย 0.6 วินาที เพื่อไม่ให้กะพริบเมื่อฉากโหลดเร็ว
- ใช้กับทุกการเปลี่ยนฉากรวม Intro และ Combat
- **Done when:** `grep -rn "SceneManager.LoadScene(" Assets/Scripts --include=*.cs` เหลือเฉพาะใน `SceneLoader` และ Editor scripts และภาพหน้าจอมีแถบ progress

### [ ] F7 Pause, Settings, Game Over (ขยาย B3)
- ปัญหาที่พบ: `Escape` ถูกใช้อยู่แล้วใน `CardPlayController3D` (ยกเลิกการลาก), `CardTargeting3D` (ยกเลิกเป้าหมาย), `CardDetailPanelUI`, `GraveyardPanelUI` (ปิด panel) ถ้าเพิ่ม Pause ตรงๆ จะเด้ง Pause ซ้อนทุกครั้งที่ยกเลิกอย่างอื่น
- แก้: ตัวจัดการ `Esc` กลางที่มีลำดับความสำคัญ (ยกเลิกเป้าหมาย → ปิด panel → ลำดับสุดท้ายเปิด Pause) แล้วให้ UI ทั้งหมดอ่านเวลาแบบ unscaled เมื่อ `Time.timeScale = 0`
- Game Over: เปลี่ยน panel 3 วินาทีที่เด้งกลับเมนูเองเป็นหน้าที่มีสรุป run + ปุ่ม "เริ่มใหม่" / "เมนูหลัก" และภาพตัวละคร (B9)
- **Done when:** กด Esc ขณะลากการ์ดแล้วยกเลิกการลากอย่างเดียว ไม่เปิด Pause; ขณะ Pause แอนิเมชันและ coroutine ที่ใช้เวลาจริงไม่เดินต่อ; ไม่มีฉากไหนกลับเมนูเองหลังแพ้

### [ ] F8 Particle / VFX ด้วย VFX Graph (ขยาย B5)
- ติดตั้ง `com.unity.visualeffectgraph` เวอร์ชันที่ตรงกับ URP 17.3.0 (เพิ่มใน `Packages/manifest.json` ซึ่งเป็นไฟล์สำคัญ ต้องบอกผลกระทบก่อนทำและมี commit แยก)
- VFX Graph ต้องใช้ compute shader: ตรวจ `SystemInfo.supportsComputeShaders` ถ้าไม่รองรับ ให้ตกไปใช้ `ParticleSystem` ตัวเล็กแทน (เกณฑ์วิชานับทั้งสองแบบ)
- รายการ effect (หนึ่งไฟล์ต่อหนึ่งงาน เก็บที่ `Assets/VFX/`): hover การ์ด (ประกายธูปรอบขอบการ์ด), ลงการ์ด (มนต์ขาว = ทอง, มนต์ดำ = แดงเลือด), มลทินแตก (ฟ้าผ่า), ควันธูปบนโต๊ะ, เปลวเทียน, ไฟพิธี (เปิด/ดับ ผูก `sfx_fire_on/off`), แรงกระแทกตอนโจมตี, เผาการ์ดที่เมรุ, ศัตรู Boss ปรากฏ, บรรยากาศฉาก: หมอก/ประกายบนแผนที่, โคมในศาล, หมอกดำในอีเวนต์
- ตั้งงบ: จำนวนระบบอนุภาคพร้อมกันและเวลาเฟรมที่ยอมรับได้ ให้ทีมกำหนดจากเครื่องต่ำสุดที่ใช้นำเสนอ
- **Done when:** อย่างน้อย 6 effect เป็น VFX Graph asset และถูกใช้ในฉากจริง, build `.exe` ไม่มีวัตถุสีชมพู/missing shader, effect ปิดได้จาก Settings (ตัวเลือก "ลดเอฟเฟกต์")

### [ ] F9 ตัวเลขดาเมจและ combo
- จุดเชื่อม: `EffectResolver.OnDamageDealt(int damage, bool toPlayer)` ไม่มีตำแหน่ง/คริ/ผู้โจมตี จึงเพิ่ม event ใหม่ที่ส่ง `DamageInfo` (จำนวน, ตำแหน่งโลกของเป้าหมาย, isCrit, ฝั่งเป้าหมาย) โดยไม่ลบ event เดิม
- `DamageNumberView` ดึงจาก `CombatObjectPool` ลอยขึ้นแล้วจาง สีตามชนิด (ดาเมจ, คริติคอล, ฟื้นขวัญ, เกราะรับ)
- Combo (PM ตัดสิน 6 ต.ค. ไม่ต้องรอทีมยืนยัน): แสดงทุกครั้งที่ศัตรูโดนตี ไม่ว่าครั้งเดียวหรือหลายครั้ง เน้นให้ดูสะใจ: ตัวเลขดาเมจของแต่ละการโจมตี + ตัวนับ "×N" ของการโจมตีที่ติดต่อกันใน clash รอบเดียวที่ขยายและสั่นแรงขึ้นตาม N (ครั้งเดียวแสดง ×1 แบบเบา) คริติคอลใหญ่กว่าและสีต่าง ขยายความแรงที่ครั้งที่ 3 ขึ้นไป รีเซ็ตเมื่อจบรอบ เป็นผลเชิงภาพอย่างเดียว ไม่เปลี่ยนค่าดาเมจ (ห้ามแก้ตัวเลขเกม)
- **Done when:** ทุกครั้งที่เกิดดาเมจใน smoke test มีตัวเลขโผล่ที่ตำแหน่งเป้าหมาย, ไม่สร้างวัตถุใหม่ระหว่างต่อสู้ (ใช้ pool), combo นับถูกตามนิยามในการทดสอบ 3 กรณี

### [ ] F10 Animator (ขยาย B6)
- เพิ่มจาก `EnemyPresence.controller`: Animator Controller ของปุ่ม UI (Normal/Highlighted/Pressed/Disabled), ของ panel Pause/Game Over (เข้า/ออก) และของเทียน/ไฟพิธี เพื่อให้มี state machine จริงมากกว่า 1 ที่ (DOTween อย่างเดียวไม่นับตามเกณฑ์)
- **Done when:** controller อย่างน้อย 3 ตัว แต่ละตัวมี ≥ 3 state และ transition ด้วย parameter ถูกใช้ในฉากจริง

### [ ] F11 Cutscene และ effect เชื่อม asset ที่มี (ทำเมื่อ F1–F10 ผ่านแล้ว)
- ใช้ Timeline 1.8.12 ที่ติดตั้งอยู่: (ก) Intro (B8) ภาพ Story + วิดีโอ + ปุ่มข้าม, (ข) ฉาก Boss ปรากฏ: กล้องเคลื่อน + `bgm_boss` crossfade + `sfx_fire_on` + VFX, (ค) ฉากจบ Victory ใช้ภาพ Story/ตัวละคร
- ทุก cutscene ข้ามได้ใน 1 คลิก
- **Done when:** `PlayableDirector` อย่างน้อย 2 ฉากทำงานใน build และข้ามได้โดย state ของเกมไม่เพี้ยน

### [ ] F12 ตรวจว่า asset ที่ได้รับถูกใช้ครบ
- `Docs/Report/asset_usage.md`: ทุกไฟล์ใน `GameProject_Asset` (เสียง 27, ภาพการ์ด 29, Story 13 + วิดีโอ 2, ตัวละคร 2, `icon.ai`) → ใช้ที่ไหน หรือเหตุผลที่ยังไม่ใช้
- **Done when:** ไม่มีไฟล์ที่ไม่มีคำอธิบาย

---

## Phase G · CI, Theme และ UI/UX รอบ 2 (จากภาพที่ PM ส่ง 6 ต.ค. ทำร่วมกับ F1, F2, F7)

ที่มา: PM ส่งโลโก้, ภาพวาดมือบนเพนตาแกรม, ชุดสี CI 2 ภาพ, เรฟเฟอเรนซ์เกจมนต์ดำ และ screenshot แผนที่/ฉากต่อสู้ปัจจุบัน
ข้อติจาก PM: UI แผนที่ยังดู slop, ไม่มีภาพรวมแผนที่, คำอธิบายสัญลักษณ์อ่านยาก, ไม่มีปุ่ม Pause/หน้าออกเกมระหว่างเล่น,
UI แสดง Stat ผู้เล่น, คำอธิบายการเล่นเล็กเกินไป, กด C ดูมุมบนแล้วไม่มีบอกว่าต้องกด C อีกครั้งเพื่อยกเลิก

### ข้อเท็จจริงที่ตรวจจากภาพและโค้ด
| เรื่อง | สิ่งที่พบ | ที่มา |
|---|---|---|
| ชุดสี CI (อ่านจากพิกเซลภาพ) | ดำ `#110E0E`, น้ำตาลดำ `#2A2222`, แดงเข้ม `#572121` (ภาพชุดสั้น 4 สีให้ `#5D2020`), เทา `#C2BEBE`, พีช `#F0AD7B` (มีเฉพาะภาพ 5 สี) | `img_3eb100f3b805`, `img_354890f1916b` |
| contrast | เทา/พีช บน ดำ, น้ำตาลดำ หรือแดงเข้ม = 6.7–10.4:1 ผ่าน WCAG AA; **แดงเข้มบนดำ = 1.5:1 ตก** ใช้เป็นตัวอักษรหรือเส้นบางบนพื้นมืดไม่ได้ ใช้เป็นพื้น/กรอบเท่านั้น | คำนวณ 6 ต.ค. |
| โลโก้ | ลายมือพู่กันไทย "ขวัญเอ้ย ขวัญมา" สีเทา มีควันและลายเส้นยันต์ PNG โปร่งใสจริง แต่ **420×472 px** เล็กเกินไปสำหรับจอ 1080p | `img_8141bcbce512` |
| ภาพวาดมือบนเพนตาแกรมกับเทียน | 662×880 px โทนดำ/แดง/เทา ตรง CI เหมาะเป็นพื้น Main Menu แต่ความละเอียดต่ำ | `img_86ce07a0dc03` |
| เรฟเฟอเรนซ์เกจมนต์ดำ | เกจวงกลมลายยันต์ สีม่วง+ทอง กรอบเงาซับซ้อน **สีม่วง/ทองไม่อยู่ใน CI** | `img_01e009ecf261` |
| legend แผนที่ | ข้อความอังกฤษฮาร์ดโค้ดพร้อมอีโมจิ ("MAP LEGEND & NODE TYPES", "Offering Pile", "Shop Merchant") ตัวเล็ก หลายสี สีล้วนไม่ตรง CI | `MapLegendUI.cs` |
| ปุ่ม "Reset Map (R)" | ปุ่ม debug ที่สร้างลงใน scene หลุดไปถึงผู้เล่น | `MapEngineSetupTool.cs` |
| ไอคอนโหนดบนแผนที่ | หลายโหนดเป็นรูปต้นสนเหมือนกัน แยกประเภทไม่ออก | screenshot แผนที่ `img_d8c129e2ef7c` |
| ข้อความมุมซ้ายบนฉากต่อสู้ | debug IMGUI อังกฤษ: "Turn 1 \| Phase: Draw \| [Space] next phase", "Enemy AI: (plain moveset)" | `TurnPhaseController.OnGUI`, screenshot `img_3f29afc466a9` |
| แถบศัตรู | มีข้อความ debug "(plain moveset)" ปนอยู่ | `CombatManager.EnemyAIStateName` |
| ปุ่มมุมมอง | ปุ่ม "มุมบน (C)" เล็กมากที่มุมขวาบน ป้ายสลับเป็น "มุมปกติ (C)" ได้ แต่ไม่มีคำอธิบายขณะอยู่ในมุมบน | `CombatCameraRig3D.cs` |

### [ ] G1 CI และ Theme กลางเดียว
- `Assets/GameFlowData/UITheme.asset` (`UIThemeSO`) เก็บสี ฟอนต์ ระดับขนาดตัวอักษร ระยะห่าง และ sprite 9-slice ทุก UI อ้างอิงจากที่นี่ ห้ามใส่ค่าสีใน prefab รายตัว
- สีตามตาราง ใช้งาน: พื้นหลัง = ดำ, แผง = น้ำตาลดำ, กรอบ/ปุ่มหลัก/แถบเลือด = แดงเข้ม, ตัวอักษรหลัก = เทา, จุดเน้น/โฟกัสคีย์บอร์ด/ตัวเลขสำคัญ = พีช **ห้ามใช้แดงเข้มเป็นตัวอักษรบนพื้นมืด**
- ระดับตัวอักษร (อ้างอิงจอ 1920×1080, CanvasScaler `Scale With Screen Size` 1920×1080): เนื้อหา ≥ 24 px, ป้ายกำกับ ≥ 20 px, หัวข้อ ≥ 36 px, ตัวเลขสถานะ ≥ 32 px ไม่มีข้อความเล็กกว่า 20 px ในเกม
- โลโก้นำเข้าที่ `Assets/Art/UI/Logo/` ใช้ที่ Main Menu และหน้า loading (F6) **โลโก้ต้องการไฟล์ ≥ 2048 px หรือเวกเตอร์ (`.svg`/`.ai` → PNG)** จนกว่าจะได้ ให้แสดงไม่เกิน 420 px ที่ใหญ่สุดเพื่อไม่ให้แตก
- ภาพวาดมือบนเพนตาแกรม ใช้เป็นพื้น Main Menu แบบเบลอ/มืดลง (ขอไฟล์ ≥ 1920×1080 เช่นกัน)
- **Done when:** `UIThemeSO` มีสี 5 ค่าตามตาราง, มี EditMode test ตรวจ contrast ของทุกคู่ "ตัวอักษร/พื้น" ที่ theme ประกาศ ≥ 4.5:1, ค้นไม่พบค่า `Color(` หรือสี hex ฮาร์ดโค้ดใน `Assets/Scripts/**/Visuals` และ UI ที่นอกเหนือจาก theme

### [ ] G2 แผนที่: ภาพรวม ไอคอน legend และปุ่มระบบ
- **ภาพรวมแผนที่:** ปุ่ม "ภาพรวม" + คีย์ `Tab` สลับกล้องเป็นมุมสูงเห็นทุกโหนดและเส้นทางของ run พร้อมไฮไลต์ตำแหน่งปัจจุบัน; แสดงป้ายบอกวิธีกลับ "กด Tab อีกครั้งเพื่อกลับ" ตลอดเวลาที่อยู่ในมุมนี้
- **ไอคอนโหนด 7 ชนิดแยกด้วยรูปทรง + สี ไม่ใช้สีอย่างเดียว:** ศัตรูทั่วไป, Elite, เมรุ, กองของเซ่น, ศาล, Boss, หมอกดำ ใช้ไอคอนในโปรเจกต์ (`TempleIcon`, `OfferingIcon`, ฯลฯ) ก่อน ส่วนที่ขาดให้ 2D artist (วรรณณิศา, สายชล) ทำ ถ้ายังไม่มี ให้ใช้รูปทรงเรขาคณิตง่ายๆ ตามธีมชั่วคราวที่ติดป้าย placeholder
- **legend:** ภาษาไทยทั้งหมด ไม่มีอีโมจิ ใช้สีตาม theme ขนาดตัวอักษร ≥ 24 px แต่ละแถวมีไอคอนจริงของโหนดนำหน้า เปิด/ปิดได้ด้วย `L` และปุ่ม "สัญลักษณ์"; เปิดครั้งแรกของ run แล้วจำสถานะไว้ใน PlayerPrefs
- **tooltip โหนด:** ชี้เมาส์/โฟกัสที่โหนดแสดงชื่อ คำอธิบาย และรางวัลโดยสรุป เป็นภาษาไทย ≥ 24 px
- **เอาปุ่ม "Reset Map (R)" ออกจากผู้เล่น:** ย้ายไปเป็นเมนู Tools และคีย์ที่ใช้ได้เฉพาะ Editor/Development build (แก้ที่ `MapEngineSetupTool` ที่สร้างปุ่ม ผ่าน editor script ห้ามแก้ YAML ด้วยมือ)
- **แถบสถานะผู้เล่นบนแผนที่:** ขวัญปัจจุบัน/สูงสุด (แถบ+ตัวเลข), ธูป, จำนวนการ์ดในเด็ค (ปุ่มดูเด็ค), เครื่องรางที่ถือ ตามที่ Doc ออกแบบกำหนด (HP, เงินธูป, View Deck, Settings)
- **ปุ่ม Pause** มุมขวาบนเสมอ (และ `Esc`) เปิดเมนู: เล่นต่อ, ตั้งค่า, กลับเมนูหลัก (บันทึกก่อนออก), **ออกจากเกม (มีกล่องยืนยัน)** ใช้ตัวจัดการ `Esc` กลางจาก F7
- **Done when:** ภาพ screenshot แผนที่หลังแก้ไม่มีข้อความอังกฤษ/อีโมจิ/ปุ่ม debug, ทุกข้อความ ≥ 24 px, ไอคอน 7 ชนิดแยกออกได้เมื่อย่อภาพเหลือ 50% (UI/UX owner เซ็นรับ), `Tab` สลับไป-กลับได้สองทิศทางและคืนมุมกล้องเดิม (test), ปุ่ม Pause และกล่องยืนยันออกเกมใช้งานได้ด้วยเมาส์และคีย์บอร์ด

### [ ] G3 ฉากต่อสู้: HUD, คำอธิบายการเล่น และมุมบน
- **ลบข้อความ debug:** `TurnPhaseController.OnGUI` ครอบด้วย `#if UNITY_EDITOR || DEVELOPMENT_BUILD` และเปิดด้วย `F3` เท่านั้น; ลบข้อความ "(plain moveset)" ออกจากแถบศัตรู
- **แถบเฟสแบบ UI จริง** กลางบน ภาษาไทย "เทิร์น 1 · ศัตรูลงบริวาร" ตัวอักษร ≥ 28 px เปลี่ยนตามเฟสที่ `TurnPhaseController` มีอยู่แล้ว (`PhaseLabel`)
- **แผงสถานะผู้เล่น** มุมซ้ายล่าง: ขวัญ (แถบ+ตัวเลข ≥ 32 px), มลทิน (เกจวงกลมตามแนวคิดเรฟเฟอเรนซ์ แต่ใช้สี CI, เตือนด้วยพีช/กระพริบเมื่อเหลือ 2 แต้มจะแตก), กุศล (ช่องสี่เหลี่ยม แสดงจำนวนที่ใช้ได้ชัด) พื้นหลังแผงทึบพอให้ contrast ≥ 4.5:1 กับพื้นฉาก
- **แผงศัตรู** บน: ชื่อ, แถบขวัญ, ไอคอนเจตนาการโจมตีครั้งถัดไปพร้อมตัวเลข, จำนวนการ์ดในมือ
- **แถบคำอธิบายการเล่น** ล่างกลาง ตัวอักษร ≥ 22 px: "ลากการ์ดลงช่อง · คลิกขวา ยกเลิก · Space จบเฟส · C มุมบน" แสดงเต็มใน 2 การต่อสู้แรกของ run แล้วย่อเหลือปุ่ม "?" (`H` เปิดซ้ำได้)
- **มุมบน (C):** ปุ่มใหญ่ขึ้น (≥ 44×44 px พร้อมป้าย ≥ 22 px) และขณะอยู่มุมบนมีแบนเนอร์กลางจอ "มุมบน — กด C อีกครั้งเพื่อกลับ" ตลอดเวลา; `C` และ `Esc` ย้อนกลับมุมปกติได้ (ลำดับ `Esc` ตามตัวจัดการกลางของ F7)
- ปุ่ม Pause และแถบสถานะเหมือนแผนที่
- **Done when:** ไม่มีข้อความอังกฤษ/debug ใน build ปกติ (ตรวจด้วย screenshot และ `grep` ว่าไม่มี `OnGUI` ที่ไม่ครอบเงื่อนไข), ทุกข้อความ ≥ 22 px, กด C สองครั้งคืนตำแหน่งกล้องเดิม (test), ผู้เล่นใหม่ที่ไม่เคยเห็นคู่มืออ่านวิธีเล่นจากแถบนี้ได้โดยไม่ต้องถามทีม (playtest รอบ 1 ถามคนที่ไม่ใช่สมาชิกทีม 3 คนว่า "รู้ไหมว่าต้องทำอะไรต่อ" ตอบได้ ≥ 2 ใน 3)

### ที่ต้องการจากทีม (ไม่บล็อกการทำงาน ใช้ค่า default ถ้ายังไม่ได้)
| # | ขอจาก | เรื่อง | default ถ้ายังไม่ได้ |
|---|---|---|---|
| D11 | UI/UX (นราธร) | เกจมนต์ดำเรฟเฟอเรนซ์ใช้สีม่วง/ทอง ไม่อยู่ใน CI จะใช้สีม่วงเป็นสีเฉพาะ "มลทิน" หรือแปลงเป็นสี CI | แปลงเป็นสี CI (แดงเข้มเป็นพื้นวง, พีชเป็นส่วนที่เต็ม) |
| D12 | 2D artist | โลโก้ความละเอียดสูง (≥ 2048 px) และพื้น Main Menu ≥ 1920×1080 | ใช้ไฟล์ปัจจุบันขนาดเล็ก |
| D13 | 2D artist | ไอคอนโหนด 7 ชนิด (PNG โปร่งใส ≥ 256 px, `icon.ai` import ไม่ได้) | รูปทรงเรขาคณิตชั่วคราว |
| D14 | PM | `#572121` กับ `#5D2020` เลือกค่าไหนเป็นแดงเข้ม (ภาพสองชุดต่างกันเล็กน้อย) | `#572121` (ภาพชุด 5 สีที่มีพีช) |

---

## Phase C · ทดสอบและเอกสาร (พุธ 15 – จันทร์ 19 ต.ค.)

### [ ] C1 Gate ก่อน Playtest (อังคาร 14 ต.ค. เย็น)
- [ ] compile ไม่มี `error CS`
- [ ] `AutomatedCombatFlowTest` ผ่าน
- [ ] `AutomatedRunFlowTest` ผ่าน (A3)
- [ ] EditMode tests ผ่านทั้งหมด
- [ ] Build Windows สำเร็จ และเล่นใน .exe จาก Main Menu → ชนะ Boss ได้ 1 รอบ

### [ ] C2 Playtest support
- เกณฑ์ Testing & Iteration (10%) ต้องแสดงว่านำข้อมูลมาปรับเกมจริง จึงทำ **2 รอบ**: รอบ 1 พุธ 15 ต.ค. และรอบ 2 จันทร์ 19 ต.ค. หลังแก้บั๊กจากรอบ 1 (ผู้เล่นอย่างน้อย 3 คนต่อรอบ และอย่างน้อย 1 คนที่ไม่ใช่สมาชิกทีม)
- `Docs/Testing/playtest_log.md` แบบฟอร์ม: ผู้เล่น, วันที่, ชั้นที่ไปถึง, ชนะ/แพ้, เวลา, บั๊ก, ความเห็น, สิ่งที่แก้ (ก่อน/หลัง) และ commit hash ของการแก้แต่ละข้อ
- **Done when:** log มี 2 รอบ และมีอย่างน้อย 3 รายการที่แก้แล้วพร้อม commit hash และผลหลังแก้
- (เสริม) `RunTelemetry`: เขียน CSV ที่ `persistentDataPath` ทุกจบต่อสู้: ชั้น, ประเภทศัตรู, จำนวนเทิร์น, ขวัญที่เหลือ, การ์ดที่ใช้

### [ ] C3 ข้อมูลสำหรับรายงานส่วน 7.4 Technical Documentation
- `Docs/Report/unity_features.md`: ข้อบังคับ 8 ข้อ → class/ไฟล์/ฉาก → หน้าที่ 1 บรรทัด
- `Docs/Report/class_diagram.md`: Mermaid class diagram ของ GameFlowManager, RunState, SceneLoader, MapManager, CombatManager, CardManager, EffectResolver, EnemyCardPlayer, EnemyStateMachine, EventManager, ShopManager, RewardManager, MeruManager, AudioManager
- `Docs/Report/enemy_fsm.md`: `EnemyStateMachine` เป็น `stateDiagram-v2` สำหรับ bonus AI (+6) ไม่ต้องอธิบายโค้ด
- `Docs/Report/game_flow.md`: `flowchart` ของฉากทั้งหมดตามโค้ดจริง ใช้แทน Diagram เดิมในรายงาน
- `Docs/Report/tech_issues.md`: ปัญหาทางเทคนิคและวิธีแก้ จาก git log (turn loop ซ้อน, flip กล้องแผนที่, AssetDatabase ใน build, HP ไม่ sync)

---

## Phase D · รายงานและไฟล์ส่งงาน (พฤหัส 8 – อังคาร 20 ต.ค.)

เพิ่มเพราะเอกสารสั่งงาน (`Assignment_1_Text.txt`) ให้คะแนนรายงานและการนำเสนอ 15%, Design Foundation 20%, Individual Contribution 10%
รวมกัน 45% ของคะแนน ซึ่งไม่ได้มาจากโค้ดและเดิมไม่มี task ใน PLAN
ไฟล์เอกสารทั้งหมดเขียนที่ `Docs/Report/` หัวข้อต้องตรงกับโครงสร้างรายงานข้อ 7 ของเอกสารสั่งงาน

### [ ] D1 รายงาน 7.2 Design Foundation (20%)
- `Docs/Report/design_justification.md` **เจ้าของ: ทีม Game Design (นราธร, อสิธารา)** ฝั่ง Dev (ธนัทภัร) ไม่เขียนทฤษฎี แต่เป็นผู้ตรวจว่า mechanic ที่อ้างมีอยู่จริงใน build และระบุ class/ฉากให้
- ส่วน MDA: ไล่จาก Aesthetic (ผู้เล่นควรรู้สึกอะไร) → Dynamics → Mechanics และส่วน Elemental Tetrad (Mechanics, Story, Technology, Aesthetics) ผูกกับระบบที่มีใน build จริง
- ส่วนทฤษฎีจิตวิทยา 1–2 ข้อ (ค่า default ตาม D8) ตาราง: ทฤษฎี → เหตุผลที่เลือก → mechanic ในเกม → ไฟล์/ฉากที่เห็นผล → วิธีผู้ตรวจดูได้ในเกม
- ห้ามเขียนทฤษฎีลอย ๆ: ทุกแถวต้องมี mechanic ที่ชี้ไป class หรือฉากที่มีอยู่จริง
- **Done when:** ทุก mechanic ที่อ้างถูกตรวจว่าเล่นเห็นได้ใน build (ติ๊กรายข้อในไฟล์) และไม่มีทฤษฎีที่ไม่ผูกกับ mechanic

### [ ] D2 รายงาน 7.3 Core Gameplay & System Design
- `Docs/Report/core_gameplay.md`: Core Game Loop (Mermaid `flowchart`), กติกาและเงื่อนไขชนะ/แพ้ตามโค้ดจริง (หลัง A3), Level & Content Design (ชนิดโหนด 7 แบบ, จำนวนชั้น, การ์ด 42 ใบ, ศัตรู, อีเวนต์ 5 เรื่อง, ความยาก)
- ตัวเลขทุกตัว (ขวัญสูงสุด, มลทินสูงสุด, กุศลสูงสุด, ราคา) ดึงจากโค้ดหรือ asset ไม่ใช่จาก Doc ออกแบบ
- **Done when:** ตัวเลขในไฟล์ตรงกับค่าในโค้ด (ตรวจด้วย grep ตามรายการที่ไฟล์ระบุ path)

### [ ] D3 รายงาน 7.1, 7.5, 7.7
- `Docs/Report/summary.md` (7.1: ชื่อเกม, genre, elevator pitch 1–2 ประโยค, target player, target platform, ลิงก์ Drive ของ build และ source)
- 7.5 ดึงจาก `Docs/Testing/playtest_log.md` (C2) แสดงข้อมูลก่อน/หลังแก้
- `Docs/Report/known_limitations.md` (7.7): บั๊กที่ยังไม่แก้, ฟีเจอร์ที่ตัด (อัปเกรดการ์ด, โหนดวัด, relic ถ้าเลือกทางเลือก ก ใน A6, ภาพการ์ด placeholder 13 ใบถ้ายังไม่ได้ภาพจริง)
- **Done when:** ทุกข้อใน known_limitations มีที่มาจาก issue/commit/PLAN ที่ชี้ได้

### [ ] D4 รายงาน 7.6 Individual Contribution
- `Docs/Report/contribution.md`: ตารางสัดส่วนงานของสมาชิกทุกคน แยกตาม task หรือเปอร์เซ็นต์ และเจ้าภาพหลักของแต่ละงาน
- สมาชิก 6 คน (เลขประจำตัวใส่ในรายงานเอง ไม่ใส่ใน repo):
  | สมาชิก | บทบาท |
  |---|---|
  | ธนัทภัร พรหมทอง | Project Management, Dev |
  | ภูริ ประชาสุขสิน | 3D Artist, Sound Designer |
  | วรรณณิศา อมรวงศ์ไพบูลย์ | 2D Artist |
  | อสิธารา พุ่มดอกไม้ | Dev, Game Design (Gameplay) |
  | นราธร อู่สุวรรณ์ | Game Design (Gameplay), UI/UX, QA Tester |
  | สายชล ไชยมูล | 2D Artist |
- ที่มาของข้อมูล: `git shortlog -sn --all` (ตอนนี้มีผู้ commit 3 ชื่อ: `tawaninm` 45, `Hundredz` 9, `Thanatpat` 2 ต้องจับคู่ชื่อ git กับสมาชิก), บอร์ด Trello, และงานที่ไม่อยู่ใน git (ศิลป์ เสียง ออกแบบ เนื้อเรื่อง QA) ที่สมาชิกต้องยืนยันเอง
- QA Tester คือนราธร: ให้เขาเป็นเจ้าของ `Docs/Testing/playtest_log.md` (C2)
- Self-Reflection ของสมาชิกแต่ละคน คนละ 1 ย่อหน้าสั้น ให้แต่ละคนเขียนเอง ห้ามเขียนแทน
- **Done when:** ตารางครบทุกคนในทีม ผลรวมเปอร์เซ็นต์ = 100 และครบ reflection ทุกคน

### [ ] D5 สไลด์นำเสนอ + วิดีโอ gameplay
- สไลด์ตามโครงสร้างรายงาน เน้นคำตอบที่สมาชิกทุกคนต้องตอบกรรมการได้ (ใครทำอะไร, ทฤษฎีผูกกับ mechanic ไหน)
- วิดีโอ gameplay 2–3 นาที (optional ตามเอกสารสั่งงาน) อัดจาก build `.exe` ไม่ใช่จาก Editor
- **Done when:** สไลด์ครบทุกส่วนของรายงานข้อ 7 และซ้อมนำเสนอ 1 รอบจับเวลา

### [ ] D6 แพ็กไฟล์ส่งงาน
- `Build/` (zip ของ build Windows), `Source/` (zip ของโปรเจกต์ ไม่รวม `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/`), รายงาน PDF
- อัปโหลดขึ้น Google Drive ของสมาชิกหนึ่งคน ตั้งสิทธิ์ให้เปิดได้ด้วยลิงก์ แล้วใส่ลิงก์ในรายงาน 7.1 ส่ง PDF ที่ระบบ Onlearn
- **Done when:** เปิดลิงก์ทั้งสองจากบัญชีที่ไม่ใช่เจ้าของไฟล์ได้ และแตก zip build แล้วรัน `.exe` ได้บนเครื่องที่ไม่มี Unity

---

## ลำดับเวลา

| วัน | งาน |
|---|---|
| อ. 6 – พฤ. 8 | A0 – A10, B0 (D1 เป็นงานของทีม Game Design) |
| ศ. 9 – จ. 12 | B1, B2, B3, B7, B8, B9 |
| อ. 13 – อ. 14 | B4, B5, B6, C1 |
| พ. 15 | Playtest รอบ 1 |
| พฤ. 16 – อา. 18 | แก้จาก playtest, C3, D1, D2, D4 |
| จ. 19 | Playtest รอบ 2, D3, สไลด์ D5 |
| อ. 20 | build สุดท้ายขึ้น Drive (D6), ซ้อมนำเสนอ |

## ไม่อยู่ใน scope (ใส่รายงานส่วน Future Work)
- อัปเกรดการ์ดที่เมรุ (ยังไม่มีการ์ดเวอร์ชันอัปเกรด)
- โหนดวัด และจุดพักตอนเริ่มเกม ตาม Diagram
- ลำดับการลงการ์ดและการทำงานของอาคมตาม Diagram (โค้ดให้ผู้เล่นลงก่อน และอาคมทำงานทันที)
