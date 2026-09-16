using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// สถานะของดิน 1 ช่อง (1 cell บน Tilemap)
/// </summary>
[Serializable]
public class FarmTile
{
    public bool isTilled;          // ไถแล้วหรือยัง
    public bool isWatered;         // รดน้ำแล้วหรือยัง (ของ "วันนี้")
    public string cropId = "";     // ปลูกอะไรอยู่ ("" = ว่าง)
    public int grownDays;          // โตมากี่วันแล้ว

    public bool HasCrop => !string.IsNullOrEmpty(cropId);
}

/// <summary>ข้อมูลสำหรับ Save 1 ช่อง (มี position ด้วย)</summary>
[Serializable]
public class FarmTileSave
{
    public int x, y;
    public bool isTilled;
    public bool isWatered;
    public string cropId;
    public int grownDays;
}

/// <summary>
/// ตัวจัดการฟาร์มทั้งหมด: ไถดิน / ปลูก / รดน้ำ / โต / เก็บเกี่ยว
/// วางไว้บน GameObject ว่าง ๆ ชื่อ "FarmManager" ใน Scene
/// </summary>
public class FarmManager : MonoBehaviour
{
    public static FarmManager Instance { get; private set; }

    [Header("Tilemap (ลากจาก Hierarchy มาใส่)")]
    [Tooltip("Tilemap ที่จะวาดดินที่ไถแล้ว - สร้างใหม่ชื่อ SoilTilemap")]
    public Tilemap soilTilemap;

    [Tooltip("Tilemap ที่จะวาดต้นพืช - สร้างใหม่ชื่อ CropTilemap")]
    public Tilemap cropTilemap;

    [Tooltip("Tilemap ที่มีกำแพง/สิ่งกีดขวาง - ช่องที่มี Tile ที่นี่จะไถไม่ได้ (ใส่ Collision ของเดิม)")]
    public Tilemap blockingTilemap;

    [Tooltip("ไม่บังคับ: ถ้าใส่ จะไถได้เฉพาะช่องที่มี Tile อยู่บน Tilemap นี้เท่านั้น")]
    public Tilemap farmableAreaTilemap;

    [Header("Tile ของดิน")]
    [Tooltip("Tile ดินที่ไถแล้ว (แนะนำ: HappyHarvest/Art/Tiles/Dirt/Tiles/Tiles_Dirt1)")]
    public TileBase tilledSoilTile;

    [Tooltip("สีที่ใช้ย้อมดินตอนรดน้ำแล้ว (ให้ดูเข้มขึ้น)")]
    public Color wateredTint = new Color(0.62f, 0.52f, 0.42f, 1f);

    [Header("พืชทั้งหมดในเกม")]
    [Tooltip("ลากไฟล์ CropData ทุกชนิดมาใส่ที่นี่")]
    public List<CropData> allCrops = new List<CropData>();

    [Header("วันปัจจุบัน")]
    public int currentDay = 1;

    // ---------- Event ให้ระบบอื่นมาเกาะทีหลัง (Inventory / Quest) ----------
    /// <summary>เรียกเมื่อเก็บเกี่ยวสำเร็จ: (พืชที่เก็บได้, จำนวน)</summary>
    public event Action<CropData, int> OnHarvested;
    /// <summary>เรียกเมื่อปลูกสำเร็จ: (พืชที่ปลูก)</summary>
    public event Action<CropData> OnPlanted;
    /// <summary>เรียกเมื่อขึ้นวันใหม่: (เลขวันใหม่)</summary>
    public event Action<int> OnDayChanged;

    // ข้อมูลดินทุกช่อง เก็บใน Dictionary (ช่องไหนไม่เคยไถ จะไม่มีใน Dictionary)
    private readonly Dictionary<Vector3Int, FarmTile> m_Tiles = new Dictionary<Vector3Int, FarmTile>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ================= ค้นหาพืชจาก id =================

    public CropData GetCrop(string cropId)
    {
        foreach (var c in allCrops)
        {
            if (c != null && c.cropId == cropId) return c;
        }
        return null;
    }

    // ================= อ่านสถานะช่อง =================

    /// <summary>แปลงตำแหน่งในโลก -> ตำแหน่งช่อง (cell)</summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return soilTilemap != null
            ? soilTilemap.WorldToCell(worldPos)
            : Vector3Int.FloorToInt(worldPos);
    }

    public FarmTile GetTile(Vector3Int cell)
    {
        m_Tiles.TryGetValue(cell, out var tile);
        return tile;
    }

    /// <summary>ช่องนี้ไถได้ไหม</summary>
    public bool CanTill(Vector3Int cell)
    {
        var tile = GetTile(cell);
        if (tile != null && tile.isTilled) return false;                       // ไถไปแล้ว

        if (blockingTilemap != null && blockingTilemap.HasTile(cell)) return false;   // มีกำแพง
        if (farmableAreaTilemap != null && !farmableAreaTilemap.HasTile(cell)) return false; // นอกเขตฟาร์ม

        return true;
    }

    public bool CanPlant(Vector3Int cell)
    {
        var tile = GetTile(cell);
        return tile != null && tile.isTilled && !tile.HasCrop;
    }

    public bool CanWater(Vector3Int cell)
    {
        var tile = GetTile(cell);
        return tile != null && tile.isTilled && !tile.isWatered;
    }

    public bool CanHarvest(Vector3Int cell)
    {
        var tile = GetTile(cell);
        if (tile == null || !tile.HasCrop) return false;

        var crop = GetCrop(tile.cropId);
        return crop != null && crop.IsReadyToHarvest(tile.grownDays);
    }

    // ================= การกระทำ =================

    /// <summary>ไถดิน (ใช้จอบ)</summary>
    public bool Till(Vector3Int cell)
    {
        if (!CanTill(cell)) return false;

        var tile = GetTile(cell);
        if (tile == null)
        {
            tile = new FarmTile();
            m_Tiles[cell] = tile;
        }
        tile.isTilled = true;

        RefreshVisual(cell);
        AudioManager.PlayTill();
        return true;
    }

    /// <summary>ปลูกเมล็ด</summary>
    public bool Plant(Vector3Int cell, CropData crop)
    {
        if (crop == null || !CanPlant(cell)) return false;

        var tile = GetTile(cell);
        tile.cropId = crop.cropId;
        tile.grownDays = 0;

        RefreshVisual(cell);
        AudioManager.PlayPlant();
        OnPlanted?.Invoke(crop);
        return true;
    }

    /// <summary>รดน้ำ</summary>
    public bool Water(Vector3Int cell)
    {
        if (!CanWater(cell)) return false;

        GetTile(cell).isWatered = true;

        RefreshVisual(cell);
        AudioManager.PlayWater();
        return true;
    }

    /// <summary>เก็บเกี่ยว</summary>
    public bool Harvest(Vector3Int cell)
    {
        if (!CanHarvest(cell)) return false;

        var tile = GetTile(cell);
        var crop = GetCrop(tile.cropId);

        // เคลียร์พืชออก แต่ดินยังไถอยู่ ปลูกซ้ำได้เลย
        tile.cropId = "";
        tile.grownDays = 0;

        RefreshVisual(cell);
        AudioManager.PlayHarvest();

        OnHarvested?.Invoke(crop, crop.produceAmount);
        Debug.Log($"[Farm] เก็บเกี่ยว {crop.displayName} x{crop.produceAmount}");
        return true;
    }

    // ================= เปลี่ยนวัน =================

    /// <summary>สรุปว่าคืนที่ผ่านมาเกิดอะไรขึ้นบ้าง (เอาไปโชว์บนจอตอนข้ามวัน)</summary>
    public struct DayResult
    {
        public int day;        // วันใหม่
        public int grew;       // โตขึ้นกี่ต้น
        public int ready;      // พร้อมเก็บกี่ต้น
        public int thirsty;    // ไม่ได้รดน้ำเลยไม่โตกี่ต้น
    }

    /// <summary>
    /// เรียกตอนผู้เล่นนอน: พืชที่ "รดน้ำแล้ว" จะโตขึ้น 1 วัน
    /// พืชที่ไม่ได้รดน้ำจะไม่โต
    /// </summary>
    public DayResult AdvanceDay()
    {
        currentDay++;

        int grew = 0;        // โตขึ้น
        int thirsty = 0;     // ไม่ได้รดน้ำ เลยไม่โต
        int ready = 0;       // โตเต็มที่แล้ว รอเก็บ

        foreach (var pair in m_Tiles)
        {
            var tile = pair.Value;

            if (tile.HasCrop)
            {
                var crop = GetCrop(tile.cropId);

                if (crop != null && crop.IsReadyToHarvest(tile.grownDays))
                {
                    ready++;
                }
                else if (tile.isWatered)
                {
                    if (crop != null) tile.grownDays++;
                    grew++;
                }
                else
                {
                    thirsty++;
                }
            }

            // น้ำแห้งทุกเช้า ต้องรดใหม่
            tile.isWatered = false;
        }

        RefreshAllVisuals();
        OnDayChanged?.Invoke(currentDay);

        string msg = $"[Farm] ขึ้นวันที่ {currentDay} — โตขึ้น {grew} ต้น, พร้อมเก็บ {ready} ต้น";
        if (thirsty > 0)
            msg += $", ไม่โต {thirsty} ต้น (ยังไม่ได้รดน้ำ! ต้องรดใหม่ทุกวัน)";

        Debug.Log(msg);

        return new DayResult
        {
            day = currentDay,
            grew = grew,
            ready = ready,
            thirsty = thirsty
        };
    }

    /// <summary>
    /// เร่งให้พืชทุกต้นโตเต็มที่ทันที — ใช้ในโหมดเจ้าของเกมเท่านั้น
    /// คืนจำนวนต้นที่ถูกเร่ง
    /// </summary>
    public int ForceGrowAll()
    {
        int grown = 0;

        foreach (var pair in m_Tiles)
        {
            var tile = pair.Value;
            if (!tile.HasCrop) continue;

            var crop = GetCrop(tile.cropId);
            if (crop == null) continue;

            tile.grownDays = crop.daysToGrow;
            tile.isWatered = true;
            grown++;
        }

        RefreshAllVisuals();
        return grown;
    }

    // ================= วาดภาพ =================

    private void RefreshVisual(Vector3Int cell)
    {
        var tile = GetTile(cell);
        if (tile == null) return;

        // --- ชั้นดิน ---
        if (soilTilemap != null)
        {
            soilTilemap.SetTile(cell, tile.isTilled ? tilledSoilTile : null);

            if (tile.isTilled)
            {
                // ปลดล็อคสีก่อน ไม่งั้น SetColor จะไม่มีผล
                soilTilemap.SetTileFlags(cell, TileFlags.None);
                soilTilemap.SetColor(cell, tile.isWatered ? wateredTint : Color.white);
            }
        }

        // --- ชั้นพืช ---
        if (cropTilemap != null)
        {
            if (tile.HasCrop)
            {
                var crop = GetCrop(tile.cropId);
                cropTilemap.SetTile(cell, crop != null ? crop.GetStageTile(tile.grownDays) : null);
            }
            else
            {
                cropTilemap.SetTile(cell, null);
            }
        }
    }

    private void RefreshAllVisuals()
    {
        foreach (var pair in m_Tiles)
            RefreshVisual(pair.Key);
    }

    // ================= Save / Load (ใช้ทีหลังตอนทำระบบ Save) =================

    public List<FarmTileSave> ExportSave()
    {
        var list = new List<FarmTileSave>();
        foreach (var pair in m_Tiles)
        {
            list.Add(new FarmTileSave
            {
                x = pair.Key.x,
                y = pair.Key.y,
                isTilled = pair.Value.isTilled,
                isWatered = pair.Value.isWatered,
                cropId = pair.Value.cropId,
                grownDays = pair.Value.grownDays
            });
        }
        return list;
    }

    public void ImportSave(List<FarmTileSave> list, int day)
    {
        // ล้างภาพเก่าออกก่อน
        foreach (var pair in m_Tiles)
        {
            if (soilTilemap != null) soilTilemap.SetTile(pair.Key, null);
            if (cropTilemap != null) cropTilemap.SetTile(pair.Key, null);
        }
        m_Tiles.Clear();

        currentDay = day;

        if (list == null) return;
        foreach (var s in list)
        {
            m_Tiles[new Vector3Int(s.x, s.y, 0)] = new FarmTile
            {
                isTilled = s.isTilled,
                isWatered = s.isWatered,
                cropId = s.cropId,
                grownDays = s.grownDays
            };
        }

        RefreshAllVisuals();
    }
}
