using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// กล่องบทสนทนา NPC
///
/// - พิมพ์ข้อความทีละตัว (typewriter)
/// - คลิก 1 ครั้งขณะพิมพ์ = โชว์ทั้งบรรทัดทันที
/// - คลิกอีกครั้ง = ไปบรรทัดถัดไป
/// - บรรทัดสุดท้าย = โชว์ปุ่ม "รับเควส" / "ไว้ก่อน"
///
/// สร้าง UI ด้วยโค้ดทั้งหมด วางไว้บน GameObject ชื่อ "DialogueUI"
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    /// <summary>ตอนนี้มีกล่องคุยเปิดอยู่ไหม (ระบบอื่นเช็คได้)</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    /// Unity 6 ปิด Domain Reload เป็นค่าเริ่มต้น ตัวแปร static เลยค้างข้ามรอบ
    /// ถ้าครั้งก่อนกด Stop ตอนกล่องคุยเปิดค้างไว้ เกมจะล็อคการกดปุ่มทั้งหมด
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => IsOpen = false;

    [Header("ความเร็วพิมพ์")]
    [Tooltip("วินาทีต่อ 1 ตัวอักษร — ยิ่งน้อยยิ่งเร็ว")]
    public float typeSpeed = 0.02f;

    [Header("สี")]
    public Color panelColor = new Color(0.08f, 0.07f, 0.07f, 0.96f);
    public Color borderColor = new Color(0.85f, 0.72f, 0.35f, 1f);
    public Color buttonColor = new Color(0.30f, 0.55f, 0.25f, 1f);
    public Color buttonAltColor = new Color(0.32f, 0.30f, 0.30f, 1f);

    private GameObject m_Panel;
    private Text m_PromptText;
    private Text m_NameText;
    private Text m_BodyText;
    private Text m_HintText;
    private GameObject m_ButtonRow;
    private Button m_AcceptButton;
    private Text m_AcceptLabel;
    private Button m_LaterButton;

    private string[] m_Lines;
    private int m_LineIndex;
    private bool m_IsTyping;
    private bool m_ButtonsShowing;
    private Coroutine m_TypeRoutine;

    private Action m_OnAccept;
    private Action m_OnClose;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildUI();
        m_Panel.SetActive(false);
        IsOpen = false;
    }

    private void Update()
    {
        if (!IsOpen || m_ButtonsShowing) return;

        bool advance =
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame ||
                                          Keyboard.current.spaceKey.wasPressedThisFrame));

        if (advance) Advance();
    }

    /// <summary>โชว์ป้ายบอกปุ่มที่ล่างกลางจอ (ส่งค่าว่างเพื่อซ่อน)</summary>
    public void SetPrompt(string text)
    {
        if (m_PromptText == null) return;
        m_PromptText.text = IsOpen ? "" : text;
    }

    // ================= เปิด / ปิด =================

    /// <summary>เปิดกล่องคุย</summary>
    /// <param name="npcName">ชื่อ NPC</param>
    /// <param name="lines">บทพูด บรรทัดละประโยค</param>
    /// <param name="acceptLabel">ข้อความบนปุ่มตอบรับ (เว้นว่าง = ไม่มีปุ่ม)</param>
    /// <param name="onAccept">กดปุ่มตอบรับแล้วทำอะไร</param>
    /// <param name="onClose">ปิดกล่องแล้วทำอะไร</param>
    public void Show(string npcName, string[] lines, string acceptLabel = "",
                     Action onAccept = null, Action onClose = null)
    {
        if (lines == null || lines.Length == 0) return;

        m_Lines = lines;
        m_LineIndex = 0;
        m_OnAccept = onAccept;
        m_OnClose = onClose;

        m_NameText.text = npcName;
        m_AcceptLabel.text = string.IsNullOrEmpty(acceptLabel) ? "ตกลง" : acceptLabel;
        m_AcceptButton.gameObject.SetActive(true);
        m_LaterButton.gameObject.SetActive(!string.IsNullOrEmpty(acceptLabel));

        m_Panel.SetActive(true);
        IsOpen = true;
        m_ButtonsShowing = false;
        m_ButtonRow.SetActive(false);

        TypeCurrentLine();
    }

    public void Close()
    {
        if (m_TypeRoutine != null) StopCoroutine(m_TypeRoutine);

        m_Panel.SetActive(false);
        IsOpen = false;
        m_ButtonsShowing = false;

        var cb = m_OnClose;
        m_OnClose = null;
        m_OnAccept = null;
        cb?.Invoke();
    }

    // ================= เดินบทสนทนา =================

    private void Advance()
    {
        if (m_IsTyping)
        {
            // ยังพิมพ์อยู่ -> โชว์ทั้งบรรทัดเลย
            if (m_TypeRoutine != null) StopCoroutine(m_TypeRoutine);
            m_BodyText.text = m_Lines[m_LineIndex];
            m_IsTyping = false;
            UpdateHint();
            return;
        }

        m_LineIndex++;

        if (m_LineIndex < m_Lines.Length)
        {
            TypeCurrentLine();
        }
        else
        {
            // จบบทพูด -> โชว์ปุ่ม
            m_ButtonsShowing = true;
            m_ButtonRow.SetActive(true);
            m_HintText.text = "";
        }
    }

    private void TypeCurrentLine()
    {
        if (m_TypeRoutine != null) StopCoroutine(m_TypeRoutine);
        m_TypeRoutine = StartCoroutine(TypeRoutine(m_Lines[m_LineIndex]));
    }

    private IEnumerator TypeRoutine(string line)
    {
        m_IsTyping = true;
        m_BodyText.text = "";
        UpdateHint();

        for (int i = 0; i < line.Length; i++)
        {
            m_BodyText.text += line[i];
            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        m_IsTyping = false;
        UpdateHint();
    }

    private void UpdateHint()
    {
        m_HintText.text = m_IsTyping ? "คลิกเพื่อข้าม" : "คลิกเพื่อไปต่อ  ▼";
    }

    private void OnAcceptClicked()
    {
        var cb = m_OnAccept;
        Close();
        cb?.Invoke();
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var font = UIFont.Get();

        var canvasGO = new GameObject("DialogueCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;   // อยู่เหนือ Hotbar

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;   // วาดฟอนต์ละเอียด 3 เท่า -> ตัวอักษรคมขึ้น

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- ป้ายบอกปุ่ม (ล่างกลางจอ เหนือ Hotbar) ----
        m_PromptText = MakeText(canvasGO.transform, font, 38, TextAnchor.LowerCenter, Color.white);
        m_PromptText.fontStyle = FontStyle.Bold;
        var promptRT = m_PromptText.rectTransform;
        promptRT.anchorMin = promptRT.anchorMax = new Vector2(0.5f, 0f);
        promptRT.pivot = new Vector2(0.5f, 0f);
        promptRT.anchoredPosition = new Vector2(0f, 156f);
        promptRT.sizeDelta = new Vector2(900f, 52f);
        m_PromptText.text = "";

        var promptShadow = m_PromptText.gameObject.AddComponent<Shadow>();
        promptShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        promptShadow.effectDistance = new Vector2(2f, -2f);

        // ---- กรอบนอก ----
        m_Panel = new GameObject("DialoguePanel", typeof(RectTransform));
        m_Panel.transform.SetParent(canvasGO.transform, false);

        var borderImg = m_Panel.AddComponent<Image>();
        borderImg.color = borderColor;

        var panelRT = (RectTransform)m_Panel.transform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 190f);   // เหนือ Hotbar
        panelRT.sizeDelta = new Vector2(1280f, 280f);

        // ---- พื้นใน ----
        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(m_Panel.transform, false);

        var innerImg = inner.AddComponent<Image>();
        innerImg.color = panelColor;

        var innerRT = (RectTransform)inner.transform;
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(5f, 5f);
        innerRT.offsetMax = new Vector2(-5f, -5f);

        // ---- ชื่อ NPC ----
        m_NameText = MakeText(inner.transform, font, 40, TextAnchor.UpperLeft, borderColor);
        m_NameText.fontStyle = FontStyle.Bold;
        var nameRT = m_NameText.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0f, -18f);
        nameRT.offsetMin = new Vector2(32f, nameRT.offsetMin.y);
        nameRT.offsetMax = new Vector2(-32f, nameRT.offsetMax.y);
        nameRT.sizeDelta = new Vector2(nameRT.sizeDelta.x, 44f);

        // ---- ข้อความ ----
        m_BodyText = MakeText(inner.transform, font, 36, TextAnchor.UpperLeft, Color.white);
        m_BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var bodyRT = m_BodyText.rectTransform;
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(32f, 96f);
        bodyRT.offsetMax = new Vector2(-32f, -70f);

        // ---- คำใบ้มุมขวาล่าง ----
        m_HintText = MakeText(inner.transform, font, 26, TextAnchor.LowerRight,
                              new Color(1f, 1f, 1f, 0.5f));
        var hintRT = m_HintText.rectTransform;
        hintRT.anchorMin = Vector2.zero;
        hintRT.anchorMax = Vector2.one;
        hintRT.offsetMin = new Vector2(32f, 18f);
        hintRT.offsetMax = new Vector2(-32f, -70f);

        // ---- แถวปุ่ม ----
        m_ButtonRow = new GameObject("Buttons", typeof(RectTransform));
        m_ButtonRow.transform.SetParent(inner.transform, false);

        var rowRT = (RectTransform)m_ButtonRow.transform;
        rowRT.anchorMin = new Vector2(1f, 0f);
        rowRT.anchorMax = new Vector2(1f, 0f);
        rowRT.pivot = new Vector2(1f, 0f);
        rowRT.anchoredPosition = new Vector2(-32f, 22f);
        rowRT.sizeDelta = new Vector2(520f, 62f);

        var rowLayout = m_ButtonRow.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16;
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        m_LaterButton = MakeButton(m_ButtonRow.transform, font, "ไว้ก่อน", buttonAltColor, out _);
        m_LaterButton.onClick.AddListener(Close);

        m_AcceptButton = MakeButton(m_ButtonRow.transform, font, "รับเควส", buttonColor, out m_AcceptLabel);
        m_AcceptButton.onClick.AddListener(OnAcceptClicked);

        m_ButtonRow.SetActive(false);
    }

    private static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Button MakeButton(Transform parent, Font font, string label, Color color, out Text labelText)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 190;
        layout.preferredHeight = 58;
        layout.minWidth = 190;
        layout.minHeight = 58;

        var img = go.AddComponent<Image>();
        img.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        labelText = MakeText(go.transform, font, 32, TextAnchor.MiddleCenter, Color.white);
        labelText.fontStyle = FontStyle.Bold;
        var rt = labelText.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        labelText.text = label;

        return button;
    }
}
