using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// แผ่นสอนเล่น — บอกว่าปุ่มไหนใช้ทำอะไร
///
/// ขึ้นเองตอนเริ่มเล่นสักพักแล้วหายไป
/// อยากดูอีกเมื่อไหร่ก็กด H
/// </summary>
public class ControlsHintUI : MonoBehaviour
{
    public static ControlsHintUI Instance { get; private set; }

    [Header("การแสดงผล")]
    [Tooltip("ตอนเริ่มเล่น โชว์ให้ดูกี่วินาทีแล้วค่อยซ่อน (0 = ไม่ต้องโชว์เอง)")]
    public float autoShowSeconds = 25f;

    [Tooltip("เวลาที่ใช้จางเข้า/ออก")]
    public float fadeTime = 0.35f;

    [Header("สี")]
    public Color panelColor = new Color(0.08f, 0.10f, 0.09f, 0.90f);
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color keyColor = new Color(0.24f, 0.30f, 0.24f, 1f);
    public Color keyTextColor = new Color(1f, 0.95f, 0.78f, 1f);
    public Color descColor = new Color(1f, 1f, 1f, 0.88f);

    /// <summary>ปุ่มกับความหมาย — แก้ตรงนี้ได้ถ้าเปลี่ยนปุ่มในเกม</summary>
    private static readonly (string key, string desc)[] Rows =
    {
        ("W A S D", "เดิน"),
        ("คลิกซ้าย", "ไถ · ปลูก · รดน้ำ · เก็บเกี่ยว"),
        ("1 - 8", "เลือกช่องอุปกรณ์"),
        ("I", "เปิดกระเป๋า"),
        ("J", "สมุดบันทึก — ดูของทั้งหมดในเกม"),
        ("E", "คุยกับ NPC / ให้อาหารสัตว์"),
        ("คลิกที่สัตว์", "ให้เดินตาม / หยุดตาม"),
        ("F", "เปิดร้าน ซื้อ-ขาย"),
        ("คลิกที่เตียง", "นอน ข้ามไปวันถัดไป"),
        ("N", "นอน (ปุ่มลัด)"),
        ("T", "ข้ามเวลา กลางวัน / กลางคืน"),
        ("Tab", "ปลด / ล็อคเมาส์"),
        ("Esc", "เมนูตั้งค่า"),
        ("H", "ซ่อน / แสดงแผ่นนี้"),
    };

    private CanvasGroup m_Group;
    private Text m_Reminder;
    private Font m_Font;

    private bool m_Shown = true;
    private float m_AutoHideLeft;

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

        m_AutoHideLeft = autoShowSeconds;
        m_Shown = autoShowSeconds > 0f;
        m_Group.alpha = m_Shown ? 1f : 0f;
    }

    // ================= อัปเดตทุกเฟรม =================

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame && !AnyWindowOpen())
        {
            m_Shown = !m_Shown;
            m_AutoHideLeft = 0f;      // กดเองแล้ว เลิกซ่อนอัตโนมัติ
        }

        // นับถอยหลังซ่อนเอง
        if (m_AutoHideLeft > 0f)
        {
            m_AutoHideLeft -= Time.unscaledDeltaTime;
            if (m_AutoHideLeft <= 0f) m_Shown = false;
        }

        // มีหน้าต่างอะไรเปิดอยู่ = ซ่อนไว้ก่อน ไม่ให้รก
        bool visible = m_Shown && !AnyWindowOpen();

        float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
        m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, visible ? 1f : 0f, step);

        bool showReminder = !m_Shown && !AnyWindowOpen();
        if (m_Reminder.enabled != showReminder) m_Reminder.enabled = showReminder;
    }

    private static bool AnyWindowOpen()
    {
        return CutsceneUI.IsPlaying || TitleScreenUI.IsOpen || NameEntryUI.IsOpen
            || SettingsMenuUI.IsOpen || DayTransitionUI.IsPlaying
            || ShopUI.IsOpen || InventoryUI.IsBackpackOpen || DialogueUI.IsOpen
            || EncyclopediaUI.IsOpen || AdminModeUI.IsOpen;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var rounded = UIShapes.RoundedRect(18);
        var keyShape = UIShapes.RoundedRect(10);

        var canvasGO = new GameObject("ControlsHintCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 18;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        const float rowHeight = 34f;
        const float padding = 18f;
        float panelHeight = Rows.Length * rowHeight + padding * 2f + 44f;

        // ---- กรอบ ----
        var frame = new GameObject("Panel", typeof(RectTransform));
        frame.transform.SetParent(canvasGO.transform, false);

        m_Group = frame.AddComponent<CanvasGroup>();
        m_Group.interactable = false;
        m_Group.blocksRaycasts = false;

        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = rounded;
        frameImage.type = Image.Type.Sliced;
        frameImage.color = borderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0f, 0f);
        frameRT.pivot = new Vector2(0f, 0f);
        frameRT.anchoredPosition = new Vector2(26f, 26f);
        frameRT.sizeDelta = new Vector2(430f, panelHeight);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 4f);

        // ---- หัวข้อ ----
        var header = MakeText(inner.transform, 26, TextAnchor.UpperLeft, borderColor);
        header.text = "ปุ่มควบคุม";
        var headerRT = header.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.offsetMin = new Vector2(padding, -44f);
        headerRT.offsetMax = new Vector2(-padding, -14f);

        // ---- แถวปุ่ม ----
        var list = new GameObject("Rows", typeof(RectTransform));
        list.transform.SetParent(inner.transform, false);

        var listRT = (RectTransform)list.transform;
        listRT.anchorMin = new Vector2(0f, 0f);
        listRT.anchorMax = new Vector2(1f, 1f);
        listRT.offsetMin = new Vector2(padding, padding);
        listRT.offsetMax = new Vector2(-padding, -50f);

        var layout = list.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        foreach (var row in Rows)
            MakeRow(list.transform, row.key, row.desc, keyShape, rowHeight);

        // ---- ข้อความเตือนตอนซ่อนอยู่ ----
        m_Reminder = MakeText(canvasGO.transform, 22, TextAnchor.LowerLeft,
                              new Color(1f, 1f, 1f, 0.5f));
        m_Reminder.text = "กด H เพื่อดูปุ่มควบคุม";

        var remRT = m_Reminder.rectTransform;
        remRT.anchorMin = remRT.anchorMax = new Vector2(0f, 0f);
        remRT.pivot = new Vector2(0f, 0f);
        remRT.anchoredPosition = new Vector2(28f, 26f);
        remRT.sizeDelta = new Vector2(420f, 30f);
        m_Reminder.enabled = false;
    }

    /// <summary>หนึ่งแถว: ป้ายปุ่มด้านซ้าย คำอธิบายด้านขวา</summary>
    private void MakeRow(Transform parent, string key, string desc, Sprite keyShape, float height)
    {
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        // ป้ายปุ่ม
        var badge = new GameObject("Key", typeof(RectTransform));
        badge.transform.SetParent(row.transform, false);

        var badgeImage = badge.AddComponent<Image>();
        badgeImage.sprite = keyShape;
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = keyColor;
        badgeImage.raycastTarget = false;

        var badgeRT = (RectTransform)badge.transform;
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0f, 0.5f);
        badgeRT.pivot = new Vector2(0f, 0.5f);
        badgeRT.anchoredPosition = new Vector2(0f, 0f);
        badgeRT.sizeDelta = new Vector2(122f, height - 6f);

        var keyText = MakeText(badge.transform, 20, TextAnchor.MiddleCenter, keyTextColor);
        keyText.text = key;
        Stretch(keyText.rectTransform, 3f);

        // คำอธิบาย
        var descText = MakeText(row.transform, 21, TextAnchor.MiddleLeft, descColor);
        descText.text = desc;

        var descRT = descText.rectTransform;
        descRT.anchorMin = Vector2.zero;
        descRT.anchorMax = Vector2.one;
        descRT.offsetMin = new Vector2(134f, 0f);
        descRT.offsetMax = new Vector2(0f, 0f);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
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
