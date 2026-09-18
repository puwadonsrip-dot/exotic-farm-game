using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ปรับตัวอักษรทั้งเกมให้อ่านสบายตาขึ้น
///
/// แก้ 2 อย่างที่ทำให้ตัวหนังสือดู "ขาวโพลน" เกินไป:
///
///   1. ตัวหนาปลอม — ฟอนต์ที่เอามาใส่เอง (Mitr Medium) มีน้ำหนักมาให้แล้ว
///      ถ้าสั่งตัวหนาซ้ำ Unity จะปั๊มเส้นให้หนาขึ้นเองแบบหยาบๆ ตัวอักษรบวมและสว่างจ้า
///
///   2. สีขาวล้วน (255,255,255) — จ้าเกินไปบนพื้นหลังสว่าง
///      เปลี่ยนเป็นขาวนวลอมครีมนิดๆ อ่านสบายตากว่ามาก แต่ยังดูเป็นสีขาวอยู่
///
/// แตะเฉพาะตัวอักษรสีขาวล้วนเท่านั้น ข้อความที่ตั้งสีไว้เอง (ทอง เขียว แดง) ไม่ยุ่ง
/// ตัวนี้สร้างตัวเองอัตโนมัติตอนเปิดเกม ไม่ต้องไปวางใน Scene
/// </summary>
public class TextPolish : MonoBehaviour
{
    /// <summary>ขาวนวล — ลดความจ้าลงแต่ยังอ่านเป็นสีขาว</summary>
    public static readonly Color SoftWhite = new Color(0.94f, 0.93f, 0.89f, 1f);

    /// <summary>สว่างเกินระดับนี้ทุกช่องสี = ถือว่าเป็นขาวล้วน</summary>
    private const float WhiteThreshold = 0.97f;

    private const float SweepInterval = 0.3f;

    private readonly HashSet<Text> m_Done = new HashSet<Text>();
    private float m_Timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("TextPolish");
        go.AddComponent<TextPolish>();
        DontDestroyOnLoad(go);
    }

    private void LateUpdate()
    {
        m_Timer -= Time.unscaledDeltaTime;
        if (m_Timer > 0f) return;

        m_Timer = SweepInterval;
        Sweep();
    }

    private void Sweep()
    {
        var texts = Object.FindObjectsByType<Text>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var text in texts)
        {
            if (text == null) continue;
            if (m_Done.Contains(text)) continue;

            Polish(text);
            m_Done.Add(text);
        }

        if (m_Done.Count > 500) m_Done.RemoveWhere(t => t == null);
    }

    private static void Polish(Text text)
    {
        // ---- เลิกใช้ตัวหนาปลอม ----
        if (UIFont.UsingProjectFont)
        {
            if (text.fontStyle == FontStyle.Bold)
                text.fontStyle = FontStyle.Normal;
            else if (text.fontStyle == FontStyle.BoldAndItalic)
                text.fontStyle = FontStyle.Italic;
        }

        // ---- ลดความจ้าของสีขาวล้วน ----
        var color = text.color;
        if (color.r >= WhiteThreshold && color.g >= WhiteThreshold && color.b >= WhiteThreshold)
        {
            text.color = new Color(SoftWhite.r, SoftWhite.g, SoftWhite.b, color.a);
        }
    }
}
