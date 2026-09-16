# Checklist เกมทำฟาร์ม — เทียบกับ Spec ที่ตั้งไว้

> **Deadline: 9 กันยายน 2026**
> ไฟล์นี้อยู่นอกโฟลเดอร์ `Assets` — Unity จะไม่ import ไม่กระทบเกม
> เปิดอ่าน/แก้ได้ด้วย Notepad หรือ VS Code

**สัญลักษณ์:** ✅ เสร็จแล้ว · 🔄 กำลังทำ · ⬜ ยังไม่เริ่ม · 🚫 ตั้งใจไม่ทำ

---

## 0. ของเดิมที่ต้องรักษาไว้ (ห้ามเขียนใหม่)

| สิ่งที่มี | สถานะ | เช็คตรงไหน |
|---|---|---|
| Movement | ✅ ไม่ถูกแตะ | `Assets/Script/PlayerMovement/PlayerMovement.cs` |
| Animation | ✅ ไม่ถูกแตะ | Player > Animator |
| Collision | ✅ ไม่ถูกแตะ | Grid > `Collision` tilemap |
| Camera | ✅ ไม่ถูกแตะ | `CinemachineCamera` + Confiner 2D |
| Map / Background / บ้าน / ฟาร์ม | ✅ ไม่ถูกแตะ | Grid > Ground, Decor, WalkBehind, WalkInfront |
| NPC ใน Map | ✅ ไม่ถูกแตะ | `character_1-8_49` |
| Fade เข้า-ออกโซน | ✅ แก้ 1 จุด | `Script/Map/MapTransition.cs` — เปลี่ยนชื่อ class ให้ตรงชื่อไฟล์ |

---

## 1. Farming 🔄 (โค้ดเสร็จ — รอทดสอบ)

| ข้อกำหนด | สถานะ | เช็คตรงไหน |
|---|---|---|
| ใช้จอบ | ✅ | กด `1` แล้ว `Space` |
| เตรียมดิน | ✅ | ดินไถโผล่บน `SoilTilemap` |
| ปลูกเมล็ด | ✅ | กด `3` / `Tab` เลือกพืช แล้ว `Space` |
| รดน้ำ | ✅ | กด `2` แล้ว `Space` — ดินเข้มขึ้น |
| พืชเติบโต | ✅ | กด `N` ข้ามวัน |
| เก็บเกี่ยว | ✅ | กด `4` แล้ว `Space` — ดู Console |
| สถานะ ดิน→เมล็ด→เติบโต→พร้อมเก็บ | ✅ | `FarmManager.cs` → คลาส `FarmTile` |
| ไม่รดน้ำ = ไม่โต | ✅ | `FarmManager.AdvanceDay()` |
| พืชทุกชนิดใช้เวลาโตเท่ากัน | ✅ | `Crops/*.asset` → `daysToGrow = 3` ทุกอัน |
| พืช 5 ชนิด | ⬜ **มี 3** | `Assets/Script/Farming/Crops/` — Carrot, Corn, Wheat |
| ไม่มี ปุ๋ย / คุณภาพพืช / ฤดูกาล / อากาศ / Stamina | 🚫 ไม่ได้ทำ | ตรวจ `FarmManager.cs` ไม่มีคำพวกนี้ |

**ไฟล์:** `Assets/Script/Farming/` → `CropData.cs`, `FarmManager.cs`, `PlayerToolController.cs`, `Editor/FarmingSetupWizard.cs`

**พืชอีก 2 ชนิดที่ยังขาด** — ต้องหา Art เพิ่ม แล้วคลิกขวา > Create > Farming > Crop Data (ไม่ต้องแก้โค้ด)

---

## 2. Inventory ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| Hotbar 6 ช่อง แสดงล่างจอ | ⬜ |
| กด 1–6 เลือกของ | ⬜ (ตอนนี้ปุ่ม 1–4 เป็นปุ่มทดสอบชั่วคราว) |
| Backpack กด `I` | ⬜ |
| เก็บของรวมกัน ไม่แบ่งหมวด | ⬜ |

> **จุดต่อที่เตรียมไว้แล้ว:** `FarmManager.OnHarvested` (event) และ `PlayerToolController.currentTool` / `currentSeed` (public) — Hotbar มาเสียบได้เลย ไม่ต้องแก้ระบบฟาร์ม

---

## 3. Shop + เงิน ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| ร้านเมล็ด/อุปกรณ์ (จอบ, เมล็ด, อาหารสัตว์) | ⬜ |
| ร้านสัตว์ | ⬜ |
| ร้านรับซื้อพืช | ⬜ |
| ร้านรับซื้อสัตว์ | ⬜ |
| เงินเป็นบาท (฿) ราคาคงที่ | ⬜ |
| ไม่มี Dynamic Pricing | 🚫 ไม่ได้ทำ |

> **ราคาตั้งไว้แล้วใน `Crops/*.asset`** — `sellPrice` / `seedPrice` ช่องละพืช

---

## 4. NPC ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| NPC 3–7 คน | ⬜ (ตอนนี้มี 1 คนใน Map) |
| แจก Quest / ขายเมล็ด / ขายอุปกรณ์ / รับซื้อพืช / ขายสัตว์ / รับซื้อสัตว์ | ⬜ |
| ไม่มีระบบความสัมพันธ์ / Dialogue Tree ซับซ้อน | 🚫 ไม่ได้ทำ |

**ของเดิมที่จะต่อยอด:** `Assets/NPCDialogueTrigger.cs` (ตอนนี้แค่เปิด-ปิด Canvas ตอนเดินชน)

---

## 5. Quest ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| Main Quest อย่างเดียว | ⬜ |
| ประเภท: ปลูก, เก็บ, ขายพืช, ซื้อสัตว์, ให้อาหาร, ขายสัตว์, ช่วย NPC, ตามหาสัตว์, ผสมสัตว์ | ⬜ |
| รางวัล: เงิน / เมล็ด / สัตว์ / ไอเทม | ⬜ |

---

## 6. House + Day/Night ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| เข้า / ออก House | ⬜ |
| เตียง + นอน | ⬜ |
| เปลี่ยนวัน | ✅ ตรรกะพร้อม — `FarmManager.AdvanceDay()` |
| Fade Out → เปลี่ยน Scene → Fade In | ✅ ของเดิมมีอยู่แล้ว — `FadeController.cs` |
| กลางวัน / กลางคืน | ⬜ |
| พืชโตตามจำนวนวัน | ✅ |

> เตียงแค่เรียก `FarmManager.Instance.AdvanceDay()` แล้ว Save — ไม่ต้องเขียนตรรกะใหม่

---

## 7. Save / Load ⬜

| ต้องบันทึก | สถานะ |
|---|---|
| วัน | ✅ พร้อม — `FarmManager.currentDay` |
| พืช / สถานะพืช / การรดน้ำ | ✅ พร้อม — `ExportSave()` / `ImportSave()` |
| เงิน | ⬜ |
| Inventory | ⬜ |
| สัตว์ | ⬜ |
| Quest Progress | ⬜ |
| เขียนไฟล์ JSON จริง | ⬜ |

---

## 8. Animal + ผสมสัตว์ ⬜ (ทำเป็นลำดับสุดท้าย)

| ข้อกำหนด | สถานะ |
|---|---|
| ซื้อ / ให้อาหาร / เลี้ยง / ขาย | ⬜ |
| ผสมสัตว์ สูตร A + B → C | ⬜ |
| บางสูตรได้ Legendary | ⬜ |
| 2 ระดับเท่านั้น: Common, Legendary | ⬜ |
| ไม่มี Rare/Epic/Uncommon/Happiness/Friendship/Level | 🚫 ไม่ได้ทำ |
| ไม่มีระบบจับสัตว์ / ผลผลิตจากสัตว์ | 🚫 ไม่ได้ทำ |

> **ถ้าเวลาไม่พอ ตัดข้อนี้ได้** — loop หลักของเกมยังเล่นจบได้โดยไม่มีสัตว์

---

## 9. Settings ⬜

| ข้อกำหนด | สถานะ |
|---|---|
| Music Volume | ⬜ |
| SFX Volume | ⬜ |
| Fullscreen | ⬜ |
| Back | ⬜ |

---

## 10. ห้ามทำ — ตรวจว่ายังไม่หลุดเข้ามา 🚫

Stamina · Energy · Season · Weather · Fishing · Mining · Crafting · Cooking ·
Player Level · Animal Level · Animal Happiness · NPC Friendship · Marriage · Dating ·
Multiplayer · Online · Dynamic Market · Crop Quality · Fertilizer · Free Placement ·
Animal Catching · Animal Products · ระบบปลดล็อกพื้นที่ขนาดใหญ่

**สถานะ: ยังไม่มีสักอย่าง ✅** — ตรวจได้โดยค้นหาคำพวกนี้ใน `Assets/Script/`
(HappyHarvest มี Weather/Season อยู่ แต่เราไม่ได้ใช้โค้ดของมันเลย ใช้แค่รูป)

---

## 11. กติกาการทำงาน (ตกลงกันไว้)

- ทำทีละระบบ ไม่ทำพร้อมกัน
- ก่อนแก้ Script เดิม ต้องบอกก่อนว่า: ไฟล์ไหน / ตรงไหน / เพื่ออะไร / กระทบอะไร
- ไม่สร้าง Art / Map / ตัวละคร / บ้าน / Background ใหม่ เว้นแต่สั่ง
- ไม่ขยาย Scope เอง

---

## เป้าหมายสุดท้าย — ทดสอบตอนใกล้ส่ง

เปิดเกม → เดิน → ทำฟาร์ม → ปลูกผัก → รดน้ำ → เก็บเกี่ยว → ขาย → ซื้อของ →
เลี้ยงสัตว์ → ผสมสัตว์ → ได้ Legendary → ทำ Quest → เข้าออกบ้าน → นอน →
เปลี่ยนวัน → Save/Load → เล่นต่อได้

**ตอนนี้ทำได้ถึง:** เปิดเกม → เดิน → ทำฟาร์ม → ปลูกผัก → รดน้ำ → เก็บเกี่ยว
