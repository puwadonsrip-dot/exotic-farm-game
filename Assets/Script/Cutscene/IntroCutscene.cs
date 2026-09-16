using UnityEngine;

/// <summary>
/// คัตซีนเปิดเกม — เล่นอัตโนมัติตอนกด Play
///
/// ปกติจะเล่นแค่ครั้งแรกครั้งเดียว (จำไว้ใน PlayerPrefs)
/// ถ้าอยากดูใหม่: Tools → Farming → ดูคัตซีนเปิดเกมอีกครั้ง
/// หรือติ๊ก Play Every Time ใน Inspector ตอนกำลังทำเกม
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    private const string SeenKey = "ExoticFarm.IntroSeen";

    [Header("เล่นเมื่อไหร่")]
    [Tooltip("ติ๊กไว้ = เล่นทุกครั้งที่กด Play (สะดวกตอนทดสอบ)")]
    public bool playEveryTime = false;

    [Tooltip("รอกี่วินาทีก่อนเริ่ม เผื่อให้ฉากโหลดเสร็จก่อน")]
    public float delay = 0.15f;

    [Header("บทพูด")]
    [Tooltip("แก้ข้อความได้ตรงนี้ — panel คือเลขช่องภาพในไฟล์รูป")]
    public CutsceneLine[] lines = DefaultStory();

    private float m_Timer;
    private bool m_Started;

    /// <summary>ล้างความจำว่าเคยดูแล้ว — เรียกจากเมนู Tools</summary>
    public static void ForgetSeen() => PlayerPrefs.DeleteKey(SeenKey);

    private void Start()
    {
        // มีหน้าเมนูอยู่ = รอให้ผู้เล่นกด "เริ่มเกมใหม่" ก่อน ไม่เล่นเอง
        if (TitleScreenUI.Instance != null)
        {
            enabled = false;
            return;
        }

        if (!playEveryTime && PlayerPrefs.GetInt(SeenKey, 0) == 1)
        {
            enabled = false;
            return;
        }
        m_Timer = delay;
    }

    private System.Action m_After;

    /// <summary>
    /// สั่งเล่นคัตซีนทันที (หน้าเมนูเรียกตัวนี้)
    /// after = สิ่งที่จะทำต่อหลังคัตซีนจบ เช่น ขึ้นหน้าตั้งชื่อ
    /// </summary>
    public void PlayNow(System.Action after = null)
    {
        m_Started = true;
        m_After = after;
        enabled = false;

        if (CutsceneUI.Instance == null)
        {
            Debug.LogWarning("[Cutscene] ไม่พบ CutsceneUI ใน Scene — ข้ามคัตซีนเปิดเกม");
            after?.Invoke();
            return;
        }

        CutsceneUI.Instance.Play(lines, OnFinished);
    }

    private void Update()
    {
        if (m_Started) return;

        m_Timer -= Time.unscaledDeltaTime;
        if (m_Timer > 0f) return;

        PlayNow();
    }

    private void OnFinished()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        var next = m_After;
        m_After = null;
        next?.Invoke();
    }

    // ================= เนื้อเรื่อง =================

    private const string Npc = "พี่สาวชาวบ้าน";

    /// <summary>
    /// เนื้อเรื่องเปิดเกม 6 ฉาก ฉากละภาพ
    /// ภาพมาจาก Assets/Art/Cutscene/Scene1.png ถึง Scene6.png ตามลำดับ
    /// ตัวติดตั้งจะใส่ไฟล์ให้เองในช่อง Image ของแต่ละบรรทัด
    /// </summary>
    public static CutsceneLine[] DefaultStory()
    {
        return new[]
        {
            // ฉาก 1 — หมู่บ้านกับฟาร์มร้าง
            new CutsceneLine { speaker = "",
                text = "ณ หมู่บ้านเล็กๆ ที่โอบล้อมด้วยภูเขาและทุ่งกว้าง\n"
                     + "มีฟาร์มร้างหลังหนึ่ง ที่รอเจ้าของกลับมานานหลายปี" },

            // ฉาก 2 — เจอพี่สาวชาวบ้าน
            new CutsceneLine { speaker = Npc,
                text = "อ้าว! กลับมาจนได้นะ รอตั้งนาน\n"
                     + "ดีใจจริงๆ ที่ได้เจอกันอีก" },

            // ฉาก 3 — ฟาร์มของคุณปู่
            new CutsceneLine { speaker = Npc,
                text = "ฟาร์มหลังนี้เคยเป็นของคุณปู่เธอ ตอนนี้เป็นของเธอแล้ว\n"
                     + "ดูรกไปหน่อย แต่ดินยังดีอยู่นะ ขาดแค่คนลงมือ" },

            // ฉาก 4 — สอนปลูก
            new CutsceneLine { speaker = Npc,
                text = "เริ่มง่ายๆ — พรวนดินด้วยจอบ แล้วหยอดเมล็ดลงไป\n"
                     + "มีข้าวสาลี แครอท กับข้าวโพด เลือกปลูกได้ตามใจเลย" },

            // ฉาก 5 — สอนรดน้ำกับขายของ
            new CutsceneLine { speaker = Npc,
                text = "รดน้ำทุกวันนะ สามวันก็เก็บเกี่ยวได้แล้ว\n"
                     + "เก็บได้เมื่อไหร่เอามาขายให้พี่ พี่รับซื้อหมดเลย" },

            // ฉาก 6 — ทิ้งท้ายเรื่องสัตว์ลึกลับ
            new CutsceneLine { speaker = "",
                text = "แต่ในป่าหลังฟาร์มนั้น มีบางอย่างเฝ้ามองเธออยู่เงียบๆ...\n"
                     + "เรื่องนั้นค่อยว่ากัน — ตอนนี้ไปเริ่มวันแรกของเธอกันก่อน" },
        };
    }
}
