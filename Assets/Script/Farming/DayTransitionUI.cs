using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// จอข้ามวัน — จอค่อยๆ มืดลงเหมือนหลับ ขึ้นเลขวันใหม่ แล้วสว่างขึ้นเป็นเช้าวันถัดไป
///
/// ใช้จังหวะเดียวกับตอนวาร์ปจบคัตซีน
/// วันจะเปลี่ยนจริงตอนจอมืดสนิท ผู้เล่นเลยไม่เห็นพืชเปลี่ยนภาพกลางอากาศ
///
/// มีป้ายบอกวันมุมซ้ายบนตลอดเวลาด้วย จะได้รู้ว่าตอนนี้วันที่เท่าไหร่
/// </summary>
public class DayTransitionUI : MonoBehaviour
{
    public static DayTransitionUI Instance { get; private set; }

    /// <summary>กำลังข้ามวันอยู่ไหม (สคริปต์อื่นใช้เช็คเพื่อหยุดรับปุ่ม)</summary>
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsPlaying = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("จังหวะ (วินาที)")]
    [Tooltip("จอมืดลง")]
    public float fadeInTime = 0.7f;

    [Tooltip("ค้างจอมืดพร้อมโชว์เลขวัน")]
    public float holdTime = 1.4f;

    [Tooltip("จอสว่างขึ้นเป็นเช้าวันใหม่")]
    public float fadeOutTime = 0.9f;

    [Header("สี")]
    [Tooltip("สีจอตอนหลับ — น้ำเงินเข้มจะดูเป็นกลางคืนกว่าสีดำล้วน")]
    public Color nightColor = new Color(0.03f, 0.04f, 0.10f, 1f);

    public Color dayNumberColor = new Color(0.98f, 0.92f, 0.62f, 1f);

    // ---- UI ----
    private Image m_Curtain;
    private Text m_DayText;
    private Text m_SummaryText;
    private Text m_HudDayText;
    private Font m_Font;

    // ---- สถานะ ----
    private enum Phase { None, FadeIn, Hold, FadeOut }
    private Phase m_Phase = Phase.None;
    private float m_PhaseLeft;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        m_Font = UIFont.Get();
        BuildUI();

        m_Curtain.gameObject.SetActive(false);
        IsPlaying = false;
    }

    private void Start()
    {
        var farm = FarmManager.Instance;
        if (farm == null) return;

        farm.OnDayChanged += OnDayChanged;
        OnDayChanged(farm.currentDay);
    }

    private void OnDestroy()
    {
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnDayChanged -= OnDayChanged;
    }

    private int m_ShownDay = 1;

    private void OnDayChanged(int day)
    {
        m_ShownDay = day;
        RefreshHud();
    }

    /// <summary>ป้ายมุมขวาบน — มีนาฬิกาต่อท้ายถ้ามีระบบกลางวันกลางคืน</summary>
    private void RefreshHud()
    {
        if (m_HudDayText == null) return;

        var clock = DayNightCycle.Instance;
        m_HudDayText.text = clock != null
            ? $"วันที่ {m_ShownDay}   {clock.ClockText}"
            : $"วันที่ {m_ShownDay}";
    }

    // ================= สั่งข้ามวัน =================

    /// <summary>นอน — ข้ามไปวันถัดไปพร้อมจอมืด</summary>
    public void Sleep()
    {
        if (IsPlaying) return;
        if (FarmManager.Instance == null)
        {
            Debug.LogWarning("[ข้ามวัน] ไม่พบ FarmManager ใน Scene");
            return;
        }

        IsPlaying = true;
        m_Phase = Phase.FadeIn;
        m_PhaseLeft = fadeInTime;

        m_Curtain.gameObject.SetActive(true);
        SetAlpha(0f);

        m_DayText.text = "";
        m_SummaryText.text = "";
    }

    // ================= อัปเดตทุกเฟรม =================

    private void Update()
    {
        RefreshHud();

        if (m_Phase == Phase.None) return;

        m_PhaseLeft -= Time.unscaledDeltaTime;

        switch (m_Phase)
        {
            case Phase.FadeIn:
            {
                float t = fadeInTime <= 0f ? 1f : 1f - (m_PhaseLeft / fadeInTime);
                SetAlpha(t);

                if (m_PhaseLeft > 0f) return;

                SetAlpha(1f);
                ChangeDay();          // เปลี่ยนวันตอนจอมืดสนิท

                m_Phase = Phase.Hold;
                m_PhaseLeft = holdTime;
                return;
            }

            case Phase.Hold:
            {
                if (m_PhaseLeft > 0f) return;

                m_Phase = Phase.FadeOut;
                m_PhaseLeft = fadeOutTime;
                return;
            }

            default:
            {
                float k = fadeOutTime <= 0f ? 1f : 1f - (m_PhaseLeft / fadeOutTime);
                SetAlpha(1f - k);

                if (m_PhaseLeft > 0f) return;

                SetAlpha(0f);
                m_Curtain.gameObject.SetActive(false);
                m_Phase = Phase.None;
                IsPlaying = false;
                return;
            }
        }
    }

    private void ChangeDay()
    {
        var result = FarmManager.Instance.AdvanceDay();

        // นอนแล้วตื่นมาต้องเป็นเช้าเสมอ
        if (DayNightCycle.Instance != null)
            DayNightCycle.Instance.SetMorning();

        m_DayText.text = $"วันที่ {result.day}";

        // สรุปว่าคืนที่ผ่านมาเกิดอะไรขึ้น
        var parts = new System.Collections.Generic.List<string>();
        if (result.grew > 0) parts.Add($"พืชโตขึ้น {result.grew} ต้น");
        if (result.ready > 0) parts.Add($"พร้อมเก็บเกี่ยว {result.ready} ต้น");
        if (result.thirsty > 0) parts.Add($"ไม่โต {result.thirsty} ต้น เพราะลืมรดน้ำ");

        // เหตุการณ์ฝั่งสัตว์ (AnimalManager ทำงานไปแล้วตอน AdvanceDay)
        var animals = AnimalManager.Instance;
        if (animals != null && !string.IsNullOrEmpty(animals.LastDayReport))
            parts.Add(animals.LastDayReport);

        m_SummaryText.text = parts.Count > 0
            ? string.Join("   •   ", parts)
            : "เช้าวันใหม่ที่เงียบสงบ";
    }

    /// <summary>ข้อความบนม่านจะจางตามม่าน แต่โผล่ช้ากว่านิดนึงให้ดูนุ่ม</summary>
    private void SetAlpha(float a)
    {
        a = Mathf.Clamp01(a);

        var c = m_Curtain.color;
        c.a = a;
        m_Curtain.color = c;

        float textAlpha = Mathf.Clamp01((a - 0.55f) / 0.45f);
        SetTextAlpha(m_DayText, textAlpha);
        SetTextAlpha(m_SummaryText, textAlpha);
    }

    private static void SetTextAlpha(Text t, float a)
    {
        if (t == null) return;

        var c = t.color;
        c.a = a;
        t.color = c;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("DayTransitionCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // เหนือ UI ทั่วไป แต่ต่ำกว่าคัตซีนกับหน้าเมนู

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        // ---- ป้ายบอกวัน (เห็นตลอดเวลา) ----
        // วางมุมขวาบนใต้เงิน เพราะมุมซ้ายบนเป็นที่ของแถบเควส
        m_HudDayText = MakeText(canvasGO.transform, 34, TextAnchor.UpperRight, Color.white);
        m_HudDayText.text = "วันที่ 1";

        var hudRT = m_HudDayText.rectTransform;
        hudRT.anchorMin = hudRT.anchorMax = new Vector2(1f, 1f);
        hudRT.pivot = new Vector2(1f, 1f);
        hudRT.anchoredPosition = new Vector2(-28f, -88f);
        hudRT.sizeDelta = new Vector2(320f, 44f);

        var hudOutline = m_HudDayText.gameObject.AddComponent<Outline>();
        hudOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        hudOutline.effectDistance = new Vector2(2f, -2f);

        // ---- ม่านกลางคืน ----
        var curtain = new GameObject("NightCurtain", typeof(RectTransform));
        curtain.transform.SetParent(canvasGO.transform, false);

        m_Curtain = curtain.AddComponent<Image>();
        m_Curtain.color = new Color(nightColor.r, nightColor.g, nightColor.b, 0f);
        m_Curtain.raycastTarget = true;   // กันเผลอกดปุ่มบนจอตอนจอมืด
        Stretch((RectTransform)curtain.transform);

        // ---- เลขวัน ----
        m_DayText = MakeText(curtain.transform, 96, TextAnchor.MiddleCenter, dayNumberColor);
        var dayRT = m_DayText.rectTransform;
        dayRT.anchorMin = new Vector2(0f, 0.46f);
        dayRT.anchorMax = new Vector2(1f, 0.68f);
        dayRT.offsetMin = Vector2.zero;
        dayRT.offsetMax = Vector2.zero;

        // ---- สรุปเมื่อคืน ----
        m_SummaryText = MakeText(curtain.transform, 34, TextAnchor.MiddleCenter,
                                 new Color(1f, 1f, 1f, 0.85f));
        var sumRT = m_SummaryText.rectTransform;
        sumRT.anchorMin = new Vector2(0f, 0.36f);
        sumRT.anchorMax = new Vector2(1f, 0.46f);
        sumRT.offsetMin = Vector2.zero;
        sumRT.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private Text MakeText(Transform parent, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font = m_Font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
