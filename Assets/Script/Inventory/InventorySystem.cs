using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ช่องเก็บของ 1 ช่อง</summary>
[Serializable]
public class ItemSlot
{
    public ItemData item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public void Clear()
    {
        item = null;
        count = 0;
    }
}

/// <summary>ของที่ผู้เล่นมีติดตัวตอนเริ่มเกม</summary>
[Serializable]
public class StartingItem
{
    public ItemData item;
    public int count = 1;
}

/// <summary>ข้อมูล 1 ช่องสำหรับ Save</summary>
[Serializable]
public class ItemSlotSave
{
    public int index;
    public string itemId;
    public int count;
}

/// <summary>
/// กระเป๋าของผู้เล่น
///
/// ช่อง 0-5   = Hotbar (แถบล่างจอ กด 1-6)
/// ช่อง 6+    = Backpack (กด I เปิด)
/// เก็บรวมกัน ไม่แบ่งหมวดหมู่ ตามที่ออกแบบไว้
///
/// วางไว้บน GameObject ชื่อ "InventorySystem"
/// </summary>
public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    /// <summary>จำนวนช่องใน Hotbar (กด 1-8 เลือก)</summary>
    public const int HotbarSize = 8;

    [Header("ขนาดกระเป๋า")]
    [Tooltip("จำนวนช่องเพิ่มเติมในกระเป๋า (นอกเหนือจาก Hotbar 8 ช่อง)")]
    public int backpackSize = 16;

    [Header("เงิน")]
    public int money = 100;

    [Header("ของติดตัวตอนเริ่มเกม")]
    public List<StartingItem> startingItems = new List<StartingItem>();

    [Header("รายชื่อไอเทมทั้งหมด (ใช้ตอน Load เกม)")]
    public List<ItemData> allItems = new List<ItemData>();

    /// <summary>ค่าที่แปลว่า "ไม่ได้ถืออะไรอยู่"</summary>
    public const int NoSelection = -1;

    /// <summary>ช่องที่เลือกอยู่ใน Hotbar — เป็น -1 ตอนมือเปล่า</summary>
    public int selectedIndex { get; private set; }

    /// <summary>ช่องทั้งหมด = Hotbar + Backpack</summary>
    public List<ItemSlot> slots { get; private set; } = new List<ItemSlot>();

    public int TotalSlots => HotbarSize + backpackSize;

    /// <summary>เรียกทุกครั้งที่ของหรือเงินเปลี่ยน — UI เอาไปวาดใหม่</summary>
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        slots.Clear();
        for (int i = 0; i < TotalSlots; i++)
            slots.Add(new ItemSlot());

        foreach (var s in startingItems)
        {
            if (s.item != null) Add(s.item, s.count);
        }
    }

    private void Start()
    {
        // เก็บเกี่ยวแล้วให้ผลผลิตเข้ากระเป๋าอัตโนมัติ
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnHarvested += HandleHarvested;
    }

    private void OnDestroy()
    {
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnHarvested -= HandleHarvested;
    }

    private void HandleHarvested(CropData crop, int amount)
    {
        if (crop == null || crop.produceItem == null)
        {
            Debug.LogWarning($"[Inventory] พืช '{(crop != null ? crop.displayName : "?")}' ยังไม่ได้ผูก Produce Item — ผลผลิตหายไป");
            return;
        }

        if (!Add(crop.produceItem, amount))
            Debug.Log("[Inventory] กระเป๋าเต็ม! เก็บของไม่ได้");
    }

    // ================= ช่องที่เลือกอยู่ =================

    public ItemSlot SelectedSlot =>
        (selectedIndex >= 0 && selectedIndex < slots.Count) ? slots[selectedIndex] : null;

    public ItemData SelectedItem
    {
        get
        {
            var s = SelectedSlot;
            return (s != null && !s.IsEmpty) ? s.item : null;
        }
    }

    /// <summary>
    /// เลือกช่องใน Hotbar
    ///
    /// กดช่องเดิมซ้ำ = เลิกถือ กลายเป็นมือเปล่า
    /// ใช้ได้ทั้งการกดปุ่มตัวเลขและการคลิกที่ช่อง
    /// </summary>
    public void SelectSlot(int index)
    {
        if (index < 0 || index >= HotbarSize) return;

        selectedIndex = selectedIndex == index ? NoSelection : index;
        OnChanged?.Invoke();
    }

    /// <summary>เลิกถือของทั้งหมด</summary>
    public void ClearSelection()
    {
        if (selectedIndex == NoSelection) return;

        selectedIndex = NoSelection;
        OnChanged?.Invoke();
    }

    /// <summary>เลือกช่องนั้นตรงๆ ไม่สลับไปมา (ใช้ตอนเลื่อนลูกกลิ้งเมาส์)</summary>
    public void SetSlot(int index)
    {
        if (index < 0 || index >= HotbarSize) return;

        selectedIndex = index;
        OnChanged?.Invoke();
    }

    // ================= เพิ่ม / ลบของ =================

    /// <summary>ใส่ของเข้ากระเป๋า คืน false ถ้าเต็ม</summary>
    public bool Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        // 1. ซ้อนกับกองเดิมก่อน
        for (int i = 0; i < slots.Count && count > 0; i++)
        {
            var s = slots[i];
            if (s.item == item && s.count < item.maxStack)
            {
                int space = item.maxStack - s.count;
                int move = Mathf.Min(space, count);
                s.count += move;
                count -= move;
            }
        }

        // 2. ที่เหลือใส่ช่องว่าง
        for (int i = 0; i < slots.Count && count > 0; i++)
        {
            var s = slots[i];
            if (s.IsEmpty)
            {
                int move = Mathf.Min(item.maxStack, count);
                s.item = item;
                s.count = move;
                count -= move;
            }
        }

        OnChanged?.Invoke();
        return count == 0;
    }

    /// <summary>เอาของออก คืน false ถ้ามีไม่พอ</summary>
    public bool Remove(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return false;
        if (CountOf(item) < count) return false;

        for (int i = 0; i < slots.Count && count > 0; i++)
        {
            var s = slots[i];
            if (s.item == item)
            {
                int take = Mathf.Min(s.count, count);
                s.count -= take;
                count -= take;
                if (s.count <= 0) s.Clear();
            }
        }

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>ใช้ของในช่องที่เลือกไป 1 ชิ้น (สำหรับเมล็ด)</summary>
    public void ConsumeSelected(int count = 1)
    {
        var s = SelectedSlot;
        if (s == null || s.IsEmpty) return;

        s.count -= count;
        if (s.count <= 0) s.Clear();

        OnChanged?.Invoke();
    }

    // ================= ย้าย / ทิ้งของ =================

    /// <summary>
    /// สลับของสองช่อง — ถ้าเป็นของชนิดเดียวกันจะรวมกองให้แทน
    ///
    /// ใช้ตอนลากไอเทมไปวางอีกช่องในกระเป๋า
    /// </summary>
    public void SwapSlots(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= slots.Count) return;
        if (to < 0 || to >= slots.Count) return;

        var a = slots[from];
        var b = slots[to];

        if (a.IsEmpty) return;

        // ของชนิดเดียวกันและกองปลายทางยังไม่เต็ม = เทรวมกัน
        if (!b.IsEmpty && a.item == b.item && b.count < b.item.maxStack)
        {
            int space = b.item.maxStack - b.count;
            int move = Mathf.Min(space, a.count);

            b.count += move;
            a.count -= move;
            if (a.count <= 0) a.Clear();
        }
        else
        {
            var item = a.item;
            int count = a.count;

            a.item = b.item;
            a.count = b.count;

            b.item = item;
            b.count = count;
        }

        // ช่องที่ถืออยู่ต้องตามของไปด้วย ไม่งั้นมือจะเปลี่ยนของเอง
        if (selectedIndex == from) selectedIndex = to < HotbarSize ? to : NoSelection;
        else if (selectedIndex == to) selectedIndex = from < HotbarSize ? from : NoSelection;

        OnChanged?.Invoke();
    }

    /// <summary>ของที่อยู่ในช่องนั้น (null ถ้าว่างหรือ index ผิด)</summary>
    public ItemData ItemAt(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index].IsEmpty ? null : slots[index].item;
    }

    public int CountAt(int index)
    {
        if (index < 0 || index >= slots.Count) return 0;
        return slots[index].IsEmpty ? 0 : slots[index].count;
    }

    /// <summary>
    /// เอาของออกจากช่องที่ระบุ คืนจำนวนที่เอาออกได้จริง
    ///
    /// count &lt;= 0 หมายถึงเอาออกทั้งกอง
    /// ใช้ทั้งตอนทิ้งลงถังขยะและตอนโยนลงพื้น
    /// </summary>
    public int RemoveAt(int index, int count = 0)
    {
        if (index < 0 || index >= slots.Count) return 0;

        var slot = slots[index];
        if (slot.IsEmpty) return 0;

        int take = count <= 0 ? slot.count : Mathf.Min(count, slot.count);

        slot.count -= take;
        if (slot.count <= 0)
        {
            slot.Clear();
            if (selectedIndex == index) selectedIndex = NoSelection;
        }

        OnChanged?.Invoke();
        return take;
    }

    public int CountOf(ItemData item)
    {
        if (item == null) return 0;

        int total = 0;
        foreach (var s in slots)
            if (s.item == item) total += s.count;
        return total;
    }

    public bool HasSpaceFor(ItemData item, int count = 1)
    {
        if (item == null) return false;

        int space = 0;
        foreach (var s in slots)
        {
            if (s.IsEmpty) space += item.maxStack;
            else if (s.item == item) space += item.maxStack - s.count;

            if (space >= count) return true;
        }
        return false;
    }

    // ================= เงิน =================

    public bool SpendMoney(int amount)
    {
        if (amount <= 0 || money < amount) return false;

        money -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        money += amount;
        OnChanged?.Invoke();
    }

    // ================= Save / Load =================

    public List<ItemSlotSave> ExportSave()
    {
        var list = new List<ItemSlotSave>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) continue;
            list.Add(new ItemSlotSave
            {
                index = i,
                itemId = slots[i].item.itemId,
                count = slots[i].count
            });
        }
        return list;
    }

    public void ImportSave(List<ItemSlotSave> list, int savedMoney)
    {
        foreach (var s in slots) s.Clear();
        money = savedMoney;

        if (list != null)
        {
            foreach (var s in list)
            {
                if (s.index < 0 || s.index >= slots.Count) continue;

                var item = FindItem(s.itemId);
                if (item == null) continue;

                slots[s.index].item = item;
                slots[s.index].count = s.count;
            }
        }

        OnChanged?.Invoke();
    }

    public ItemData FindItem(string itemId)
    {
        foreach (var it in allItems)
            if (it != null && it.itemId == itemId) return it;
        return null;
    }
}
