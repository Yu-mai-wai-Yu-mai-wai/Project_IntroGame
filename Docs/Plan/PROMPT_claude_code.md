# Prompt สำหรับ Claude Code (เครื่อง local)

copy ทีละกล่องไปวางใน Claude Code ที่เปิดในโฟลเดอร์ `D:\Unity2026PJ\Project_IntroGame`
ปิด Unity Editor ก่อนเริ่มทุกครั้ง เพราะคำสั่ง batch mode จะชนกับ project lock

## 1. Prompt เริ่มงาน (ใช้ครั้งแรก หรือเมื่อเปิด session ใหม่)

```
อ่าน CLAUDE.md และ Docs/Plan/PLAN_to_21Oct.md ให้ครบก่อน

แล้วทำสิ่งนี้ก่อนแก้โค้ด:
1. git status และ git log -3 แล้วบอกว่าตอนนี้อยู่ branch ไหน มีไฟล์ค้างไหม
2. ตั้ง $UNITY และ $PROJ ตาม CLAUDE.md ตรวจว่า path ของ Unity.exe มีอยู่จริง ถ้าไม่มีให้หาใน "C:\Program Files\Unity\Hub\Editor\" แล้วบอกผม
3. รัน compile check และ AutomatedCombatFlowTest เพื่อเก็บผลตั้งต้น (baseline)
4. ตรวจว่ามีไฟล์ใน Assets/Audio/ และ Assets/Art/Cards/ หรือยัง

รายงานผลเป็นตารางสั้นๆ แล้วรอคำสั่ง ยังไม่ต้องแก้อะไร
```

## 2. Prompt ทำงานทีละ task (เปลี่ยนแค่รหัส task)

```
ทำ task A1 ตาม Docs/Plan/PLAN_to_21Oct.md

ขั้นตอน:
1. อ่านโค้ดที่เกี่ยวข้องก่อน แล้วสรุปแผนการแก้สั้นๆ: ไฟล์ที่จะสร้าง/แก้, class/method ที่เพิ่ม, ผลกระทบกับระบบอื่น
2. ทำตามแผน โดยยึดกฎใน CLAUDE.md (ห้ามใช้ AssetDatabase ใน runtime, ห้ามแก้ตัวเลขเกม, ฉาก/prefab สร้างผ่าน editor script)
3. ตรวจด้วย compile check, AutomatedCombatFlowTest และเงื่อนไข "Done when" ของ task นี้
4. ถ้าผ่านทั้งหมด ติ๊ก [x] ของ task ใน PLAN แล้ว commit หนึ่งครั้ง ข้อความแบบ conventional commit
5. รายงาน: ทำอะไรไป, ผลการตรวจแต่ละข้อ (ใส่ exit code / บรรทัด log ที่ยืนยัน), สิ่งที่ผมต้องเปิด Unity ดูเอง

ถ้าเจอสิ่งที่ต้องตัดสินใจเชิง design (ตัวเลขเกม, ชื่อในเกม, ตัดฟีเจอร์) หรือ Done when ตรวจไม่ผ่านหลังลอง 2 รอบ ให้หยุดแล้วถามผม ห้ามเดา
```

ลำดับที่แนะนำ: `A0 → A1 → A2 → A3 → A4 → A5 → A6 → A7` แล้วค่อย `B1 → B2 → B3 → B7 → B4 → B5 → B6`

## 3. Prompt ทำทั้ง Phase A ต่อเนื่อง (ถ้ามั่นใจแล้ว)

```
ทำ task A0 ถึง A7 ใน Docs/Plan/PLAN_to_21Oct.md ตามลำดับ ทีละ task ทีละ commit

กติกา:
- จบแต่ละ task ต้องผ่าน compile check, AutomatedCombatFlowTest และ Done when ของ task นั้นก่อนเริ่ม task ถัดไป
- ใช้ค่า default ในตาราง "รอทีมตัดสิน" ถ้าต้องใช้ และระบุไว้ใน commit message
- ถ้า task ไหนติดเกิน 2 รอบ ให้ข้าม task นั้น บันทึกเหตุผลไว้ใต้ task ใน PLAN แล้วทำ task ถัดไป
- ห้ามรัน setup tool ที่สร้างฉากใหม่ทับฉากเดิม (Setup Event/Shop/Reward/Main Menu Scene) ยกเว้น MeruSetupTool ใน A4

จบแล้วสรุปเป็นตาราง: task, สถานะ, commit hash, สิ่งที่ผมต้องทดสอบด้วยมือใน Unity
```

## 4. Prompt ตรวจงานก่อน push

```
ตรวจงานทั้งหมดที่ยังไม่ได้ push:
1. git log origin/main..HEAD --oneline
2. รัน compile check, AutomatedCombatFlowTest, AutomatedRunFlowTest (ถ้ามีแล้ว) และ EditMode tests ทั้งหมด
3. รัน BuildScript.BuildWindows
4. ไล่ diff หาสิ่งที่ผิดกฎใน CLAUDE.md: UnityEditor ใน runtime, ตัวเลขเกมที่เปลี่ยน, ไฟล์ใน Library/Temp/Build ที่หลุดเข้ามา, .meta ที่หาย
สรุปผลเป็นตาราง ถ้าผ่านทั้งหมดให้บอกว่าพร้อม push แต่ยังไม่ต้อง push
```
