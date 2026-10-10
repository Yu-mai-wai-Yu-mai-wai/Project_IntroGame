# การใช้ Asset Final (Phase H)

อัปเดต 10 ต.ค. 2569 ต้นทาง `D:\Tawanagent\GameProject_Asset` ปลายทาง branch `feature/final-assets`

| ต้นทาง | ปลายทางในโปรเจกต์ | ใช้ที่ไหน | สถานะ |
|---|---|---|---|
| `3D\Final_New_Patch\CombatFinal.unitypackage` | `Assets/ProjectAsset/CombatFinal/` (Combat.fbx, 25 material, texture) | `CombatTestScene` ผ่าน `CombatSceneDressingTool` (ชื่อวัตถุ `CombatDressing`) | ใช้แล้ว ตรวจด้วย render ภาพ |
| `3D\Final_New_Patch\NavigateFinal.unitypackage` | `Assets/ProjectAsset/NavigateFinal/` (MapNavigate.fbx, material, texture) | `MapTestScene` วัตถุ `MapNavigateEnvironment` แทนโมเดล Demo | ใช้แล้ว |
| `3D\Final_New_Patch\Scarecrow.unitypackage` | `Assets/ProjectAsset/CombatFinal/ScareCrow.fbx` | หุ่นไล่กาอยู่ใน Combat.fbx แล้ว ไฟล์แยกนี้ยังไม่วางในฉากใด | นำเข้าไว้ ยังไม่ใช้ |
| `Art\Iconโหนด\Final_Icon_node_in_Map\*.png` (7) | `Assets/Art/MapIcons/` | `MapEngineData/Profiles/*Profile.asset` ช่อง `icon` | ใช้แล้ว (จับคู่ตาม D15 ด้านล่าง) |
| `Art\Card Design\**\*.png` (42) | `Assets/Art/Cards/` | `CardDataSO.artwork` ผ่าน `CardArtBinder` | ใช้แล้ว `bound=42 missing=0` |
| `Art\Event_Node_img\ทางเปลี่ยว.PNG` | `Assets/Art/Events/` | `Event_WanderingShaman.illustration` | ใช้แล้ว (D16) |
| `Art\MapPaper\*.PNG` (4) | `Assets/Art/MapPaper/` | ยังไม่ผูก | นำเข้าไว้ รอตัดสินวิธีใช้ (ดูหมายเหตุ) |
| `Detail\Game Design\Cards.html` | ไม่คัดลอก | ตรวจค่าการ์ด 42 ใบเทียบ `CardDataSO` | ค่าตรงทุกใบ ไม่ต้องแก้ |

## การจับคู่ไอคอนโหนด (D15, ค่า default จากชื่อโปรไฟล์ ต้องให้ตะวันหรือศิลปินยืนยัน)

| โปรไฟล์ | NodeType | ไอคอน |
|---|---|---|
| EnemyProfile | MinorEnemy | ป่า |
| EliteProfile | EliteEnemy | วัด |
| BossProfile | Boss | ทางเปลี่ยว |
| EventProfile | Event | ควันดำ |
| RestProfile | RestSite (เมรุ) | เมรุ |
| StoreProfile | Store (ศาล) | ศาล |
| TreasureProfile | Treasure (กองของเซ่น) | ของเซ่น |

## หมายเหตุ

- `MapPaper` เป็นภาพ 1920x1080 ของกระดาษที่ขาดซ้ายหรือขวา: `หัว` (ขอบขาดด้านซ้าย), `เริ่มวนรอบแรก`, `ต่อไปเรื่อยๆ`, `ท้าย` (ขอบขาดด้านขวา) กระดาษใน MapNavigate.fbx (`PaperFloor` + `PaperMapMat`) ใช้ตัวเองอยู่แล้ว จึงยังไม่ผูกจนกว่าจะรู้ว่าต้องต่อกระดาษเป็นช่วงตามจำนวนชั้นหรือไม่
- การ์ดอัปเดต: `cardImage` เดิมของ 5 ใบ (ตะกรุด, ผ้ายันต์, ลูกประคำ, สายสิญจน์, บทแผ่เมตตา) ถูกล้าง เพื่อให้ภาพ Final ใน `artwork` แสดง (หน้ารายละเอียดการ์ดซ่อน `artwork` เมื่อมี `cardImage`)
- ไอคอนโหนดต้นฉบับเป็นภาพตั้ง 596x843 มีช่องว่างรอบตัว ตรวจขนาดบนแผนที่จริงใน Play mode
- โมเดลเดิม (`MapNavigate/`, `CombatDemo/`) ยังอยู่ในโปรเจกต์ ไม่ได้ลบ ลบได้เมื่อยืนยันว่าไม่มีฉากอ้างถึง
