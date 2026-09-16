using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าตั้งชื่อตัวละคร — ขึ้นหลังกด "เริ่มเกมใหม่" ก่อนเข้าคัตซีน
///
/// รับตัวอักษรจาก Keyboard.current.onTextInput โดยตรง
/// เลยพิมพ์ภาษาไทยได้ และไม่ต้องพึ่ง InputField ของ Unity
/// </summary>
public class NameEntryUI : MonoBehaviour
{
    public static NameEntryUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsOpen = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("สี")]
    public Color panelColor = new Color(0.10f, 0.12f, 0.10f, 0.98f);
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color fieldColor = new Color(0.05f, 0.06f, 0.05f, 1f);
    public Color confirmColor = new Color(0.34f, 0.55f, 0.26f, 1f);

    private GameObject m_Root;
    private Text m_FieldText;
    private Text m_WarningText;
    private Button m_ConfirmButton;
    private Font m_Font;

    private string m_Typed = "";
    private Action m_OnDone;
    private int m_OpenedFrame = -1;

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
        IsOpen = false;
    }

    private void OnDisable() => Unsubscribe();

    // ================= เปิด / ปิด =================

    public void Show(Action onDone)
    {
        m_OnDone = onDone;
        m_Typed = PlayerProfile.HasName ? PlayerProfile.Name : "";
        m_OpenedFrame = Time.frameCount;

        m_Root.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;

        m_WarningText.text = "";
        RefreshField();

        if (Keyboard.current != null)
            Keyboard.current.onTextInput += OnTextInput;
    }

    public void Confirm()
    {
        if (!IsOpen) return;

        if (m_Typed.Trim().Length == 0)
        {
            m_WarningText.text = "ใส่ชื่อก่อนนะ";
            return;
        }

        PlayerProfile.SetName(m_Typed);

        Unsubscribe();
        m_Root.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;

        var cb = m_OnDone;
        m_OnDone = null;
        cb?.Invoke();
    }

    private void Unsubscribe()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
    }

    // ================= รับตัวอักษร =================

    private void OnTextInput(char c)
    {
        if (!IsOpen) return;
        if (Time.frameCount == m_OpenedFrame) return;   // กันตัวอักษรค้างจากเฟรมก่อน

        // ตัดปุ่มควบคุมทิ้ง (Enter, Backspace, Tab ฯลฯ จัดการแยก)
        if (c < ' ') return;

        if (m_Typed.Length >= PlayerProfile.MaxNameLength)
        {
            m_WarningText.text = $"ยาวได้ไม่เกิน {PlayerProfile.MaxNameLength} ตัว";
            return;
        }

        m_Typed += c;
        m_WarningText.text = "";
        RefreshField();
    }

    private void Update()
    {
        if (!IsOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.backspaceKey.wasPressedThisFrame && m_Typed.Length > 0)
        {
            m_Typed = m_Typed.Substring(0, m_Typed.Length - 1);
            m_WarningText.text = "";
            RefreshField();
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            Confirm();

        RefreshCaret();
    }

    private void RefreshField()
    {
        m_ConfirmButton.interactable = m_Typed.Trim().Length > 0;
        RefreshCaret();
    }

    /// <summary>ขีดกะพริบท้ายข้อความ ให้รู้ว่าพิมพ์ได้อยู่</summary>
    private void RefreshCaret()
    {
        bool caretOn = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;

        if (m_Typed.Length == 0)
        {
            m_FieldText.color = new Color(1f, 1f, 1f, 0.4f);
            m_FieldText.text = caretOn ? "พิมพ์ชื่อที่นี่ |" : "พิมพ์ชื่อที่นี่";
            return;
        }

        m_FieldText.color = Color.white;
        m_FieldText.text = caretOn ? m_Typed + "|" : m_Typed;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var rounded = UIShapes.RoundedRect(24);

        var canvasGO = new GameObject("NameEntryCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;   // เหนือหน้าเมนูหลัก

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        m_Root = new GameObject("NameEntryRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);
        Stretch((RectTransform)m_Root.transform);

        // ---- กรอบ ----
        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(m_Root.transform, false);

        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = rounded;
        frameImage.type = Image.Type.Sliced;
        frameImage.color = borderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = Vector2.zero;
        frameRT.sizeDelta = new Vector2(820f, 400f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 6f);

        // ---- หัวข้อ ----
        var header = MakeText(inner.transform, 46, TextAnchor.UpperCenter, borderColor);
        header.text = "ตั้งชื่อตัวละคร";
        var headerRT = header.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -34f);
        headerRT.sizeDelta = new Vector2(0f, 56f);

        // ---- ช่องกรอก ----
        var field = new GameObject("Field", typeof(RectTransform));
        field.transform.SetParent(inner.transform, false);

        var fieldImage = field.AddComponent<Image>();
        fieldImage.sprite = rounded;
        fieldImage.type = Image.Type.Sliced;
        fieldImage.color = fieldColor;

        var fieldRT = (RectTransform)field.transform;
        fieldRT.anchorMin = fieldRT.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRT.pivot = new Vector2(0.5f, 0.5f);
        fieldRT.anchoredPosition = new Vector2(0f, 26f);
        fieldRT.sizeDelta = new Vector2(660f, 96f);

        m_FieldText = MakeText(field.transform, 44, TextAnchor.MiddleCenter, Color.white);
        Stretch(m_FieldText.rectTransform, 18f);

        // ---- คำเตือน ----
        m_WarningText = MakeText(inner.transform, 26, TextAnchor.MiddleCenter,
                                 new Color(0.95f, 0.55f, 0.45f, 1f));
        var warnRT = m_WarningText.rectTransform;
        warnRT.anchorMin = warnRT.anchorMax = new Vector2(0.5f, 0.5f);
        warnRT.pivot = new Vector2(0.5f, 0.5f);
        warnRT.anchoredPosition = new Vector2(0f, -40f);
        warnRT.sizeDelta = new Vector2(660f, 32f);

        // ---- ปุ่มยืนยัน ----
        m_ConfirmButton = MakeButton(inner.transform, "เริ่มเล่น", confirmColor, 300f, 74f, rounded);
        var okRT = (RectTransform)m_ConfirmButton.transform;
        okRT.anchorMin = okRT.anchorMax = new Vector2(0.5f, 0f);
        okRT.pivot = new Vector2(0.5f, 0f);
        okRT.anchoredPosition = new Vector2(0f, 34f);
        m_ConfirmButton.onClick.AddListener(Confirm);

        // ---- คำใบ้ ----
        var hint = MakeText(inner.transform, 22, TextAnchor.LowerCenter,
                            new Color(1f, 1f, 1f, 0.45f));
        hint.text = "พิมพ์ได้ทั้งไทยและอังกฤษ  •  กด Enter เพื่อเริ่มเล่น";
        var hintRT = hint.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.anchoredPosition = new Vector2(0f, 8f);
        hintRT.sizeDelta = new Vector2(0f, 28f);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private Button MakeButton(Transform parent, string label, Color color,
                              float width, float height, Sprite rounded)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText(go.transform, 32, TextAnchor.MiddleCenter, Color.white);
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
