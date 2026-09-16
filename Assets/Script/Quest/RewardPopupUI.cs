using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าต่าง "เควสสำเร็จ!" — โชว์รายละเอียดรางวัลที่ได้
///
/// เด้งขึ้นอัตโนมัติตอนกดปุ่ม "รับรางวัล" กับ NPC
/// สร้าง UI ด้วยโค้ดทั้งหมด วางไว้บน GameObject ชื่อ "QuestManager" ได้เลย
/// </summary>
public class RewardPopupUI : MonoBehaviour
{
    /// <summary>ตอนนี้หน้าต่างรางวัลเปิดอยู่ไหม</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    /// Unity 6 ปิด Domain Reload เป็นค่าเริ่มต้น ตัวแปร static เลยค้างข้ามรอบ
    /// ถ้าครั้งก่อนกด Stop ตอนหน้าต่างรางวัลเปิดค้างไว้ เกมจะล็อคการกดปุ่มทั้งหมด
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => IsOpen = false;

    [Header("สี")]
    public Color panelColor = new Color(0.09f, 0.08f, 0.07f, 0.97f);
    public Color borderColor = new Color(1f, 0.82f, 0.30f, 1f);
    public Color rowColor = new Color(0.16f, 0.15f, 0.14f, 0.9f);
    public Color buttonColor = new Color(0.30f, 0.55f, 0.25f, 1f);

    [Header("ข้อความ")]
    public string headerText = "เควสสำเร็จ!";
    public string moneyPrefix = "฿";

    private GameObject m_Panel;
    private Text m_QuestTitle;
    private RectTransform m_RowContainer;
    private readonly List<GameObject> m_Rows = new List<GameObject>();
    private Font m_Font;

    private void Start()
    {
        m_Font = UIFont.Get();
        BuildUI();
        m_Panel.SetActive(false);
        IsOpen = false;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnRewardGranted += Show;
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnRewardGranted -= Show;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // กด Space / E / Esc ปิดได้เหมือนกัน
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame ||
                           kb.eKey.wasPressedThisFrame ||
                           kb.escapeKey.wasPressedThisFrame))
            Close();
    }

    // ================= เปิด / ปิด =================

    public void Show(QuestData quest)
    {
        if (quest == null) return;

        m_QuestTitle.text = quest.title;

        ClearRows();

        if (quest.rewardMoney > 0)
            AddRow(null, "เงิน", $"{moneyPrefix} {quest.rewardMoney:N0}");

        if (quest.rewardItem != null && quest.rewardItemCount > 0)
            AddRow(quest.rewardItem.icon, quest.rewardItem.displayName, $"x{quest.rewardItemCount}");

        if (m_Rows.Count == 0)
            AddRow(null, "ไม่มีรางวัล", "");

        m_Panel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        m_Panel.SetActive(false);
        IsOpen = false;
    }

    // ================= แถวรางวัล =================

    private void ClearRows()
    {
        foreach (var row in m_Rows)
            Destroy(row);
        m_Rows.Clear();
    }

    private void AddRow(Sprite icon, string label, string amount)
    {
        var row = new GameObject("RewardRow", typeof(RectTransform));
        row.transform.SetParent(m_RowContainer, false);

        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 76;
        layout.minHeight = 76;

        var bg = row.AddComponent<Image>();
        bg.color = rowColor;

        // ไอคอน (ถ้าไม่มีรูป เช่นเงิน จะเว้นว่าง)
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(row.transform, false);

            var iconImage = iconGO.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(14f, 0f);
            iconRT.sizeDelta = new Vector2(56f, 56f);
        }

        var nameText = MakeText(row.transform, 32, TextAnchor.MiddleLeft, Color.white);
        nameText.text = label;
        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(icon != null ? 84f : 20f, 0f);
        nameRT.offsetMax = new Vector2(-140f, 0f);

        var amountText = MakeText(row.transform, 36, TextAnchor.MiddleRight, borderColor);
        amountText.text = amount;
        var amountRT = amountText.rectTransform;
        amountRT.anchorMin = Vector2.zero;
        amountRT.anchorMax = Vector2.one;
        amountRT.offsetMin = new Vector2(0f, 0f);
        amountRT.offsetMax = new Vector2(-20f, 0f);

        m_Rows.Add(row);
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("RewardCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;   // อยู่บนสุด เหนือกล่องคุย

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;   // วาดฟอนต์ละเอียด 3 เท่า -> ตัวอักษรคมขึ้น

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- ฉากหลังมืด ----
        m_Panel = new GameObject("RewardRoot", typeof(RectTransform));
        m_Panel.transform.SetParent(canvasGO.transform, false);

        var dim = m_Panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        var dimRT = (RectTransform)m_Panel.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // ---- กรอบทอง ----
        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(m_Panel.transform, false);

        var frameImage = frame.AddComponent<Image>();
        frameImage.color = borderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = Vector2.zero;
        frameRT.sizeDelta = new Vector2(620f, 480f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.color = panelColor;

        var innerRT = (RectTransform)inner.transform;
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(5f, 5f);
        innerRT.offsetMax = new Vector2(-5f, -5f);

        // ---- หัวข้อ ----
        var header = MakeText(inner.transform, 48, TextAnchor.UpperCenter, borderColor);
        header.text = headerText;
        var headerRT = header.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -24f);
        headerRT.sizeDelta = new Vector2(0f, 52f);

        m_QuestTitle = MakeText(inner.transform, 30, TextAnchor.UpperCenter,
                                new Color(1f, 1f, 1f, 0.75f));
        var qtRT = m_QuestTitle.rectTransform;
        qtRT.anchorMin = new Vector2(0f, 1f);
        qtRT.anchorMax = new Vector2(1f, 1f);
        qtRT.pivot = new Vector2(0.5f, 1f);
        qtRT.anchoredPosition = new Vector2(0f, -80f);
        qtRT.sizeDelta = new Vector2(0f, 36f);

        // ---- รายการรางวัล ----
        var container = new GameObject("Rewards", typeof(RectTransform));
        container.transform.SetParent(inner.transform, false);

        m_RowContainer = (RectTransform)container.transform;
        m_RowContainer.anchorMin = new Vector2(0f, 1f);
        m_RowContainer.anchorMax = new Vector2(1f, 1f);
        m_RowContainer.pivot = new Vector2(0.5f, 1f);
        m_RowContainer.anchoredPosition = new Vector2(0f, -136f);
        m_RowContainer.offsetMin = new Vector2(30f, m_RowContainer.offsetMin.y);
        m_RowContainer.offsetMax = new Vector2(-30f, m_RowContainer.offsetMax.y);
        m_RowContainer.sizeDelta = new Vector2(-60f, 240f);

        var rowLayout = container.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 12;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childAlignment = TextAnchor.UpperCenter;

        // ---- ปุ่มปิด ----
        var buttonGO = new GameObject("OkButton", typeof(RectTransform));
        buttonGO.transform.SetParent(inner.transform, false);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor;

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(Close);

        var buttonRT = (RectTransform)buttonGO.transform;
        buttonRT.anchorMin = buttonRT.anchorMax = new Vector2(0.5f, 0f);
        buttonRT.pivot = new Vector2(0.5f, 0f);
        buttonRT.anchoredPosition = new Vector2(0f, 28f);
        buttonRT.sizeDelta = new Vector2(240f, 62f);

        var buttonLabel = MakeText(buttonGO.transform, 34, TextAnchor.MiddleCenter, Color.white);
        buttonLabel.text = "รับรางวัล";
        var labelRT = buttonLabel.rectTransform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
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
