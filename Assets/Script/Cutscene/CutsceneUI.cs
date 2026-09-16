using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หนึ่งบรรทัดของคัตซีน
/// </summary>
[Serializable]
public class CutsceneLine
{
    [Tooltip("ภาพของฉากนี้ — เว้นว่าง = ใช้ภาพเดิมของฉากก่อนหน้าต่อ")]
    public Texture2D image;

    [Tooltip("ชื่อคนพูด — เว้นว่างไว้ถ้าเป็นข้อความบรรยาย")]
    public string speaker = "";

    [TextArea(2, 4)]
    public string text = "";
}

/// <summary>
/// หน้าต่างคัตซีน — แสดงภาพทีละฉาก พร้อมข้อความพิมพ์ทีละตัว
///
/// ใช้ไฟล์ภาพฉากละไฟล์จาก Assets/Art/Cutscene/Scene1.png ถึง Scene6.png
/// อยากเปลี่ยนภาพฉากไหน ก็เซฟทับไฟล์นั้นได้เลย ไม่ต้องแก้อะไรในเกม
/// </summary>
public class CutsceneUI : MonoBehaviour
{
    public static CutsceneUI Instance { get; private set; }

    /// <summary>ตอนนี้คัตซีนกำลังเล่นอยู่ไหม (สคริปต์อื่นใช้เช็คเพื่อหยุดรับปุ่ม)</summary>
    public static bool IsPlaying { get; private set; }

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    /// ถ้าครั้งก่อนกด Stop ตอนคัตซีนยังเล่นอยู่ Time.timeScale จะค้างที่ 0
    /// ทำให้รอบถัดไปเกมหยุดนิ่งทั้งเกม เลยต้องคืนค่าให้ด้วย
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsPlaying = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("ความเร็ว")]
    [Tooltip("ตัวอักษรต่อวินาที")]
    public float charsPerSecond = 45f;

    [Tooltip("เวลาที่ใช้เฟดตอนเปลี่ยนช่องภาพ (วินาที)")]
    public float fadeTime = 0.25f;

    [Header("จังหวะจบคัตซีน")]
    [Tooltip("เวลาที่ใช้มืดลงตอนจบ (วินาที)")]
    public float exitFadeTime = 0.55f;

    [Tooltip("เวลาที่ใช้สว่างขึ้นตอนโผล่เข้าเกม (วินาที)")]
    public float enterFadeTime = 0.8f;

    [Header("สี")]
    public Color boxColor = new Color(0.93f, 0.88f, 0.75f, 0.98f);
    public Color boxBorderColor = new Color(0.42f, 0.32f, 0.20f, 1f);
    public Color textColor = new Color(0.16f, 0.12f, 0.08f, 1f);
    public Color speakerTagColor = new Color(0.68f, 0.24f, 0.24f, 1f);

    // ---- UI ที่สร้างด้วยโค้ด ----
    private GameObject m_Root;
    private RawImage m_Art;
    private AspectRatioFitter m_ArtFitter;
    private Image m_SpeakerTag;
    private Text m_SpeakerText;
    private Text m_BodyText;
    private Text m_ContinueHint;
    private Font m_Font;

    // ---- สถานะระหว่างเล่น ----
    private CutsceneLine[] m_Lines;
    private int m_Index;
    private float m_Revealed;         // จำนวนตัวอักษรที่โชว์แล้ว
    private Texture2D m_CurrentImage;
    private float m_FadeLeft;
    private Action m_OnFinished;
    private int m_StartedFrame = -1;
    private float m_SavedTimeScale = 1f;

    // ---- ม่านดำตอนวาร์ปเข้าเกม ----
    private enum Phase { None, FadeToBlack, FadeToGame }
    private Phase m_Phase = Phase.None;
    private float m_PhaseLeft;
    private Image m_Curtain;

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
        m_Root.SetActive(false);
        IsPlaying = false;
    }

    // ================= เล่น / จบ =================

    public void Play(CutsceneLine[] lines, Action onFinished = null)
    {
        if (lines == null || lines.Length == 0)
        {
            onFinished?.Invoke();
            return;
        }

        m_Lines = lines;
        m_Index = 0;
        m_OnFinished = onFinished;
        m_CurrentImage = null;
        m_Phase = Phase.None;

        if (m_Curtain != null) m_Curtain.gameObject.SetActive(false);
        m_StartedFrame = Time.frameCount;

        m_Root.SetActive(true);
        IsPlaying = true;

        // หยุดเวลาในเกม แต่ UI ยังขยับได้เพราะใช้ unscaledDeltaTime
        m_SavedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        ShowLine(0);
        Debug.Log($"[Cutscene] เริ่มคัตซีน {lines.Length} บรรทัด");
    }

    /// <summary>
    /// จบคัตซีน — ไม่ตัดเข้าเกมทันที แต่ค่อยๆ มืดลงก่อน แล้วสว่างขึ้นในเกม
    /// กดข้ามก็ใช้ทางเดียวกัน จะได้ไม่กระตุกตา
    /// </summary>
    public void Finish()
    {
        if (!IsPlaying || m_Phase != Phase.None) return;

        m_Phase = Phase.FadeToBlack;
        m_PhaseLeft = exitFadeTime;

        m_Curtain.gameObject.SetActive(true);
        SetCurtainAlpha(0f);
    }

    /// <summary>ปิดคัตซีนจริงๆ แล้วส่งการควบคุมคืนให้เกม (เรียกตอนจอมืดสนิท)</summary>
    private void HandOverToGame()
    {
        m_Root.SetActive(false);
        IsPlaying = false;
        Time.timeScale = m_SavedTimeScale <= 0f ? 1f : m_SavedTimeScale;

        var cb = m_OnFinished;
        m_OnFinished = null;
        m_Lines = null;

        Debug.Log("[Cutscene] จบคัตซีน เข้าเกม");
        cb?.Invoke();
    }

    private void SetCurtainAlpha(float a)
    {
        if (m_Curtain == null) return;

        var c = m_Curtain.color;
        c.a = Mathf.Clamp01(a);
        m_Curtain.color = c;
    }

    /// <summary>ม่านดำตอนเปลี่ยนจากคัตซีนเข้าเกม</summary>
    private void UpdateTransition()
    {
        m_PhaseLeft -= Time.unscaledDeltaTime;

        if (m_Phase == Phase.FadeToBlack)
        {
            float t = exitFadeTime <= 0f ? 1f : 1f - (m_PhaseLeft / exitFadeTime);
            SetCurtainAlpha(t);

            if (m_PhaseLeft > 0f) return;

            // จอมืดสนิทแล้ว สลับเข้าเกมตรงนี้ ผู้เล่นจะไม่เห็นรอยต่อ
            SetCurtainAlpha(1f);
            HandOverToGame();

            m_Phase = Phase.FadeToGame;
            m_PhaseLeft = enterFadeTime;
            return;
        }

        // ค่อยๆ เปิดม่านให้เห็นเกม
        float k = enterFadeTime <= 0f ? 1f : 1f - (m_PhaseLeft / enterFadeTime);
        SetCurtainAlpha(1f - k);

        if (m_PhaseLeft > 0f) return;

        SetCurtainAlpha(0f);
        m_Curtain.gameObject.SetActive(false);
        m_Phase = Phase.None;
    }

    // ================= อัปเดตทุกเฟรม =================

    private void Update()
    {
        // กำลังวาร์ปเข้าเกมอยู่ = ไม่รับปุ่มอะไรทั้งนั้น
        if (m_Phase != Phase.None)
        {
            UpdateTransition();
            return;
        }

        if (!IsPlaying) return;

        float dt = Time.unscaledDeltaTime;

        // เฟดภาพตอนเปลี่ยนช่อง
        if (m_FadeLeft > 0f)
        {
            m_FadeLeft -= dt;
            float a = fadeTime <= 0f ? 1f : Mathf.Clamp01(1f - (m_FadeLeft / fadeTime));
            var c = m_Art.color;
            c.a = a;
            m_Art.color = c;
        }

        // พิมพ์ข้อความทีละตัว
        var line = m_Lines[m_Index];
        int total = line.text != null ? line.text.Length : 0;

        if (m_Revealed < total)
        {
            m_Revealed += charsPerSecond * dt;
            int shown = Mathf.Clamp(Mathf.FloorToInt(m_Revealed), 0, total);
            m_BodyText.text = line.text.Substring(0, shown);
        }

        bool done = m_Revealed >= total;
        m_ContinueHint.enabled = done;
        if (done)
        {
            // กะพริบเบาๆ ให้รู้ว่ารอกดอยู่
            var c = m_ContinueHint.color;
            c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.6f));
            m_ContinueHint.color = c;
        }

        // กันการกดค้างจากเฟรมที่เพิ่งเปิด
        if (Time.frameCount == m_StartedFrame) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Finish();
            return;
        }

        bool advance =
            (kb != null && (kb.spaceKey.wasPressedThisFrame
                            || kb.enterKey.wasPressedThisFrame
                            || kb.eKey.wasPressedThisFrame))
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (!advance) return;

        if (!done)
        {
            // กดครั้งแรก = โชว์ข้อความทั้งบรรทัดทันที
            m_Revealed = total;
            m_BodyText.text = line.text;
            return;
        }

        if (m_Index + 1 >= m_Lines.Length) Finish();
        else ShowLine(m_Index + 1);
    }

    private void ShowLine(int index)
    {
        m_Index = index;
        m_Revealed = 0f;

        var line = m_Lines[index];

        m_BodyText.text = "";
        m_ContinueHint.enabled = false;

        bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
        m_SpeakerTag.gameObject.SetActive(hasSpeaker);
        if (hasSpeaker) m_SpeakerText.text = line.speaker;

        // ไม่ได้ใส่ภาพไว้ = ใช้ภาพเดิมต่อ / ภาพเดิมอยู่แล้ว = ไม่ต้องเฟดใหม่
        if (line.image == null || line.image == m_CurrentImage) return;

        m_CurrentImage = line.image;
        ApplyImage(line.image);

        var col = m_Art.color;
        col.a = 0f;
        m_Art.color = col;
        m_FadeLeft = fadeTime;
    }

    private void ApplyImage(Texture2D tex)
    {
        m_Art.enabled = true;
        m_Art.texture = tex;

        // ปรับสัดส่วนกรอบให้ตรงกับไฟล์ ภาพจะได้ไม่ยืด
        if (m_ArtFitter != null && tex.height > 0)
            m_ArtFitter.aspectRatio = (float)tex.width / tex.height;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("CutsceneCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;   // ทับทุกอย่าง

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- พื้นหลังดำเต็มจอ ----
        m_Root = new GameObject("CutsceneRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var bg = m_Root.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.04f, 1f);
        Stretch((RectTransform)m_Root.transform);

        // ---- กรอบภาพ ----
        var artHolder = new GameObject("ArtArea", typeof(RectTransform));
        artHolder.transform.SetParent(m_Root.transform, false);

        // ภาพเต็มจอ กล่องข้อความลอยทับด้านล่าง เหมือนเกมแนวเล่าเรื่องทั่วไป
        var holderRT = (RectTransform)artHolder.transform;
        holderRT.anchorMin = Vector2.zero;
        holderRT.anchorMax = Vector2.one;
        holderRT.offsetMin = Vector2.zero;
        holderRT.offsetMax = Vector2.zero;

        var artGO = new GameObject("Art", typeof(RectTransform));
        artGO.transform.SetParent(artHolder.transform, false);

        m_Art = artGO.AddComponent<RawImage>();
        m_Art.raycastTarget = false;

        var artRT = (RectTransform)artGO.transform;
        artRT.anchorMin = artRT.anchorMax = new Vector2(0.5f, 0.5f);
        artRT.pivot = new Vector2(0.5f, 0.5f);
        artRT.anchoredPosition = Vector2.zero;

        // Envelope = ขยายให้เต็มจอ ส่วนที่เกินยอมให้ล้นออกไป ภาพจะได้ไม่ยืดและไม่มีขอบดำ
        m_ArtFitter = artGO.AddComponent<AspectRatioFitter>();
        m_ArtFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        m_ArtFitter.aspectRatio = 1.78f;

        // ---- กล่องข้อความ ----
        var frame = new GameObject("TextFrame", typeof(RectTransform));
        frame.transform.SetParent(m_Root.transform, false);

        var frameImage = frame.AddComponent<Image>();
        frameImage.color = boxBorderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = new Vector2(0f, 0f);
        frameRT.anchorMax = new Vector2(1f, 0f);
        frameRT.pivot = new Vector2(0.5f, 0f);
        frameRT.anchoredPosition = new Vector2(0f, 48f);
        frameRT.offsetMin = new Vector2(90f, 48f);
        frameRT.offsetMax = new Vector2(-90f, 48f);
        frameRT.sizeDelta = new Vector2(frameRT.sizeDelta.x, 236f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.color = boxColor;
        Stretch((RectTransform)inner.transform, 6f);

        // ---- ป้ายชื่อคนพูด (ลอยเหนือกล่อง) ----
        var tag = new GameObject("SpeakerTag", typeof(RectTransform));
        tag.transform.SetParent(frame.transform, false);

        m_SpeakerTag = tag.AddComponent<Image>();
        m_SpeakerTag.color = speakerTagColor;

        var tagRT = (RectTransform)tag.transform;
        tagRT.anchorMin = tagRT.anchorMax = new Vector2(0f, 1f);
        tagRT.pivot = new Vector2(0f, 0f);
        tagRT.anchoredPosition = new Vector2(26f, -4f);
        tagRT.sizeDelta = new Vector2(240f, 56f);

        m_SpeakerText = MakeText(tag.transform, 32, TextAnchor.MiddleCenter, Color.white);
        Stretch(m_SpeakerText.rectTransform);

        // ---- ข้อความหลัก ----
        m_BodyText = MakeText(inner.transform, 38, TextAnchor.UpperLeft, textColor);
        m_BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        m_BodyText.lineSpacing = 1.25f;
        var bodyRT = m_BodyText.rectTransform;
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(34f, 54f);
        bodyRT.offsetMax = new Vector2(-34f, -26f);

        // ---- คำใบ้ให้กดต่อ ----
        m_ContinueHint = MakeText(inner.transform, 26, TextAnchor.LowerRight,
                                  new Color(0.35f, 0.28f, 0.18f, 1f));
        m_ContinueHint.text = "คลิก หรือ Space เพื่อไปต่อ  ▼";
        var hintRT = m_ContinueHint.rectTransform;
        hintRT.anchorMin = Vector2.zero;
        hintRT.anchorMax = Vector2.one;
        hintRT.offsetMin = new Vector2(30f, 14f);
        hintRT.offsetMax = new Vector2(-30f, -30f);

        // ---- ปุ่มข้าม ----
        var skip = MakeButton(m_Root.transform, "ข้าม  (Esc)",
                              new Color(0.22f, 0.21f, 0.20f, 0.9f), 210f);
        var skipRT = (RectTransform)skip.transform;
        skipRT.anchorMin = skipRT.anchorMax = new Vector2(1f, 1f);
        skipRT.pivot = new Vector2(1f, 1f);
        skipRT.anchoredPosition = new Vector2(-26f, -18f);
        skip.onClick.AddListener(Finish);

        // ---- ม่านดำ ----
        // เป็นพี่น้องกับ CutsceneRoot ไม่ใช่ลูก จะได้ยังค้างบนจอตอนปิดคัตซีนไปแล้ว
        // และต่อท้ายสุด จะได้วาดทับทุกอย่างรวมถึงปุ่มข้าม
        var curtain = new GameObject("Curtain", typeof(RectTransform));
        curtain.transform.SetParent(canvasGO.transform, false);

        m_Curtain = curtain.AddComponent<Image>();
        m_Curtain.color = new Color(0f, 0f, 0f, 0f);
        m_Curtain.raycastTarget = false;   // ไม่ขวางการเล่นตอนม่านกำลังเปิด
        Stretch((RectTransform)curtain.transform);

        curtain.SetActive(false);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private Button MakeButton(Transform parent, string label, Color color, float width)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, 54f);

        var img = go.AddComponent<Image>();
        img.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText(go.transform, 26, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        Stretch(text.rectTransform);

        return button;
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
