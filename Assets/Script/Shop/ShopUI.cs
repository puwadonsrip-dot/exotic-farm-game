using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างขายผลผลิตให้ NPC
///
/// เปิดด้วยการกด F ตอนยืนใกล้ NPC ที่รับซื้อ (แยกจากการคุยเควส)
/// สร้าง UI ด้วยโค้ดทั้งหมด วางไว้บน GameObject ชื่อ "ShopUI"
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    /// <summary>ตอนนี้หน้าต่างร้านเปิดอยู่ไหม</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    /// Unity 6 ปิด Domain Reload เป็นค่าเริ่มต้น ตัวแปร static เลยค้างข้ามรอบ
    /// ถ้าครั้งก่อนกด Stop ตอนเปิดร้านค้างไว้ เกมจะล็อคการกดปุ่มทั้งหมด
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => IsOpen = false;

    [Header("สี")]
    public Color panelColor = new Color(0.09f, 0.08f, 0.07f, 0.97f);
    public Color borderColor = new Color(0.55f, 0.78f, 0.45f, 1f);
    public Color rowColor = new Color(0.16f, 0.15f, 0.14f, 0.9f);
    public Color sellButtonColor = new Color(0.30f, 0.55f, 0.25f, 1f);
    public Color sellAllButtonColor = new Color(0.55f, 0.45f, 0.20f, 1f);
    public Color closeButtonColor = new Color(0.32f, 0.30f, 0.30f, 1f);

    [Header("ข้อความ")]
    public string moneyPrefix = "฿";

    [Header("สีปุ่มแท็บ")]
    public Color tabActiveColor = new Color(0.30f, 0.55f, 0.25f, 1f);
    public Color tabIdleColor = new Color(0.22f, 0.21f, 0.20f, 0.9f);
    public Color buyButtonColor = new Color(0.25f, 0.42f, 0.62f, 1f);

    private enum Tab { Sell, Seeds, Eggs, Animals }

    private Tab m_Tab = Tab.Sell;
    private readonly List<Image> m_TabImages = new List<Image>();

    private GameObject m_Root;
    private Text m_HeaderText;
    private Text m_MoneyText;
    private Text m_EmptyText;
    private RectTransform m_RowContainer;
    private readonly List<GameObject> m_Rows = new List<GameObject>();
    private Font m_Font;
    private int m_OpenedFrame = -1;
    private string m_NpcName = "";

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

    private void Update()
    {
        if (!IsOpen) return;

        // กันปิดทันทีในเฟรมเดียวกับที่เพิ่งเปิด
        // NPCInteractable เห็น F แล้วสั่ง Open() ในเฟรมนี้
        // ถ้า Update ของ ShopUI ทำงานทีหลัง มันจะเห็น F ตัวเดิมแล้วสั่งปิดต่อทันที
        // ผลคือหน้าต่างเปิด-ปิดในเฟรมเดียว มองเห็นเป็น "กด F ไม่ติด"
        if (Time.frameCount == m_OpenedFrame) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame))
            Close();
    }

    // ================= เปิด / ปิด =================

    public void Open(string npcName)
    {
        m_NpcName = npcName;
        m_Tab = Tab.Sell;

        m_Root.SetActive(true);
        IsOpen = true;
        m_OpenedFrame = Time.frameCount;

        Refresh();
        Debug.Log($"[Shop] เปิดร้านของ {npcName}");
    }

    public void Close()
    {
        m_Root.SetActive(false);
        IsOpen = false;
        AudioManager.PlayClose();
    }

    // ================= วาดรายการ =================

    /// <summary>สลับแท็บ</summary>
    private void SetTab(Tab tab)
    {
        m_Tab = tab;
        Refresh();
    }

    private void Refresh()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        foreach (var row in m_Rows) Destroy(row);
        m_Rows.Clear();

        m_HeaderText.text = m_Tab switch
        {
            Tab.Seeds => $"ซื้อเมล็ดจาก{m_NpcName}",
            Tab.Eggs => $"ซื้อไข่สัตว์จาก{m_NpcName}",
            Tab.Animals => $"ซื้อสัตว์ตัวโตจาก{m_NpcName}",
            _ => $"ขายผลผลิตให้{m_NpcName}",
        };

        for (int i = 0; i < m_TabImages.Count; i++)
        {
            if (m_TabImages[i] == null) continue;
            m_TabImages[i].color = (int)m_Tab == i ? tabActiveColor : tabIdleColor;
        }

        switch (m_Tab)
        {
            case Tab.Seeds: BuildBuyList(inv); break;
            case Tab.Eggs: BuildAnimalList(inv, adults: false); break;
            case Tab.Animals: BuildAnimalList(inv, adults: true); break;
            default: BuildSellList(inv); break;
        }

        m_EmptyText.gameObject.SetActive(m_Rows.Count == 0);
        m_MoneyText.text = $"เงินของคุณ:  {moneyPrefix} {inv.money:N0}";
    }

    // ---- แท็บขาย: เอาของในกระเป๋าที่ขายได้มาแสดง ----
    private void BuildSellList(InventorySystem inv)
    {
        m_EmptyText.text = "ยังไม่มีผลผลิตให้ขาย\nไปเก็บเกี่ยวพืชมาก่อนนะ";

        // รวมของชนิดเดียวกันจากทุกช่อง
        var totals = new Dictionary<ItemData, int>();
        foreach (var slot in inv.slots)
        {
            if (slot.IsEmpty) continue;
            if (slot.item.sellPrice <= 0) continue;          // ขายไม่ได้
            if (slot.item.kind == ItemKind.Tool) continue;   // ไม่ให้ขายอุปกรณ์

            totals.TryGetValue(slot.item, out int have);
            totals[slot.item] = have + slot.count;
        }

        foreach (var pair in totals)
            AddRow(pair.Key, pair.Value);
    }

    // ---- แท็บซื้อ: เอาเมล็ดของพืชทุกชนิดมาแสดง ----
    private void BuildBuyList(InventorySystem inv)
    {
        m_EmptyText.text = "ตอนนี้ยังไม่มีอะไรขาย";

        var farm = FarmManager.Instance;
        if (farm == null) return;

        foreach (var crop in farm.allCrops)
        {
            if (crop == null || crop.seedItem == null) continue;
            if (crop.seedItem.buyPrice <= 0) continue;

            AddBuyRow(crop.seedItem, inv.CountOf(crop.seedItem));
        }
    }

    /// <summary>กล่องแถวเปล่าพร้อมไอคอน ใช้ร่วมกันทั้งแท็บซื้อและขาย</summary>
    private GameObject MakeRow(ItemData item)
    {
        var row = new GameObject("ShopRow", typeof(RectTransform));
        row.transform.SetParent(m_RowContainer, false);

        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 78;
        layout.minHeight = 78;

        var bg = row.AddComponent<Image>();
        bg.color = rowColor;

        if (item.icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(row.transform, false);

            var iconImage = iconGO.AddComponent<Image>();
            iconImage.sprite = item.icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(14f, 0f);
            iconRT.sizeDelta = new Vector2(56f, 56f);
        }

        return row;
    }

    private void AddRow(ItemData item, int count)
    {
        var row = MakeRow(item);

        // ชื่อ + จำนวน
        var nameText = MakeText(row.transform, 30, TextAnchor.MiddleLeft, Color.white);
        nameText.text = $"{item.displayName}   x{count}";
        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(84f, 0f);
        nameRT.offsetMax = new Vector2(-400f, 0f);

        // ราคาต่อชิ้น
        var priceText = MakeText(row.transform, 30, TextAnchor.MiddleRight, borderColor);
        priceText.text = $"{moneyPrefix} {item.sellPrice}";
        var priceRT = priceText.rectTransform;
        priceRT.anchorMin = Vector2.zero;
        priceRT.anchorMax = Vector2.one;
        priceRT.offsetMin = new Vector2(0f, 0f);
        priceRT.offsetMax = new Vector2(-290f, 0f);

        // ปุ่มขาย 1
        var one = MakeButton(row.transform, "ขาย 1", sellButtonColor, 120f);
        var oneRT = (RectTransform)one.transform;
        oneRT.anchorMin = oneRT.anchorMax = new Vector2(1f, 0.5f);
        oneRT.pivot = new Vector2(1f, 0.5f);
        oneRT.anchoredPosition = new Vector2(-152f, 0f);
        one.onClick.AddListener(() => Sell(item, 1));

        // ปุ่มขายทั้งหมด
        var all = MakeButton(row.transform, "ขายหมด", sellAllButtonColor, 140f);
        var allRT = (RectTransform)all.transform;
        allRT.anchorMin = allRT.anchorMax = new Vector2(1f, 0.5f);
        allRT.pivot = new Vector2(1f, 0.5f);
        allRT.anchoredPosition = new Vector2(-8f, 0f);
        int amount = count;
        all.onClick.AddListener(() => Sell(item, amount));

        m_Rows.Add(row);
    }

    // ---- แท็บซื้อไข่ / ซื้อสัตว์ตัวโต ----
    private void BuildAnimalList(InventorySystem inv, bool adults)
    {
        m_EmptyText.text = adults ? "ยังไม่มีสัตว์ให้ซื้อ" : "ยังไม่มีไข่ให้ซื้อ";

        var manager = AnimalManager.Instance;
        if (manager == null) return;

        foreach (var animal in manager.animals)
        {
            if (animal == null || !animal.HasFrames) continue;
            AddAnimalRow(animal, adults);
        }
    }

    /// <summary>แถวสำหรับซื้อไข่หรือตัวโต</summary>
    private void AddAnimalRow(AnimalData animal, bool adult)
    {
        var item = adult ? animal.adultItem : animal.eggItem;
        if (item == null) return;

        int price = adult ? animal.buyPrice : animal.eggPrice;

        var row = new GameObject("ShopRow", typeof(RectTransform));
        row.transform.SetParent(m_RowContainer, false);

        // แถวสูงกว่าปกติ เพราะมีสองบรรทัด
        var layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 104;
        layout.minHeight = 104;

        var bg = row.AddComponent<Image>();
        bg.color = rowColor;

        // ---- ไอคอน ----
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(row.transform, false);

        var iconImage = iconGO.AddComponent<Image>();
        iconImage.sprite = item.icon != null ? item.icon : animal.Icon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var iconRT = (RectTransform)iconGO.transform;
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(16f, 0f);
        iconRT.sizeDelta = new Vector2(76f, 76f);

        string produceName = animal.produceItem != null ? animal.produceItem.displayName : "ผลผลิต";

        // ---- ชื่อ (บรรทัดบน) ----
        var nameText = MakeText(row.transform, 30, TextAnchor.LowerLeft, Color.white);
        nameText.text = adult ? $"{animal.displayName}  (ตัวโต)" : $"ไข่{animal.displayName}";

        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 0.5f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(108f, 2f);
        nameRT.offsetMax = new Vector2(-430f, -14f);

        // ---- คำอธิบาย (บรรทัดล่าง) ----
        var descText = MakeText(row.transform, 21, TextAnchor.UpperLeft,
                                new Color(1f, 1f, 1f, 0.62f));
        descText.text = adult
            ? $"ให้อาหารได้ทันที — ได้{produceName}"
            : $"ฟักเป็นตัวเล็ก เลี้ยง {animal.daysToAdult} วันถึงโต";

        var descRT = descText.rectTransform;
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.5f);
        descRT.offsetMin = new Vector2(108f, 14f);
        descRT.offsetMax = new Vector2(-430f, -2f);

        // ---- ราคา ----
        var priceText = MakeText(row.transform, 32, TextAnchor.MiddleRight, borderColor);
        priceText.text = $"{moneyPrefix} {price}";

        var priceRT = priceText.rectTransform;
        priceRT.anchorMin = Vector2.zero;
        priceRT.anchorMax = Vector2.one;
        priceRT.offsetMin = new Vector2(0f, 0f);
        priceRT.offsetMax = new Vector2(-200f, 0f);

        // ---- ปุ่มซื้อ ----
        var buy = MakeButton(row.transform, "ซื้อ", adult ? sellAllButtonColor : buyButtonColor, 160f);
        var buyRT = (RectTransform)buy.transform;
        buyRT.anchorMin = buyRT.anchorMax = new Vector2(1f, 0.5f);
        buyRT.pivot = new Vector2(1f, 0.5f);
        buyRT.anchoredPosition = new Vector2(-16f, 0f);
        buy.onClick.AddListener(() => BuyAnimal(animal, item, price));

        m_Rows.Add(row);
    }

    private void BuyAnimal(AnimalData animal, ItemData item, int price)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || animal == null || item == null) return;

        if (inv.money < price)
        {
            m_MoneyText.text = $"เงินไม่พอ! ต้องมี {moneyPrefix} {price:N0}";
            return;
        }

        if (!inv.Add(item, 1))
        {
            m_MoneyText.text = "กระเป๋าเต็ม! ใช้ของให้ว่างก่อน";
            return;
        }

        inv.SpendMoney(price);
        AudioManager.PlayBuySell();

        Debug.Log($"[Shop] ซื้อ {item.displayName} จ่าย {price}฿");
        m_MoneyText.text = $"ได้{item.displayName}แล้ว — เลือกจากช่องล่างจอ แล้วคลิกวางในคอก";
    }

    /// <summary>แถวสำหรับซื้อเมล็ด</summary>
    private void AddBuyRow(ItemData item, int owned)
    {
        var row = MakeRow(item);

        var nameText = MakeText(row.transform, 30, TextAnchor.MiddleLeft, Color.white);
        nameText.text = owned > 0
            ? $"{item.displayName}   (มีอยู่ {owned})"
            : item.displayName;

        var nameRT = nameText.rectTransform;
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(84f, 0f);
        nameRT.offsetMax = new Vector2(-400f, 0f);

        var priceText = MakeText(row.transform, 30, TextAnchor.MiddleRight, borderColor);
        priceText.text = $"{moneyPrefix} {item.buyPrice}";
        var priceRT = priceText.rectTransform;
        priceRT.anchorMin = Vector2.zero;
        priceRT.anchorMax = Vector2.one;
        priceRT.offsetMin = new Vector2(0f, 0f);
        priceRT.offsetMax = new Vector2(-290f, 0f);

        var one = MakeButton(row.transform, "ซื้อ 1", buyButtonColor, 120f);
        var oneRT = (RectTransform)one.transform;
        oneRT.anchorMin = oneRT.anchorMax = new Vector2(1f, 0.5f);
        oneRT.pivot = new Vector2(1f, 0.5f);
        oneRT.anchoredPosition = new Vector2(-152f, 0f);
        one.onClick.AddListener(() => Buy(item, 1));

        var five = MakeButton(row.transform, "ซื้อ 5", sellAllButtonColor, 140f);
        var fiveRT = (RectTransform)five.transform;
        fiveRT.anchorMin = fiveRT.anchorMax = new Vector2(1f, 0.5f);
        fiveRT.pivot = new Vector2(1f, 0.5f);
        fiveRT.anchoredPosition = new Vector2(-8f, 0f);
        five.onClick.AddListener(() => Buy(item, 5));

        m_Rows.Add(row);
    }

    // ================= ซื้อ =================

    private void Buy(ItemData item, int count)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || item == null || count <= 0) return;

        int total = item.buyPrice * count;

        if (inv.money < total)
        {
            Debug.Log($"[Shop] เงินไม่พอ ต้องมี {total}฿ แต่มี {inv.money}฿");
            m_MoneyText.text = $"เงินไม่พอ! ต้องมี {moneyPrefix} {total:N0}";
            return;
        }

        // เพิ่มของก่อน เผื่อกระเป๋าเต็มจะได้ไม่เสียเงินฟรี
        if (!inv.Add(item, count))
        {
            Debug.Log("[Shop] กระเป๋าเต็ม ซื้อไม่ได้");
            m_MoneyText.text = "กระเป๋าเต็ม! ใช้ของให้ว่างก่อน";
            return;
        }

        inv.SpendMoney(total);
        AudioManager.PlayBuySell();

        Debug.Log($"[Shop] ซื้อ {item.displayName} x{count} จ่าย {total}฿");
        Refresh();
    }

    // ================= ขาย =================

    private void Sell(ItemData item, int count)
    {
        var inv = InventorySystem.Instance;
        if (inv == null || item == null || count <= 0) return;

        int have = inv.CountOf(item);
        count = Mathf.Min(count, have);
        if (count <= 0) return;

        if (!inv.Remove(item, count)) return;

        int total = item.sellPrice * count;
        inv.AddMoney(total);
        AudioManager.PlayBuySell();

        // บอกระบบเควสว่าขายไปแล้ว (เผื่อมีเควสให้ขายพืช)
        if (QuestManager.Instance != null && item.crop != null)
            QuestManager.Instance.Report(QuestObjective.SellCrop, item.crop.cropId, count);

        Debug.Log($"[Shop] ขาย {item.displayName} x{count} ได้ {total}฿");
        Refresh();
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("ShopCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;   // วาดฟอนต์ละเอียด 3 เท่า -> ตัวอักษรคมขึ้น

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- ฉากหลังมืด ----
        m_Root = new GameObject("ShopRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        var dimRT = (RectTransform)m_Root.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // ---- กรอบ ----
        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(m_Root.transform, false);

        var frameImage = frame.AddComponent<Image>();
        frameImage.color = borderColor;

        var frameRT = (RectTransform)frame.transform;
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = Vector2.zero;
        frameRT.sizeDelta = new Vector2(1180f, 660f);

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
        m_HeaderText = MakeText(inner.transform, 44, TextAnchor.UpperCenter, borderColor);
        m_HeaderText.text = "ขายผลผลิต";
        var headerRT = m_HeaderText.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0f, -22f);
        headerRT.sizeDelta = new Vector2(0f, 50f);

        // ---- แท็บ ขาย / ซื้อเมล็ด / ซื้อสัตว์ ----
        m_TabImages.Clear();
        m_TabImages.Add(MakeTab(inner.transform, "ขายผลผลิต", -324f, Tab.Sell));
        m_TabImages.Add(MakeTab(inner.transform, "ซื้อเมล็ด", -108f, Tab.Seeds));
        m_TabImages.Add(MakeTab(inner.transform, "ซื้อไข่", 108f, Tab.Eggs));
        m_TabImages.Add(MakeTab(inner.transform, "ซื้อสัตว์โต", 324f, Tab.Animals));

        // ---- กรอบที่เลื่อนได้ ----
        // ของเยอะเกินกรอบก็เลื่อนดูได้ ไม่ล้นออกไปบังปุ่มข้างล่าง
        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        scrollGO.transform.SetParent(inner.transform, false);

        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(28f, 100f);     // เว้นล่างให้เงินกับปุ่มปิด
        scrollRT.offsetMax = new Vector2(-28f, -152f);   // เว้นบนให้หัวข้อกับแท็บ

        scrollGO.AddComponent<RectMask2D>();             // ตัดส่วนที่ล้นออกนอกกรอบ

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.viewport = scrollRT;

        // ---- รายการ ----
        var container = new GameObject("Rows", typeof(RectTransform));
        container.transform.SetParent(scrollGO.transform, false);

        m_RowContainer = (RectTransform)container.transform;
        m_RowContainer.anchorMin = new Vector2(0f, 1f);
        m_RowContainer.anchorMax = new Vector2(1f, 1f);
        m_RowContainer.pivot = new Vector2(0.5f, 1f);
        m_RowContainer.anchoredPosition = Vector2.zero;
        m_RowContainer.sizeDelta = new Vector2(0f, 0f);

        var rowLayout = container.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 10;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childAlignment = TextAnchor.UpperCenter;

        // สูงตามจำนวนแถวเอง ScrollRect จะได้รู้ว่าต้องเลื่อนแค่ไหน
        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = m_RowContainer;

        // ---- ข้อความตอนไม่มีของ ----
        m_EmptyText = MakeText(inner.transform, 32, TextAnchor.MiddleCenter,
                               new Color(1f, 1f, 1f, 0.6f));
        var emptyRT = m_EmptyText.rectTransform;
        emptyRT.anchorMin = Vector2.zero;
        emptyRT.anchorMax = Vector2.one;
        emptyRT.offsetMin = new Vector2(40f, 140f);
        emptyRT.offsetMax = new Vector2(-40f, -120f);

        // ---- เงิน ----
        m_MoneyText = MakeText(inner.transform, 34, TextAnchor.LowerLeft, Color.white);
        var moneyRT = m_MoneyText.rectTransform;
        moneyRT.anchorMin = moneyRT.anchorMax = new Vector2(0f, 0f);
        moneyRT.pivot = new Vector2(0f, 0f);
        moneyRT.anchoredPosition = new Vector2(30f, 34f);
        moneyRT.sizeDelta = new Vector2(500f, 44f);

        // ---- ปุ่มปิด ----
        var close = MakeButton(inner.transform, "ปิด  (Esc)", closeButtonColor, 220f);
        var closeRT = (RectTransform)close.transform;
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 0f);
        closeRT.pivot = new Vector2(1f, 0f);
        closeRT.anchoredPosition = new Vector2(-30f, 26f);
        close.onClick.AddListener(Close);
    }

    /// <summary>ปุ่มแท็บด้านบน วางเรียงกันกลางหน้าต่าง</summary>
    private Image MakeTab(Transform parent, string label, float x, Tab tab)
    {
        var button = MakeButton(parent, label, tabIdleColor, 208f);

        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -84f);

        button.onClick.AddListener(() => SetTab(tab));
        return button.targetGraphic as Image;
    }

    private Button MakeButton(Transform parent, string label, Color color, float width)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, 56f);

        var img = go.AddComponent<Image>();
        img.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText(go.transform, 28, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        var textRT = text.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

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
