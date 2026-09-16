using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// วงจรกลางวัน–กลางคืน
///
/// เวลาเดินเองเรื่อยๆ ฟ้าค่อยๆ มืดลงและสว่างขึ้นตามชั่วโมงในเกม
/// ตอนกลางคืนจะมีแสงวงกลมติดตามตัวผู้เล่นไปด้วย เดินไปไหนก็สว่างรอบตัว
///
/// กด T = ข้ามไปกลางคืนทันที กดอีกทีข้ามไปตอนเช้า
/// นอน (N) หรือข้ามวัน = เริ่มเช้าวันใหม่เสมอ
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("ความเร็วเวลา")]
    [Tooltip("1 วันในเกมใช้เวลาจริงกี่นาที (30 = กลางวันประมาณ 17 นาที)")]
    public float minutesPerDay = 30f;

    [Tooltip("เวลาที่เริ่มเล่น (6 = หกโมงเช้า)")]
    [Range(0f, 24f)]
    public float startHour = 6f;

    [Header("เวลาตอนนี้ (ดูอย่างเดียว)")]
    [Range(0f, 24f)]
    public float hour = 6f;

    [Header("ช่วงเวลา")]
    [Tooltip("กี่โมงถือว่าเข้ากลางคืน")]
    public float nightHour = 19.5f;

    [Tooltip("กี่โมงถือว่าเช้า")]
    public float morningHour = 6f;

    [Header("ไฟในฉาก")]
    [Tooltip("ไฟส่องทั้งฉาก — ตัวติดตั้งจะหาให้เอง")]
    public Light2D globalLight;

    [Tooltip("ไฟวงกลมรอบตัวผู้เล่น — ตัวติดตั้งจะสร้างให้เอง")]
    public Light2D playerLight;

    [Tooltip("ความสว่างรอบตัวตอนมือเปล่า — ให้พอมองเห็นทางเท่านั้น")]
    public float playerLightMax = 0.45f;

    [Tooltip("รัศมีแสงรอบตัวตอนมือเปล่า (ช่อง)")]
    public float playerLightRadius = 3.2f;

    [Header("ตะเกียงถือ")]
    [Tooltip("ไอเทมตะเกียง — ถือแล้วสว่างขึ้นมาก ตัวติดตั้งใส่ให้เอง")]
    public ItemData lanternItem;

    [Tooltip("ความสว่างตอนถือตะเกียง")]
    public float lanternLightMax = 1.9f;

    [Tooltip("รัศมีแสงตอนถือตะเกียง (ช่อง)")]
    public float lanternLightRadius = 7.5f;

    [Tooltip("สีแสงตอนถือตะเกียง")]
    public Color lanternColor = new Color(1f, 0.78f, 0.45f);

    [Tooltip("ตะเกียงกะพริบแรงแค่ไหน")]
    [Range(0f, 0.3f)]
    public float lanternFlicker = 0.12f;

    [Header("การกดข้ามเวลา")]
    [Tooltip("กดข้ามแล้วใช้เวลากี่วินาทีในการเปลี่ยนฟ้า")]
    public float warpTime = 2.2f;

    [Tooltip("เปิด/ปิดปุ่ม T")]
    public bool enableTimeSkipKey = true;

    // ---- ตารางความสว่างของแต่ละชั่วโมง ----
    // เริ่มเกมตอน 6 โมง = ช่วงพระอาทิตย์ขึ้นพอดี ฟ้าส้มจัด
    private static readonly float[] KeyHours =
        { 0f, 4.5f, 5.5f, 6f, 7f, 9f, 12f, 16f, 18f, 19.5f, 20.5f, 21.5f, 24f };

    // เช้ากับเย็นต้องดูต่างกันชัดๆ
    //   เช้า = สว่าง สีทองอมส้ม สดใส
    //   เย็น = หรี่ลง สีแดงเข้ม แล้วไล่เป็นม่วง
    private static readonly float[] KeyIntensity =
        { 0.20f, 0.24f, 0.58f, 0.82f, 0.93f, 0.99f, 1.00f,
          0.97f, 0.82f, 0.44f, 0.28f, 0.20f, 0.20f };

    private static readonly Color[] KeyColor =
    {
        new Color(0.32f, 0.40f, 0.72f),   // 0น.   ดึกสงัด ฟ้าน้ำเงินเข้ม
        new Color(0.38f, 0.44f, 0.74f),   // 4.5น. ใกล้รุ่ง
        new Color(0.95f, 0.68f, 0.40f),   // 5.5น. ฟ้าเริ่มสาง ส้มอ่อน
        new Color(1.00f, 0.80f, 0.34f),   // 6น.   แดดเช้า เหลืองทอง สดใส
        new Color(1.00f, 0.86f, 0.50f),   // 7น.   เช้าทองอ่อน
        new Color(1.00f, 0.96f, 0.88f),   // 9น.   สายๆ
        new Color(1.00f, 0.99f, 0.96f),   // 12น.  เที่ยง แสงขาว
        new Color(1.00f, 0.93f, 0.84f),   // 16น.  บ่าย
        new Color(1.00f, 0.74f, 0.52f),   // 18น.  เย็นทอง
        new Color(0.90f, 0.42f, 0.30f),   // 19.5น. พลบค่ำ แดงส้มเข้ม
        new Color(0.55f, 0.36f, 0.58f),   // 20.5น. ฟ้าม่วง
        new Color(0.32f, 0.40f, 0.72f),   // 21.5น. กลางคืน
        new Color(0.32f, 0.40f, 0.72f),   // 24น.
    };

    private bool m_Warping;
    private float m_WarpLeft;    // เหลืออีกกี่ชั่วโมงในเกมถึงจะถึงเป้า
    private float m_WarpSpeed;   // ชั่วโมงต่อวินาทีจริง

    /// <summary>ตอนนี้เป็นกลางคืนไหม</summary>
    public bool IsNight => hour >= nightHour || hour < morningHour;

    /// <summary>เวลาแบบ 07:30 เอาไปโชว์บนจอ</summary>
    public string ClockText
    {
        get
        {
            int h = Mathf.FloorToInt(hour) % 24;
            int m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
            return $"{h:00}:{m:00}";
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        hour = startHour;
    }

    private void Start()
    {
        if (globalLight == null)
            Debug.LogWarning("[กลางวันกลางคืน] ยังไม่ได้ใส่ Global Light — ฟ้าจะไม่มืด");

        Apply();
    }

    private void Update()
    {
        // Time.deltaTime = หยุดเดินเองตอนเกมหยุด (คัตซีน เมนู ข้ามวัน)
        float dt = Time.deltaTime;

        if (m_Warping)
        {
            // นับเป็น "เหลืออีกกี่ชั่วโมง" แทนการเทียบกับเลขเป้าหมาย
            // เพราะพอข้ามเที่ยงคืนแล้วเลขชั่วโมงจะวนกลับไป 0 เทียบกันไม่ได้
            float step = Mathf.Min(m_WarpSpeed * dt, m_WarpLeft);

            hour += step;
            m_WarpLeft -= step;

            if (m_WarpLeft <= 0.0001f) m_Warping = false;
        }
        else if (minutesPerDay > 0f)
        {
            hour += dt * (24f / (minutesPerDay * 60f));
        }

        hour = Mathf.Repeat(hour, 24f);

        HandleKey();
        Apply();
    }

    private void HandleKey()
    {
        if (!enableTimeSkipKey) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.tKey.wasPressedThisFrame) return;

        // หยุดรับปุ่มตอนมีหน้าต่างเปิดอยู่
        if (SettingsMenuUI.IsOpen || NameEntryUI.IsOpen || CutsceneUI.IsPlaying
            || TitleScreenUI.IsOpen || DayTransitionUI.IsPlaying
            || DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
            || AdminModeUI.IsOpen) return;

        // กำลังวาร์ปอยู่แล้วกดซ้ำ = หยุดตรงเวลาที่เห็นอยู่ตอนนั้นเลย
        if (m_Warping)
        {
            StopWarp();
            return;
        }

        if (IsNight) WarpTo(morningHour);
        else WarpTo(nightHour + 0.6f);
    }

    /// <summary>หยุดการวาร์ปทันที ค้างไว้ที่เวลาปัจจุบัน ไม่กระโดดไปไหนต่อ</summary>
    public void StopWarp()
    {
        if (!m_Warping) return;

        m_Warping = false;
        m_WarpLeft = 0f;
        Apply();

        Debug.Log($"[เวลา] หยุดที่ {ClockText}");
    }

    // ================= สั่งเปลี่ยนเวลา =================

    /// <summary>ข้ามไปเวลาที่ต้องการ แบบค่อยๆ เปลี่ยนฟ้าให้ไม่กระตุกตา</summary>
    public void WarpTo(float targetHour)
    {
        targetHour = Mathf.Repeat(targetHour, 24f);
        hour = Mathf.Repeat(hour, 24f);

        // เวลาเดินหน้าอย่างเดียว ไม่ถอยหลัง
        float distance = Mathf.Repeat(targetHour - hour, 24f);

        // ใกล้มากอยู่แล้วก็ไม่ต้องทำอะไร
        if (distance < 0.05f) return;

        m_WarpLeft = distance;
        m_WarpSpeed = warpTime > 0f ? distance / warpTime : distance * 1000f;
        m_Warping = true;

        Debug.Log($"[เวลา] วาร์ปจาก {ClockText} ไป {(int)targetHour:00}:{(int)((targetHour % 1f) * 60f):00}");
    }

    /// <summary>ตั้งเป็นเช้าวันใหม่ทันที (ใช้ตอนนอน จอมืดอยู่แล้วเลยไม่ต้องค่อยๆ เปลี่ยน)</summary>
    public void SetMorning()
    {
        m_Warping = false;
        m_WarpLeft = 0f;
        hour = Mathf.Repeat(morningHour, 24f);
        Apply();
    }

    // ================= ปรับแสง =================

    private void Apply()
    {
        Evaluate(hour, out float intensity, out Color color);

        if (globalLight != null)
        {
            globalLight.intensity = intensity;
            globalLight.color = color;
        }

        if (playerLight == null) return;

        // ยิ่งฟ้ามืด ไฟรอบตัวยิ่งสว่าง
        float darkness = Mathf.InverseLerp(0.95f, 0.25f, intensity);

        bool holdingLantern = IsHoldingLantern();

        float maxPower = holdingLantern ? lanternLightMax : playerLightMax;
        float radius = holdingLantern ? lanternLightRadius : playerLightRadius;

        // ตะเกียงเป็นเปลวไฟ เลยกะพริบเบาๆ
        float wobble = holdingLantern && lanternFlicker > 0f
            ? 1f + Mathf.Sin(Time.time * 7f) * lanternFlicker
            : 1f;

        float lightPower = darkness * maxPower * wobble;

        bool on = lightPower > 0.03f;
        if (playerLight.enabled != on) playerLight.enabled = on;
        if (!on) return;

        playerLight.intensity = lightPower;
        playerLight.pointLightOuterRadius = radius;
        playerLight.pointLightInnerRadius = radius * 0.2f;

        if (holdingLantern) playerLight.color = lanternColor;
    }

    /// <summary>ตอนนี้ถือตะเกียงอยู่ในช่องที่เลือกไหม</summary>
    private bool IsHoldingLantern()
    {
        if (lanternItem == null) return false;
        if (InventorySystem.Instance == null) return false;

        return InventorySystem.Instance.SelectedItem == lanternItem;
    }

    /// <summary>หาความสว่างกับสีของชั่วโมงที่กำหนด โดยไล่สีระหว่างจุดในตาราง</summary>
    private static void Evaluate(float atHour, out float intensity, out Color color)
    {
        atHour = Mathf.Repeat(atHour, 24f);

        for (int i = 0; i < KeyHours.Length - 1; i++)
        {
            if (atHour < KeyHours[i] || atHour > KeyHours[i + 1]) continue;

            float t = Mathf.InverseLerp(KeyHours[i], KeyHours[i + 1], atHour);
            t = Mathf.SmoothStep(0f, 1f, t);

            intensity = Mathf.Lerp(KeyIntensity[i], KeyIntensity[i + 1], t);
            color = Color.Lerp(KeyColor[i], KeyColor[i + 1], t);
            return;
        }

        intensity = KeyIntensity[0];
        color = KeyColor[0];
    }
}
