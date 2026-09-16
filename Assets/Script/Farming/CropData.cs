using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// ข้อมูลของพืช 1 ชนิด (เก็บเป็นไฟล์ Asset ไม่ใช่ GameObject)
/// สร้างไฟล์ใหม่ได้ที่: คลิกขวาใน Project > Create > Farming > Crop Data
/// </summary>
[CreateAssetMenu(fileName = "NewCrop", menuName = "Farming/Crop Data")]
public class CropData : ScriptableObject
{
    [Header("ข้อมูลทั่วไป")]
    [Tooltip("ไอดีที่ไม่ซ้ำกัน ใช้ตอน Save/Load เช่น carrot, corn, wheat")]
    public string cropId = "carrot";

    [Tooltip("ชื่อที่โชว์ในเกม เช่น แครอท")]
    public string displayName = "แครอท";

    [Tooltip("รูปไอคอนเมล็ด ใช้ใน Hotbar/ร้านค้า (ยังไม่ใส่ก็ได้)")]
    public Sprite seedIcon;

    [Tooltip("รูปไอคอนผลผลิต ใช้ใน Inventory/ร้านค้า (ยังไม่ใส่ก็ได้)")]
    public Sprite produceIcon;

    [Header("การเติบโต")]
    [Tooltip("Tile ของแต่ละระยะ เรียงจากเพิ่งปลูก -> พร้อมเก็บ (ปกติ 4 อัน)")]
    public TileBase[] growthStageTiles;

    [Tooltip("ต้องผ่านไปกี่วัน (ที่รดน้ำแล้ว) ถึงจะเก็บเกี่ยวได้")]
    public int daysToGrow = 3;

    [Header("ไอเทมที่ผูกกับพืชนี้")]
    [Tooltip("ไอเทมเมล็ด — ใช้ตอนปลูก (Wizard ตั้งให้อัตโนมัติ)")]
    public ItemData seedItem;

    [Tooltip("ไอเทมผลผลิต — ได้ตอนเก็บเกี่ยว (Wizard ตั้งให้อัตโนมัติ)")]
    public ItemData produceItem;

    [Header("ผลผลิต / ราคา")]
    [Tooltip("เก็บเกี่ยว 1 ครั้งได้กี่ชิ้น")]
    public int produceAmount = 1;

    [Tooltip("ราคาขายต่อชิ้น (บาท)")]
    public int sellPrice = 30;

    [Tooltip("ราคาซื้อเมล็ด (บาท)")]
    public int seedPrice = 10;

    /// <summary>จำนวนระยะการเติบโตทั้งหมด</summary>
    public int StageCount => growthStageTiles != null ? growthStageTiles.Length : 0;

    /// <summary>
    /// แปลง "จำนวนวันที่โตแล้ว" เป็น "ระยะที่ควรแสดง"
    /// เช่น มี 4 ระยะ ใช้เวลา 3 วัน -> วันที่ 0,1,2,3 = ระยะ 0,1,2,3
    /// </summary>
    public int GetStageIndex(int grownDays)
    {
        if (StageCount == 0) return 0;
        if (daysToGrow <= 0) return StageCount - 1;

        float t = Mathf.Clamp01(grownDays / (float)daysToGrow);
        return Mathf.RoundToInt(t * (StageCount - 1));
    }

    /// <summary>Tile ที่ควรวาดตอนนี้</summary>
    public TileBase GetStageTile(int grownDays)
    {
        if (StageCount == 0) return null;
        return growthStageTiles[GetStageIndex(grownDays)];
    }

    /// <summary>โตเต็มที่แล้วหรือยัง (เก็บเกี่ยวได้)</summary>
    public bool IsReadyToHarvest(int grownDays)
    {
        return grownDays >= daysToGrow;
    }
}
