using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// สมุดบันทึก — รวมของทุกอย่างในเกมไว้ที่เดียว
///
/// แบ่งเป็น 3 หน้า: พืช / สัตว์ / ของใช้
/// แต่ละรายการมีรูป ชื่อ ราคา และรายละเอียด
///
/// เปิดด้วยปุ่ม J
/// </summary>
public class EncyclopediaUI : MonoBehaviour
{
    public static EncyclopediaUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsOpen = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("สี")]
    public Color panelColor = new Color(0.12f, 0.11f, 0.09f, 0.98f);
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color rowColor = new Color(0.18f, 0.17f, 0.15f, 0.9f);
    public Color tabActiveColor = new Color(0.34f, 0.52f, 0.28f, 1f);
    public Color tabIdleColor = new Color(0.22f, 0.21f, 0.19f, 0.95f);

    private enum Page { Crops, Animals, Tools }

    private GameObject m_Root;
    private RectTransform m_List;
    private Text m_HeaderText;
    private Text m_EmptyText;
    private Font m_Font;
    private Sprite m_Rounded;

    private readonly List<Image> m_TabImages = new List<Image>();
    private readonly List<GameObject> m_Rows = new List<GameObject>();
    private Page m_Page = Page.Crops;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        m_Font = UIFont.Get();
        m_Rounded = UIShapes.RoundedRect(20);

        BuildUI();

        m_Root.SetActive(false);
        IsOpen = false;
    }

    // ================= เปิด / ปิด =================

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        m_Root.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;

        ShowPage(m_Page);
    }

    public void Close()
    {
        m_Root.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (IsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (!kb.jKey.wasPressedThisFrame) return;

        // เปิดได้เฉพาะตอนไม่มีหน้าต่างอื่นค้างอยู่
        bool busy = DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
                    || RewardPopupUI.IsOpen || CutsceneUI.IsPlaying
                    || TitleScreenUI.IsOpen || NameEntryUI.IsOpen
                    || DayTransitionUI.IsPlaying || SettingsMenuUI.IsOpen
                    || AnimalInfoUI.IsOpen || AdminModeUI.IsOpen;

        if (IsOpen || !busy) Toggle();
    }

    // ================= เนื้อหาแต่ละหน้า =================

    private void ShowPage(Page page)
    {
        m_Page = page;

        for (int i = 0; i < m_TabImages.Count; i++)
        {
            if (m_TabImages[i] == null) continue;
            m_TabImages[i].color = (int)page == i ? tabActiveColor : tabIdleColor;
        }

        foreach (var row in m_Rows) Destroy(row);
        m_Rows.Clear();

        switch (page)
        {
            case Page.Crops: BuildCrops(); break;
            case Page.Animals: BuildAnimals(); break;
            default: BuildTools(); break;
        }

        m_EmptyText.gameObject.SetActive(m_Rows.Count == 0);
    }

    private void BuildCrops()
    {
        m_HeaderText.text = "พืชที่ปลูกได้ในเกม";
        m_EmptyText.text = "ยังไม่มีข้อมูลพืช";

        var farm = FarmManager.Instance;
        if (farm == null) return;

        foreach (var crop in farm.allCrops)
        {
            if (crop == null) continue;

            var icon = crop.produceItem != null ? crop.produceItem.icon : null;
            if (icon == null && crop.seedItem != null) icon = crop.seedItem.icon;

            int seedPrice = crop.seedItem != null ? crop.seedItem.buyPrice : 0;
            int sellPrice = crop.produceItem != null ? crop.produceItem.sellPrice : 0;
            int profit = sellPrice - seedPrice;

            AddRow(icon, crop.displayName,
                $"เมล็ด ฿{seedPrice}   ขายได้ ฿{sellPrice}   กำไร ฿{profit}",
                $"โตเต็มที่ใน {crop.daysToGrow} วัน — ต้องรดน้ำทุกวัน");
        }
    }

    private void BuildAnimals()
    {
        m_HeaderText.text = "สัตว์ที่เลี้ยงได้ในเกม";
        m_EmptyText.text = "ยังไม่มีข้อมูลสัตว์";

        var manager = AnimalManager.Instance;
        if (manager == null) return;

        foreach (var animal in manager.animals)
        {
            if (animal == null || !animal.HasFrames) continue;

            string produce = animal.produceItem != null
                ? animal.produceItem.displayName
                : "ผลผลิต";

            AddRow(animal.Icon, animal.displayName,
                $"ไข่ ฿{animal.eggPrice}   ตัวโต ฿{animal.buyPrice}   ให้{produce}",
                $"ลูกสัตว์โตใน {animal.daysToAdult} วัน — ให้อาหารทุกวันถึงจะออกผลผลิต");
        }
    }

    private void BuildTools()
    {
        m_HeaderText.text = "ของใช้และผลผลิต";
        m_EmptyText.text = "ยังไม่มีข้อมูลของใช้";

        var inv = InventorySystem.Instance;
        if (inv == null || inv.allItems == null) return;

        foreach (var item in inv.allItems)
        {
            if (item == null) continue;
            if (item.kind == ItemKind.Seed || item.kind == ItemKind.Animal) continue;

            string kind = item.kind switch
            {
                ItemKind.Tool => "อุปกรณ์",
                ItemKind.Feed => "อาหารสัตว์",
                _ => "ผลผลิต",
            };

            string price = item.sellPrice > 0
                ? $"ขายได้ ฿{item.sellPrice}"
                : item.buyPrice > 0 ? $"ราคาซื้อ ฿{item.buyPrice}" : "ขายไม่ได้";

            AddRow(item.icon, item.displayName, $"{kind}   {price}", "");
        }
    }

    // ================= แถวรายการ =================

    private void AddRow(Sprite icon, string title, string line1, string line2)
    {
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(m_List, false);

        float height = string.IsNullOrEmpty(line2) ? 84f : 108f;

        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;

        var bg = row.AddComponent<Image>();
        bg.sprite = m_Rounded;
        bg.type = Image.Type.Sliced;
        bg.color = rowColor;

        // ---- รูป ----
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(row.transform, false);

            var image = iconGO.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(18f, 0f);
            iconRT.sizeDelta = new Vector2(64f, 64f);
        }

        // ---- ชื่อ ----
        var name = MakeText(row.transform, 28, TextAnchor.UpperLeft, Color.white);
        name.text = title;

        var nameRT = name.rectTransform;
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(96f, height - 46f);
        nameRT.offsetMax = new Vector2(-20f, -12f);

        // ---- บรรทัดข้อมูล ----
        var info = MakeText(row.transform, 22, TextAnchor.UpperLeft, borderColor);
        info.text = line1;

        var infoRT = info.rectTransform;
        infoRT.anchorMin = Vector2.zero;
        infoRT.anchorMax = Vector2.one;
        infoRT.offsetMin = new Vector2(96f, string.IsNullOrEmpty(line2) ? 12f : 34f);
        infoRT.offsetMax = new Vector2(-20f, -48f);

        if (!string.IsNullOrEmpty(line2))
        {
            var note = MakeText(row.transform, 20, TextAnchor.UpperLeft,
                                new Color(1f, 1f, 1f, 0.55f));
            note.text = line2;

            var noteRT = note.rectTransform;
            noteRT.anchorMin = Vector2.zero;
            noteRT.anchorMax = Vector2.one;
            noteRT.offsetMin = new Vector2(96f, 10f);
            noteRT.offsetMax = new Vector2(-20f, -76f);
        }

        m_Rows.Add(row);
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("EncyclopediaCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 42;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildOpenButton(canvasGO.transform);

        m_Root = new GameObject("BookRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        Stretch((RectTransform)m_Root.transform);

        // ---- กรอบ ----
        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(m_Root.transform, false);

        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = m_Rounded;
        frameImage.type = Image.Type.Sliced;
        frameImage.color = borderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = Vector2.zero;
        frameRT.sizeDelta = new Vector2(1180f, 720f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = m_Rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 6f);

        // ---- หัวข้อ ----
        var title = MakeText(inner.transform, 40, TextAnchor.UpperCenter, borderColor);
        title.text = "สมุดบันทึก";

        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.offsetMin = new Vector2(20f, -68f);
        titleRT.offsetMax = new Vector2(-20f, -18f);

        // ---- แท็บ ----
        m_TabImages.Clear();
        m_TabImages.Add(MakeTab(inner.transform, "พืช", -230f, Page.Crops));
        m_TabImages.Add(MakeTab(inner.transform, "สัตว์", 0f, Page.Animals));
        m_TabImages.Add(MakeTab(inner.transform, "ของใช้", 230f, Page.Tools));

        // ---- หัวข้อย่อย ----
        m_HeaderText = MakeText(inner.transform, 24, TextAnchor.UpperLeft,
                                new Color(1f, 1f, 1f, 0.7f));

        var headerRT = m_HeaderText.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.offsetMin = new Vector2(34f, -190f);
        headerRT.offsetMax = new Vector2(-34f, -156f);

        // ---- กรอบเลื่อน ----
        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        scrollGO.transform.SetParent(inner.transform, false);

        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(28f, 92f);
        scrollRT.offsetMax = new Vector2(-28f, -196f);

        scrollGO.AddComponent<RectMask2D>();

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.viewport = scrollRT;

        var listGO = new GameObject("List", typeof(RectTransform));
        listGO.transform.SetParent(scrollGO.transform, false);

        m_List = (RectTransform)listGO.transform;
        m_List.anchorMin = new Vector2(0f, 1f);
        m_List.anchorMax = new Vector2(1f, 1f);
        m_List.pivot = new Vector2(0.5f, 1f);
        m_List.anchoredPosition = Vector2.zero;
        m_List.sizeDelta = Vector2.zero;

        var listLayout = listGO.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 10f;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;

        var fitter = listGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = m_List;

        // ---- ข้อความตอนไม่มีข้อมูล ----
        m_EmptyText = MakeText(inner.transform, 26, TextAnchor.MiddleCenter,
                               new Color(1f, 1f, 1f, 0.5f));
        Stretch(m_EmptyText.rectTransform, 60f);

        // ---- ปุ่มปิด ----
        var close = MakeButton(inner.transform, "ปิด  (J หรือ Esc)", tabIdleColor, 280f, 58f);
        var closeRT = (RectTransform)close.transform;
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(0.5f, 0f);
        closeRT.pivot = new Vector2(0.5f, 0f);
        closeRT.anchoredPosition = new Vector2(0f, 20f);
        close.onClick.AddListener(Close);
    }

    /// <summary>
    /// ปุ่มสมุดบันทึกที่เห็นตลอดเวลาบนหน้าจอ
    /// วางไว้ซ้ายของปุ่มฟันเฟือง มีชื่อกำกับให้รู้ว่าคืออะไร
    /// </summary>
    private void BuildOpenButton(Transform parent)
    {
        var go = new GameObject("BookButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = UIShapes.Book();
        image.preserveAspect = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Open);

        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.9f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.74f, 1f);
        button.colors = colors;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-118f, 30f);   // ซ้ายของปุ่มฟันเฟือง
        rt.sizeDelta = new Vector2(72f, 72f);

        // ---- ชื่อกำกับใต้ปุ่ม ----
        var label = MakeText(parent, 20, TextAnchor.UpperCenter,
                             new Color(1f, 0.95f, 0.8f, 0.85f));
        label.text = "สมุดบันทึก (J)";

        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var labelRT = label.rectTransform;
        labelRT.anchorMin = labelRT.anchorMax = new Vector2(1f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(-154f, 28f);
        labelRT.sizeDelta = new Vector2(200f, 26f);
    }

    private Image MakeTab(Transform parent, string label, float x, Page page)
    {
        var button = MakeButton(parent, label, tabIdleColor, 210f, 58f);

        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -78f);

        button.onClick.AddListener(() => ShowPage(page));
        return button.targetGraphic as Image;
    }

    // ================= ตัวช่วย =================

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private Button MakeButton(Transform parent, string label, Color color,
                              float width, float height)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.sprite = m_Rounded;
        img.type = Image.Type.Sliced;
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
