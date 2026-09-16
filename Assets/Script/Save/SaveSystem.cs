using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>ข้อมูลทั้งหมดที่บันทึกลงไฟล์</summary>
[Serializable]
public class SaveData
{
    public string playerName = "";
    public string savedAt = "";

    // ตำแหน่งที่ตัวละครยืนตอนกดเซฟ
    public float px, py, pz;

    public int day = 1;
    public int money;

    public List<ItemSlotSave> items = new List<ItemSlotSave>();
    public List<FarmTileSave> tiles = new List<FarmTileSave>();

    public int questIndex;
    public int questState;
    public int questProgress;

    public float hourOfDay = 6f;
    public List<AnimalManager.AnimalSave> animals = new List<AnimalManager.AnimalSave>();
}

/// <summary>
/// บันทึก / โหลดเกม เป็นไฟล์ JSON ไฟล์เดียว
///
/// เซฟจะจำตำแหน่งที่ตัวละครยืนอยู่ตอนกดเซฟ
/// พอโหลดหรือกดรีสตาร์ท ตัวละครจะวาร์ปกลับไปจุดนั้น
/// </summary>
public static class SaveSystem
{
    private const string FileName = "exoticfarm_save.json";

    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => File.Exists(Path);

    // ================= บันทึก =================

    public static bool Save()
    {
        var data = new SaveData
        {
            playerName = PlayerProfile.Name,
            savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var p = player.transform.position;
            data.px = p.x;
            data.py = p.y;
            data.pz = p.z;
        }

        var farm = FarmManager.Instance;
        if (farm != null)
        {
            data.day = farm.currentDay;
            data.tiles = farm.ExportSave();
        }

        var inv = InventorySystem.Instance;
        if (inv != null)
        {
            data.money = inv.money;
            data.items = inv.ExportSave();
        }

        var qm = QuestManager.Instance;
        if (qm != null)
        {
            data.questIndex = qm.currentIndex;
            data.questState = (int)qm.state;
            data.questProgress = qm.progress;
        }

        if (DayNightCycle.Instance != null)
            data.hourOfDay = DayNightCycle.Instance.hour;

        if (AnimalManager.Instance != null)
            data.animals = AnimalManager.Instance.ExportSave();

        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] บันทึกไม่สำเร็จ: " + e.Message);
            return false;
        }

        Debug.Log($"[Save] บันทึกแล้ว — วันที่ {data.day}, ตำแหน่ง ({data.px:F1}, {data.py:F1})");
        return true;
    }

    // ================= โหลด =================

    public static SaveData Peek()
    {
        if (!HasSave) return null;

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] อ่านไฟล์เซฟไม่ได้: " + e.Message);
            return null;
        }
    }

    public static bool Load()
    {
        var data = Peek();
        if (data == null) return false;

        // ---- ตำแหน่งตัวละคร ----
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var body = player.GetComponent<Rigidbody2D>();
            var target = new Vector3(data.px, data.py, data.pz);

            player.transform.position = target;
            if (body != null)
            {
                body.position = target;       // กัน Rigidbody ดึงกลับที่เดิม
                body.linearVelocity = Vector2.zero;
            }
        }

        // ---- ฟาร์ม ----
        if (FarmManager.Instance != null)
            FarmManager.Instance.ImportSave(data.tiles, data.day);

        // ---- ของในกระเป๋า ----
        if (InventorySystem.Instance != null)
            InventorySystem.Instance.ImportSave(data.items, data.money);

        // ---- เควส ----
        if (QuestManager.Instance != null)
            QuestManager.Instance.ImportSave(data.questIndex, data.questState, data.questProgress);

        // ---- เวลาในวัน ----
        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.hour = data.hourOfDay;
            DayNightCycle.Instance.StopWarp();
        }

        // ---- สัตว์ ----
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.ImportSave(data.animals);

        // ---- ชื่อ ----
        if (!string.IsNullOrEmpty(data.playerName))
            PlayerProfile.SetName(data.playerName);

        Debug.Log($"[Save] โหลดเซฟแล้ว — วันที่ {data.day}");
        return true;
    }

    public static void Delete()
    {
        if (!HasSave) return;

        try { File.Delete(Path); }
        catch (Exception e) { Debug.LogError("[Save] ลบไฟล์เซฟไม่ได้: " + e.Message); }
    }

    /// <summary>ข้อความสรุปไฟล์เซฟ เอาไปโชว์ในเมนู</summary>
    public static string Describe()
    {
        var data = Peek();
        if (data == null) return "ยังไม่มีไฟล์เซฟ";

        return $"{data.playerName}  •  วันที่ {data.day}  •  {data.money:N0}฿\nบันทึกเมื่อ {data.savedAt}";
    }
}
