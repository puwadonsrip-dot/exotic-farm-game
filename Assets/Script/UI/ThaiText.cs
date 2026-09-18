/// <summary>
/// ตัวช่วยตัดข้อความภาษาไทยโดยไม่ทำให้ตัวอักษรขาด
///
/// ปัญหา: ภาษาไทย 1 ตัวที่ตาเห็น อาจใช้หลาย char ซ้อนกัน
///   "กิ"  = ก + สระอิ         (2 char)
///   "กี้" = ก + สระอี + ไม้โท  (3 char)
///   "เก"  = สระเอ + ก         (2 char — สระมาก่อนพยัญชนะ)
///
/// ถ้าตัดด้วย Substring ตรงๆ จะได้ "ก" เปล่าๆ แล้วค่อยเด้งสระขึ้นมาทีหลัง
/// หรือได้สระ "เ" ลอยอยู่ตัวเดียว — ทำให้ข้อความดูขาดๆ วิ่นๆ
///
/// ตัวนี้จะเลื่อนจุดตัดไปข้างหน้าจนจบกลุ่มอักษรเสมอ
/// </summary>
public static class ThaiText
{
    /// <summary>
    /// สระบน สระล่าง วรรณยุกต์ และตัวการันต์ — พวกนี้เกาะพยัญชนะตัวหน้า ไม่มีความกว้างของตัวเอง
    /// ครอบคลุม: ั  ิ ี ึ ื ุ ู ฺ  ็ ่ ้ ๊ ๋ ์ ํ ๎
    /// </summary>
    public static bool IsCombining(char c)
    {
        return c == 'ั'
            || (c >= 'ิ' && c <= 'ฺ')
            || (c >= '็' && c <= '๎');
    }

    /// <summary>
    /// สระหน้า: เ แ โ ใ ไ — เขียนไว้ข้างหน้าแต่ออกเสียงตามหลังพยัญชนะ
    /// ถ้าโชว์ตัวเดียวจะเป็นสระลอย ต้องลากพยัญชนะตัวถัดไปมาด้วย
    /// </summary>
    public static bool IsLeadingVowel(char c)
    {
        return c >= 'เ' && c <= 'ไ';
    }

    /// <summary>
    /// หาจุดตัดที่ปลอดภัยที่สุดที่ &gt;= want
    ///
    /// ใช้แทน Substring(0, want) ตอนพิมพ์ข้อความทีละตัว
    /// </summary>
    public static int SafeCut(string text, int want)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (want <= 0) return 0;
        if (want >= text.Length) return text.Length;

        int i = want;

        // กันตัดกลาง surrogate pair (อีโมจิ / อักษรนอก BMP)
        if (char.IsLowSurrogate(text[i])) i++;

        // เดินหน้าให้พ้นสระ/วรรณยุกต์ที่เกาะตัวก่อนหน้าอยู่
        while (i < text.Length && IsCombining(text[i])) i++;

        // ตัวก่อนจุดตัดเป็นสระหน้า = ต้องลากพยัญชนะตามมาด้วย (เผื่อซ้อนกันหลายชั้น เช่น "เเ")
        while (i > 0 && i < text.Length && IsLeadingVowel(text[i - 1]))
        {
            i++;
            if (i < text.Length && char.IsLowSurrogate(text[i])) i++;
            while (i < text.Length && IsCombining(text[i])) i++;
        }

        return i > text.Length ? text.Length : i;
    }

    /// <summary>ตัดข้อความแบบปลอดภัย คืนสตริงที่พร้อมเอาไปโชว์</summary>
    public static string Cut(string text, int want)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Substring(0, SafeCut(text, want));
    }

    /// <summary>
    /// ลบตัวอักษรท้ายสุด 1 ตัว "แบบที่ตาเห็น" — ใช้ตอนกด Backspace
    /// ถ้าท้ายสตริงเป็นวรรณยุกต์ จะลบวรรณยุกต์นั้นตัวเดียว (ถูกต้องตามที่ผู้พิมพ์คาดหวัง)
    /// แต่ถ้าเหลือสระหน้าลอยอยู่ตัวเดียว จะลบทิ้งไปด้วย
    /// </summary>
    public static string Backspace(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        int i = text.Length - 1;

        if (char.IsLowSurrogate(text[i]) && i > 0) i--;

        // เหลือสระหน้าค้างท้าย = ลบทิ้งด้วย ไม่ปล่อยให้ลอยเดี่ยว
        if (i > 0 && IsLeadingVowel(text[i - 1]) && !IsCombining(text[i])) i--;

        return text.Substring(0, i);
    }
}
