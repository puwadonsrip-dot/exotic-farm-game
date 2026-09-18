using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวหาฟอนต์ที่ใช้ทั้งเกม
///
/// ลำดับการหา:
///   1. ฟอนต์ที่เราเอามาใส่เองใน Assets/Resources/Fonts/  ← สวยที่สุด แนะนำให้ใส่
///   2. ฟอนต์ของ Windows ที่รองรับภาษาไทย
///   3. ฟอนต์ติดมากับ Unity (ภาษาไทยจะเป็นกล่องสี่เหลี่ยม — ทางเลือกสุดท้าย)
///
/// อยากเปลี่ยนฟอนต์ทั้งเกม: เอาไฟล์ .ttf ไปวางที่
///     Assets/Resources/Fonts/GameFont.ttf
/// แค่นั้น ไม่ต้องแก้โค้ดตรงไหนเลย
/// </summary>
public static class UIFont
{
    private static Font s_Cached;

    /// <summary>
    /// ฟอนต์ในโปรเจกต์ที่จะลองหาก่อน (พาธเทียบจากโฟลเดอร์ Resources ไม่ต้องใส่นามสกุล)
    ///
    /// ชื่อ "GameFont" คือชื่อกลางๆ — ตั้งชื่อไฟล์เป็นอันนี้แล้วใช้ได้เลย
    /// ที่เหลือเผื่อไว้เฉยๆ เอาไฟล์ชื่อเดิมจากที่โหลดมาวางได้โดยไม่ต้องเปลี่ยนชื่อ
    /// </summary>
    private static readonly string[] ProjectFonts =
    {
        "Fonts/GameFont",
        "Fonts/Kanit-SemiBold",
        "Fonts/Kanit-Medium",
        "Fonts/Kanit-Regular",
        "Fonts/Mitr-Medium",
        "Fonts/Mitr-Regular",
        "Fonts/Prompt-Medium",
        "Fonts/Prompt-Regular",
        "Fonts/NotoSansThai-SemiBold",
        "Fonts/NotoSansThai-Regular",
        "Fonts/Sarabun-SemiBold",
        "Fonts/Sarabun-Regular",
    };

    /// <summary>ฟอนต์ของ Windows ที่ใช้แทนได้ เรียงจากที่ดูดีที่สุดลงมา</summary>
    private static readonly string[] SystemFonts =
    {
        "Leelawadee UI",      // ตัวที่สะอาดและทันสมัยที่สุดที่ Windows มีให้
        "Leelawadee",
        "Noto Sans Thai",
        "Tahoma",
        "Microsoft Sans Serif",
        "Angsana New",
        "Cordia New",
        "Arial Unicode MS",
    };

    /// <summary>ตัวอักษรไทยที่ใช้ทดสอบว่าฟอนต์พิมพ์ไทยได้จริง ("ก ไก่")</summary>
    private const char ThaiTestChar = 'ก';

    /// <summary>
    /// ขนาดที่ใช้สร้างฟอนต์จากระบบ
    ///
    /// ยิ่งใหญ่ Unity ยิ่งวาดตัวอักษรลงแผ่นภาพละเอียดขึ้น ตัวหนังสือเลยคมกว่า
    /// แลกกับใช้หน่วยความจำมากขึ้นนิดหน่อย — คุ้มมากสำหรับ UI ที่มีตัวอักษรใหญ่
    /// </summary>
    private const int SystemFontSize = 64;

    public static Font Get()
    {
        if (s_Cached != null) return s_Cached;

        // ---- 1. ฟอนต์ที่เอามาใส่เองในโปรเจกต์ ----
        foreach (var path in ProjectFonts)
        {
            var font = Resources.Load<Font>(path);
            if (font == null) continue;

            s_Cached = font;
            Debug.Log($"[UIFont] ใช้ฟอนต์จากโปรเจกต์: {font.name}");
            return s_Cached;
        }

        // ---- 2. ฟอนต์ของ Windows ----
        foreach (var name in SystemFonts)
        {
            var font = Font.CreateDynamicFontFromOSFont(name, SystemFontSize);
            if (font == null || !font.HasCharacter(ThaiTestChar)) continue;

            s_Cached = font;
            return s_Cached;
        }

        // ---- 3. ไม่เจอเลย ----
        Debug.LogWarning("[UIFont] ไม่พบฟอนต์ที่รองรับภาษาไทยในเครื่อง ข้อความไทยอาจแสดงเป็นกล่องสี่เหลี่ยม");
        s_Cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return s_Cached;
    }

    /// <summary>
    /// ใส่ขอบดำบางๆ รอบตัวอักษร ให้อ่านออกชัดบนพื้นหลังสีอะไรก็ได้
    ///
    /// เกมส่วนใหญ่ทำแบบนี้ เพราะตัวหนังสือขาวบนพื้นสว่างจะจมหายไป
    /// เรียกหลังสร้าง Text เสร็จ: UIFont.AddOutline(myText);
    /// </summary>
    public static void AddOutline(Text text, float thickness = 1.6f, float alpha = 0.75f)
    {
        if (text == null) return;
        if (text.GetComponent<Outline>() != null) return;

        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, alpha);
        outline.effectDistance = new Vector2(thickness, -thickness);
        outline.useGraphicAlpha = true;
    }
}
