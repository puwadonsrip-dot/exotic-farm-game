using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ภาษาที่เกมรองรับ</summary>
public enum GameLanguage
{
    Thai,
    English,
}

/// <summary>
/// ตัวแปลข้อความของทั้งเกม
///
/// ทำไมทำแบบนี้: UI ทุกหน้าในเกมเขียนข้อความไทยฝังไว้ในโค้ดตรงๆ กว่า 20 ไฟล์
/// ถ้าจะไล่แก้ทีละจุดคงใช้เวลานานและพลาดง่าย เลยใช้วิธีแปลตอนแสดงผลแทน —
/// <see cref="LocalizationApplier"/> จะกวาดดูข้อความบนจอแล้วเปลี่ยนให้ตามภาษาที่เลือก
///
/// เพิ่มคำแปลใหม่: ใส่คู่ไทย→อังกฤษลงใน Phrases ข้างล่างได้เลย ไม่ต้องแก้ที่อื่น
/// </summary>
public static class Localization
{
    private const string Key = "game_language";

    private static GameLanguage s_Current = GameLanguage.Thai;
    private static bool s_Loaded;

    /// <summary>เรียกเมื่อผู้เล่นสลับภาษา — UI เอาไปวาดใหม่</summary>
    public static event Action OnChanged;

    public static GameLanguage Current
    {
        get
        {
            Load();
            return s_Current;
        }
        set
        {
            Load();
            if (s_Current == value) return;

            s_Current = value;
            PlayerPrefs.SetInt(Key, (int)value);
            PlayerPrefs.Save();

            OnChanged?.Invoke();
        }
    }

    public static bool IsEnglish => Current == GameLanguage.English;

    public static void Toggle()
    {
        Current = IsEnglish ? GameLanguage.Thai : GameLanguage.English;
    }

    private static void Load()
    {
        if (s_Loaded) return;

        s_Loaded = true;
        s_Current = (GameLanguage)PlayerPrefs.GetInt(Key, (int)GameLanguage.Thai);
    }

    // ================= การแปล =================

    private static List<KeyValuePair<string, string>> s_SortedFragments;

    /// <summary>
    /// แปลข้อความ 1 ชิ้น
    ///
    /// วิธีแปลมี 2 ชั้น:
    ///   1. จับคู่ทั้งประโยคจาก Phrases — ได้ประโยคอังกฤษที่อ่านลื่น
    ///   2. ถ้าไม่เจอ ค่อยแทนเฉพาะ "ชื่อเฉพาะ" จาก Fragments (ชื่อพืช ชื่อสัตว์ ชื่อของ)
    ///
    /// ที่แยกสองชั้นเพราะถ้าแทนคำทั่วไปมั่วๆ กลางประโยค จะได้ข้อความปนกันอ่านไม่รู้เรื่อง
    /// เช่น "ปลูกพืชให้หมู่บ้าน" จะกลายเป็น "ปลูกCropsให้หมู่บ้าน" ซึ่งแย่กว่าไม่แปลเลย
    /// </summary>
    public static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!IsEnglish) return text;

        if (Phrases.TryGetValue(text, out var whole)) return whole;

        // ไม่มีอักษรไทยเลย = ไม่ต้องแตะ (ตัวเลข ชื่อผู้เล่น ฯลฯ)
        if (!HasThai(text)) return text;

        BuildSortedFragments();

        var result = text;
        foreach (var pair in s_SortedFragments)
        {
            if (result.Length < pair.Key.Length) continue;
            if (!result.Contains(pair.Key)) continue;

            result = result.Replace(pair.Key, pair.Value);
        }

        return result;
    }

    private static bool HasThai(string text)
    {
        foreach (var c in text)
            if (c >= '฀' && c <= '๿') return true;

        return false;
    }

    /// <summary>เรียงวลียาวไปสั้น เพื่อไม่ให้คำสั้นไปตัดคำยาวก่อน</summary>
    private static void BuildSortedFragments()
    {
        if (s_SortedFragments != null) return;

        s_SortedFragments = new List<KeyValuePair<string, string>>(Fragments);
        s_SortedFragments.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    // ================= ชื่อเฉพาะ (แทนกลางประโยคได้) =================

    /// <summary>
    /// คำที่แทนที่กลางประโยคได้อย่างปลอดภัย — เป็นชื่อเฉพาะทั้งหมด
    /// ไม่ใส่คำทั่วไปอย่าง "พืช" "สัตว์" ลงตรงนี้ เพราะมันไปโผล่กลางประโยคอื่นได้
    /// </summary>
    private static readonly Dictionary<string, string> Fragments = new Dictionary<string, string>
    {
        // พืช
        { "เมล็ดแครอท", "Carrot Seeds" },
        { "เมล็ดข้าวโพด", "Corn Seeds" },
        { "เมล็ดข้าวสาลี", "Wheat Seeds" },
        { "เมล็ดมะเขือเทศ", "Tomato Seeds" },
        { "เมล็ดฟักทอง", "Pumpkin Seeds" },
        { "แครอท", "Carrot" },
        { "ข้าวโพด", "Corn" },
        { "ข้าวสาลี", "Wheat" },
        { "มะเขือเทศ", "Tomato" },
        { "ฟักทอง", "Pumpkin" },

        // สัตว์
        { "ฮัสกี้หิมะ", "Snow Husky" },
        { "หมูหมี", "Pigbear" },
        { "ยีราฟขนแกะ", "Girasheep" },
        { "แฮมสเตอร์ผึ้ง", "Beemster" },
        { "กระต่ายฉลาม", "Sharkbun" },

        // อุปกรณ์
        { "บัวรดน้ำ", "Watering Can" },
        { "ตะเกียง", "Lantern" },
        { "จอบ", "Hoe" },

        // หน่วยที่ปนกับตัวเลขเสมอ
        { "วันที่", "Day" },
    };

    // ================= ตารางคำแปล =================

    private static readonly Dictionary<string, string> Phrases = new Dictionary<string, string>
    {
        // ---- หน้าเมนูหลัก ----
        { "เล่นต่อ", "Continue" },
        { "เริ่มเกมใหม่", "New Game" },
        { "ออกจากเกม", "Quit" },
        { "กด Enter เพื่อเริ่มใหม่", "Press Enter to start" },
        { "กด Enter เพื่อเริ่มเกมใหม่", "Press Enter for a new game" },
        { "เข้าเกมเลย  (ข้ามเรื่องราว)", "Skip to Game" },
        { "พิมพ์ได้ทั้งไทยและอังกฤษ  •  กด Enter เพื่อเริ่มเล่น", "Thai and English both work  •  press Enter to start" },

        // ---- ข้อความที่มีปุ่มกำกับอยู่ด้วย (ต้องใส่ทั้งประโยค) ----
        { "สมุดบันทึก (J)", "Journal (J)" },
        { "กด H เพื่อดูปุ่มควบคุม", "Press H for controls" },
        { "ปุ่มควบคุม", "Controls" },
        { "สมุดบันทึก — ดูของทั้งหมดในเกม", "Journal — everything in the game" },
        { "BAG        ( I )", "BAG        ( I )" },

        // ---- เมนูตั้งค่า ----
        { "ตั้งค่าทั่วไป", "General" },
        { "ตั้งค่า", "Settings" },
        { "ควบคุม", "Controls" },
        { "เซฟ", "Save" },
        { "รีสตาร์ท", "Restart" },
        { "ระดับเสียง", "Volume" },
        { "เบาลง", "Quieter" },
        { "ดังขึ้น", "Louder" },
        { "ปิดเสียง", "Mute" },
        { "ปกติ", "Normal" },
        { "ดังสุด", "Max" },
        { "ดังกว่าปกติ", "louder than normal" },
        { "ปิดอยู่", "muted" },
        { "โหมดจอ", "Screen" },
        { "เต็มจอ", "Fullscreen" },
        { "หน้าต่าง", "Windowed" },
        { "สลับเต็มจอ / หน้าต่าง", "Toggle Fullscreen" },
        { "ชื่อตัวละคร", "Player name" },
        { "ภาษา", "Language" },
        { "ไทย", "Thai" },
        { "อังกฤษ", "English" },
        { "เปลี่ยนภาษา", "Change language" },
        { "ปิด", "Close" },
        { "บันทึกเกม", "Save game" },
        { "โหลดเกม", "Load game" },
        { "เริ่มใหม่ทั้งหมด", "Restart everything" },

        // ---- กระเป๋า / ไอเทม ----
        { "ทั้งหมด", "All" },
        { "อุปกรณ์", "Tools" },
        { "เมล็ด", "Seeds" },
        { "ผลผลิต", "Produce" },
        { "อาหารสัตว์", "Feed" },
        { "สัตว์ / ไข่", "Animals / Eggs" },
        { "ลากของมาทิ้งตรงนี้", "Drag items here to delete" },
        { "ลากออกนอกกระเป๋า = วางลงพื้น", "Drag outside the bag to drop on the ground" },
        { "ทิ้งแล้วเอาคืนไม่ได้", "This cannot be undone" },
        { "ทิ้งเลย", "Delete" },
        { "ไม่ทิ้ง", "Cancel" },
        { "ทิ้ง", "Delete" },
        { "เลยไหม", "?" },
        { "ทั้งกองเลยไหม", "— the whole stack?" },
        { "กระเป๋าเต็ม", "Bag is full" },

        // ---- ร้านค้า ----
        { "ขายผลผลิต", "Sell" },
        { "ซื้อเมล็ด", "Seeds" },
        { "ซื้อไข่", "Eggs" },
        { "ซื้อสัตว์โต", "Adults" },
        { "เงินของคุณ", "Your money" },
        { "ซื้อ", "Buy" },
        { "ขาย", "Sell" },
        { "ตัวโต", "adult" },
        { "ราคา", "Price" },

        // ---- ชื่อเควสทั้ง 9 ข้อ ----
        { "ปลูกพืชให้หมู่บ้าน", "Crops for the Village" },
        { "เก็บเกี่ยวผลผลิต", "Harvest Time" },
        { "ลองปลูกอย่างอื่นบ้าง", "Try Something New" },
        { "ขายของครั้งแรก", "Your First Sale" },
        { "ข้าวโพดราคาดี", "Corn Pays Well" },
        { "ฟาร์มที่เลี้ยงตัวเองได้", "A Self-Sufficient Farm" },
        { "รอยเท้าประหลาด", "Strange Footprints" },
        { "สวนมะเขือเทศ", "The Tomato Garden" },
        { "ฟักทองยักษ์", "The Giant Pumpkin" },
        { "ไปคุยกับ พี่สาวชาวบ้าน", "Go talk to the village girl" },
        { "พี่สาวชาวบ้าน", "Village Girl" },

        // ---- เควส ----
        { "เควส", "Quest" },
        { "ภารกิจ", "Quest" },
        { "สำเร็จแล้ว", "Complete" },
        { "รางวัล", "Reward" },
        { "ได้รับ", "Received" },

        // ---- วัน / เวลา ----
        { "วันที่", "Day" },
        { "เช้า", "Morning" },
        { "กลางวัน", "Day" },
        { "กลางคืน", "Night" },
        { "เที่ยง", "Noon" },
        { "สรุปวัน", "Day Summary" },
        { "วันใหม่", "New Day" },
        { "นอนหลับ", "Sleep" },
        { "ตื่นนอน", "Wake up" },

        // ---- สัตว์ ----
        { "เลือด", "Health" },
        { "ค่าความสัมพันธ์", "Friendship" },
        { "ความสัมพันธ์", "Friendship" },
        { "ความหิว", "Hunger" },
        { "ความอิ่ม", "Fullness" },
        { "การเจริญเติบโต", "Growth" },
        { "ภาวะของสัตว์", "Condition" },
        { "ภาวะ", "Condition" },
        { "ให้อาหาร", "Feed" },
        { "ลูกสัตว์", "Baby" },
        { "โตเต็มวัย", "Adult" },
        { "ผสมพันธุ์", "Breeding" },
        { "คอก", "Pen" },

        // ---- สมุดบันทึก ----
        { "สมุดบันทึก", "Journal" },
        { "พืช", "Crops" },
        { "สัตว์", "Animals" },
        { "ไอเทม", "Items" },
        { "ไข่", "Eggs" },

        // ---- ปุ่มควบคุม ----
        { "เดิน", "Move" },
        { "ใช้อุปกรณ์", "Use tool" },
        { "คุยกับ NPC", "Talk to NPC" },
        { "เปิดร้านค้า", "Open shop" },
        { "กระเป๋า", "Bag" },
        { "ดูปุ่มควบคุม", "Controls" },
        { "ปลดเมาส์", "Free cursor" },
        { "ล็อกเมาส์", "Lock cursor" },
        { "เมนูตั้งค่า", "Settings menu" },
        { "คลิกซ้าย", "Left click" },
        { "คลิกขวา", "Right click" },
        { "เลือกช่องของ", "Select slot" },
        { "วาร์ป", "Warp" },
        { "นอนบนเตียง", "Sleep in bed" },

        // ---- พืช ----
        { "แครอท", "Carrot" },
        { "ข้าวโพด", "Corn" },
        { "ข้าวสาลี", "Wheat" },
        { "มะเขือเทศ", "Tomato" },
        { "ฟักทอง", "Pumpkin" },
        { "เมล็ดแครอท", "Carrot Seeds" },
        { "เมล็ดข้าวโพด", "Corn Seeds" },
        { "เมล็ดข้าวสาลี", "Wheat Seeds" },
        { "เมล็ดมะเขือเทศ", "Tomato Seeds" },
        { "เมล็ดฟักทอง", "Pumpkin Seeds" },

        // ---- สัตว์แต่ละชนิด ----
        { "ฮัสกี้หิมะ", "Snow Husky" },
        { "หมูหมี", "Pigbear" },
        { "ยีราฟขนแกะ", "Girasheep" },
        { "แฮมสเตอร์ผึ้ง", "Beemster" },
        { "กระต่ายฉลาม", "Sharkbun" },

        // ---- อุปกรณ์ ----
        { "จอบ", "Hoe" },
        { "บัวรดน้ำ", "Watering Can" },
        { "ตะเกียง", "Lantern" },

        // ---- คำทั่วไป ----
        { "ยืนยัน", "Confirm" },
        { "ยกเลิก", "Cancel" },
        { "ตกลง", "OK" },
        { "กลับ", "Back" },
        { "ถัดไป", "Next" },
        { "ราคาซื้อ", "Buy price" },
        { "ราคาขาย", "Sell price" },
        { "ชิ้น", " pcs" },
    };
}
