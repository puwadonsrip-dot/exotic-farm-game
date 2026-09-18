using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// ตัวช่วยติดตั้งระบบ Farming + Inventory อัตโนมัติ
///
/// วิธีใช้: เมนูด้านบนของ Unity > Tools > Farming > Setup Farming System
///
/// สคริปต์นี้ทำงานเฉพาะใน Editor เท่านั้น (อยู่ในโฟลเดอร์ Editor)
/// ไม่ถูกรวมเข้าไปในตัวเกมตอน Build
///
/// ปลอดภัย: กดซ้ำได้ ถ้ามีของอยู่แล้วจะไม่สร้างซ้ำ และไม่ลบของเดิม
/// </summary>
public static class FarmingSetupWizard
{
    private const string FarmingFolder = "Assets/Script/Farming";
    private const string CropFolder = "Assets/Script/Farming/Crops";
    private const string InventoryFolder = "Assets/Script/Inventory";
    private const string ItemFolder = "Assets/Script/Inventory/Items";
    private const string MarkerPath = "Assets/Script/Farming/TargetMarker.png";

    private const string DirtTilePath = "Assets/HappyHarvest/Art/Tiles/Dirt/Tiles/Tiles_Dirt1.asset";
    private const string HoeIconPath = "Assets/HappyHarvest/Art/Tools/Hoe/Sprites/hoe_icon.png";
    private const string CanIconPath = "Assets/HappyHarvest/Art/Tools/Watercan/Sprites/Sprite_WaterCan_Icon.png";

    /// <summary>ข้อมูลตั้งต้นของพืชแต่ละชนิด</summary>
    private class CropDef
    {
        public string assetName;   // ชื่อไฟล์ + ชื่อโฟลเดอร์ Art ของ HappyHarvest
        public string id;
        public string thaiName;
        public int sellPrice;
        public int seedPrice;
        public int stageCount;
        public int spriteStart;    // ไฟล์รูประยะแรกเริ่มที่เลขอะไร (แครอทเริ่ม 00, ที่เหลือเริ่ม 01)
        public int startingSeeds;

        /// <summary>
        /// โฟลเดอร์เก็บรูป — เว้นว่าง = ใช้ของ HappyHarvest
        /// พืชที่เราเพิ่มเองจะชี้มาที่ Assets/Art/Crops/ชื่อพืช
        /// </summary>
        public string spriteFolder;

        public string Folder => string.IsNullOrEmpty(spriteFolder)
            ? $"Assets/HappyHarvest/Art/Crops/{assetName}/Sprites"
            : spriteFolder;

        public string StagePath(int number) => $"{Folder}/Sprite_{assetName}_{number:00}.png";
        public string IconPath => $"{Folder}/Sprite_{assetName}_icon.png";
        public string SeedBagPath => $"{Folder}/Sprite_{assetName}_seedbag.png";
    }

    private static readonly CropDef[] Crops =
    {
        new CropDef { assetName = "Carrot", id = "carrot", thaiName = "แครอท",    sellPrice = 30, seedPrice = 10, stageCount = 4, spriteStart = 0, startingSeeds = 5 },
        new CropDef { assetName = "Corn",   id = "corn",   thaiName = "ข้าวโพด",  sellPrice = 45, seedPrice = 15, stageCount = 5, spriteStart = 1, startingSeeds = 0 },
        new CropDef { assetName = "Wheat",  id = "wheat",  thaiName = "ข้าวสาลี", sellPrice = 25, seedPrice = 8,  stageCount = 5, spriteStart = 1, startingSeeds = 3 },

        // พืชที่เพิ่มเข้ามาเอง — รูปมาจาก Assets/Art/Crops/ (ตัดด้วย CropSheetSlicer)
        new CropDef { assetName = "Tomato",  id = "tomato",  thaiName = "มะเขือเทศ", sellPrice = 38, seedPrice = 12, stageCount = 6, spriteStart = 0, startingSeeds = 0, spriteFolder = $"{CropSheetSlicer.CropArtRoot}/Tomato" },
        new CropDef { assetName = "Pumpkin", id = "pumpkin", thaiName = "ฟักทอง",   sellPrice = 70, seedPrice = 25, stageCount = 6, spriteStart = 0, startingSeeds = 0, spriteFolder = $"{CropSheetSlicer.CropArtRoot}/Pumpkin" },
    };

    // ใส่ 2 เมนู (อังกฤษ + ไทย) ชี้มาที่ฟังก์ชันเดียวกัน กดอันไหนก็ได้
    /// <summary>
    /// ล้างความจำว่า "เคยดูคัตซีนแล้ว" — กดแล้วรอบหน้าที่กด Play คัตซีนจะเล่นอีกครั้ง
    /// </summary>
    [MenuItem("Tools/Farming/ดูคัตซีนเปิดเกมอีกครั้ง")]
    public static void ReplayIntro()
    {
        IntroCutscene.ForgetSeen();
        EditorUtility.DisplayDialog(
            "คัตซีนเปิดเกม",
            "เรียบร้อย — กด Play รอบหน้าคัตซีนจะเล่นให้ดูใหม่ตั้งแต่ต้น",
            "โอเค");
    }

    /// <summary>
    /// สร้างจุดวาร์ป 2 อันที่ชี้หากัน วางไว้ข้างผู้เล่น
    /// ลากไปวางตรงไหนก็ได้ เดินชนแล้ววาร์ปทันที
    /// </summary>
    [MenuItem("Tools/Farming/สร้างจุดวาร์ปคู่ใหม่")]
    public static void CreateWarpPair()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        var a = new GameObject("WarpPoint A");
        var b = new GameObject("WarpPoint B");

        Undo.RegisterCreatedObjectUndo(a, "Create Warp A");
        Undo.RegisterCreatedObjectUndo(b, "Create Warp B");

        a.transform.position = origin + new Vector3(-3f, 2f, 0f);
        b.transform.position = origin + new Vector3(3f, 2f, 0f);

        var warpA = a.AddComponent<WarpPoint>();
        var warpB = b.AddComponent<WarpPoint>();

        warpA.destination = b.transform;
        warpB.destination = a.transform;

        Selection.activeGameObject = a;

        EditorUtility.DisplayDialog(
            "สร้างจุดวาร์ปแล้ว",
            "สร้าง 'WarpPoint A' กับ 'WarpPoint B' ให้แล้ว ชี้หากันเรียบร้อย\n\n"
            + "ลากทั้งสองอันไปวางตรงที่ต้องการได้เลย\n"
            + "เลือกแล้วจะเห็นวงกลมฟ้าคือระยะที่วาร์ป และเส้นโยงไปหาคู่ของมัน",
            "โอเค");
    }

    [MenuItem("Tools/Farming/Setup Farming System")]
    [MenuItem("Tools/Farming/ติดตั้งระบบ Farming")]
    public static void Setup()
    {
        var log = new List<string>();

        EnsureFolder("Assets/Script", "Farming");
        EnsureFolder("Assets/Script", "Inventory");
        EnsureFolder(FarmingFolder, "Crops");
        EnsureFolder(InventoryFolder, "Items");

        EnsureFolder(FarmingFolder, "CropTiles");

        // ---------- 0. ตัดภาพพืชที่เพิ่มเข้ามาเอง ----------
        CropSheetSlicer.SliceIfNeeded(log);

        // ---------- 1. ข้อมูลพืช ----------
        var cropAssets = new List<CropData>();
        foreach (var def in Crops)
        {
            var crop = EnsureCrop(log, def);
            cropAssets.Add(crop);

            // สร้าง Tile ของเราเองจากไฟล์รูป แล้วเซ็ตทับทุกครั้ง
            // (Tile ของ HappyHarvest ไม่มีรูปในตัว ใช้แล้วพืชจะไม่โผล่)
            if (crop != null)
            {
                crop.growthStageTiles = BuildCropTiles(log, def);
                crop.daysToGrow = StagesPerCrop - 1;   // 4 ระยะ = 3 วัน โตวันละ 1 ระยะพอดี
                EditorUtility.SetDirty(crop);
            }
        }

        // ---------- 2. ไอเทม ----------
        var allItems = new List<ItemData>();
        var startingItems = new List<StartingItem>();

        var hoe = EnsureItem(log, "Hoe", "hoe", "จอบ", ItemKind.Tool,
            HoeIconPath, maxStack: 1, buyPrice: 50, sellPrice: 0, toolType: ToolType.Hoe);
        var can = EnsureItem(log, "WateringCan", "watering_can", "บัวรดน้ำ", ItemKind.Tool,
            CanIconPath, maxStack: 1, buyPrice: 40, sellPrice: 0, toolType: ToolType.WateringCan);

        // ตะเกียงถือ — ถือแล้วสว่างขึ้นมากตอนกลางคืน
        var lantern = EnsureItem(log, "Lantern", "lantern", "ตะเกียง", ItemKind.Tool,
            "Assets/HappyHarvest/Art/Environment/Lamps/LampAndLantern/Sprites/Lantern/Sprite_Lantern.png",
            maxStack: 1, buyPrice: 120, sellPrice: 0, toolType: ToolType.Hand);

        allItems.Add(hoe);
        allItems.Add(can);
        allItems.Add(lantern);

        startingItems.Add(new StartingItem { item = hoe, count = 1 });
        startingItems.Add(new StartingItem { item = can, count = 1 });
        startingItems.Add(new StartingItem { item = lantern, count = 1 });

        for (int i = 0; i < Crops.Length; i++)
        {
            var def = Crops[i];
            var crop = cropAssets[i];

            var seed = EnsureItem(log, def.assetName + "Seed", def.id + "_seed",
                "เมล็ด" + def.thaiName, ItemKind.Seed, def.SeedBagPath,
                maxStack: 99, buyPrice: def.seedPrice, sellPrice: 0, crop: crop);

            var produce = EnsureItem(log, def.assetName, def.id,
                def.thaiName, ItemKind.Produce, def.IconPath,
                maxStack: 99, buyPrice: 0, sellPrice: def.sellPrice, crop: crop);

            allItems.Add(seed);
            allItems.Add(produce);

            // ผูกไอเทมกลับเข้าไปในข้อมูลพืช
            if (crop != null)
            {
                crop.seedItem = seed;
                crop.produceItem = produce;
                EditorUtility.SetDirty(crop);
            }

            if (def.startingSeeds > 0)
                startingItems.Add(new StartingItem { item = seed, count = def.startingSeeds });
        }

        // ---------- 3. หา Grid เดิมใน Scene ----------
        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid == null)
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("ติดตั้งไม่สำเร็จ",
                "ไม่พบ GameObject ที่มี Component 'Grid' ใน Scene ปัจจุบัน\n\n" +
                "สร้างไฟล์ข้อมูลให้แล้ว แต่ยังตั้งค่า Scene ไม่ได้\n" +
                "กรุณาเปิด Scene ที่มีแผนที่ (Assets/project.unity) ก่อนแล้วกดใหม่", "ตกลง");
            return;
        }

        // ---------- 4. Tilemap ดิน + พืช ----------
        var soil = EnsureTilemap(log, grid.transform, "SoilTilemap", "Ground", 1);
        var cropMap = EnsureTilemap(log, grid.transform, "CropTilemap", "Ground", 2);

        // ---------- 5. FarmManager ----------
        var farm = Object.FindFirstObjectByType<FarmManager>();
        if (farm == null)
        {
            var go = new GameObject("FarmManager");
            Undo.RegisterCreatedObjectUndo(go, "Create FarmManager");
            farm = go.AddComponent<FarmManager>();
            log.Add("สร้าง GameObject 'FarmManager'");
        }

        Undo.RecordObject(farm, "Setup FarmManager");
        farm.soilTilemap = soil;
        farm.cropTilemap = cropMap;
        farm.blockingTilemap = FindTilemapByName(grid.transform, "Collision");
        farm.tilledSoilTile = AssetDatabase.LoadAssetAtPath<TileBase>(DirtTilePath);
        farm.allCrops = cropAssets;
        EditorUtility.SetDirty(farm);

        if (farm.blockingTilemap == null)
            log.Add("⚠ ไม่พบ Tilemap ชื่อ 'Collision' — ช่อง Blocking Tilemap ยังว่าง ให้ลากใส่เอง");
        if (farm.tilledSoilTile == null)
            log.Add("⚠ ไม่พบ Tile ดิน (Tiles_Dirt1) — ให้ลากใส่ช่อง Tilled Soil Tile เอง");

        // ---------- 6. InventorySystem + UI ----------
        var inv = Object.FindFirstObjectByType<InventorySystem>();
        if (inv == null)
        {
            var go = new GameObject("InventorySystem");
            Undo.RegisterCreatedObjectUndo(go, "Create InventorySystem");
            inv = go.AddComponent<InventorySystem>();
            go.AddComponent<InventoryUI>();
            log.Add("สร้าง GameObject 'InventorySystem' + InventoryUI");
        }
        else if (inv.GetComponent<InventoryUI>() == null)
        {
            Undo.AddComponent<InventoryUI>(inv.gameObject);
            log.Add("เพิ่ม InventoryUI ให้ InventorySystem");
        }

        // ตั้งหน้าตา Hotbar ใหม่ทุกครั้งที่กด Wizard
        // (ถ้าคุณปรับขนาด/สีเองใน Inspector แล้วไม่อยากให้ทับ ให้ลบบล็อกนี้ออก)
        var ui = inv.GetComponent<InventoryUI>();
        if (ui != null)
        {
            Undo.RecordObject(ui, "Setup Inventory UI");
            ui.slotSize = 96;
            ui.slotSpacing = 4;
            ui.borderWidth = 5;
            ui.selectedBorderWidth = 8;
            ui.columns = InventorySystem.HotbarSize;
            EditorUtility.SetDirty(ui);
            log.Add($"ตั้งหน้าตา Hotbar แบบ Minecraft ({InventorySystem.HotbarSize} ช่อง ขนาด 96)");
        }

        Undo.RecordObject(inv, "Setup Inventory");
        inv.allItems = allItems;
        inv.backpackSize = 16;   // 8 ช่อง Hotbar + 16 ช่องกระเป๋า = 24 ช่อง = 3 แถวพอดี
        if (inv.startingItems == null || inv.startingItems.Count == 0)
        {
            inv.startingItems = startingItems;
            log.Add($"ตั้งของเริ่มต้น: จอบ, บัวรดน้ำ, ตะเกียง, เมล็ดแครอท x5, เมล็ดข้าวสาลี x3");
        }
        else
        {
            // มีของเริ่มต้นอยู่แล้ว = ไม่ทับของเดิม แต่เติมของใหม่ที่ยังขาดให้
            // ไม่งั้นของที่เพิ่มเข้ามาทีหลัง (เช่นตะเกียง) จะไม่เคยเข้ากระเป๋าเลย
            int added = 0;

            foreach (var entry in startingItems)
            {
                if (entry.item == null) continue;
                if (inv.startingItems.Exists(e => e.item == entry.item)) continue;

                inv.startingItems.Add(entry);
                log.Add($"เพิ่มของเริ่มต้นที่ยังขาด: {entry.item.displayName} x{entry.count}");
                added++;
            }

            if (added == 0) log.Add("ของเริ่มต้นครบอยู่แล้ว");
        }
        EditorUtility.SetDirty(inv);

        // ---------- 6.5 Quest + NPC + กล่องบทสนทนา ----------
        SetupQuestAndNPC(log, allItems);

        // ตรงนี้ SetupAnimals เพิ่งเติมไข่กับตัวสัตว์ลงในรายการไปหมาดๆ
        // ต้องยัดกลับเข้า InventorySystem แล้วสั่งเซฟอีกรอบ
        // ไม่งั้นรายการที่ถูกบันทึกลงซีนจะเป็นของก่อนหน้าที่ยังไม่มีสัตว์
        Undo.RecordObject(inv, "Setup Inventory Items");
        inv.allItems = new List<ItemData>(allItems);
        EditorUtility.SetDirty(inv);
        log.Add($"รายชื่อไอเทมทั้งหมด {inv.allItems.Count} ชิ้น (รวมไข่และตัวสัตว์แล้ว)");

        // ---------- 7. Script บน Player ----------
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            log.Add("⚠ ไม่พบ GameObject ที่ติด Tag 'Player' — ต้องใส่ PlayerToolController เอง");
        }
        else
        {
            var tool = player.GetComponent<PlayerToolController>();
            if (tool == null)
            {
                tool = Undo.AddComponent<PlayerToolController>(player);
                log.Add("เพิ่ม PlayerToolController ให้ Player");
            }

            // แสดงของที่ถืออยู่ติดมือตัวละคร
            if (player.GetComponent<PlayerHeldItem>() == null)
            {
                Undo.AddComponent<PlayerHeldItem>(player);
                log.Add("เพิ่ม PlayerHeldItem ให้ Player (โชว์ของที่ถืออยู่ติดมือ)");
            }

            if (tool.targetMarker == null)
            {
                Undo.RecordObject(tool, "Setup Tool Controller");
                tool.targetMarker = EnsureTargetMarker(log);
                EditorUtility.SetDirty(tool);
            }
        }

        // ---------- 8. บันทึก ----------
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        log.Add("");
        log.Add("เสร็จแล้ว! อย่าลืมกด Ctrl+S เซฟ Scene");
        Debug.Log("[Farming Setup]\n" + string.Join("\n", log));
        EditorUtility.DisplayDialog("ติดตั้งสำเร็จ", string.Join("\n", log), "เยี่ยม");
    }

    // ==================================================================

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    // ==================== Quest + NPC ====================

    private const string QuestFolder = "Assets/Script/Quest/Quests";

    /// <summary>
    /// ทุกพืชมี 4 ระยะเท่ากัน = ใช้เวลาโต 3 วันเท่ากัน (ตาม Spec)
    /// ปลูก = ระยะ 0, วันที่ 1 = ระยะ 1, วันที่ 2 = ระยะ 2, วันที่ 3 = ระยะ 3 (เก็บได้)
    /// </summary>
    private const int StagesPerCrop = 4;

    private static void SetupQuestAndNPC(List<string> log, List<ItemData> allItems)
    {
        EnsureFolder("Assets/Script", "Quest");
        EnsureFolder("Assets/Script/Quest", "Quests");

        ItemData FindItem(string id) => allItems.Find(i => i != null && i.itemId == id);

        // ---- เควส ----
        var quests = new List<QuestData>
        {
            EnsureQuest(log, "Q1_PlantCarrot", q =>
            {
                q.questId = "q1_plant_carrot";
                q.title = "ปลูกพืชให้หมู่บ้าน";
                q.offerLines = new[]
                {
                    "เฮ้ คุณนะ คนมาใหม่ใช่ไหม",
                    "พอดีหมู่บ้านเราขาดแคลนพืชผล ช่วยไปปลูกให้หน่อยได้ไหม",
                    "เอาแครอทสัก 3 ต้นก็พอ อย่าลืมรดน้ำทุกวันนะ ไม่งั้นมันไม่โต"
                };
                q.objective = QuestObjective.PlantCrop;
                q.objectiveText = "ปลูกแครอท";
                q.targetCropId = "carrot";
                q.requiredAmount = 3;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "โอ้! ปลูกครบแล้วเหรอ เร็วจริง ๆ",
                    "นี่ เอาไปเป็นค่าแรง แล้วก็เมล็ดเพิ่มอีกหน่อย"
                };
                q.rewardMoney = 100;
                q.rewardItem = FindItem("carrot_seed");
                q.rewardItemCount = 5;
            }),

            EnsureQuest(log, "Q2_HarvestCarrot", q =>
            {
                q.questId = "q2_harvest_carrot";
                q.title = "เก็บเกี่ยวผลผลิต";
                q.offerLines = new[]
                {
                    "รอให้แครอทโตเต็มที่แล้วเก็บมาให้ฉันหน่อยสิ",
                    "จำได้นะ ต้องรดน้ำทุกวัน 3 วันถึงจะโต"
                };
                q.objective = QuestObjective.HarvestCrop;
                q.objectiveText = "เก็บเกี่ยวแครอท";
                q.targetCropId = "carrot";
                q.requiredAmount = 3;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "สวยงาม! แครอทลูกใหญ่มาก",
                    "เธอมีแววเป็นชาวไร่ที่เก่งเลยนะ"
                };
                q.rewardMoney = 150;
                q.rewardItem = FindItem("wheat_seed");
                q.rewardItemCount = 5;
            }),

            EnsureQuest(log, "Q3_PlantWheat", q =>
            {
                q.questId = "q3_plant_wheat";
                q.title = "ลองปลูกอย่างอื่นบ้าง";
                q.offerLines = new[]
                {
                    "เมล็ดข้าวสาลีที่ให้ไป ลองปลูกดูสิ",
                    "พืชแต่ละอย่างขายได้ราคาไม่เท่ากันนะ",
                    "ปลูกสัก 5 ต้น เดี๋ยวจะได้รู้ว่าอันไหนคุ้มกว่า"
                };
                q.objective = QuestObjective.PlantCrop;
                q.objectiveText = "ปลูกข้าวสาลี";
                q.targetCropId = "wheat";
                q.requiredAmount = 5;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "แปลงเธอเริ่มดูเป็นฟาร์มจริงๆ แล้วนะ",
                    "นี่ เมล็ดข้าวโพด เก็บไว้ใช้ทีหลัง มันขายได้ราคาดีที่สุดเลย"
                };
                q.rewardMoney = 200;
                q.rewardItem = FindItem("corn_seed");
                q.rewardItemCount = 5;
            }),

            EnsureQuest(log, "Q4_FirstSale", q =>
            {
                q.questId = "q4_first_sale";
                q.title = "ขายของครั้งแรก";
                q.offerLines = new[]
                {
                    "ปลูกเป็นแล้ว ทีนี้ต้องหาเงินเป็นด้วย",
                    "เดินมาใกล้ๆ ฉันแล้วกด F จะเปิดหน้าร้านรับซื้อ",
                    "ลองขายผลผลิตสัก 5 ชิ้นดู อะไรก็ได้"
                };
                q.objective = QuestObjective.SellCrop;
                q.objectiveText = "ขายผลผลิต (กด F ตอนอยู่ใกล้ฉัน)";
                q.targetCropId = "";              // พืชอะไรก็ได้
                q.requiredAmount = 5;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "เห็นไหม เงินเข้ากระเป๋าแล้ว",
                    "เอาเมล็ดแครอทไปเพิ่ม ยิ่งปลูกเยอะยิ่งได้เยอะ"
                };
                q.rewardMoney = 250;
                q.rewardItem = FindItem("carrot_seed");
                q.rewardItemCount = 8;
            }),

            EnsureQuest(log, "Q5_HarvestCorn", q =>
            {
                q.questId = "q5_harvest_corn";
                q.title = "ข้าวโพดราคาดี";
                q.offerLines = new[]
                {
                    "ข้าวโพดขายได้ตั้ง 45 บาทต่อฝัก แพงกว่าแครอทเกือบเท่าตัว",
                    "เมล็ดก็แพงกว่านิดหน่อย แต่คุ้มแน่นอน",
                    "เก็บเกี่ยวมาให้ได้สัก 5 ฝักสิ"
                };
                q.objective = QuestObjective.HarvestCrop;
                q.objectiveText = "เก็บเกี่ยวข้าวโพด";
                q.targetCropId = "corn";
                q.requiredAmount = 5;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "ฝักใหญ่มาก! เธอดูแลมันดีจริงๆ",
                    "ตอนนี้เธอรู้หมดแล้วว่าฟาร์มทำงานยังไง"
                };
                q.rewardMoney = 400;
                q.rewardItem = FindItem("corn_seed");
                q.rewardItemCount = 8;
            }),

            EnsureQuest(log, "Q6_BigSale", q =>
            {
                q.questId = "q6_big_sale";
                q.title = "ฟาร์มที่เลี้ยงตัวเองได้";
                q.offerLines = new[]
                {
                    "ถึงเวลาพิสูจน์ว่าฟาร์มนี้อยู่รอดได้ด้วยตัวเอง",
                    "ขายผลผลิตให้ได้รวม 20 ชิ้น จะเป็นอะไรก็ได้",
                    "ค่อยๆ ทำไป ปลูก รดน้ำ นอน เก็บ ขาย วนไปเรื่อยๆ"
                };
                q.objective = QuestObjective.SellCrop;
                q.objectiveText = "ขายผลผลิตรวม";
                q.targetCropId = "";
                q.requiredAmount = 20;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "ยี่สิบชิ้น! ฟาร์มร้างกลายเป็นฟาร์มจริงแล้ว",
                    "คุณปู่เธอคงภูมิใจมากถ้าได้เห็น"
                };
                q.rewardMoney = 800;
                q.rewardItem = FindItem("wheat_seed");
                q.rewardItemCount = 10;
            }),

            EnsureQuest(log, "Q7_StrangeTracks", q =>
            {
                q.questId = "q7_strange_tracks";
                q.title = "รอยเท้าประหลาด";
                q.offerLines = new[]
                {
                    "นี่... เธอเห็นรอยเท้าแถวชายป่าหลังฟาร์มไหม",
                    "ไม่เหมือนของสัตว์อะไรที่ฉันเคยเห็นเลย",
                    "คืนก่อนฉันได้ยินเสียงด้วย เหมือนมีอะไรมาเดินวนแถวนี้",
                    "ระวังตัวไว้ก็ดีนะ... หรือมันอาจจะไม่ได้น่ากลัวอย่างที่คิด"
                };
                q.objective = QuestObjective.TalkOnly;
                q.objectiveText = "คุยกับพี่สาวชาวบ้าน";
                q.targetCropId = "";
                q.requiredAmount = 1;
                q.returnToNPC = false;            // คุยจบก็จบเลย
                q.completeLines = new[]
                {
                    "ไว้รู้อะไรเพิ่มจะบอกนะ"
                };
                q.rewardMoney = 300;
                q.rewardItem = null;
                q.rewardItemCount = 0;
            }),

            EnsureQuest(log, "Q8_PlantTomato", q =>
            {
                q.questId = "q8_plant_tomato";
                q.title = "สวนมะเขือเทศ";
                q.offerLines = new[]
                {
                    "มีของใหม่มาลงร้านแล้ว — เมล็ดมะเขือเทศกับเมล็ดฟักทอง",
                    "มะเขือเทศขายได้ 38 บาท แพงกว่าแครอทนิดหน่อย แต่ปลูกง่ายพอกัน",
                    "กด F ที่ฉันแล้วเลือกแท็บซื้อเมล็ด เอาไปปลูกสัก 5 ต้นสิ"
                };
                q.objective = QuestObjective.PlantCrop;
                q.objectiveText = "ปลูกมะเขือเทศ";
                q.targetCropId = "tomato";
                q.requiredAmount = 5;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "แปลงมะเขือเทศสวยมาก สีแดงสดเลย",
                    "นี่ เมล็ดฟักทองไปลองปลูกดู ตัวนี้แหละของจริง"
                };
                q.rewardMoney = 500;
                q.rewardItem = FindItem("pumpkin_seed");
                q.rewardItemCount = 5;
            }),

            EnsureQuest(log, "Q9_HarvestPumpkin", q =>
            {
                q.questId = "q9_harvest_pumpkin";
                q.title = "ฟักทองยักษ์";
                q.offerLines = new[]
                {
                    "ฟักทองเป็นพืชที่แพงที่สุดในหมู่บ้าน ลูกละ 70 บาท",
                    "เมล็ดก็แพงตาม 25 บาท แต่ปลูกทีเดียวคุ้มยาว",
                    "เก็บเกี่ยวมาให้ได้ 5 ลูก แล้วฉันจะให้อะไรดีๆ"
                };
                q.objective = QuestObjective.HarvestCrop;
                q.objectiveText = "เก็บเกี่ยวฟักทอง";
                q.targetCropId = "pumpkin";
                q.requiredAmount = 5;
                q.returnToNPC = true;
                q.completeLines = new[]
                {
                    "ห้าลูกเต็มๆ! ลูกใหญ่กว่าที่ฉันเคยปลูกอีก",
                    "ฟาร์มนี้กลายเป็นฟาร์มที่ดีที่สุดในหมู่บ้านไปแล้วนะ",
                    "เอาเมล็ดฟักทองไปอีกชุด ปลูกต่อได้เลย"
                };
                q.rewardMoney = 1200;
                q.rewardItem = FindItem("pumpkin_seed");
                q.rewardItemCount = 10;
            })
        };

        // ---- QuestManager + QuestUI ----
        var qm = Object.FindFirstObjectByType<QuestManager>();
        if (qm == null)
        {
            var go = new GameObject("QuestManager");
            Undo.RegisterCreatedObjectUndo(go, "Create QuestManager");
            qm = go.AddComponent<QuestManager>();
            go.AddComponent<QuestUI>();
            go.AddComponent<RewardPopupUI>();
            log.Add("สร้าง GameObject 'QuestManager' + QuestUI + RewardPopupUI");
        }
        else
        {
            if (qm.GetComponent<QuestUI>() == null)
                Undo.AddComponent<QuestUI>(qm.gameObject);

            if (qm.GetComponent<RewardPopupUI>() == null)
            {
                Undo.AddComponent<RewardPopupUI>(qm.gameObject);
                log.Add("เพิ่ม RewardPopupUI (หน้าต่างแสดงรางวัล)");
            }
        }

        Undo.RecordObject(qm, "Setup QuestManager");
        qm.quests = quests;

        // เพิ่มเควสใหม่ตอนที่เล่นจบไปแล้ว -> ปลุกให้รับเควสต่อได้
        if (qm.state == QuestState.AllDone && qm.currentIndex < quests.Count)
        {
            qm.state = QuestState.Offered;
            qm.progress = 0;
            log.Add($"มีเควสใหม่ {quests.Count - qm.currentIndex} อัน — ไปคุยกับ NPC เพื่อรับได้เลย");
        }

        EditorUtility.SetDirty(qm);

        // ---- DialogueUI ----
        if (Object.FindFirstObjectByType<DialogueUI>() == null)
        {
            var go = new GameObject("DialogueUI");
            Undo.RegisterCreatedObjectUndo(go, "Create DialogueUI");
            go.AddComponent<DialogueUI>();
            log.Add("สร้าง GameObject 'DialogueUI' (กล่องบทสนทนา)");
        }

        // ---- ShopUI (หน้าต่างขายผลผลิต) ----
        if (Object.FindFirstObjectByType<ShopUI>() == null)
        {
            var go = new GameObject("ShopUI");
            Undo.RegisterCreatedObjectUndo(go, "Create ShopUI");
            go.AddComponent<ShopUI>();
            log.Add("สร้าง GameObject 'ShopUI' (หน้าต่างขายผลผลิต - กด F)");
        }

        // ---- ม่านดำตอนวาร์ป ----
        if (Object.FindFirstObjectByType<ScreenFadeUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("ScreenFade");
            Undo.RegisterCreatedObjectUndo(go, "Create ScreenFade");
            go.AddComponent<ScreenFadeUI>();
            log.Add("สร้าง GameObject 'ScreenFade' (เอฟเฟกต์จอมืดตอนวาร์ป)");
        }

        // ---- เตียงนอน ----
        SetupBed(log);

        // ---- สัตว์ ----
        SetupAnimals(log, allItems);

        // ---- เสียง ----
        SetupAudio(log);

        // ---- กลางวัน / กลางคืน ----
        // ตัวแปร lantern อยู่คนละฟังก์ชัน เลยต้องหยิบจากรายการไอเทมที่ส่งเข้ามาแทน
        SetupDayNight(log, FindItem("lantern"));

        // ---- ปรับไฟที่วางไว้แล้วให้ใช้ค่าล่าสุด ----
        int lamps = StreetLampTool.RefreshAllLamps();
        if (lamps > 0) log.Add($"ปรับความสว่างไฟในฉากแล้ว {lamps} ดวง");

        // ---- จอข้ามวัน ----
        if (Object.FindFirstObjectByType<DayTransitionUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("DayTransition");
            Undo.RegisterCreatedObjectUndo(go, "Create DayTransition");
            go.AddComponent<DayTransitionUI>();
            log.Add("สร้าง GameObject 'DayTransition' (จอมืดตอนข้ามวัน + ป้ายบอกวัน)");
        }

        // ---- คัตซีนเปิดเกม ----
        SetupCutscene(log);

        // ---- NPC ----
        // ลบตัวที่แปะผิดที่ออกก่อน
        // (เคยมีบั๊ก: ไปเกาะอยู่กับลูกของป้าย GO TO FARM พอปิดป้ายนั้น NPC ก็ใช้ไม่ได้)
        foreach (var stray in Object.FindObjectsByType<NPCInteractable>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (IsUsableNPC(stray.gameObject)) continue;

            log.Add($"ลบ NPCInteractable ที่แปะผิดที่บน '{stray.gameObject.name}'");
            Undo.DestroyObjectImmediate(stray);
        }

        var npc = Object.FindFirstObjectByType<NPCInteractable>();
        if (npc == null)
        {
            var npcGO = FindNPCGameObject();
            if (npcGO == null)
            {
                log.Add("⚠ ไม่พบ NPC ใน Scene — ต้องใส่ NPCInteractable ให้ NPC เอง");
            }
            else
            {
                npc = Undo.AddComponent<NPCInteractable>(npcGO);
                log.Add($"เพิ่ม NPCInteractable ให้ '{npcGO.name}'");

                // ปิดสคริปต์ทักทายแบบเก่า ไม่ให้ Canvas เด้งซ้อนกับกล่องคุยใหม่
                var oldTrigger = npcGO.GetComponent<NPCDialogueTrigger>();
                if (oldTrigger != null && oldTrigger.enabled)
                {
                    Undo.RecordObject(oldTrigger, "Disable old NPC trigger");
                    oldTrigger.enabled = false;
                    EditorUtility.SetDirty(oldTrigger);
                    log.Add("ปิด NPCDialogueTrigger ตัวเก่า (ไม่ได้ลบ เปิดคืนได้ใน Inspector)");
                }
            }
        }

        // ---- เปลี่ยนชื่อ NPC ให้เป็นชื่อล่าสุด (เฉพาะตอนยังเป็นชื่อเก่า) ----
        if (npc != null && (string.IsNullOrEmpty(npc.npcName) || npc.npcName == "พี่สาวชาวนา" || npc.npcName == "ลุงชาวนา"))
        {
            Undo.RecordObject(npc, "Rename NPC");
            npc.npcName = "พี่สาวชาวบ้าน";
            EditorUtility.SetDirty(npc);
            log.Add("ตั้งชื่อ NPC เป็น 'พี่สาวชาวบ้าน'");
        }

        // ---- ตั้งระยะคุยให้กว้างพอ ----
        if (npc != null && npc.interactRange < 4f)
        {
            Undo.RecordObject(npc, "Setup NPC Range");
            npc.interactRange = 4f;
            EditorUtility.SetDirty(npc);
            log.Add("ตั้งระยะคุยกับ NPC เป็น 4 ช่อง");
        }

        // ---- ให้ลูกศรชี้ไปหา NPC (ตั้งทับทุกครั้ง เผื่อเคยชี้ผิดตัว) ----
        var questUI = qm.GetComponent<QuestUI>();
        if (questUI != null && npc != null && questUI.pointTarget != npc.transform)
        {
            Undo.RecordObject(questUI, "Setup Quest UI");
            questUI.pointTarget = npc.transform;
            EditorUtility.SetDirty(questUI);
            log.Add($"ตั้งลูกศรให้ชี้ไปที่ '{npc.name}'");
        }

        // ---- ย้ายแถบเควสไปกลางจอฝั่งซ้าย (ค่าเก่าคือ -28 = ชิดบน) ----
        if (questUI != null && questUI.panelPosition.y < -1f)
        {
            Undo.RecordObject(questUI, "Move Quest Panel");
            questUI.panelPosition = new Vector2(28f, 0f);
            EditorUtility.SetDirty(questUI);
            log.Add("ย้ายแถบเควสไปกลางจอฝั่งซ้าย");
        }

        // ---- รูปตัวละครมุมซ้ายบน ----
        var portrait = Object.FindFirstObjectByType<PlayerPortraitUI>(FindObjectsInactive.Include);
        if (portrait == null)
        {
            var go = new GameObject("PlayerPortrait");
            Undo.RegisterCreatedObjectUndo(go, "Create PlayerPortrait");
            portrait = go.AddComponent<PlayerPortraitUI>();
            log.Add("สร้าง GameObject 'PlayerPortrait' (รูปตัวละคร + แผ่นป้ายสเตตัส)");
        }

        // ---- สมุดบันทึก (กด J) ----
        if (Object.FindFirstObjectByType<EncyclopediaUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("Encyclopedia");
            Undo.RegisterCreatedObjectUndo(go, "Create Encyclopedia");
            go.AddComponent<EncyclopediaUI>();
            log.Add("สร้าง GameObject 'Encyclopedia' (สมุดบันทึกรวมของในเกม — กด J)");
        }

        // ---- โหมดเจ้าของเกม (กด F9) ----
        // ลบ GameObject 'AdminMode' ทิ้งได้เลยตอนส่งงานจริง เกมยังทำงานครบ
        if (Object.FindFirstObjectByType<AdminModeUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("AdminMode");
            Undo.RegisterCreatedObjectUndo(go, "Create AdminMode");
            go.AddComponent<AdminModeUI>();
            log.Add("สร้าง GameObject 'AdminMode' (โหมดเจ้าของเกม — กด F9)");
        }

        // ---- แผ่นสอนเล่น (ปุ่มควบคุม) ----
        if (Object.FindFirstObjectByType<ControlsHintUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("ControlsHint");
            Undo.RegisterCreatedObjectUndo(go, "Create ControlsHint");
            go.AddComponent<ControlsHintUI>();
            log.Add("สร้าง GameObject 'ControlsHint' (แผ่นสอนเล่น กด H)");
        }

        // ---- เมนูตั้งค่า (ปุ่มฟันเฟือง) ----
        if (Object.FindFirstObjectByType<SettingsMenuUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("SettingsMenu");
            Undo.RegisterCreatedObjectUndo(go, "Create SettingsMenu");
            go.AddComponent<SettingsMenuUI>();
            log.Add("สร้าง GameObject 'SettingsMenu' (ปุ่มฟันเฟือง + เซฟ + รีสตาร์ท)");
        }

        // ---- หน้าตั้งชื่อตัวละคร ----
        if (Object.FindFirstObjectByType<NameEntryUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("NameEntry");
            Undo.RegisterCreatedObjectUndo(go, "Create NameEntry");
            go.AddComponent<NameEntryUI>();
            log.Add("สร้าง GameObject 'NameEntry' (หน้าตั้งชื่อตัวละคร)");
        }

        // ---- ป้ายชื่อเหนือหัวตัวละคร ----
        if (Object.FindFirstObjectByType<PlayerNameTagUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("PlayerNameTag");
            Undo.RegisterCreatedObjectUndo(go, "Create PlayerNameTag");
            go.AddComponent<PlayerNameTagUI>();
            log.Add("สร้าง GameObject 'PlayerNameTag' (ป้ายชื่อลอยเหนือหัว)");
        }

        // ค่าเดิมซูมไว้ 2.2 เห็นแค่ครึ่งตัว -> ย่อให้เห็นเต็มตัว
        if (portrait.zoom > 1.5f)
        {
            Undo.RecordObject(portrait, "Fix Portrait Zoom");
            portrait.zoom = 1.05f;
            portrait.portraitOffset = new Vector2(0f, -4f);
            EditorUtility.SetDirty(portrait);
            log.Add("ย่อรูปตัวละครให้เห็นเต็มตัว");
        }
    }

    // ==================== คัตซีนเปิดเกม ====================

    /// <summary>โฟลเดอร์เก็บภาพคัตซีน — ใช้ไฟล์ Scene1.png ถึง Scene6.png</summary>
    private const string CutsceneArtFolder = "Assets/Art/Cutscene";

    private static void SetupCutscene(List<string> log)
    {
        var cutscene = Object.FindFirstObjectByType<CutsceneUI>();
        if (cutscene == null)
        {
            var go = new GameObject("Cutscene");
            Undo.RegisterCreatedObjectUndo(go, "Create Cutscene");
            cutscene = go.AddComponent<CutsceneUI>();
            go.AddComponent<IntroCutscene>();
            log.Add("สร้าง GameObject 'Cutscene' (คัตซีนเปิดเกม)");
        }
        else if (cutscene.GetComponent<IntroCutscene>() == null)
        {
            Undo.AddComponent<IntroCutscene>(cutscene.gameObject);
            log.Add("เพิ่ม IntroCutscene");
        }

        var intro = cutscene.GetComponent<IntroCutscene>();

        // ---- ตั้งบท 6 ฉาก แล้วใส่ภาพให้ครบ ----
        SetupCutsceneStory(intro, log);

        // ---- หน้าเมนูหลัก ----
        SetupTitleScreen(log);
    }

    // ==================== เตียงนอน ====================

    private const string BedSpritePath =
        "Assets/HappyHarvest/Art/Interior/Bed/Sprites/Sprite_Bed.png";

    private static void SetupBed(List<string> log)
    {
        if (Object.FindFirstObjectByType<BedInteractable>(FindObjectsInactive.Include) != null) return;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BedSpritePath);
        if (sprite == null)
        {
            log.Add("⚠ ไม่พบรูปเตียง — ยังใช้ปุ่ม N ข้ามวันได้เหมือนเดิม");
            return;
        }

        var go = new GameObject("Bed");
        Undo.RegisterCreatedObjectUndo(go, "Create Bed");

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Ground";
        renderer.sortingOrder = 5;

        go.AddComponent<BedInteractable>();

        // วางไว้ทางซ้ายของผู้เล่น จะได้ไม่ทับคอกสัตว์ที่อยู่ทางขวา
        var player = GameObject.FindGameObjectWithTag("Player");
        go.transform.position = player != null
            ? player.transform.position + new Vector3(-4f, 1.5f, 0f)
            : Vector3.zero;

        log.Add("สร้าง GameObject 'Bed' (เดินไปกด E เพื่อนอน — ลากย้ายตำแหน่งได้)");
    }

    // ==================== สัตว์ ====================

    private const string AnimalFolder = "Assets/Script/Animal/Animals";

    /// <summary>ข้อมูลตั้งต้นของสัตว์แต่ละชนิด</summary>
    /// <summary>
    /// scale = ขนาดตัวเทียบกัน ให้ดูสมส่วนตามชนิดสัตว์
    /// แฮมสเตอร์เล็กสุด ยีราฟใหญ่สุด
    /// </summary>
    /// <summary>
    /// สัตว์โตเต็มวัยจะผลิต "เมล็ดพืช" ให้ เจ้าของฟาร์มจะได้ไม่ต้องเสียเงินซื้อเมล็ด
    /// สัตว์ยิ่งแพง ยิ่งให้เมล็ดพืชที่แพง
    ///
    /// adultPrice = ซื้อตัวโต ใช้ได้ทันที
    /// eggPrice   = ซื้อไข่ ถูกกว่า แต่ต้องเลี้ยง 3 วัน
    /// </summary>
    private static readonly (string file, string id, string thai,
                             int adultPrice, int eggPrice,
                             string seedId, float scale)[] AnimalDefs =
    {
        ("Husky",     "husky",     "ฮัสกี้หิมะ",     360, 200, "wheat_seed",   1.10f),
        ("Pigbear",   "pigbear",   "หมูหมี",        420, 240, "carrot_seed",  1.30f),
        ("Girasheep", "girasheep", "ยีราฟขนแกะ",    560, 320, "tomato_seed",  1.45f),
        ("Beemster",  "beemster",  "แฮมสเตอร์ผึ้ง", 680, 390, "corn_seed",    0.85f),
        ("Sharkbun",  "sharkbun",  "กระต่ายฉลาม",   900, 520, "pumpkin_seed", 1.00f),
    };

    private static void SetupAnimals(List<string> log, List<ItemData> allItems)
    {
        EnsureFolder("Assets/Script", "Animal");
        EnsureFolder("Assets/Script/Animal", "Animals");

        // วาดไอคอนผลผลิตชั่วคราวให้ก่อน ถ้ายังไม่มีรูปจริง
        ProduceIconGenerator.Generate(log);

        // จัดชื่อกับตั้งค่ารูปไอคอนไข่ก่อน ไอเทมถึงจะมีรูปให้ใส่
        EggIconImporter.Run(log);

        // รูปที่วาดมาเอง — ลบพื้นหลังดำ ตัดขอบว่าง จัดให้เป็นจัตุรัส
        ItemIconProcessor.Run(log);

        // ตัดภาพก่อน ข้อมูลสัตว์ถึงจะมีรูปให้ใส่
        AnimalSheetSlicer.SliceIfNeeded(log);

        // ภาพที่ตัดไว้แล้วก็ปรับขนาดตามค่าล่าสุด ไม่ต้องตัดใหม่
        AnimalSheetSlicer.RefreshExistingImports(log);

        var assets = new List<AnimalData>();

        foreach (var def in AnimalDefs)
        {
            // ผลผลิตของสัตว์ = เมล็ดพืชที่ขายในร้าน
            // เลี้ยงสัตว์แล้วได้เมล็ดฟรี ไม่ต้องเสียเงินซื้อ
            var seed = allItems.Find(i => i != null && i.itemId == def.seedId);

            var animal = EnsureAnimal(log, def.file, def.id, def.thai,
                                      def.adultPrice, def.eggPrice, seed, def.scale);
            if (animal == null) continue;

            string animalSprite =
                $"{AnimalSheetSlicer.AnimalArtRoot}/{def.file}/Sprite_{def.file}_0.png";

            // ---- ไข่ — ฟักออกมาเป็นตัวเล็กของชนิดนี้เท่านั้น ----
            var egg = EnsureItem(log, def.file + "Egg", def.id + "_egg",
                "ไข่" + def.thai, ItemKind.Animal,
                $"Assets/Art/Items/{def.file}Egg.png",
                maxStack: 9, buyPrice: def.eggPrice, sellPrice: 0);

            if (egg != null)
            {
                egg.animal = animal;
                egg.kind = ItemKind.Animal;
                egg.spawnAsAdult = false;
                egg.buyPrice = def.eggPrice;

                // ยังไม่มีรูปไข่ -> ยืมรูปตัวสัตว์มาใช้ชั่วคราว ช่องจะได้ไม่ว่าง
                if (egg.icon == null)
                    egg.icon = AssetDatabase.LoadAssetAtPath<Sprite>(animalSprite);

                EditorUtility.SetDirty(egg);
            }

            // ---- ตัวโตเต็มวัย — วางแล้วใช้งานได้ทันที ----
            var adult = EnsureItem(log, def.file + "Adult", def.id + "_adult",
                def.thai, ItemKind.Animal, animalSprite,
                maxStack: 9, buyPrice: def.adultPrice, sellPrice: 0);

            if (adult != null)
            {
                adult.animal = animal;
                adult.kind = ItemKind.Animal;
                adult.spawnAsAdult = true;
                adult.buyPrice = def.adultPrice;
                adult.displayName = def.thai;

                if (adult.icon == null)
                    adult.icon = AssetDatabase.LoadAssetAtPath<Sprite>(animalSprite);

                EditorUtility.SetDirty(adult);
            }

            animal.eggItem = egg;
            animal.adultItem = adult;
            EditorUtility.SetDirty(animal);

            // ต้องอยู่ในรายชื่อไอเทมทั้งหมดด้วย ไม่งั้น:
            //   - ตอนโหลดเซฟจะหาไข่/สัตว์ในกระเป๋าไม่เจอ แล้วของหายไปเฉยๆ
            //   - โหมดเจ้าของเกมเสกออกมาไม่ได้
            if (egg != null && !allItems.Contains(egg)) allItems.Add(egg);
            if (adult != null && !allItems.Contains(adult)) allItems.Add(adult);

            assets.Add(animal);
        }

        // ---- หน้าต่างดูรายละเอียดสัตว์ ----
        if (Object.FindFirstObjectByType<AnimalInfoUI>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("AnimalInfo");
            Undo.RegisterCreatedObjectUndo(go, "Create AnimalInfo");
            go.AddComponent<AnimalInfoUI>();
            log.Add("สร้าง GameObject 'AnimalInfo' (หน้าต่างดูค่าสัตว์ — คลิกขวาที่ตัวสัตว์)");
        }

        // ---- ตัวจัดการสัตว์ + คอก ----
        var manager = Object.FindFirstObjectByType<AnimalManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("AnimalManager");
            Undo.RegisterCreatedObjectUndo(go, "Create AnimalManager");
            manager = go.AddComponent<AnimalManager>();

            // วางคอกไว้ข้างๆ ผู้เล่น จะได้เดินไปเจอง่าย
            var player = GameObject.FindGameObjectWithTag("Player");
            go.transform.position = player != null
                ? player.transform.position + new Vector3(6f, 0f, 0f)
                : Vector3.zero;

            log.Add("สร้าง GameObject 'AnimalManager' (คอกสัตว์ — ลากย้ายตำแหน่งได้)");
        }

        Undo.RecordObject(manager, "Setup Animals");
        manager.animals = assets;

        // ตอนนี้สัตว์ต้องซื้อเอาเอง ไม่แจกฟรีตอนเริ่มเกมแล้ว
        // (ค่าเดิมถูกเก็บไว้ในไฟล์ฉาก แก้ในโค้ดอย่างเดียวไม่พอ ต้องมาเซ็ตทับตรงนี้)
        if (manager.spawnAllAtStart)
        {
            manager.spawnAllAtStart = false;
            log.Add("ปิดการแจกสัตว์ฟรีตอนเริ่มเกม — ต้องไปซื้อที่ร้าน "
                    + "(อยากเปิดคืนให้ติ๊ก Spawn All At Start ที่ AnimalManager หลังรัน Setup)");
        }

        // ชั้น Default อยู่ล่างสุดในโปรเจกต์นี้ สัตว์จะโดนพื้นดินบังจนมองไม่เห็น
        if (manager.sortingLayer == "Default" || string.IsNullOrEmpty(manager.sortingLayer))
        {
            manager.sortingLayer = "Player";
            log.Add("ย้ายสัตว์ไปวาดบนชั้น 'Player' (ชั้น Default โดนพื้นดินบัง)");
        }

        // คอกอยู่ไกลผู้เล่นเกินไป = หาไม่เจอ ดึงกลับมาไว้ข้างๆ
        var playerNow = GameObject.FindGameObjectWithTag("Player");
        if (playerNow != null)
        {
            float away = Vector2.Distance(manager.transform.position, playerNow.transform.position);
            if (away > 30f)
            {
                manager.transform.position = playerNow.transform.position + new Vector3(6f, 0f, 0f);
                log.Add("ย้ายคอกสัตว์กลับมาใกล้ผู้เล่น (เมื่อก่อนอยู่ไกลเกินไป)");
            }
        }

        EditorUtility.SetDirty(manager);

        var pen = manager.transform.position;
        log.Add($"คอกสัตว์อยู่ที่ตำแหน่ง ({pen.x:F1}, {pen.y:F1}) — ลาก GameObject 'AnimalManager' ย้ายได้");

        log.Add(assets.Count > 0
            ? $"ใส่สัตว์ให้คอกแล้ว {assets.Count} ชนิด"
            : "⚠ ยังไม่มีสัตว์ — วางไฟล์ภาพใน Assets/Art/Animals/ ก่อน");
    }

    private static AnimalData EnsureAnimal(List<string> log, string fileName, string id,
                                           string thaiName, int adultPrice, int eggPrice,
                                           ItemData produce, float scale)
    {
        string path = $"{AnimalFolder}/{fileName}.asset";
        var data = AssetDatabase.LoadAssetAtPath<AnimalData>(path);

        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<AnimalData>();

        // เก็บเฟรมทั้งหมดที่ตัดไว้
        var frames = new List<Sprite>();
        for (int i = 0; i < 12; i++)
        {
            string spritePath = $"{AnimalSheetSlicer.AnimalArtRoot}/{fileName}/Sprite_{fileName}_{i}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null) break;
            frames.Add(sprite);
        }

        if (frames.Count == 0)
        {
            if (isNew) Object.DestroyImmediate(data);
            return null;
        }

        data.animalId = id;
        data.displayName = thaiName;
        data.frames = frames.ToArray();
        data.scale = scale;
        data.buyPrice = adultPrice;
        data.eggPrice = eggPrice;
        data.produceItem = produce;
        data.producePrice = produce != null ? produce.buyPrice : 0;
        data.dailyIncome = data.producePrice;   // ใช้ตอนยังไม่มีไอเทมผลผลิต

        if (isNew)
        {
            AssetDatabase.CreateAsset(data, path);
            log.Add($"สร้างข้อมูลสัตว์ {thaiName} ({frames.Count} เฟรม)");
        }
        else
        {
            EditorUtility.SetDirty(data);
        }

        return data;
    }

    // ==================== เสียง ====================

    private const string AudioRoot = "Assets/HappyHarvest/Audio";

    private static void SetupAudio(List<string> log)
    {
        var audio = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (audio == null)
        {
            var go = new GameObject("AudioManager");
            Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
            audio = go.AddComponent<AudioManager>();
            log.Add("สร้าง GameObject 'AudioManager'");
        }

        // ใส่ให้เฉพาะช่องที่ยังว่าง จะได้ไม่ทับของที่ผู้ใช้เปลี่ยนเอง
        Undo.RecordObject(audio, "Setup Audio");

        int filled = 0;

        if (audio.ambienceDay == null)
            filled += Assign(ref audio.ambienceDay, $"{AudioRoot}/Ambience/Background ambience outside - Day.wav");

        if (audio.ambienceNight == null)
            filled += Assign(ref audio.ambienceNight, $"{AudioRoot}/Ambience/Background ambience outside - Night.wav");

        if (audio.plantClip == null)
            filled += Assign(ref audio.plantClip, $"{AudioRoot}/Planting/Planting crop.wav");

        if (audio.harvestClip == null)
            filled += Assign(ref audio.harvestClip, $"{AudioRoot}/Planting/Picking up crop.wav");

        if (audio.buySellClip == null)
            filled += Assign(ref audio.buySellClip, $"{AudioRoot}/UI/Buy _ Sell.wav");

        if (audio.selectClip == null)
            filled += Assign(ref audio.selectClip, $"{AudioRoot}/UI/Select item.wav");

        if (audio.closeClip == null)
            filled += Assign(ref audio.closeClip, $"{AudioRoot}/UI/Close Window.wav");

        if (audio.cropReadyClip == null)
            filled += Assign(ref audio.cropReadyClip,
                $"{AudioRoot}/UI/Timer sound when the crops are ready.wav");

        if (audio.tillClips == null || audio.tillClips.Length == 0)
        {
            audio.tillClips = LoadClips(
                $"{AudioRoot}/Planting/Digging using a shovel-001.wav",
                $"{AudioRoot}/Planting/Digging using a shovel-002.wav",
                $"{AudioRoot}/Planting/Digging using a shovel-003.wav");

            filled += audio.tillClips.Length;
        }

        if (audio.waterClips == null || audio.waterClips.Length == 0)
        {
            audio.waterClips = LoadClips(
                $"{AudioRoot}/Planting/Watering crop-001.wav",
                $"{AudioRoot}/Planting/Watering crop-002.wav");

            filled += audio.waterClips.Length;
        }

        EditorUtility.SetDirty(audio);

        log.Add(filled > 0
            ? $"ใส่ไฟล์เสียงให้ AudioManager แล้ว {filled} เสียง"
            : "AudioManager มีเสียงครบอยู่แล้ว");
    }

    private static int Assign(ref AudioClip target, string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[เสียง] ไม่พบไฟล์: {path}");
            return 0;
        }

        target = clip;
        return 1;
    }

    private static AudioClip[] LoadClips(params string[] paths)
    {
        var list = new List<AudioClip>();

        foreach (var path in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) list.Add(clip);
            else Debug.LogWarning($"[เสียง] ไม่พบไฟล์: {path}");
        }

        return list.ToArray();
    }

    // ==================== กลางวัน / กลางคืน ====================

    private static void SetupDayNight(List<string> log, ItemData lantern = null)
    {
        var cycle = Object.FindFirstObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (cycle == null)
        {
            var go = new GameObject("DayNightCycle");
            Undo.RegisterCreatedObjectUndo(go, "Create DayNightCycle");
            cycle = go.AddComponent<DayNightCycle>();
            log.Add("สร้าง GameObject 'DayNightCycle' (ระบบกลางวัน–กลางคืน)");
        }

        // ---- ตะเกียงถือ ----
        if (lantern != null && cycle.lanternItem != lantern)
        {
            Undo.RecordObject(cycle, "Setup Lantern");
            cycle.lanternItem = lantern;
            EditorUtility.SetDirty(cycle);
            log.Add("ผูกตะเกียงเข้ากับระบบแสง — ถือแล้วสว่างขึ้น");
        }

        // ค่าเดิมแสงรอบตัวสว่างเกิน (1.15) -> หรี่ลงให้ตะเกียงมีความหมาย
        if (cycle.playerLightMax > 0.7f)
        {
            Undo.RecordObject(cycle, "Dim Player Light");
            cycle.playerLightMax = 0.45f;
            cycle.playerLightRadius = 3.2f;
            EditorUtility.SetDirty(cycle);
            log.Add("หรี่แสงรอบตัวผู้เล่นลง (ตอนมือเปล่า)");
        }

        // ค่าเดิมเวลาเดินไวไป (10 นาที/วัน) และวาร์ปเร็วไป (0.7 วิ) -> ปรับให้ช้าลง
        if (cycle.minutesPerDay <= 12f || cycle.warpTime <= 1f)
        {
            Undo.RecordObject(cycle, "Slow Down Day Night");

            if (cycle.minutesPerDay <= 12f) cycle.minutesPerDay = 30f;
            if (cycle.warpTime <= 1f) cycle.warpTime = 2.2f;

            EditorUtility.SetDirty(cycle);
            log.Add("ปรับเวลาให้เดินช้าลง (1 วัน = 30 นาทีจริง) และวาร์ปนุ่มขึ้น");
        }

        var lights = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        // ---- หาไฟที่ส่องทั้งฉาก ----
        UnityEngine.Rendering.Universal.Light2D globalLight = null;
        foreach (var light in lights)
        {
            if (light.lightType != UnityEngine.Rendering.Universal.Light2D.LightType.Global) continue;
            globalLight = light;
            break;
        }

        if (globalLight == null)
        {
            log.Add("⚠ ไม่พบ Global Light 2D ในฉาก — ฟ้าจะไม่มืดลง");
        }
        else if (cycle.globalLight != globalLight)
        {
            Undo.RecordObject(cycle, "Setup DayNight");
            cycle.globalLight = globalLight;
            EditorUtility.SetDirty(cycle);
            log.Add($"ผูกไฟทั้งฉาก '{globalLight.name}' เข้ากับระบบกลางวัน–กลางคืน");
        }

        // ---- ไฟรอบตัวผู้เล่น ----
        if (cycle.playerLight != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            log.Add("⚠ ไม่พบ Player — ยังไม่ได้สร้างไฟรอบตัว");
            return;
        }

        var existing = player.transform.Find("PlayerLight");
        UnityEngine.Rendering.Universal.Light2D playerLight;

        if (existing != null)
        {
            playerLight = existing.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        }
        else
        {
            var lightGO = new GameObject("PlayerLight");
            Undo.RegisterCreatedObjectUndo(lightGO, "Create PlayerLight");
            lightGO.transform.SetParent(player.transform, false);
            lightGO.transform.localPosition = Vector3.zero;

            playerLight = lightGO.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            playerLight.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            playerLight.color = new Color(1f, 0.93f, 0.72f);
            playerLight.intensity = 0f;
            playerLight.pointLightInnerRadius = 1.2f;
            playerLight.pointLightOuterRadius = 5.5f;
            playerLight.falloffIntensity = 0.7f;

            log.Add("สร้างไฟวงกลมรอบตัวผู้เล่น (สว่างเฉพาะตอนกลางคืน)");
        }

        if (playerLight == null) return;

        // ให้ส่องโดนเลเยอร์เดียวกับไฟทั้งฉาก ไม่งั้นจะไม่เห็นผล
        CopyTargetSortingLayers(globalLight, playerLight, log);

        Undo.RecordObject(cycle, "Setup DayNight");
        cycle.playerLight = playerLight;
        EditorUtility.SetDirty(cycle);
    }

    /// <summary>
    /// คัดลอกรายการ Sorting Layer ที่ไฟส่องถึง จากไฟทั้งฉากมาให้ไฟรอบตัว
    /// ค่านี้ไม่มี property ให้เซ็ตตรงๆ เลยต้องแก้ผ่าน SerializedObject
    /// </summary>
    private static void CopyTargetSortingLayers(
        UnityEngine.Rendering.Universal.Light2D source,
        UnityEngine.Rendering.Universal.Light2D target,
        List<string> log)
    {
        if (source == null || target == null) return;

        const string field = "m_ApplyToSortingLayers";

        var from = new SerializedObject(source).FindProperty(field);
        if (from == null || !from.isArray) return;

        var toObject = new SerializedObject(target);
        var to = toObject.FindProperty(field);
        if (to == null) return;

        to.arraySize = from.arraySize;
        for (int i = 0; i < from.arraySize; i++)
        {
            to.GetArrayElementAtIndex(i).intValue =
                from.GetArrayElementAtIndex(i).intValue;
        }

        toObject.ApplyModifiedProperties();
        log.Add("ตั้งให้ไฟรอบตัวส่องโดนเลเยอร์เดียวกับไฟทั้งฉาก");
    }

    /// <summary>หน้าเมนูที่ขึ้นก่อนคัตซีน</summary>
    private static void SetupTitleScreen(List<string> log)
    {
        var title = Object.FindFirstObjectByType<TitleScreenUI>(FindObjectsInactive.Include);
        if (title == null)
        {
            var go = new GameObject("TitleScreen");
            Undo.RegisterCreatedObjectUndo(go, "Create TitleScreen");
            title = go.AddComponent<TitleScreenUI>();
            log.Add("สร้าง GameObject 'TitleScreen' (หน้าเมนูหลัก)");
        }

        // ปุ่มบนจอต้องมี EventSystem ถึงจะกดได้
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            log.Add("สร้าง EventSystem (ไม่งั้นปุ่มบนจอกดไม่ได้)");
        }

        // เลิกใช้ Scene หน้าปกอันเก่า ไม่งั้นจะมีเมนูขึ้นซ้อนกันสองอัน
        DisableOldMenuScene(log);

        if (title.background != null) return;

        // ใช้ภาพฉากแรกเป็นพื้นหลังเมนู (ในภาพมีโลโก้เกมอยู่แล้ว)
        var bg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Cutscene/Scene1.png");
        if (bg == null) return;

        Undo.RecordObject(title, "Setup TitleScreen");
        title.background = bg;
        EditorUtility.SetDirty(title);
        log.Add("ใส่ภาพพื้นหลังให้หน้าเมนูหลัก");
    }

    /// <summary>
    /// ปิด Scene หน้าปกอันเก่าใน Build Settings (ไม่ได้ลบไฟล์ เปิดคืนได้)
    /// ตอนนี้เมนูอยู่ในฉากเกมแล้ว ถ้าเปิดทั้งคู่จะเจอเมนูสองรอบ
    /// </summary>
    private static void DisableOldMenuScene(List<string> log)
    {
        const string oldMenu = "Assets/Scenes/MainMenu.unity";

        var scenes = EditorBuildSettings.scenes;
        bool changed = false;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path != oldMenu || !scenes[i].enabled) continue;
            scenes[i].enabled = false;
            changed = true;
        }

        if (!changed) return;

        EditorBuildSettings.scenes = scenes;
        log.Add("ปิด Scene หน้าปกอันเก่าใน Build Settings (ไฟล์ยังอยู่ เปิดคืนได้)");
    }

    /// <summary>
    /// อัปเดตบทเป็นเวอร์ชัน 6 ฉาก และใส่ภาพรายฉากให้ถ้ามีไฟล์อยู่
    /// ไฟล์ที่มองหา: Assets/Art/Cutscene/Scene1.png ... Scene6.png
    /// </summary>
    private static void SetupCutsceneStory(IntroCutscene intro, List<string> log)
    {
        if (intro == null) return;

        Undo.RecordObject(intro, "Setup Intro Cutscene");

        // บทเวอร์ชันเก่ามี 11 บรรทัด -> เปลี่ยนเป็น 6 ฉาก
        if (intro.lines == null || intro.lines.Length != 6)
        {
            intro.lines = IntroCutscene.DefaultStory();
            log.Add("ตั้งบทคัตซีนเปิดเกมเป็น 6 ฉาก");
        }

        // ฉากที่ i ใช้ไฟล์ Scene(i+1).png เสมอ — ใส่ทับทุกครั้งให้ตรงลำดับแน่นอน
        int found = 0;
        var missing = new List<int>();

        for (int i = 0; i < intro.lines.Length; i++)
        {
            string path = $"{CutsceneArtFolder}/Scene{i + 1}.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
            {
                missing.Add(i + 1);
                continue;
            }

            FixCutsceneImport(tex, log);
            intro.lines[i].image = tex;
            found++;
        }

        log.Add($"ใส่ภาพให้คัตซีนแล้ว {found}/{intro.lines.Length} ฉาก (Scene1–Scene6)");

        if (missing.Count > 0)
            log.Add($"⚠ ไม่พบไฟล์ภาพฉาก: {string.Join(", ", missing.ConvertAll(n => $"Scene{n}.png"))}");

        EditorUtility.SetDirty(intro);
    }

    /// <summary>ตั้งค่านำเข้าไฟล์รูปให้คมและไม่เพี้ยน</summary>
    private static void FixCutsceneImport(Texture2D tex, List<string> log)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;   // RawImage ใช้ Texture ธรรมดา
            changed = true;
        }
        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;            // กันภาพช่องข้างๆ เล็ดลอด
            changed = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;                       // ภาพจะได้ไม่เบลอ
            changed = true;
        }
        if (importer.maxTextureSize < 2048)
        {
            importer.maxTextureSize = 2048;                       // เก็บรายละเอียดพิกเซลอาร์ต
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            // ภาพคัตซีนมีแค่ 6 รูป ยอมกินแรมเพิ่มเพื่อให้ท้องฟ้าไล่สีเนียน
            // ถ้าบีบอัด จะเห็นรอยด่างเป็นตารางตรงพื้นที่สีเรียบๆ
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;   // ห้ามยืดขนาด ไม่งั้นช่องเพี้ยน
            changed = true;
        }

        if (!changed) return;

        importer.SaveAndReimport();
        log.Add("ปรับการนำเข้าไฟล์รูปคัตซีนให้คมและไม่เพี้ยน");
    }

    /// <summary>หา GameObject ที่น่าจะเป็น NPC ใน Scene</summary>
    private static GameObject FindNPCGameObject()
    {
        // 1. ตัวที่มีสคริปต์ทักทายเก่าอยู่แล้ว = NPC แน่นอน
        var oldTrigger = Object.FindFirstObjectByType<NPCDialogueTrigger>();
        if (oldTrigger != null && IsUsableNPC(oldTrigger.gameObject))
            return oldTrigger.gameObject;

        // 2. ชื่อมีคำว่า npc หรือ character
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            var go = sr.gameObject;
            if (!IsUsableNPC(go)) continue;

            string n = go.name.ToLowerInvariant();
            if (n.Contains("npc") || n.Contains("character"))
                return go;
        }
        return null;
    }

    /// <summary>
    /// กรองตัวที่เอามาเป็น NPC ไม่ได้
    ///
    /// เคยเกิดบั๊ก: Wizard ไปแปะ NPCInteractable ใส่ลูกของป้าย "GO TO FARM"
    /// พอผู้ใช้ปิดป้ายนั้น สคริปต์ NPC ก็ถูกปิดตามไปด้วย เกมเลยคุยกับ NPC ไม่ได้
    /// </summary>
    private static bool IsUsableNPC(GameObject go)
    {
        if (go == null) return false;
        if (!go.activeInHierarchy) return false;                       // ถูกปิดอยู่
        if (go.CompareTag("Player")) return false;                     // ตัวผู้เล่นเอง
        if (PrefabUtility.IsPartOfPrefabInstance(go)) return false;    // เป็นชิ้นส่วนของ Prefab อื่น
        return true;
    }

    private static QuestData EnsureQuest(List<string> log, string assetName, System.Action<QuestData> fill)
    {
        string path = $"{QuestFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<QuestData>(path);
        if (existing != null) return existing;

        var data = ScriptableObject.CreateInstance<QuestData>();
        fill(data);

        AssetDatabase.CreateAsset(data, path);
        log.Add($"สร้างเควส {assetName}: {data.title}");
        return data;
    }

    /// <summary>สร้างไฟล์ CropData ถ้ายังไม่มี (ถ้ามีอยู่แล้วจะไม่แตะค่าเดิม)</summary>
    private static CropData EnsureCrop(List<string> log, CropDef def)
    {
        string path = $"{CropFolder}/{def.assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CropData>(path);
        if (existing != null) return existing;

        var data = ScriptableObject.CreateInstance<CropData>();
        data.cropId = def.id;
        data.displayName = def.thaiName;
        data.daysToGrow = 3;
        data.produceAmount = 1;
        data.sellPrice = def.sellPrice;
        data.seedPrice = def.seedPrice;

        AssetDatabase.CreateAsset(data, path);
        log.Add($"สร้างพืช {def.assetName}");
        return data;
    }

    /// <summary>
    /// สร้าง Tile ธรรมดาของเราเองจากไฟล์รูปแต่ละระยะ
    ///
    /// ทำไมต้องสร้างเอง: Tile ของ HappyHarvest (Tile_Carrot_01 ฯลฯ) เป็น RuleTile
    /// ที่ไม่มีรูปอยู่ในตัว มันไปเรียก Prefab ของ HappyHarvest มาแสดงแทน
    /// ซึ่ง Prefab นั้นต้องพึ่ง GameManager/TerrainManager ที่เราไม่ได้ใช้ -> พืชเลยไม่โผล่
    /// </summary>
    private static TileBase[] BuildCropTiles(List<string> log, CropDef def)
    {
        string folder = $"{FarmingFolder}/CropTiles";
        var tiles = new List<TileBase>();
        int missing = 0;

        for (int i = 0; i < StagesPerCrop; i++)
        {
            // พืชบางชนิดมีรูป 5 ระยะ บางชนิด 4 ระยะ
            // เลือกมาให้เหลือ 4 ระยะเท่ากันทุกชนิด โดยเอารูปแรกและรูปสุดท้ายเสมอ
            int pick = Mathf.RoundToInt(i * (def.stageCount - 1) / (float)(StagesPerCrop - 1));
            int spriteNumber = def.spriteStart + pick;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(def.StagePath(spriteNumber));
            if (sprite == null)
            {
                missing++;
                continue;
            }

            string tilePath = $"{folder}/{def.assetName}_Stage{i}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                tile.color = Color.white;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else
            {
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }

            tiles.Add(tile);
        }

        if (missing > 0)
            log.Add($"⚠ {def.assetName}: หารูปไม่เจอ {missing} ระยะ");

        log.Add($"สร้าง Tile พืช {def.assetName} {tiles.Count} ระยะ (จากไฟล์รูปโดยตรง)");
        return tiles.ToArray();
    }

    /// <summary>สร้างไฟล์ ItemData ถ้ายังไม่มี</summary>
    private static ItemData EnsureItem(List<string> log, string assetName, string id, string thaiName,
        ItemKind kind, string iconPath, int maxStack, int buyPrice, int sellPrice,
        ToolType toolType = ToolType.Hand, CropData crop = null)
    {
        string path = $"{ItemFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);

        if (existing != null)
        {
            // ไอเทมที่สร้างไว้ตอนยังไม่มีไฟล์รูป จะไม่มีไอคอนติดมา
            // เจอทีหลังเมื่อไหร่ก็เติมให้ ส่วนค่าอื่นไม่แตะ เผื่อผู้ใช้แก้เองไว้
            if (existing.icon == null)
            {
                var found = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (found != null)
                {
                    existing.icon = found;
                    EditorUtility.SetDirty(existing);
                    log.Add($"เติมไอคอนให้ {assetName} (เมื่อก่อนยังไม่มีไฟล์รูป)");
                }
            }

            // พืชที่เพิ่งมีรูปทีหลังก็ต้องผูกกลับเข้าไปด้วย
            if (existing.crop == null && crop != null)
            {
                existing.crop = crop;
                EditorUtility.SetDirty(existing);
            }

            return existing;
        }

        var data = ScriptableObject.CreateInstance<ItemData>();
        data.itemId = id;
        data.displayName = thaiName;
        data.kind = kind;
        data.toolType = toolType;
        data.crop = crop;
        data.maxStack = maxStack;
        data.buyPrice = buyPrice;
        data.sellPrice = sellPrice;
        data.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

        if (data.icon == null)
            log.Add($"⚠ {assetName}: ไม่พบไอคอนที่ {iconPath} — ช่องจะว่าง ให้ลากรูปใส่เอง");

        AssetDatabase.CreateAsset(data, path);
        log.Add($"สร้างไอเทม {assetName}");
        return data;
    }

    /// <summary>สร้าง Tilemap ลูกของ Grid ถ้ายังไม่มี</summary>
    private static Tilemap EnsureTilemap(List<string> log, Transform gridTransform,
        string name, string sortingLayer, int sortingOrder)
    {
        var existing = FindTilemapByName(gridTransform, name);
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(gridTransform, false);

        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;

        log.Add($"สร้าง Tilemap '{name}' (Sorting: {sortingLayer} / {sortingOrder})");
        return go.GetComponent<Tilemap>();
    }

    private static Tilemap FindTilemapByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                var tm = child.GetComponent<Tilemap>();
                if (tm != null) return tm;
            }
        }
        return null;
    }

    /// <summary>สร้างกรอบสี่เหลี่ยมสำหรับชี้ช่องเป้าหมาย</summary>
    private static SpriteRenderer EnsureTargetMarker(List<string> log)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerPath);

        if (sprite == null)
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    tex.SetPixel(x, y, isEdge ? Color.white : Color.clear);
                }
            }
            tex.Apply();

            File.WriteAllBytes(MarkerPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(MarkerPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(MarkerPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerPath);
            log.Add("สร้างรูปกรอบชี้เป้า TargetMarker.png");
        }

        var go = new GameObject("TargetMarker");
        Undo.RegisterCreatedObjectUndo(go, "Create TargetMarker");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 1f, 1f, 0.6f);
        sr.sortingLayerName = "Decor";
        sr.sortingOrder = 100;

        log.Add("สร้าง TargetMarker (กรอบขาวบอกช่องที่กำลังเล็ง)");
        return sr;
    }
}
