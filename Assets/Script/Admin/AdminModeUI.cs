using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// โหมดเจ้าของเกม — เสกของ เสกสัตว์ เสกเงิน คุมเวลา
///
/// เปิดด้วยปุ่ม F9 (ตั้งใจเลือกปุ่มที่กดโดนยาก จะได้ไม่เผลอเปิดตอนเล่นจริง)
///
/// ตัวนี้แยกไฟล์ไว้ต่างหาก ลบ GameObject 'AdminMode' ทิ้งได้เลยตอนส่งงานจริง
/// ไม่มีระบบอื่นเรียกใช้ ลบแล้วเกมยังทำงานครบทุกอย่าง
/// </summary>
public class AdminModeUI : MonoBehaviour
{
    public static AdminModeUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsOpen = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("ปุ่มเปิด")]
    public Key openKey = Key.F9;

    [Header("สี")]
    public Color panelColor = new Color(0.10f, 0.09f, 0.12f, 0.98f);
    public Color borderColor = new Color(0.78f, 0.32f, 0.36f, 1f);
    public Color rowColor = new Color(0.17f, 0.16f, 0.19f, 0.92f);
    public Color tabActiveColor = new Color(0.62f, 0.28f, 0.32f, 1f);
    public Color tabIdleColor = new Color(0.22f, 0.21f, 0.23f, 0.95f);
    public Color actionColor = new Color(0.30f, 0.44f, 0.60f, 1f);

    private enum Page { Items, Animals, World }

    private GameObject m_Root;
    private RectTransform m_List;
    private Text m_StatusText;
    private Font m_Font;
    private Sprite m_Rounded;

    private readonly List<Image> m_TabImages = new List<Image>();
    private readonly List<GameObject> m_Rows = new List<GameObject>();
    private Page m_Page = Page.Items;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        m_Font = UIFont.Get();
        m_Rounded = UIShapes.RoundedRect(18);

        BuildUI();

        m_Root.SetActive(false);
        IsOpen = false;
    }

    // ================= เปิด / ปิด =================

    public void Open()
    {
        m_Root.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;

        m_StatusText.text = "";
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

        if (kb[openKey].wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
            return;
        }

        if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
    }

    // ================= หน้าต่างๆ =================

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
            case Page.Items: BuildItems(); break;
            case Page.Animals: BuildAnimals(); break;
            default: BuildWorld(); break;
        }
    }

    // ---- เสกของ ----
    private void BuildItems()
    {
        var inv = InventorySystem.Instance;
        if (inv == null || inv.allItems == null) return;

        foreach (var item in inv.allItems)
        {
            if (item == null) continue;

            var row = MakeRow(item.icon, item.displayName, KindName(item.kind));

            AddAction(row, "+1", actionColor, () => Give(item, 1));
            AddAction(row, "+10", actionColor, () => Give(item, 10));
            AddAction(row, "+99", tabIdleColor, () => Give(item, 99));
        }
    }

    private static string KindName(ItemKind kind) => kind switch
    {
        ItemKind.Tool => "อุปกรณ์",
        ItemKind.Seed => "เมล็ด",
        ItemKind.Animal => "สัตว์ / ไข่",
        ItemKind.Feed => "อาหารสัตว์",
        _ => "ผลผลิต",
    };

    private void Give(ItemData item, int count)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        if (inv.Add(item, count)) Report($"ได้ {item.displayName} x{count}");
        else Report("กระเป๋าเต็ม");
    }

    // ---- เสกสัตว์ ----
    private void BuildAnimals()
    {
        var manager = AnimalManager.Instance;
        if (manager == null)
        {
            MakeRow(null, "ไม่พบระบบสัตว์ในฉากนี้", "");
            return;
        }

        foreach (var animal in manager.animals)
        {
            if (animal == null || !animal.HasFrames) continue;

            var row = MakeRow(animal.Icon, animal.displayName,
                $"ไข่ ฿{animal.eggPrice} · ตัวโต ฿{animal.buyPrice}");

            AddAction(row, "วางตัวโต", actionColor, () => SpawnAnimal(animal, true));
            AddAction(row, "วางลูกสัตว์", tabIdleColor, () => SpawnAnimal(animal, false));
        }
    }

    private void SpawnAnimal(AnimalData animal, bool adult)
    {
        var manager = AnimalManager.Instance;
        if (manager == null) return;

        var spot = manager.PenCenter;
        var placed = manager.SpawnAt(animal, spot, adult);

        Report(placed != null
            ? $"วาง{animal.displayName}{(adult ? "ตัวโต" : "ตัวเล็ก")}ลงคอกแล้ว"
            : "วางไม่สำเร็จ");
    }

    // ---- เงิน เวลา และอื่นๆ ----
    private void BuildWorld()
    {
        var money = MakeRow(null, "เงิน", InventorySystem.Instance != null
            ? $"ตอนนี้มี ฿{InventorySystem.Instance.money:N0}" : "");

        AddAction(money, "+1,000", actionColor, () => GiveMoney(1000));
        AddAction(money, "+10,000", actionColor, () => GiveMoney(10000));
        AddAction(money, "ล้างเงิน", tabIdleColor, () => SetMoney(0));

        var time = MakeRow(null, "เวลาในเกม", DayNightCycle.Instance != null
            ? $"ตอนนี้ {DayNightCycle.Instance.ClockText}" : "ไม่มีระบบเวลา");

        AddAction(time, "เช้า", actionColor, () => SetTime(6f));
        AddAction(time, "เที่ยง", actionColor, () => SetTime(12f));
        AddAction(time, "กลางคืน", tabIdleColor, () => SetTime(21f));

        var day = MakeRow(null, "วัน", FarmManager.Instance != null
            ? $"ตอนนี้วันที่ {FarmManager.Instance.currentDay}" : "");

        AddAction(day, "ข้ามวัน", actionColor, SkipDay);

        var crops = MakeRow(null, "พืชในแปลง", "เร่งให้โตเต็มที่ทันที");
        AddAction(crops, "โตทันที", actionColor, GrowCrops);

        var animals = MakeRow(null, "สัตว์ในคอก", "เติมเลือดและความอิ่มให้เต็ม");
        AddAction(animals, "ฟื้นฟูทุกตัว", actionColor, HealAnimals);
    }

    private void GiveMoney(int amount)
    {
        InventorySystem.Instance?.AddMoney(amount);
        Report($"ได้เงิน ฿{amount:N0}");
        ShowPage(Page.World);
    }

    private void SetMoney(int amount)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        inv.SpendMoney(inv.money);
        if (amount > 0) inv.AddMoney(amount);

        Report("ตั้งเงินใหม่แล้ว");
        ShowPage(Page.World);
    }

    private void SetTime(float hour)
    {
        var cycle = DayNightCycle.Instance;
        if (cycle == null) return;

        cycle.StopWarp();
        cycle.hour = hour;

        Report($"ตั้งเวลาเป็น {cycle.ClockText}");
        ShowPage(Page.World);
    }

    private void SkipDay()
    {
        FarmManager.Instance?.AdvanceDay();
        DayNightCycle.Instance?.SetMorning();

        Report("ข้ามไปวันถัดไปแล้ว");
        ShowPage(Page.World);
    }

    private void GrowCrops()
    {
        var farm = FarmManager.Instance;
        if (farm == null) return;

        int grown = farm.ForceGrowAll();
        Report($"เร่งพืชโตเต็มที่ {grown} ต้น");
    }

    private void HealAnimals()
    {
        var animals = Object.FindObjectsByType<AnimalInstance>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var animal in animals)
        {
            animal.health = 100f;
            animal.hunger = 100f;
            animal.friendship = Mathf.Min(100f, animal.friendship + 20f);
        }

        Report($"ฟื้นฟูสัตว์ {animals.Length} ตัว");
    }

    private void Report(string message)
    {
        m_StatusText.text = message;
        Debug.Log("[แอดมิน] " + message);
    }

    // ================= สร้างแถว =================

    private Transform MakeRow(Sprite icon, string title, string subtitle)
    {
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(m_List, false);

        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 76f;
        layout.minHeight = 76f;

        var bg = row.AddComponent<Image>();
        bg.sprite = m_Rounded;
        bg.type = Image.Type.Sliced;
        bg.color = rowColor;

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
            iconRT.anchoredPosition = new Vector2(14f, 0f);
            iconRT.sizeDelta = new Vector2(54f, 54f);
        }

        var name = MakeText(row.transform, 26, TextAnchor.LowerLeft, Color.white);
        name.text = title;

        var nameRT = name.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 0.5f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(icon != null ? 80f : 18f, 0f);
        nameRT.offsetMax = new Vector2(-470f, -8f);

        var note = MakeText(row.transform, 19, TextAnchor.UpperLeft,
                            new Color(1f, 1f, 1f, 0.55f));
        note.text = subtitle;

        var noteRT = note.rectTransform;
        noteRT.anchorMin = new Vector2(0f, 0f);
        noteRT.anchorMax = new Vector2(1f, 0.5f);
        noteRT.offsetMin = new Vector2(icon != null ? 80f : 18f, 8f);
        noteRT.offsetMax = new Vector2(-470f, 0f);

        // แถวเก็บปุ่มไว้ด้านขวา
        var actions = new GameObject("Actions", typeof(RectTransform));
        actions.transform.SetParent(row.transform, false);

        var actionsRT = (RectTransform)actions.transform;
        actionsRT.anchorMin = new Vector2(1f, 0f);
        actionsRT.anchorMax = new Vector2(1f, 1f);
        actionsRT.pivot = new Vector2(1f, 0.5f);
        actionsRT.anchoredPosition = new Vector2(-14f, 0f);
        actionsRT.sizeDelta = new Vector2(450f, -16f);

        var actionsLayout = actions.AddComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 8f;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;

        m_Rows.Add(row);
        return actions.transform;
    }

    private void AddAction(Transform parent, string label, Color color,
                           UnityEngine.Events.UnityAction action)
    {
        var button = MakeButton(parent, label, color, 0f, 50f);

        var element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 40f + label.Length * 13f;
        element.minWidth = element.preferredWidth;

        button.onClick.AddListener(action);
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("AdminCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;   // เหนือทุกอย่าง ยกเว้นคัตซีน

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        m_Root = new GameObject("AdminRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch((RectTransform)m_Root.transform);

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
        frameRT.sizeDelta = new Vector2(1240f, 740f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = m_Rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 6f);

        var title = MakeText(inner.transform, 38, TextAnchor.UpperCenter, borderColor);
        title.text = "โหมดเจ้าของเกม";

        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.offsetMin = new Vector2(20f, -64f);
        titleRT.offsetMax = new Vector2(-20f, -16f);

        m_TabImages.Clear();
        m_TabImages.Add(MakeTab(inner.transform, "เสกของ", -230f, Page.Items));
        m_TabImages.Add(MakeTab(inner.transform, "เสกสัตว์", 0f, Page.Animals));
        m_TabImages.Add(MakeTab(inner.transform, "เงิน · เวลา", 230f, Page.World));

        // ---- กรอบเลื่อน ----
        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        scrollGO.transform.SetParent(inner.transform, false);

        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(24f, 92f);
        scrollRT.offsetMax = new Vector2(-24f, -150f);

        scrollGO.AddComponent<RectMask2D>();

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
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
        listLayout.spacing = 8f;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;

        var fitter = listGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = m_List;

        // ---- ข้อความแจ้งผล ----
        m_StatusText = MakeText(inner.transform, 22, TextAnchor.LowerLeft,
                                new Color(0.65f, 0.9f, 0.6f, 1f));

        var statusRT = m_StatusText.rectTransform;
        statusRT.anchorMin = new Vector2(0f, 0f);
        statusRT.anchorMax = new Vector2(1f, 0f);
        statusRT.pivot = new Vector2(0f, 0f);
        statusRT.anchoredPosition = new Vector2(26f, 24f);
        statusRT.sizeDelta = new Vector2(-320f, 30f);

        var close = MakeButton(inner.transform, "ปิด  (F9 หรือ Esc)", tabIdleColor, 280f, 54f);
        var closeRT = (RectTransform)close.transform;
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 0f);
        closeRT.pivot = new Vector2(1f, 0f);
        closeRT.anchoredPosition = new Vector2(-24f, 20f);
        close.onClick.AddListener(Close);
    }

    private Image MakeTab(Transform parent, string label, float x, Page page)
    {
        var button = MakeButton(parent, label, tabIdleColor, 216f, 54f);

        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -74f);

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

        var text = MakeText(go.transform, 24, TextAnchor.MiddleCenter, Color.white);
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
