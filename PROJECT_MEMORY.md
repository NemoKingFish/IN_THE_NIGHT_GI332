# PROJECT_MEMORY

## Purpose

### Implementation Update 2026-05-07

- Removed jump from player control in both movement paths:
  - `Assets/Scripts/player/NetworkPlayerInput.cs`
  - `Assets/Scripts/player/NetworkPlayerMotor.cs`
  - `Assets/Scripts/player/NetworkPlayerAnimation.cs`
  - `Assets/Scenes/Kan/Anomaly/PhotonScenePlayerAvatar.cs`
- Player movement should now be treated as ground-based only:
  - walk
  - sprint
  - gravity / fall
- Animation should no longer enter `Jump` state from player input

### Implementation Update 2026-05-04

- Implemented a new round-flow in code around `GameRoundManager`:
  - 7 rounds total
  - phase progression 1/2/3 by round number
  - wrong answer returns to the start round of the current phase
  - score is no longer used for progression
  - added `SpawnLockdown` stage between memorize and investigation
- Added anomaly phase filtering in `AnomalySpawnPoint`
- Added local spawn-pad teleport hooks for phase resets in `PhotonScenePlayerSpawnManager`
- Added code-only door helpers:
  - `PhaseDoorController`
  - `PhaseAccessManager`
- Added rough ending controller:
  - `EndGameSceneController`
- Added in-game victory UI flow in gameplay canvas:
  - `GameEndPanelUI`
  - attached to `Assets/Scenes/Kan/Gameplay/Canvas.prefab`
  - shows `Back To Menu` and `Back To Lobby` when the game reaches `Victory`
  - `Canvas.prefab` now contains a real editable `GameEndPanel` hierarchy
  - `GameEndPanelUI` now prefers prefab references and only falls back to runtime creation if those references are missing
- Added memorize helper UI:
  - `MemorizePhaseAdvanceUI`
  - attached to `Assets/Scenes/Kan/Gameplay/Manager.prefab`
  - shows a `Start Investigation` button during manual memorize phase
- Added three test anomaly prefabs in `Assets/Scenes/Kan/Gameplay/`
  - `TestAnomalyCube.prefab`
  - `TestAnomalySphere.prefab`
  - `TestAnomalyCapsule.prefab`
- Fixed the custom anomaly inspector so `Anomaly Phase` is visible and editable in Unity
- Applied code-side bug fixes:
  - room password text no longer reveals the actual password
  - generated password field is hidden in create-room UI
  - room panel slot layout was enlarged/tightened for 4 players
  - player name label now billboards toward the camera
  - fixed the name-label facing direction so remote player names no longer appear mirrored
  - added another Photon player-prefab fallback path through `Assets/Prefabs/Network_PlayerArmature.prefab`
  - improved runtime fallback player visuals so missing-prefab cases render as a readable blue placeholder instead of a bright magenta capsule

### Still Needs Scene Wiring / Validation

- Memorize phase is now designed to support manual advance, but existing scenes may still need UI/button/trigger hookup to call `AdvanceMemorizePhase()`.
- Door scripts were added, but scene references still need to be assigned in Unity.
- The old separate `END GAME` scene is no longer required for the main win flow.
- Phase-based door opening and spawn-room locking should be tested in the actual target scene.

ไฟล์นี้ใช้เป็นแหล่งอ้างอิงกลางของโปรเจกต์ `IN THE NIGHT` เพื่อช่วยลดการอธิบายซ้ำ ลดการคลาดเคลื่อนของ scope งาน และใช้สรุปสถานะล่าสุดของโปรเจกต์ได้เร็วในอนาคต

ควรอัปเดตไฟล์นี้เมื่อมีการเปลี่ยนแปลงเรื่อง:

- gameplay หลัก
- phase / map progression
- ระบบ online
- milestone
- technical decision
- scope ของ MVP

---

## Project Identity

### Name

- `IN THE NIGHT`

### Genre

- Co-op Horror
- Anomaly Detection
- Observation / Memory-based Multiplayer Game

### High Concept

ผู้เล่นหลายคนเข้าไปสำรวจพื้นที่ยามค่ำคืน ต้องจดจำสภาพปกติของแมพ ค้นหาความผิดปกติ และตอบผ่าน checklist ให้ถูกต้องเพื่อผ่านแต่ละรอบ

### Core Experience

- จำสิ่งที่ปกติ
- หา anomaly
- คุยกับเพื่อน
- ตัดสินใจร่วมกัน
- ผ่านรอบเพื่อปลดล็อกพื้นที่เพิ่ม

---

## Locked Gameplay Rules

ข้อมูลในส่วนนี้คือกติกาที่ตกลงกันแล้ว ณ ตอนนี้ และควรถูกใช้เป็นฐานก่อนแก้ระบบ

### Base Game Flow

1. โหลดฉากเกม
2. ผู้เล่นเกิดในห้อง Spawn
3. เริ่มรอบ
4. เข้าช่วงจำแมพปกติ
5. กลับห้อง Spawn
6. ระบบสุ่ม event anomaly ว่ามีหรือไม่มี
7. ผู้เล่นออกไปสำรวจฉาก
8. ผู้เล่นเปิด Checklist
9. ผู้เล่นเลือกประเภท Anomaly
10. ผู้เล่นทุกคน Submit
11. ระบบตรวจคำตอบ

### Win / Lose Evaluation Rule

- ระบบหลักตอนนี้ใช้แค่ `ตอบถูก` หรือ `ตอบผิด`
- นำ `logic คะแนน` ออกจาก core gameplay ไปก่อน
- การผ่านเกมในตอนนี้ควรอิงกับ `progression ของรอบและ phase` ไม่ใช่ score

### Spawn Room Door Rule

- ในห้องต่าง ๆ มีการเตรียม `ประตู` ไว้แล้ว
- สำหรับ `ห้อง Spawn`:
  - ในช่วงที่ระบบกำลัง `สุ่ม event anomaly`
  - ประตูห้อง Spawn ควร `ปิดล็อกไว้`
  - ผู้เล่นต้องอยู่ในห้อง Spawn จนกว่าการสุ่ม anomaly จะเสร็จ

### Round Count

- เกมมีทั้งหมด `7 รอบ`

### Correct Answer Rule

ถ้าทายถูก:

- ไปยังรอบถัดไป
- เปิดพื้นที่เพิ่มตาม phase progression

### Wrong Answer Rule

ถ้าทายผิด:

- ไม่รีเซ็ตทั้งเกม
- ผู้เล่นกลับไปที่ `ต้น phase ปัจจุบัน`
- checkpoint ที่ใช้คือห้อง Spawn ของ phase นั้น

### Phase Progression

- `รอบ 1-3` = `Phase 1`
- `รอบ 4-5` = `Phase 2`
- `รอบ 6-7` = `Phase 3`

### Map Access Rules

- `Phase 1` เปิดให้เล่นเฉพาะ `โซนกลาง`
- `Phase 2` เปิด `พื้นที่ฝั่งซ้าย` เพิ่ม
- `Phase 3` เปิด `พื้นที่ฝั่งขวา` เพิ่ม

### Phase Unlock Rule

- การปลดล็อก phase ในเชิงฉาก หมายถึง `การเปิดประตูร้าน / ประตูพื้นที่` ของโซนนั้น
- ถ้าพื้นที่ยังไม่ถูกปลดล็อก ประตูของพื้นที่นั้นควรยัง `ปิดอยู่`

### Persistent Area Rule

เมื่อเข้า phase ใหม่:

- พื้นที่เก่า `ยังเปิดอยู่ด้วย`

ดังนั้นโครงสร้างพื้นที่เป็นแบบ cumulative:

- `Phase 1` = กลาง
- `Phase 2` = กลาง + ซ้าย
- `Phase 3` = กลาง + ซ้าย + ขวา

### Checkpoint Rule

- ถ้าผิดใน `Phase 1` กลับ checkpoint ของ `Phase 1`
- ถ้าผิดใน `Phase 2` กลับ checkpoint ของ `Phase 2`
- ถ้าผิดใน `Phase 3` กลับ checkpoint ของ `Phase 3`

### Anomaly Phase Rule

- ระบบ anomaly ไม่ควรอิงแค่ `anomaly id`
- anomaly แต่ละจุดควรมีข้อมูล `anomaly phase` ด้วย
- anomaly จะต้องทำงานได้ก็ต่อเมื่อ `phase ปัจจุบัน` อนุญาตให้พื้นที่นั้นเปิดใช้งานแล้ว
- เป้าหมายคือป้องกันกรณี:
  - ประตูร้านยังปิดอยู่
  - แต่ anomaly ภายในร้านที่ยังไม่เปิดกลับถูกสุ่ม/ทำงานขึ้นมาก่อน

### Recommended Anomaly Data

ข้อมูลขั้นต่ำที่ anomaly ควรมี:

- `anomaly id`
- `anomaly type`
- `anomaly phase`
- `zone / room reference`
- `is available in current phase`

### Empty Checklist Rule

- ผู้เล่นสามารถ `กด Submit ได้แม้ checklist ว่าง`
- ใช้สำหรับกรณีที่รอบนั้น `ไม่มี anomaly`

### Shared Checklist Rule

- เมื่อผู้เล่นทุกคน submit ครบ
- ระบบจะใช้ `checklist กลางชุดเดียว`
- ไม่ได้ตัดสินผลจาก checklist แยกคน

### Spawn Checkpoint Rule

- checkpoint ของแต่ละ phase อยู่ใน `ห้อง Spawn`
- ใช้ตำแหน่ง spawn point ของผู้เล่นแต่ละคนเป็นจุดกลับเมื่อผิด

### Time Rule

- ช่วง `จำแมพ` ของทุก phase: `ไม่จำกัดเวลา`
- ช่วง `สำรวจ` ของแต่ละ phase: `มีเวลาได้`
- เวลาในช่วงสำรวจของแต่ละ phase อาจต่างกันตาม `ขนาด / ความกว้างของแมพ`
- เวลาในช่วงสำรวจควรถูกทำให้ `ปรับแก้ได้ง่าย` เพื่อใช้ทดสอบกับผู้เล่นภายนอกแล้วนำมาปรับ gameplay ภายหลัง

### Default Exploration Time

ค่าเริ่มต้นที่แนะนำตอนนี้:

- `Phase 1` = `120 วินาที`
- `Phase 2` = `180 วินาที`
- `Phase 3` = `240 วินาที`

เหตุผล:

- Phase 1 ใช้เฉพาะโซนกลาง จึงควรใช้เวลาสั้นสุด
- Phase 2 เปิดพื้นที่เพิ่มทางซ้าย ทำให้ระยะสำรวจมากขึ้น
- Phase 3 เปิดเต็มฝั่งขวา ทำให้พื้นที่รวมกว้างสุด

หมายเหตุ:

- ค่านี้เป็น `default tuning`
- ต้องทำให้แก้ไขได้จาก inspector / manager script เพื่อใช้เทส gameplay ได้ง่าย

### Door Unlock / Control Rule

- การเข้า phase ใหม่ทำให้ `ประตูร้าน` ของพื้นที่ phase นั้นเปิด
- ควรมี manager หรือระบบกลางคุมการเปิด/ปิดประตูตาม phase
- ควรแยก logic ประตูออกจาก gameplay หลักเพื่อปรับเทสได้ง่าย
- สำหรับสคริปต์ประตู ควรปรับ `position` และ `rotation` ได้ใน inspector เพื่อใช้เทสการเปิดประตู

### Spawn Door Readability Rule

- ตอนเริ่มรอบจำแมพ ประตูห้อง Spawn จะเปิด
- ต้องออกแบบการเปิดประตูให้ผู้เล่น `รับรู้ได้ชัด` ว่าประตูเปิดเมื่อใด
- เป้าหมายคือไม่ให้ผู้เล่นสับสนว่าช่วงไหนออกจากห้อง Spawn ได้

---

## Map Layout Summary

อ้างอิงจากไฟล์ `C:\Users\kitti\Downloads\IN_THE_NIGHT_Map.drawio`

### Confirmed Layout

- ด้านบนของแมพคือ `ห้องเกิด / ห้องรอ (Spawn Room)`
- ตรงกลางคือ `ตลาดกลางขนาดใหญ่`
- ฝั่งซ้ายมีร้าน `ร้านค้า 1-7`
- ฝั่งขวามีร้าน `ร้านค้า 8-14`

### Phase Interpretation From Map

- `Phase 1` = พื้นที่แกนหลักของ `ตลาดกลาง`
- `Phase 2` = เปิดพื้นที่ฝั่งซ้ายเพิ่ม
- `Phase 3` = เปิดพื้นที่ฝั่งขวาเพิ่ม

หมายเหตุ:

- ควรใช้ layout นี้เป็นฐานสำหรับผูก anomaly phase และ door unlock logic
- ถ้ามีการย้ายห้อง / เปลี่ยนการจัดโซน ต้องอัปเดตไฟล์นี้ด้วย

---

## Design Interpretation

### Current Best Interpretation

ตัวเกมควรถูกพัฒนาเป็น:

- เกม co-op anomaly horror ที่ค่อย ๆ ขยายพื้นที่ให้สำรวจ
- ความยากเพิ่มขึ้นจากการเปิดพื้นที่มากขึ้น ไม่ใช่แค่การเพิ่มจำนวน anomaly

### Intended Progression Feeling

- ช่วงแรก: ผู้เล่นเรียนรู้แมพ
- ช่วงกลาง: เริ่มมีการจดจำพื้นที่หลายโซน
- ช่วงท้าย: ผู้เล่นต้องจัดการข้อมูลจากแมพเกือบทั้งหมด

---

## Confirmed Technical State

### Engine / Project

- Unity URP project

### Online System Used For Lobby / Room

- ใช้ `Photon PUN / Photon Cloud`

### What Photon Is Used For

- connect online
- lobby
- create room
- join room
- scene sync
- room state / anomaly gameplay flow

### Can Players In Different Locations Play Together?

- ได้ ถ้ามีอินเทอร์เน็ต และ Photon config ยังใช้งานได้

### Can It Run As LAN-only Right Now?

- ไม่ได้ในความหมายของ local LAN offline
- ตอนนี้ยังเป็น Photon Cloud flow

### Other Networking In Project

โปรเจกต์นี้มี `Unity Netcode` อยู่ด้วย แต่ปัจจุบันดูใช้ในระบบรอง เช่น:

- movement prototype
- train / rail interaction

### Important Technical Risk

มี networking สองสายอยู่พร้อมกัน:

- Photon
- Unity Netcode

ถ้าจะพัฒนาอย่างจริงจังต่อ ต้องตัดสินใจให้ชัดว่า:

- ใช้ Photon เป็นแกนหลักของเกม
- หรือย้ายทั้งเกมไป Netcode ในอนาคต

สถานะปัจจุบัน:

- `Photon` ใกล้กับ gameplay loop หลักมากกว่า

---

## Confirmed Systems Found In Project

### Main Systems

- anomaly system
- checklist system
- lobby / room flow
- round flow
- score progression

### Secondary / Prototype Systems

- network movement
- train interaction
- additional scene prototypes

### Archived / Unused Feature Direction

- เดิมเคยมีแนวคิดจะทำเกมเป็น `ขี่รถไฟตามหา anomaly`
- ตอนนี้แนวคิดนี้ถูกนำออกจาก core game direction แล้ว
- ให้เก็บไว้เป็น `unused / archived feature idea`
- ไม่ควรใช้เป็นฐานการพัฒนาหลักในตอนนี้

### Scene Cluster Most Relevant

กลุ่ม scene ที่เกี่ยวกับแกนเกมมากที่สุดคือ:

- `Assets/Scenes/Kan/...`

---

## Profiling / Performance Notes

### Profiling Capture Reviewed

- `NetcodeDemo329D_2026-05-02_20-07-29`

### Key Takeaway

จากการวิเคราะห์ไฟล์ profiler ที่เคยตรวจ:

- ตัวที่หนักสุดดูไปทาง `Rendering` และ `Jobs`
- ไม่ได้หนักสุดที่ `Network`

### Important Caveat

capture ที่ตรวจเป็นลักษณะ `Editor / Deep Profile` จึงไม่ควรใช้เป็นตัวแทน memory/CPU ของ player build จริงแบบตรง ๆ

---

## Documentation Files Already Created

### Existing Files

- `GDD_IN_THE_NIGHT.md`
- `IN_THE_NIGHT_Flowchart.drawio`
- `PROJECT_MEMORY.md`

### Documentation Sync Status

- `GDD_IN_THE_NIGHT.md` was updated to match the current 7-round / 3-phase gameplay rules
- `IN_THE_NIGHT_Flowchart.drawio` was updated to match:
  - `SpawnLockdown`
  - phase-based door unlocking
  - empty-checklist submission
  - wrong-answer return to current phase start
  - in-canvas `GameEndPanel` flow

### Intended Role Of Each File

- `GDD_IN_THE_NIGHT.md`
  - ใช้เป็น GDD / ภาพรวมเกม / แนวคิดและขอบเขต

- `IN_THE_NIGHT_Flowchart.drawio`
  - ใช้เป็น flow chart ของ gameplay

- `PROJECT_MEMORY.md`
  - ใช้เป็นแหล่งอ้างอิงสถานะล่าสุดของโปรเจกต์และข้อตกลงหลัก

---

## What Should Be Remembered In Future Discussions

ถ้าต้องสรุปสิ่งที่สำคัญที่สุดที่ควรถูกอ้างอิงเสมอ มีดังนี้:

1. เกมนี้คือเกม co-op anomaly horror
2. ระบบห้องออนไลน์หลักใช้ Photon Cloud
3. เกมมี 7 รอบ
4. รอบ 1-3 = Phase 1, รอบ 4-5 = Phase 2, รอบ 6-7 = Phase 3
5. พื้นที่เก่ายังเปิดอยู่เมื่อเข้า phase ใหม่
6. ถ้าทายผิด ให้กลับ checkpoint ของ phase ปัจจุบัน
7. ระบบ anomaly + checklist คือแกนหลักของโปรเจกต์
8. train / netcode movement ยังเป็นระบบรองหรือ prototype

---

## Current Scope Assumptions

ณ ตอนนี้สมมติฐานที่ควรใช้ก่อนจนกว่าจะมีการเปลี่ยนเพิ่ม:

- เกมยังเน้น anomaly gameplay มากกว่ารถไฟ
- phase-based map expansion เป็นระบบ progression หลัก
- room / session online จะยังอิง Photon
- การพัฒนาควรยึด gameplay agreement ก่อนขยาย feature
- ระบบรอบตอนนี้ไม่ควรผูกกับ score

---

## Recommended Development Priorities

ลำดับความสำคัญที่ควรทำต่อ:

1. ล็อก gameplay rule ให้ครบ
2. ผูก phase logic เข้ากับ map access
3. ตัด score ออกจาก core gameplay flow
4. ทำระบบเปิด/ปิดประตูร้านตาม phase
5. เพิ่ม `phase logic` เข้าไปใน anomaly system
6. สร้าง checkpoint system ตาม phase โดยใช้ spawn point ในห้อง Spawn
7. ทำระบบประตูห้อง Spawn ให้ปิดล็อกระหว่างช่วงสุ่ม anomaly
8. ทำ round manager ให้รองรับ 7 รอบจริง
9. ทำ shared checklist resolution ให้ชัด
10. ทำระบบ exploration timer ที่ปรับค่าแยกตาม phase ได้
11. เชื่อม anomaly spawn logic กับ phase progression
12. ทำหน้า ending แบบชั่วคราว พร้อมปุ่ม `Back to Menu` และ `Back to Lobby`
13. ปรับ UI checklist / round status ให้ตรงกับ flow ใหม่
14. คงระบบรอง เช่น train ไว้ในหมวด archived feature

---

## Open Questions

เรื่องที่ยังควรตัดสินใจในอนาคต:

- anomaly แต่ละ phase จะต่างกันแค่พื้นที่ หรือรวมถึงประเภท/ความยาก
- anomaly ที่อยู่ใน phase สูงกว่า จะถูกปิดการทำงานทั้งหมดหรือถูกแค่ตัดออกจากการสุ่ม

---

## Deferred / Shelved Ideas

รายการนี้คือไอเดียที่ยังไม่ควรทำตอนนี้ แต่ควรจำไว้เผื่อใช้ในอนาคต

### Firebase Leaderboard

- เคยมีแนวคิดจะทำ `Firebase leaderboard`
- ตอนนี้ `ยังไม่อยากทำ`
- ไม่ถือเป็นส่วนของ MVP ปัจจุบัน
- ให้เก็บเป็น `deferred idea`

### Train-based Anomaly Game

- เคยคิดจะทำเกมแนวขี่รถไฟตามหา anomaly
- ประเมินแล้วไม่น่าเวิร์กกับทิศทางปัจจุบัน
- ให้เก็บเป็น `archived concept`

---

## Immediate UI Requirement

- ควรมีหน้า `ending / win game` แบบลวก ๆ ก่อน
- ในหน้านี้ควรมีปุ่ม:
  - `Back to Menu`
  - `Back to Lobby`

### Ending Flow Decision

- เมื่อจบเกมแล้ว `Back to Lobby` ต้องกลับไปที่ `lobby scene`

---

## Handoff Task List

รายการนี้คือสิ่งที่ควรทำต่อสำหรับคนที่มารับงานพัฒนาหลังจากนี้

### Gameplay / System

1. ปรับ round / phase manager ให้รองรับ `7 รอบ`
2. กำหนด phase progression:
   - รอบ 1-3 = Phase 1
   - รอบ 4-5 = Phase 2
   - รอบ 6-7 = Phase 3
3. ทำระบบ `ย้อนกลับไปต้น phase ปัจจุบัน` เมื่อทายผิด
4. ผูก checkpoint กับห้อง Spawn
5. ตัด score logic ออกจาก core flow

### Anomaly System

6. เพิ่มข้อมูล `anomaly phase` ให้ anomaly แต่ละจุด
7. จำกัดการทำงาน / การสุ่ม anomaly ตาม phase ปัจจุบัน
8. รองรับกรณี `ไม่มี anomaly` และ checklist ว่าง
9. ทำ shared checklist resolution โดยใช้ checklist กลางชุดเดียว

### Door / Map Progression

10. ทำ manager สำหรับเปิด/ปิดประตูร้านตาม phase
11. ทำระบบประตูห้อง Spawn:
    - ปิดระหว่างช่วงสุ่ม anomaly
    - เปิดเมื่อเริ่มช่วงจำแมพ
12. ทำให้สคริปต์ประตูปรับ `position` และ `rotation` ได้ง่ายสำหรับการเทส

### Timing / Tuning

13. ทำ exploration timer แยกตาม phase
14. ตั้งค่าเริ่มต้น:
    - Phase 1 = 120s
    - Phase 2 = 180s
    - Phase 3 = 240s
15. ทำให้เวลาเหล่านี้แก้ไขได้ง่ายจาก inspector

### UI / Scene Flow

16. ทำหน้า ending / win game แบบชั่วคราว
17. เพิ่มปุ่ม:
    - Back to Menu
    - Back to Lobby
18. กำหนด flow ให้ `Back to Lobby` กลับไป lobby scene

### Documentation

19. อัปเดต flowchart ให้ตรงกับกติกาล่าสุด
20. ใช้ `PROJECT_MEMORY.md` เป็นแหล่งอ้างอิงหลักก่อนแก้ระบบ

---

## Bug List

รายการนี้คือบั๊ก / issue ที่ตรวจพบจากการเทส และควรเก็บไว้เป็นงานอ้างอิง

### Bug 1: Player Model Not Showing Correctly

อาการ:

- โมเดลผู้เล่นไม่แสดงตามที่ควร
- ตอนเทสเห็นเป็นวัตถุสีชมพู / placeholder แทนโมเดลจริง
- ชื่อผู้เล่นหันไม่ตรงและกลับด้านในบางมุม

ผลกระทบ:

- ภาพลักษณ์ตัวละครไม่ถูกต้อง
- อ่านชื่อผู้เล่นได้ยาก
- ลดความชัดเจนของ multiplayer presence

แนวทางตรวจ:

- ตรวจ prefab ของ player ที่ถูก spawn จริง
- ตรวจ material / shader ที่หายหรือไม่รองรับ
- ตรวจการหมุนของ name label / canvas / text ให้หันเข้ากล้องหรือหันถูกทิศ

Current code-side status:

- `PhotonScenePlayerAvatar` now rotates the name label toward the camera with the correct forward direction
- `PhotonScenePlayerSpawnManager` now tries both `Assets/Scenes/Kan/Player.prefab` and `Assets/Prefabs/Network_PlayerArmature.prefab` before falling back
- if no compatible prefab resolves at runtime, the fallback avatar now uses an explicit material and no longer shows as a bright magenta capsule
- if the real humanoid still does not appear in build, the remaining missing piece is a runtime-loadable prefab reference instead of editor-only asset lookup

### Bug 2: Password Display / Password Flow

อาการ:

- ระบบ password มีปัญหา

แนวทางแก้ที่ตัดสินใจแล้ว:

- ปิดการแสดงช่องกรอกรหัสแบบที่ทำให้เกิดปัญหา
- ให้ดูหรือเข้าถึงรหัสผ่านผ่าน `ข้อมูลตัวห้อง` แทน

หมายเหตุ:

- ต้องทบทวน UI/UX ของการเข้าห้องอีกครั้งหลังแก้

### Bug 3: Lobby UI Overflow / Panel Too Small

อาการ:

- UI เกินพื้นที่
- กรอบขาว / panel มีขนาดเล็กเกินไปสำหรับผู้เล่น `4 คน`

ผลกระทบ:

- รายชื่อผู้เล่นล้น
- อ่านยาก
- layout ของ room panel ไม่พอสำหรับ multiplayer เต็มห้อง

แนวทางแก้:

- ขยาย panel หลัก
- ปรับ layout ของ player slot สำหรับ 4 คนเต็ม
- ตรวจ responsive / safe spacing ของข้อความและ input fields

Current code-side status:

- `LobbyTestPhotonController` already enlarges the room panel, increases player-list height, and reduces each slot height to fit 4 players more safely
- room summaries and room headers now show password state only as `Require/Open`

---

## Update Rule

ถ้ามีการเปลี่ยนเรื่องใดต่อไปนี้ ต้องอัปเดตไฟล์นี้ก่อนหรือพร้อมกับการเปลี่ยนโค้ด:

- จำนวนรอบ
- phase progression
- พื้นที่ที่เปิดในแต่ละ phase
- online architecture
- core loop
- กติกาแพ้/ชนะ
- checkpoint logic
 
---

## Audio Direction

Audio should now support emotion and readability for the finished core loop, not just ambience.

### Existing Audio In Project

- `Assets/Scenes/Phukao/Audio/loop_fixed.wav`
- `Assets/StarterAssets/ThirdPersonController/Character/Sfx/Player_Footstep_01.wav` to `Player_Footstep_10.wav`
- `Assets/StarterAssets/ThirdPersonController/Character/Sfx/Player_Land.wav`

### Existing Audio Playback Hooks

- `LoopSFX`
- `FootstepReceiver`
- `StarterAssets.ThirdPersonController`

### Recommended Audio Categories

1. Base ambience loop for the whole map
- quiet night / empty market / uneasy room tone

2. Phase ambience layers
- Phase 1 = restrained
- Phase 2 = denser and less safe
- Phase 3 = highest tension

3. Memorize start cue
- subtle cue that tells players the area is safe to study

4. Spawn lockdown cue
- short sting when players are pulled back into Spawn and the door closes

5. Investigation start cue
- short release cue when Spawn opens and exploration begins

6. Phase unlock cue
- stronger cue when left or right area opens for the first time

7. Checklist UI sounds
- open
- close
- toggle / tick
- submit
- waiting for teammates

8. Round result sounds
- correct
- wrong
- victory

9. Door sounds
- Spawn room open / close
- phase shop door open

10. Anomaly emotion layer
- optional subtle tension layer during investigation
- anomaly-specific one-shots for light flicker, creature presence, dragged objects, electrical buzz, etc.

### Smallest High-Impact Audio Pass

If the team wants the fastest emotional improvement first, prioritize:

1. base ambience
2. Spawn room door open / close
3. memorize -> lockdown -> investigation cues
4. checklist submit / correct / wrong
5. phase unlock cue
6. anomaly-specific sounds
