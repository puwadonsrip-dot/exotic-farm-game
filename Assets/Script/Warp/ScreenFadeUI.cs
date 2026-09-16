using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ม่านดำเต็มจอ ใช้ตอนวาร์ปจากที่หนึ่งไปอีกที่หนึ่ง
///
/// จอมืดลง → ย้ายตัวละครตอนจอมืดสนิท → จอสว่างขึ้นที่ใหม่
/// ผู้เล่นจะไม่เห็นรอยต่อ เหมือนเดินผ่านประตู
/// </summary>
public class ScreenFadeUI : MonoBehaviour
{
    public static ScreenFadeUI Instance { get; private set; }

    /// <summary>กำลังวาร์ปอยู่ไหม — สคริปต์อื่นใช้เช็คเพื่อหยุดรับปุ่ม</summary>
    public static bool IsFading { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsFading = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("จังหวะ (วินาที)")]
    public float fadeOutTime = 0.35f;
    [Tooltip("ค้างจอดำไว้กี่วินาที — เผื่อเวลาให้กล้องกับขอบเขตแมพตั้งตัวก่อนจอสว่าง")]
    public float holdTime = 0.4f;
    public float fadeInTime = 0.45f;

    [Header("สี")]
    public Color curtainColor = new Color(0.02f, 0.02f, 0.03f, 1f);

    private Image m_Curtain;

    private enum Phase { None, Out, Hold, In }
    private Phase m_Phase = Phase.None;
    private float m_Left;
    private Action m_OnBlack;
    private float m_SavedTimeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildUI();
        m_Curtain.gameObject.SetActive(false);
    }

    // ================= สั่งวาร์ป =================

    /// <summary>จอมืด → ทำสิ่งที่สั่งไว้ (ย้ายตัวละคร) → จอสว่าง</summary>
    public bool Play(Action onBlack)
    {
        if (IsFading) return false;

        IsFading = true;
        m_OnBlack = onBlack;

        m_Phase = Phase.Out;
        m_Left = fadeOutTime;

        m_Curtain.gameObject.SetActive(true);
        SetAlpha(0f);

        // หยุดเกมไว้ ไม่ให้เดินต่อระหว่างจอมืด
        m_SavedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        return true;
    }

    private void Update()
    {
        if (m_Phase == Phase.None) return;

        m_Left -= Time.unscaledDeltaTime;

        switch (m_Phase)
        {
            case Phase.Out:
            {
                float t = fadeOutTime <= 0f ? 1f : 1f - (m_Left / fadeOutTime);
                SetAlpha(t);

                if (m_Left > 0f) return;

                SetAlpha(1f);

                // จอมืดสนิทแล้ว ย้ายตัวละครตรงนี้
                var job = m_OnBlack;
                m_OnBlack = null;
                job?.Invoke();

                // คืนเวลาให้เดินทันทีตั้งแต่ยังจอดำ
                // กล้องใช้ Time.deltaTime ในการไล่ตาม ถ้าเวลายังหยุดอยู่มันจะไม่ขยับเลย
                // แล้วพอจอสว่างค่อยเริ่มไถลตาม กลายเป็นกล้องตามไม่ทัน
                Time.timeScale = m_SavedTimeScale <= 0f ? 1f : m_SavedTimeScale;

                m_Phase = Phase.Hold;
                m_Left = holdTime;
                return;
            }

            case Phase.Hold:
            {
                if (m_Left > 0f) return;

                m_Phase = Phase.In;
                m_Left = fadeInTime;
                return;
            }

            default:
            {
                float k = fadeInTime <= 0f ? 1f : 1f - (m_Left / fadeInTime);
                SetAlpha(1f - k);

                if (m_Left > 0f) return;

                SetAlpha(0f);
                m_Curtain.gameObject.SetActive(false);

                m_Phase = Phase.None;
                IsFading = false;

                // กันเวลาค้างที่ 0 เผื่อมีอะไรไปแก้ระหว่างทาง
                if (Time.timeScale <= 0f) Time.timeScale = 1f;
                return;
            }
        }
    }

    private void SetAlpha(float a)
    {
        var c = m_Curtain.color;
        c.a = Mathf.Clamp01(a);
        m_Curtain.color = c;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;   // เหนือ HUD ทุกอย่าง แต่ต่ำกว่าคัตซีน

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var curtain = new GameObject("Curtain", typeof(RectTransform));
        curtain.transform.SetParent(canvasGO.transform, false);

        m_Curtain = curtain.AddComponent<Image>();
        m_Curtain.color = new Color(curtainColor.r, curtainColor.g, curtainColor.b, 0f);
        m_Curtain.raycastTarget = false;

        var rt = (RectTransform)curtain.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
