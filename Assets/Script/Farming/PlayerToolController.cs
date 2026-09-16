using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// อุปกรณ์ที่ผู้เล่นถืออยู่ตอนนี้
/// </summary>
public enum ToolType
{
    Hand,        // มือเปล่า - ใช้เก็บเกี่ยว
    Hoe,         // จอบ - ใช้ไถดิน
    WateringCan, // บัวรดน้ำ
    Seed         // เมล็ด - ใช้ปลูก
}

/// <summary>
/// ตัวควบคุมการใช้อุปกรณ์ของผู้เล่น
/// วางไว้บน GameObject "Player" (เพิ่มเข้าไป ไม่ต้องลบ PlayerMovement เดิม)
///
/// สคริปต์นี้ "ไม่แตะ" PlayerMovement เลย
/// มันแค่อ่านค่า Animator (InputX/InputY/LastInputX/LastInputY) เพื่อรู้ว่าผู้เล่นหันไปทางไหน
///
/// อุปกรณ์ที่ถืออยู่มาจากช่องที่เลือกใน Hotbar (กด 1-6)
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerToolController : MonoBehaviour
{
    [Header("ระยะเอื้อม")]
    [Tooltip("จุดกึ่งกลางของตัวละครสำหรับคำนวณช่องเป้าหมาย (เว้นว่าง = ใช้ตำแหน่ง Player)")]
    public Transform aimOrigin;

    [Tooltip("ขยับจุดเล็งขึ้น/ลง ถ้าจุดเล็งไม่ตรงเท้าตัวละคร ให้ปรับค่านี้")]
    public Vector2 aimOffset = new Vector2(0f, 0f);

    [Tooltip("เล็งไกลจากตัวกี่ช่อง")]
    public float aimDistance = 1f;

    [Header("การเล็งด้วยเมาส์")]
    [Tooltip("เปิด = เล็งช่องที่เมาส์ชี้ / ปิด = เล็งช่องหน้าตัวละครเสมอ")]
    public bool aimWithMouse = true;

    [Tooltip("เอื้อมได้ไกลสุดกี่ช่อง — เมาส์ชี้ไกลกว่านี้จะกลับไปเล็งช่องหน้าตัวแทน")]
    public float maxReach = 2.5f;

    [Header("การกดค้าง")]
    [Tooltip("กดเมาส์ค้างแล้วทำซ้ำทุกกี่วินาที (สะดวกตอนรดน้ำหลายช่อง)")]
    public float holdRepeatDelay = 0.22f;

    [Header("ตัวชี้ช่องเป้าหมาย (ไม่บังคับ)")]
    [Tooltip("ลาก GameObject ที่มี SpriteRenderer กรอบสี่เหลี่ยมมาใส่ เพื่อให้เห็นว่ากำลังเล็งช่องไหน")]
    public SpriteRenderer targetMarker;

    public Color markerValidColor = new Color(1f, 1f, 1f, 0.6f);
    public Color markerInvalidColor = new Color(1f, 0.3f, 0.3f, 0.4f);

    [Header("อุปกรณ์ปัจจุบัน (อ่านอย่างเดียว - มาจาก Hotbar)")]
    public ToolType currentTool = ToolType.Hand;
    public CropData currentSeed;

    [Header("ปุ่มทดสอบ")]
    [Tooltip("กด N เพื่อข้ามวัน — ปิดได้เมื่อทำเตียงนอนเสร็จแล้ว")]
    public bool enableDaySkipKey = true;

    private Animator m_Animator;
    private Vector3Int m_TargetCell;
    private float m_NextRepeat;

    /// <summary>เมาส์อยู่บนปุ่มหรือหน้าต่าง UI อยู่ไหม — จะได้ไม่เผลอไถดินตอนกดปุ่มบนจอ</summary>
    private static bool IsPointerOverUI()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
        if (aimOrigin == null) aimOrigin = transform;
    }

    private void Update()
    {
        SyncToolFromHotbar();
        UpdateTargetCell();
        UpdateMarker();
        HandleInput();
    }

    // ---------- อ่านอุปกรณ์จาก Hotbar ----------

    /// <summary>ดูว่าช่องที่เลือกใน Hotbar เป็นอะไร แล้วเปลี่ยนอุปกรณ์ตาม</summary>
    private void SyncToolFromHotbar()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;   // ไม่มีระบบกระเป๋า = ใช้ค่าที่ตั้งใน Inspector

        var item = inv.SelectedItem;

        if (item == null)
        {
            currentTool = ToolType.Hand;
            currentSeed = null;
            m_HeldAnimal = null;
            return;
        }

        m_HeldAnimal = item.kind == ItemKind.Animal ? item : null;

        switch (item.kind)
        {
            case ItemKind.Tool:
                currentTool = item.toolType;
                currentSeed = null;
                break;

            case ItemKind.Seed:
                currentTool = ToolType.Seed;
                currentSeed = item.crop;
                break;

            default:   // Produce / Feed / Animal = ใช้มือเปล่า
                currentTool = ToolType.Hand;
                currentSeed = null;
                break;
        }
    }

    /// <summary>ตัวสัตว์ที่ถืออยู่ในมือตอนนี้ (ถ้ามี) — คลิกเพื่อวางลงพื้น</summary>
    private ItemData m_HeldAnimal;

    /// <summary>วางสัตว์ลงตรงช่องที่เล็งอยู่</summary>
    private bool TryPlaceAnimal()
    {
        if (m_HeldAnimal == null || m_HeldAnimal.animal == null) return false;

        var manager = AnimalManager.Instance;
        var inv = InventorySystem.Instance;

        if (manager == null || inv == null)
        {
            DialogueUI.Instance?.SetPrompt("ยังไม่มีระบบสัตว์ในฉากนี้");
            return true;
        }

        Vector3 spot = FarmManager.Instance != null && FarmManager.Instance.soilTilemap != null
            ? FarmManager.Instance.soilTilemap.GetCellCenterWorld(m_TargetCell)
            : transform.position;

        // ล็อคให้อยู่ในคอก = ต้องวางในคอกเท่านั้น
        if (manager.keepInsidePen && !manager.IsInsidePen(spot))
        {
            DialogueUI.Instance?.SetPrompt("ต้องวางในคอกเท่านั้น — เดินไปที่คอกก่อน");
            return true;
        }

        bool asAdult = m_HeldAnimal.spawnAsAdult;

        var placed = manager.SpawnAt(m_HeldAnimal.animal, spot, asAdult);
        if (placed == null) return true;

        inv.Remove(m_HeldAnimal, 1);

        string who = m_HeldAnimal.animal.displayName;
        DialogueUI.Instance?.SetPrompt(asAdult
            ? $"ปล่อย{who}ตัวโตลงคอกแล้ว"
            : $"ไข่ฟักออกมาเป็นลูก{who}!");

        return true;
    }

    // ---------- หาช่องที่กำลังเล็ง ----------

    /// <summary>อ่านทิศทางที่ตัวละครหันอยู่ จากค่า Animator ที่ PlayerMovement เดิมตั้งไว้</summary>
    private Vector2 GetFacingDirection()
    {
        Vector2 dir = new Vector2(
            m_Animator.GetFloat("InputX"),
            m_Animator.GetFloat("InputY"));

        // ถ้ายืนอยู่เฉย ๆ ให้ใช้ทิศที่เดินล่าสุด
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = new Vector2(
                m_Animator.GetFloat("LastInputX"),
                m_Animator.GetFloat("LastInputY"));
        }

        // ถ้ายังไม่เคยเดินเลย ให้หันลงเป็นค่าเริ่มต้น
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;

        // บีบให้เหลือ 4 ทิศ (บน/ล่าง/ซ้าย/ขวา)
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private void UpdateTargetCell()
    {
        if (FarmManager.Instance == null) return;

        Vector3 origin = aimOrigin.position + (Vector3)aimOffset;

        // เล็งด้วยเมาส์ก่อน ถ้าชี้อยู่ในระยะเอื้อม
        if (aimWithMouse && TryGetMouseCell(origin, out Vector3Int mouseCell))
        {
            m_TargetCell = mouseCell;
            return;
        }

        // ไม่งั้นเล็งช่องหน้าตัวละครเหมือนเดิม
        Vector3 aimPoint = origin + (Vector3)(GetFacingDirection() * aimDistance);
        m_TargetCell = FarmManager.Instance.WorldToCell(aimPoint);
    }

    /// <summary>ช่องที่เมาส์ชี้อยู่ — คืน false ถ้าไกลเกินเอื้อม หรือเมาส์อยู่บนหน้าต่าง UI</summary>
    private bool TryGetMouseCell(Vector3 origin, out Vector3Int cell)
    {
        cell = default;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        var cam = Camera.main;
        if (cam == null) return false;

        Vector3 screen = mouse.position.ReadValue();
        screen.z = -cam.transform.position.z;      // กล้อง 2D อยู่หลังฉาก

        Vector3 world = cam.ScreenToWorldPoint(screen);
        world.z = 0f;

        if (Vector2.Distance(world, origin) > maxReach) return false;

        cell = FarmManager.Instance.WorldToCell(world);
        return true;
    }

    private void UpdateMarker()
    {
        if (targetMarker == null || FarmManager.Instance == null) return;

        // มีหน้าต่างอะไรเปิดอยู่ = ซ่อนกรอบ
        targetMarker.enabled = !InventoryUI.IsBackpackOpen && !DialogueUI.IsOpen
                               && !RewardPopupUI.IsOpen && !ShopUI.IsOpen
                               && !CutsceneUI.IsPlaying && !TitleScreenUI.IsOpen
                               && !AdminModeUI.IsOpen;
        if (!targetMarker.enabled) return;

        var soil = FarmManager.Instance.soilTilemap;
        if (soil != null)
            targetMarker.transform.position = soil.GetCellCenterWorld(m_TargetCell);

        targetMarker.color = CanUseCurrentToolHere() ? markerValidColor : markerInvalidColor;
    }

    // ---------- ใช้อุปกรณ์ ----------

    private bool CanUseCurrentToolHere()
    {
        var farm = FarmManager.Instance;
        if (farm == null) return false;

        // พืชโตเต็มที่ = เก็บได้เสมอ ไม่ว่าจะถืออะไรอยู่
        if (farm.CanHarvest(m_TargetCell)) return true;

        switch (currentTool)
        {
            case ToolType.Hoe: return farm.CanTill(m_TargetCell);
            case ToolType.WateringCan: return farm.CanWater(m_TargetCell);
            case ToolType.Seed: return currentSeed != null && farm.CanPlant(m_TargetCell);
            case ToolType.Hand: return false;
        }
        return false;
    }

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // มีหน้าต่างอะไรเปิดอยู่ = ห้ามใช้อุปกรณ์
        if (InventoryUI.IsBackpackOpen || DialogueUI.IsOpen
            || RewardPopupUI.IsOpen || ShopUI.IsOpen
            || CutsceneUI.IsPlaying || TitleScreenUI.IsOpen) return;

        // กำลังข้ามวัน / เปิดเมนู / ตั้งชื่อ / ปลดเมาส์อยู่ = หยุดรับปุ่มทั้งหมด
        if (DayTransitionUI.IsPlaying || SettingsMenuUI.IsOpen
            || NameEntryUI.IsOpen || SettingsMenuUI.CursorFree
            || ScreenFadeUI.IsFading || AnimalInfoUI.IsOpen
            || AdminModeUI.IsOpen) return;

        // ---- คลิกซ้าย = ใช้อุปกรณ์ (กดค้างได้ ทำซ้ำเรื่อยๆ) ----
        // ชี้อยู่บนเตียง = ปล่อยให้เตียงรับคลิกไป ไม่ต้องไถดิน
        var mouse = Mouse.current;
        if (mouse != null && !IsPointerOverUI()
            && !BedInteractable.MouseOverBed
            && !AnimalInstance.MouseOverAnimal)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                UseCurrentTool();
                m_NextRepeat = Time.time + holdRepeatDelay;
            }
            else if (mouse.leftButton.isPressed && Time.time >= m_NextRepeat)
            {
                UseCurrentTool();
                m_NextRepeat = Time.time + holdRepeatDelay;
            }
        }

        // ---- Space = ใช้อุปกรณ์ (ปุ่มสำรอง) ----
        if (kb.spaceKey.wasPressedThisFrame)
            UseCurrentTool();

        // ---- E = ใช้อุปกรณ์ ----
        // ยกเว้นตอนยืนใกล้ NPC (E คือคุย) หรือใกล้สัตว์ (E คือให้อาหาร)
        if (kb.eKey.wasPressedThisFrame
            && !NPCInteractable.PlayerNearNPC
            && !AnimalInstance.PlayerNearAnimal
            && !BedInteractable.PlayerNearBed)
            UseCurrentTool();

        // N = ข้ามไปวันถัดไป (ใช้ทดสอบ ก่อนจะมีเตียงนอน)
        if (enableDaySkipKey && kb.nKey.wasPressedThisFrame)
        {
            if (DayTransitionUI.Instance != null)
                DayTransitionUI.Instance.Sleep();               // มีจอมืดให้ดูด้วย
            else if (FarmManager.Instance != null)
                FarmManager.Instance.AdvanceDay();              // ไม่มีจอมืด ก็ข้ามวันดื้อๆ
        }
    }

    /// <summary>ใช้อุปกรณ์ที่ถืออยู่กับช่องที่เล็ง (เรียกจากปุ่ม หรือจากระบบอื่นก็ได้)</summary>
    public void UseCurrentTool()
    {
        // ---- ถือตัวสัตว์อยู่ = วางลงพื้นก่อนเรื่องอื่น ----
        if (TryPlaceAnimal()) return;

        var farm = FarmManager.Instance;
        if (farm == null)
        {
            Debug.LogWarning("[PlayerToolController] ไม่พบ FarmManager ใน Scene");
            return;
        }

        // ---- เก็บเกี่ยวมาก่อนเสมอ ----
        // ถ้าพืชตรงหน้าโตเต็มที่แล้ว กด Space = เก็บ ไม่ว่าจะถืออุปกรณ์อะไรอยู่
        if (farm.CanHarvest(m_TargetCell))
        {
            TryHarvest(farm);
            return;
        }

        switch (currentTool)
        {
            case ToolType.Hoe:
                farm.Till(m_TargetCell);
                break;

            case ToolType.WateringCan:
                farm.Water(m_TargetCell);
                break;

            case ToolType.Seed:
                if (currentSeed == null)
                {
                    Debug.Log("[Tool] ยังไม่ได้เลือกเมล็ด (กด 1-6 เลือกช่องที่มีถุงเมล็ด)");
                }
                else if (farm.Plant(m_TargetCell, currentSeed))
                {
                    // ปลูกสำเร็จ = เมล็ดหายไป 1
                    if (InventorySystem.Instance != null)
                        InventorySystem.Instance.ConsumeSelected(1);
                }
                break;

            case ToolType.Hand:
                // มือเปล่าแต่ไม่มีอะไรให้เก็บ — ไม่ต้องทำอะไร
                break;
        }
    }

    /// <summary>เก็บเกี่ยว โดยเช็คก่อนว่ากระเป๋ามีที่ว่างพอ</summary>
    private void TryHarvest(FarmManager farm)
    {
        var tile = farm.GetTile(m_TargetCell);
        var crop = tile != null ? farm.GetCrop(tile.cropId) : null;
        var inv = InventorySystem.Instance;

        // กระเป๋าเต็ม -> ไม่เก็บ ปล่อยให้พืชอยู่ในแปลงต่อ จะได้ไม่หายฟรี
        if (inv != null && crop != null && crop.produceItem != null
            && !inv.HasSpaceFor(crop.produceItem, crop.produceAmount))
        {
            Debug.Log("[Farm] กระเป๋าเต็ม! เอาของไปขายก่อนแล้วค่อยกลับมาเก็บ");
            return;
        }

        farm.Harvest(m_TargetCell);
    }
}
