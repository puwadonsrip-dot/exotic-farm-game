using UnityEngine;

/// <summary>ประเภทของไอเทม</summary>
public enum ItemKind
{
    Tool,     // อุปกรณ์ เช่น จอบ บัวรดน้ำ (ใช้ได้ไม่หมด)
    Seed,     // เมล็ด (ใช้แล้วหายไป 1)
    Produce,  // ผลผลิตที่เก็บเกี่ยวได้ (เอาไปขาย)
    Feed,     // อาหารสัตว์
    Animal    // ตัวสัตว์ที่ซื้อมา เอาไปวางลงฟาร์มได้
}

/// <summary>
/// ข้อมูลไอเทม 1 ชนิด
/// สร้างใหม่ได้ที่: คลิกขวาใน Project > Create > Farming > Item Data
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Farming/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("ข้อมูลทั่วไป")]
    [Tooltip("ไอดีที่ไม่ซ้ำกัน ใช้ตอน Save/Load เช่น hoe, carrot_seed, carrot")]
    public string itemId = "new_item";

    [Tooltip("ชื่อที่โชว์ในเกม")]
    public string displayName = "ไอเทมใหม่";

    [Tooltip("รูปไอคอนที่โชว์ใน Hotbar / กระเป๋า")]
    public Sprite icon;

    public ItemKind kind = ItemKind.Produce;

    [Header("ถ้าเป็น Tool")]
    [Tooltip("ใช้เมื่อ Kind = Tool เท่านั้น")]
    public ToolType toolType = ToolType.Hand;

    [Header("ถ้าเป็น Seed หรือ Produce")]
    [Tooltip("พืชที่ผูกกับไอเทมนี้")]
    public CropData crop;

    [Header("ถ้าเป็น Animal")]
    [Tooltip("สัตว์ที่จะโผล่ออกมาตอนวางลงฟาร์ม")]
    public AnimalData animal;

    [Tooltip("เปิด = วางแล้วได้ตัวโตเต็มวัยเลย / ปิด = เป็นไข่ ฟักออกมาเป็นตัวเล็ก")]
    public bool spawnAsAdult;

    [Header("การเก็บ / ราคา")]
    [Tooltip("ซ้อนกันได้สูงสุดกี่ชิ้นต่อ 1 ช่อง")]
    public int maxStack = 99;

    [Tooltip("ราคาซื้อจากร้าน (บาท) — ใส่ 0 ถ้าร้านไม่ขาย")]
    public int buyPrice = 0;

    [Tooltip("ราคาที่ร้านรับซื้อ (บาท) — ใส่ 0 ถ้าขายไม่ได้")]
    public int sellPrice = 0;
}
