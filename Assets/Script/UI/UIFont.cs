using UnityEngine;

/// <summary>
/// ตัวหาฟอนต์ที่แสดงภาษาไทยได้
///
/// ฟอนต์ที่ Unity แถมมา (LegacyRuntime.ttf) ไม่มีตัวอักษรไทย
/// ถ้าใช้ตัวนั้น ข้อความไทยจะขึ้นเป็นกล่องสี่เหลี่ยม □□□
///
/// ตัวนี้จะไปยืมฟอนต์จาก Windows แทน แล้วเช็คว่าพิมพ์ไทยได้จริงก่อนใช้
/// </summary>
public static class UIFont
{
    private static Font s_Cached;

    /// <summary>ฟอนต์ที่ลองตามลำดับ (ตัวไหนมีในเครื่องและรองรับไทยก็ใช้ตัวนั้น)</summary>
    private static readonly string[] Candidates =
    {
        "Leelawadee UI",
        "Leelawadee",
        "Tahoma",
        "Noto Sans Thai",
        "Angsana New",
        "Cordia New",
        "Arial Unicode MS"
    };

    /// <summary>ตัวอักษรไทยที่ใช้ทดสอบ ("ก ไก่")</summary>
    private const char ThaiTestChar = 'ก';

    public static Font Get()
    {
        if (s_Cached != null) return s_Cached;

        foreach (var name in Candidates)
        {
            var font = Font.CreateDynamicFontFromOSFont(name, 32);
            if (font != null && font.HasCharacter(ThaiTestChar))
            {
                s_Cached = font;
                return s_Cached;
            }
        }

        // หาไม่เจอเลย — ใช้ของ Unity ไปก่อน (ภาษาไทยจะเป็นกล่อง)
        Debug.LogWarning("[UIFont] ไม่พบฟอนต์ที่รองรับภาษาไทยในเครื่อง ข้อความไทยอาจแสดงเป็นกล่องสี่เหลี่ยม");
        s_Cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return s_Cached;
    }
}
