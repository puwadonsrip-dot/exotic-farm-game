using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าจอ Hotbar + กระเป๋า สไตล์คล้าย Minecraft
///
/// สร้าง UI ทั้งหมดด้วยโค้ดตอนเกมเริ่ม ไม่ต้องมี Prefab ไม่ต้องจัดใน Scene
/// วางไว้บน GameObject เดียวกับ InventorySystem
///
/// ปุ่ม: 1-6 = เลือกช่อง Hotbar, I = เปิด/ปิดกระเป๋า, Esc = ปิด
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("ขนาด (ปรับได้)")]
    [Tooltip("ความกว้าง/สูงของช่อง 1 ช่อง (พิกเซล) — Minecraft ประมาณ 96-110")]
    public int slotSize = 96;

    [Tooltip("ระยะห่างระหว่างช่อง — Minecraft ช่องเกือบติดกัน ใช้ 4")]
    public int slotSpacing = 4;

    [Tooltip("ความหนาของขอบช่อง")]
    public int borderWidth = 5;

    [Tooltip("ความหนาของขอบช่องที่เลือกอยู่ (หนากว่าปกติ)")]
    public int selectedBorderWidth = 8;

    [Tooltip("จำนวนช่องต่อแถวในกระเป๋า (ให้เท่ากับจำนวนช่อง Hotbar จะดูเป็นระเบียบสุด)")]
    public int columns = 8;

    [Header("สี")]
    [Tooltip("สีขอบช่องปกติ")]
    public Color slotBorderColor = new Color(0.42f, 0.42f, 0.45f, 1f);

    [Tooltip("สีขอบช่องที่เลือกอยู่")]
    public Color selectedBorderColor = Color.white;

    [Tooltip("สีพื้นในช่อง")]
    public Color slotFillColor = new Color(0.20f, 0.20f, 0.22f, 0.92f);

    [Tooltip("สีแถบพื้นหลังของ Hotbar")]
    public Color barColor = new Color(0.07f, 0.07f, 0.08f, 0.80f);

    [Tooltip("สีพื้นหลังของหน้าต่างกระเป๋า")]
    public Color panelColor = new Color(0.10f, 0.09f, 0.09f, 0.96f);

    [Header("เงิน")]
    [Tooltip("ถ้าสัญลักษณ์ ฿ ขึ้นเป็นกล่องสี่เหลี่ยม ให้เปลี่ยนเป็น B")]
    public string moneyPrefix = "฿";

    /// <summary>ตอนนี้เปิดกระเป๋าอยู่ไหม (ระบบอื่นเช็คได้ เช่นล็อคการใช้อุปกรณ์)</summary>
    public static bool IsBackpackOpen { get; private set; }

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    /// Unity 6 ปิด Domain Reload เป็นค่าเริ่มต้น ตัวแปร static เลยค้างข้ามรอบ
    /// ถ้าครั้งก่อนกด Stop ตอนเปิดกระเป๋าค้างไว้ เกมจะคิดว่ากระเป๋าเปิดอยู่ตลอดแล้วกดอะไรไม่ได้เลย
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => IsBackpackOpen = false;

    /// <summary>ตัวเดียวในฉาก — ให้ตัวลากของเรียกใช้ตอนจะทิ้งของ</summary>
    public static InventoryUI Instance { get; private set; }

    private class SlotView
    {
        public Image border;
        public RectTransform innerRT;
        public Image icon;
        public Text count;

        /// <summary>ตัวจัดการลากวางของช่องนี้ — ต้องอัปเดต slotIndex ทุกครั้งที่วาดใหม่</summary>
        public ItemDragHandler drag;
    }

    private readonly List<SlotView> m_HotbarViews = new List<SlotView>();
    private readonly List<SlotView> m_BagViews = new List<SlotView>();

    private GameObject m_BackpackPanel;
    private Text m_MoneyText;
    private Font m_Font;

    /// <summary>แท็บกรองของในกระเป๋า — เรียงตามลำดับปุ่มบนจอ</summary>
    private enum BagTab { All, Tools, Seeds, Produce }

    [Header("สีแท็บในกระเป๋า")]
    public Color tabActiveColor = new Color(0.34f, 0.52f, 0.28f, 1f);
    public Color tabIdleColor = new Color(0.20f, 0.20f, 0.19f, 0.95f);

    private BagTab m_BagTab = BagTab.All;
    private readonly List<Image> m_BagTabImages = new List<Image>();
    private readonly List<int> m_FilteredSlots = new List<int>();

    // ---- หน้าต่างถาม (ใช้ทั้งตอนทิ้งของและตอนปล่อยสัตว์ลงคอก) ----
    private GameObject m_TrashConfirm;
    private Text m_TrashText;
    private int m_PendingTrashSlot = -1;

    private Button m_BtnPlace, m_BtnTrash, m_BtnCancel;
    private Text m_BtnPlaceLabel, m_BtnTrashLabel, m_BtnCancelLabel;

    private void Start()
    {
        Instance = this;
        m_Font = UIFont.Get();   // ฟอนต์ที่แสดงภาษาไทยและสัญลักษณ์ ฿ ได้

        BuildUI();

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.OnChanged += Redraw;
            Redraw();
        }
        else
        {
            Debug.LogError("[InventoryUI] ไม่พบ InventorySystem ใน Scene");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (InventorySystem.Instance != null)
            InventorySystem.Instance.OnChanged -= Redraw;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || InventorySystem.Instance == null) return;

        // มีหน้าต่างอื่นเปิดอยู่ = ไม่รับปุ่มใด ๆ
        if (DialogueUI.IsOpen || RewardPopupUI.IsOpen || ShopUI.IsOpen
            || CutsceneUI.IsPlaying || TitleScreenUI.IsOpen
            || DayTransitionUI.IsPlaying || SettingsMenuUI.IsOpen
            || NameEntryUI.IsOpen || AdminModeUI.IsOpen) return;

        // ปุ่ม 1-6 เลือกช่อง Hotbar
        var digits = new[]
        {
            kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
            kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key
        };

        for (int i = 0; i < digits.Length; i++)
        {
            if (digits[i].wasPressedThisFrame)
                InventorySystem.Instance.SelectSlot(i);
        }

        // ลูกกลิ้งเมาส์เลื่อนช่อง (เหมือน Minecraft)
        if (Mouse.current != null && !IsBackpackOpen)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int dir = scroll > 0 ? -1 : 1;
                // ลูกกลิ้งเลื่อนไปช่องถัดไปตรงๆ ไม่ต้องสลับเปิดปิดเหมือนกดปุ่มตัวเลข
                int current = InventorySystem.Instance.selectedIndex;
                if (current < 0) current = dir > 0 ? -1 : InventorySystem.HotbarSize;

                int next = current + dir;
                if (next < 0) next = InventorySystem.HotbarSize - 1;
                if (next >= InventorySystem.HotbarSize) next = 0;

                InventorySystem.Instance.SetSlot(next);
            }
        }

        if (kb.iKey.wasPressedThisFrame) ToggleBackpack();
        if (kb.escapeKey.wasPressedThisFrame && IsBackpackOpen) ToggleBackpack();
    }

    public void ToggleBackpack()
    {
        IsBackpackOpen = !IsBackpackOpen;
        if (m_BackpackPanel != null) m_BackpackPanel.SetActive(IsBackpackOpen);
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var inv = InventorySystem.Instance;
        int totalSlots = inv != null ? inv.TotalSlots : InventorySystem.HotbarSize;

        // ---- Canvas ----
        var canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // ต้องสูงกว่า HUD อื่น (รูปตัวละคร 20, แผ่นสอนเล่น 18, แถบเควส)
        // ไม่งั้นกระเป๋าจะโดนของพวกนั้นทับตอนเปิด
        canvas.sortingOrder = 30;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;   // วาดฟอนต์ละเอียด 3 เท่า -> ตัวอักษรคมขึ้น

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- เงิน (มุมขวาบน) ----
        m_MoneyText = CreateText(canvasGO.transform, "MoneyText", 50, TextAnchor.UpperRight);
        var moneyRT = m_MoneyText.rectTransform;
        moneyRT.anchorMin = moneyRT.anchorMax = new Vector2(1f, 1f);
        moneyRT.pivot = new Vector2(1f, 1f);
        moneyRT.anchoredPosition = new Vector2(-28f, -24f);
        moneyRT.sizeDelta = new Vector2(400f, 56f);

        // ---- แถบพื้นหลัง Hotbar ----
        const int barPadding = 10;
        int hotbarWidth = InventorySystem.HotbarSize * slotSize
                        + (InventorySystem.HotbarSize - 1) * slotSpacing;

        var bar = new GameObject("HotbarBar", typeof(RectTransform));
        bar.transform.SetParent(canvasGO.transform, false);

        var barImage = bar.AddComponent<Image>();
        barImage.color = barColor;

        var barRT = (RectTransform)bar.transform;
        // ปล่อยของบนแถบ Hotbar (ไม่ตรงช่อง) = ไม่ทำอะไร ไม่ใช่การโยนทิ้ง
        bar.AddComponent<InventoryUIArea>();

        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 20f);
        barRT.sizeDelta = new Vector2(hotbarWidth + barPadding * 2, slotSize + barPadding * 2);

        // ---- ช่อง Hotbar ----
        var hotbar = new GameObject("Hotbar", typeof(RectTransform));
        hotbar.transform.SetParent(bar.transform, false);

        var hotbarRT = (RectTransform)hotbar.transform;
        hotbarRT.anchorMin = Vector2.zero;
        hotbarRT.anchorMax = Vector2.one;
        hotbarRT.offsetMin = new Vector2(barPadding, barPadding);
        hotbarRT.offsetMax = new Vector2(-barPadding, -barPadding);

        var hotbarLayout = hotbar.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.spacing = slotSpacing;
        hotbarLayout.childForceExpandWidth = false;
        hotbarLayout.childForceExpandHeight = false;
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < InventorySystem.HotbarSize; i++)
            m_HotbarViews.Add(CreateSlot(hotbar.transform, (i + 1).ToString(), i));

        // ---- กระเป๋า (กลางจอ ซ่อนไว้ก่อน) ----
        int rows = Mathf.CeilToInt(totalSlots / (float)columns);
        int gridWidth = columns * slotSize + (columns - 1) * slotSpacing;
        int gridHeight = rows * slotSize + (rows - 1) * slotSpacing;
        const int padding = 40;        // ขอบรอบตารางช่อง
        const int titleHeight = 84;    // แถบหัวข้อด้านบน ต้องสูงพอไม่ให้ตัวอักษรทับตาราง
        const int tabHeight = 66;      // แถวปุ่มแท็บใต้หัวข้อ
        const int trashHeight = 104;   // แถบถังขยะใต้ตาราง

        // ตัวครอบทั้งจอ มีฉากหลังดำจางๆ ให้กระเป๋าเด่นออกมาจากฉาก
        m_BackpackPanel = new GameObject("BackpackRoot", typeof(RectTransform));
        m_BackpackPanel.transform.SetParent(canvasGO.transform, false);

        var dim = m_BackpackPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        var dimRT = (RectTransform)m_BackpackPanel.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        var panelGO = new GameObject("BackpackPanel", typeof(RectTransform));
        panelGO.transform.SetParent(m_BackpackPanel.transform, false);

        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = panelColor;

        // ปล่อยของบนพื้นที่ว่างของหน้ากระเป๋า = ไม่ทำอะไร คืนที่เดิม
        panelGO.AddComponent<InventoryUIArea>();

        var panelRT = (RectTransform)panelGO.transform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = new Vector2(0f, 40f);
        panelRT.sizeDelta = new Vector2(gridWidth + padding * 2,
                                        gridHeight + padding * 2 + titleHeight + tabHeight + trashHeight);

        var title = CreateText(panelGO.transform, "Title", 40, TextAnchor.MiddleCenter);
        title.text = "BAG        ( I )";

        // กินพื้นที่แถบหัวข้อทั้งแถบพอดี ไม่ล้นลงไปทับตารางช่องข้างล่าง
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -padding * 0.5f);
        titleRT.sizeDelta = new Vector2(0f, titleHeight - padding * 0.5f);

        // ---- แถวแท็บกรองของ ----
        var tabRow = new GameObject("Tabs", typeof(RectTransform));
        tabRow.transform.SetParent(panelGO.transform, false);

        var tabRowRT = (RectTransform)tabRow.transform;
        tabRowRT.anchorMin = tabRowRT.anchorMax = new Vector2(0.5f, 1f);
        tabRowRT.pivot = new Vector2(0.5f, 1f);
        tabRowRT.anchoredPosition = new Vector2(0f, -titleHeight + 8f);
        tabRowRT.sizeDelta = new Vector2(gridWidth, tabHeight - 14f);

        var tabLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 10f;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;

        m_BagTabImages.Clear();
        MakeBagTab(tabRow.transform, "ทั้งหมด", BagTab.All);
        MakeBagTab(tabRow.transform, "อุปกรณ์", BagTab.Tools);
        MakeBagTab(tabRow.transform, "เมล็ด", BagTab.Seeds);
        MakeBagTab(tabRow.transform, "ผลผลิต", BagTab.Produce);

        var grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(panelGO.transform, false);

        var gridRT = (RectTransform)grid.transform;
        gridRT.anchorMin = gridRT.anchorMax = new Vector2(0.5f, 1f);
        gridRT.pivot = new Vector2(0.5f, 1f);
        gridRT.anchoredPosition = new Vector2(0f, -titleHeight - tabHeight);
        gridRT.sizeDelta = new Vector2(gridWidth, gridHeight);

        var gridLayout = grid.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(slotSize, slotSize);
        gridLayout.spacing = new Vector2(slotSpacing, slotSpacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;

        // ช่อง 8 ช่องแรกคือช่องเดียวกับ Hotbar เลยคลิกเลือกได้
        // (ใช้ได้เฉพาะแท็บ "ทั้งหมด" ที่ช่องเรียงตรงกับช่องจริง)
        for (int i = 0; i < totalSlots; i++)
        {
            bool isHotbarSlot = i < InventorySystem.HotbarSize;

            m_BagViews.Add(CreateSlot(grid.transform,
                isHotbarSlot ? (i + 1).ToString() : "",
                isHotbarSlot ? i : -1));
        }

        BuildTrashZone(panelGO.transform, gridWidth, trashHeight, padding);
        BuildTrashConfirm(m_BackpackPanel.transform);

        m_BackpackPanel.SetActive(false);
        IsBackpackOpen = false;
    }

    // ================= ถังขยะ =================

    /// <summary>แถบถังขยะใต้ตาราง ลากของมาปล่อยแล้วทิ้งถาวร</summary>
    private void BuildTrashZone(Transform parent, int gridWidth, int height, int padding)
    {
        var zone = new GameObject("TrashZone", typeof(RectTransform));
        zone.transform.SetParent(parent, false);

        var image = zone.AddComponent<Image>();
        image.sprite = UIShapes.RoundedRect(16);
        image.type = Image.Type.Sliced;
        image.color = new Color(0.36f, 0.13f, 0.14f, 0.92f);

        zone.AddComponent<InventoryTrashZone>();

        var rt = (RectTransform)zone.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, padding * 0.5f);
        rt.sizeDelta = new Vector2(gridWidth, height - padding * 0.5f);

        var label = CreateText(zone.transform, "Label", 30, TextAnchor.MiddleCenter);
        label.text = "ลากของมาทิ้งตรงนี้        |        ลากออกนอกกระเป๋า = วางลงพื้น";
        label.color = new Color(1f, 0.82f, 0.82f, 0.92f);

        var labelRT = label.rectTransform;
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(12f, 8f);
        labelRT.offsetMax = new Vector2(-12f, -8f);
    }

    /// <summary>หน้าต่างยืนยันก่อนทิ้งของ — ทิ้งแล้วเอาคืนไม่ได้ เลยต้องถามก่อน</summary>
    private void BuildTrashConfirm(Transform parent)
    {
        m_TrashConfirm = new GameObject("TrashConfirm", typeof(RectTransform));
        m_TrashConfirm.transform.SetParent(parent, false);

        var dim = m_TrashConfirm.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        m_TrashConfirm.AddComponent<InventoryUIArea>();

        var dimRT = (RectTransform)m_TrashConfirm.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(m_TrashConfirm.transform, false);

        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = UIShapes.RoundedRect(18);
        boxImg.type = Image.Type.Sliced;
        boxImg.color = new Color(0.14f, 0.13f, 0.14f, 0.99f);

        var boxRT = (RectTransform)box.transform;
        boxRT.anchorMin = boxRT.anchorMax = new Vector2(0.5f, 0.5f);
        boxRT.pivot = new Vector2(0.5f, 0.5f);
        boxRT.anchoredPosition = Vector2.zero;
        boxRT.sizeDelta = new Vector2(680f, 260f);

        m_TrashText = CreateText(box.transform, "Message", 32, TextAnchor.MiddleCenter);
        var msgRT = m_TrashText.rectTransform;
        msgRT.anchorMin = new Vector2(0f, 0.4f);
        msgRT.anchorMax = new Vector2(1f, 1f);
        msgRT.offsetMin = new Vector2(24f, 0f);
        msgRT.offsetMax = new Vector2(-24f, -20f);

        m_BtnPlace = MakeConfirmButton(box.transform, new Color(0.28f, 0.52f, 0.30f, 1f),
                                       ConfirmPlaceAnimal, out m_BtnPlaceLabel);
        m_BtnTrash = MakeConfirmButton(box.transform, new Color(0.62f, 0.22f, 0.23f, 1f),
                                       ConfirmTrash, out m_BtnTrashLabel);
        m_BtnCancel = MakeConfirmButton(box.transform, new Color(0.26f, 0.26f, 0.28f, 1f),
                                        CloseConfirm, out m_BtnCancelLabel);

        m_TrashConfirm.SetActive(false);
    }

    private Button MakeConfirmButton(Transform parent, Color color,
                                     UnityEngine.Events.UnityAction action, out Text label)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = UIShapes.RoundedRect(14);
        image.type = Image.Type.Sliced;
        image.color = color;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 28f);
        rt.sizeDelta = new Vector2(250f, 68f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        label = CreateText(go.transform, "Label", 29, TextAnchor.MiddleCenter);
        var textRT = label.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return button;
    }

    private static void PlaceButton(Button button, string label, Text labelText, float x)
    {
        button.gameObject.SetActive(true);
        labelText.text = label;
        ((RectTransform)button.transform).anchoredPosition = new Vector2(x, 28f);
    }

    // ---- ลากของมาทิ้งถังขยะ ----

    /// <summary>ตัวลากของเรียกเข้ามาเมื่อปล่อยของลงถังขยะ</summary>
    public void AskTrash(int slotIndex)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || m_TrashConfirm == null) return;

        var item = inv.ItemAt(slotIndex);
        if (item == null) return;

        m_PendingTrashSlot = slotIndex;

        int count = inv.CountAt(slotIndex);
        m_TrashText.text = count > 1
            ? $"ทิ้ง {item.displayName} x{count} ทั้งกองเลยไหม\n\nทิ้งแล้วเอาคืนไม่ได้"
            : $"ทิ้ง {item.displayName} เลยไหม\n\nทิ้งแล้วเอาคืนไม่ได้";

        m_BtnPlace.gameObject.SetActive(false);
        PlaceButton(m_BtnTrash, "ทิ้งเลย", m_BtnTrashLabel, -150f);
        PlaceButton(m_BtnCancel, "ไม่ทิ้ง", m_BtnCancelLabel, 150f);

        ShowConfirm();
    }

    // ---- ลากสัตว์หรือไข่ออกจากกระเป๋า ----

    /// <summary>
    /// ถามว่าจะเอาสัตว์ออกมาเลี้ยงหรือทิ้ง
    ///
    /// สัตว์กับไข่ต่างจากของอื่นตรงที่วางลงพื้นเฉยๆ ไม่ได้ ต้องปล่อยเข้าคอกให้มันใช้ชีวิต
    /// </summary>
    public void AskAnimal(int slotIndex)
    {
        var inv = InventorySystem.Instance;
        var manager = AnimalManager.Instance;
        if (inv == null || manager == null || m_TrashConfirm == null) return;

        var item = inv.ItemAt(slotIndex);
        if (item == null) return;

        var animal = manager.FindByItem(item, out bool asAdult);
        if (animal == null) return;

        m_PendingTrashSlot = slotIndex;

        m_TrashText.text = asAdult
            ? $"{animal.displayName}  (ตัวโตเต็มวัย)\n\nจะปล่อยลงคอกให้ใช้ชีวิตเลยไหม"
            : $"ไข่{animal.displayName}\n\nวางลงคอกแล้วจะฟักออกมาเป็นตัวเล็ก";

        PlaceButton(m_BtnPlace, "วางเลี้ยง", m_BtnPlaceLabel, -230f);
        PlaceButton(m_BtnTrash, "ทิ้ง", m_BtnTrashLabel, 0f);
        PlaceButton(m_BtnCancel, "ยกเลิก", m_BtnCancelLabel, 230f);

        ShowConfirm();
    }

    /// <summary>ปล่อยสัตว์ลงคอก — ไข่ออกมาเป็นตัวเล็ก ตัวโตออกมาโตเลย</summary>
    private void ConfirmPlaceAnimal()
    {
        var inv = InventorySystem.Instance;
        var manager = AnimalManager.Instance;

        if (inv != null && manager != null && m_PendingTrashSlot >= 0)
        {
            var item = inv.ItemAt(m_PendingTrashSlot);
            var animal = manager.FindByItem(item, out bool asAdult);

            if (animal != null && inv.RemoveAt(m_PendingTrashSlot, 1) > 0)
            {
                var placed = manager.SpawnAt(animal, manager.PenCenter, asAdult);

                if (placed != null)
                {
                    AudioManager.PlayBuySell();
                    Debug.Log($"[สัตว์] ปล่อย {animal.displayName} " +
                              $"({(asAdult ? "ตัวโต" : "ลูกสัตว์")}) ลงคอกแล้ว");
                }
            }
        }

        CloseConfirm();
    }

    // ---- ร่วมกัน ----

    private void ShowConfirm()
    {
        m_TrashConfirm.SetActive(true);
        m_TrashConfirm.transform.SetAsLastSibling();
    }

    private void ConfirmTrash()
    {
        var inv = InventorySystem.Instance;
        if (inv != null && m_PendingTrashSlot >= 0)
        {
            var item = inv.ItemAt(m_PendingTrashSlot);
            int taken = inv.RemoveAt(m_PendingTrashSlot);

            if (item != null && taken > 0)
                Debug.Log($"[กระเป๋า] ทิ้ง {item.displayName} x{taken}");
        }

        CloseConfirm();
    }

    private void CloseConfirm()
    {
        m_PendingTrashSlot = -1;
        if (m_TrashConfirm != null) m_TrashConfirm.SetActive(false);
        AudioManager.PlaySelect();
    }

    /// <summary>ปุ่มแท็บกรองของในกระเป๋า</summary>
    private void MakeBagTab(Transform parent, string label, BagTab tab)
    {
        var go = new GameObject("Tab", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = UIShapes.RoundedRect(14);
        image.type = Image.Type.Sliced;
        image.color = tab == BagTab.All ? tabActiveColor : tabIdleColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SetBagTab(tab));

        var text = CreateText(go.transform, "Label", 26, TextAnchor.MiddleCenter);
        text.text = label;

        var textRT = text.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        m_BagTabImages.Add(image);
    }

    /// <summary>
    /// สร้างช่องเก็บของ 1 ช่อง
    /// slotIndex ตั้งแต่ 0 ขึ้นไป = คลิกเลือกช่องนั้นได้ (ใส่ -1 ถ้าไม่ให้คลิก)
    /// </summary>
    private SlotView CreateSlot(Transform parent, string hotkeyLabel, int slotIndex = -1)
    {
        var view = new SlotView();

        // ---- ขอบนอก ----
        var slotGO = new GameObject("Slot", typeof(RectTransform));
        slotGO.transform.SetParent(parent, false);
        ((RectTransform)slotGO.transform).sizeDelta = new Vector2(slotSize, slotSize);

        // บอกขนาดให้ LayoutGroup รู้ ไม่งั้นช่องจะแบนเป็น 0
        var layoutElement = slotGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = slotSize;
        layoutElement.preferredHeight = slotSize;
        layoutElement.minWidth = slotSize;
        layoutElement.minHeight = slotSize;

        view.border = slotGO.AddComponent<Image>();
        view.border.color = slotBorderColor;

        // ---- คลิกเลือกช่องด้วยเมาส์ ----
        if (slotIndex >= 0)
        {
            var button = slotGO.AddComponent<Button>();
            button.targetGraphic = view.border;
            button.transition = Selectable.Transition.None;   // สีขอบคุมเองอยู่แล้ว

            int captured = slotIndex;   // เก็บค่าไว้ ไม่งั้นทุกปุ่มจะชี้ช่องสุดท้าย
            button.onClick.AddListener(() =>
            {
                InventorySystem.Instance?.SelectSlot(captured);
                AudioManager.PlaySelect();
            });
        }

        // ---- ลากย้ายของ ----
        // ใส่ทุกช่อง รวมช่องในกระเป๋าที่ index เปลี่ยนไปตามแท็บที่กรอง
        // (DrawSlot จะอัปเดต slotIndex ให้ตรงทุกครั้งที่วาดใหม่)
        view.drag = slotGO.AddComponent<ItemDragHandler>();
        view.drag.slotIndex = slotIndex;

        // ---- พื้นในช่อง ----
        var innerGO = new GameObject("Inner", typeof(RectTransform));
        innerGO.transform.SetParent(slotGO.transform, false);

        var innerImage = innerGO.AddComponent<Image>();
        innerImage.color = slotFillColor;
        innerImage.raycastTarget = false;

        view.innerRT = (RectTransform)innerGO.transform;
        view.innerRT.anchorMin = Vector2.zero;
        view.innerRT.anchorMax = Vector2.one;
        SetInset(view.innerRT, borderWidth);

        // ---- เลขปุ่มมุมซ้ายบน ----
        if (!string.IsNullOrEmpty(hotkeyLabel))
        {
            var key = CreateText(slotGO.transform, "Hotkey", 26, TextAnchor.UpperLeft);
            key.text = hotkeyLabel;
            key.color = new Color(1f, 1f, 1f, 0.5f);

            var keyRT = key.rectTransform;
            keyRT.anchorMin = Vector2.zero;
            keyRT.anchorMax = Vector2.one;
            keyRT.offsetMin = new Vector2(9f, 6f);
            keyRT.offsetMax = new Vector2(-6f, -7f);
        }

        // ---- ไอคอนไอเทม ----
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(slotGO.transform, false);

        view.icon = iconGO.AddComponent<Image>();
        view.icon.preserveAspect = true;
        view.icon.raycastTarget = false;

        var iconRT = (RectTransform)iconGO.transform;
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        SetInset(iconRT, borderWidth + 10);

        // ---- จำนวนมุมขวาล่าง ----
        view.count = CreateText(slotGO.transform, "Count", 32, TextAnchor.LowerRight);
        var countRT = view.count.rectTransform;
        countRT.anchorMin = Vector2.zero;
        countRT.anchorMax = Vector2.one;
        countRT.offsetMin = new Vector2(6f, 6f);
        countRT.offsetMax = new Vector2(-9f, -6f);

        return view;
    }

    private static void SetInset(RectTransform rt, float inset)
    {
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = m_Font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // เงาดำให้ตัวเลขอ่านง่ายบนพื้นสว่าง
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    // ================= วาดใหม่ =================

    private void Redraw()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        if (m_MoneyText != null)
            m_MoneyText.text = $"{moneyPrefix} {inv.money:N0}";

        for (int i = 0; i < m_HotbarViews.Count; i++)
            DrawSlot(m_HotbarViews[i], i, true);

        DrawBag(inv);
    }

    /// <summary>
    /// วาดช่องในกระเป๋าตามแท็บที่เลือกอยู่
    /// แท็บ "ทั้งหมด" = ช่องตรงกับช่องจริง 1:1
    /// แท็บอื่น = กรองเอาเฉพาะของชนิดนั้นมาเรียงใหม่ตั้งแต่ช่องแรก
    /// </summary>
    private void DrawBag(InventorySystem inv)
    {
        m_FilteredSlots.Clear();

        if (m_BagTab == BagTab.All)
        {
            for (int i = 0; i < inv.slots.Count; i++) m_FilteredSlots.Add(i);
        }
        else
        {
            var wanted = m_BagTab switch
            {
                BagTab.Tools => ItemKind.Tool,
                BagTab.Seeds => ItemKind.Seed,
                _ => ItemKind.Produce,
            };

            for (int i = 0; i < inv.slots.Count; i++)
            {
                var slot = inv.slots[i];
                if (slot.IsEmpty || slot.item.kind != wanted) continue;

                m_FilteredSlots.Add(i);
            }
        }

        for (int i = 0; i < m_BagViews.Count; i++)
        {
            int slotIndex = i < m_FilteredSlots.Count ? m_FilteredSlots[i] : -1;
            DrawSlot(m_BagViews[i], slotIndex, false);
        }

        // ไฮไลต์แท็บที่เลือกอยู่
        for (int i = 0; i < m_BagTabImages.Count; i++)
        {
            if (m_BagTabImages[i] == null) continue;
            m_BagTabImages[i].color = (int)m_BagTab == i ? tabActiveColor : tabIdleColor;
        }
    }

    private void SetBagTab(BagTab tab)
    {
        m_BagTab = tab;
        AudioManager.PlaySelect();
        Redraw();
    }

    private void DrawSlot(SlotView view, int index, bool showSelection)
    {
        var inv = InventorySystem.Instance;
        if (view == null || inv == null || index >= inv.slots.Count) return;

        // ช่องในกระเป๋าเลื่อนไปมาได้ตามแท็บ ต้องบอกตัวลากทุกครั้งว่าตอนนี้คุมช่องไหนอยู่
        if (view.drag != null) view.drag.slotIndex = index;

        // index ติดลบ = ช่องนี้ไม่มีของให้แสดงในแท็บที่กรองอยู่
        if (index < 0)
        {
            view.icon.sprite = null;
            view.icon.enabled = false;
            view.count.text = "";
            view.border.color = slotBorderColor;
            SetInset(view.innerRT, borderWidth);
            return;
        }

        var slot = inv.slots[index];

        if (slot.IsEmpty)
        {
            view.icon.sprite = null;
            view.icon.enabled = false;
            view.count.text = "";
        }
        else
        {
            view.icon.sprite = slot.item.icon;
            view.icon.enabled = slot.item.icon != null;
            view.count.text = slot.count > 1 ? slot.count.ToString() : "";
        }

        bool selected = showSelection && index == inv.selectedIndex;
        view.border.color = selected ? selectedBorderColor : slotBorderColor;
        SetInset(view.innerRT, selected ? selectedBorderWidth : borderWidth);
    }
}
