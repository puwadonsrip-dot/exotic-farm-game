using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างดูรายละเอียดสัตว์ — คลิกขวาที่ตัวสัตว์เพื่อเปิด
///
/// โชว์ เลือด / ความสัมพันธ์ / ความหิว / การเจริญเติบโต / ภาวะ
/// ค่าอัปเดตสดตลอดเวลาที่เปิดอยู่
/// </summary>
public class AnimalInfoUI : MonoBehaviour
{
    public static AnimalInfoUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => IsOpen = false;

    [Header("สี")]
    public Color panelColor = new Color(0.10f, 0.12f, 0.10f, 0.98f);
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color barBackColor = new Color(0.05f, 0.06f, 0.05f, 1f);

    public Color healthColor = new Color(0.85f, 0.32f, 0.32f, 1f);
    public Color friendshipColor = new Color(0.92f, 0.45f, 0.62f, 1f);
    public Color hungerColor = new Color(0.95f, 0.70f, 0.28f, 1f);
    public Color growthColor = new Color(0.45f, 0.80f, 0.45f, 1f);

    private GameObject m_Root;
    private Image m_Portrait;
    private Text m_NameText;
    private Text m_ConditionText;
    private Text m_AgeText;
    private Font m_Font;

    private Bar m_Health;
    private Bar m_Friendship;
    private Bar m_Hunger;
    private Bar m_Growth;

    private AnimalInstance m_Target;

    /// <summary>หลอดค่า 1 หลอด</summary>
    private class Bar
    {
        public RectTransform fill;
        public Text label;
    }

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

    // ================= เปิด / ปิด =================

    public void Show(AnimalInstance animal)
    {
        if (animal == null || animal.data == null) return;

        m_Target = animal;
        m_Root.SetActive(true);
        IsOpen = true;

        Refresh();
    }

    public void Close()
    {
        m_Root.SetActive(false);
        IsOpen = false;
        m_Target = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // ตัวสัตว์หายไป (ขายหรือลบ) ก็ปิดหน้าต่าง
        if (m_Target == null)
        {
            Close();
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        var animal = m_Target;
        var data = animal.data;

        // รูปเป็นของสัตว์ตัวที่กดดูเท่านั้น และขยับตามอนิเมชั่นของมันด้วย
        if (data.HasFrames)
        {
            int frame = Mathf.FloorToInt(Time.unscaledTime * 3f) % data.frames.Length;
            m_Portrait.sprite = data.frames[frame];
            m_Portrait.enabled = true;
        }
        else
        {
            m_Portrait.enabled = false;
        }

        m_NameText.text = animal.IsAdult
            ? data.displayName
            : $"{data.displayName} (ตัวเล็ก)";

        m_ConditionText.text = animal.Condition;
        m_ConditionText.color = animal.ConditionColor;

        m_AgeText.text = animal.IsAdult
            ? $"โตเต็มวัยแล้ว  •  อายุ {animal.ageDays} วัน"
            : $"ลูกสัตว์  •  อีก {Mathf.Max(0, data.daysToAdult - animal.ageDays)} วันจะโตเต็มวัย";

        SetBar(m_Health, animal.health / 100f, $"{Mathf.RoundToInt(animal.health)} / 100");
        SetBar(m_Friendship, animal.friendship / 100f, $"{Mathf.RoundToInt(animal.friendship)} / 100");
        SetBar(m_Hunger, animal.hunger / 100f, $"{Mathf.RoundToInt(animal.hunger)} / 100");
        SetBar(m_Growth, animal.GrowthProgress, $"{Mathf.RoundToInt(animal.GrowthProgress * 100f)}%");
    }

    private static void SetBar(Bar bar, float value, string text)
    {
        if (bar == null) return;

        value = Mathf.Clamp01(value);
        bar.fill.anchorMax = new Vector2(value, 1f);
        bar.label.text = text;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var rounded = UIShapes.RoundedRect(20);

        var canvasGO = new GameObject("AnimalInfoCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;   // เหนือ HUD แต่ต่ำกว่าเมนูตั้งค่า

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        m_Root = new GameObject("InfoRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
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
        frameRT.sizeDelta = new Vector2(1220f, 560f);   // เผื่อที่คอลัมน์เกณฑ์ด้านขวา

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 6f);

        // ---- รูปตัวสัตว์ ----
        var portraitBox = new GameObject("PortraitBox", typeof(RectTransform));
        portraitBox.transform.SetParent(inner.transform, false);

        var boxImage = portraitBox.AddComponent<Image>();
        boxImage.sprite = rounded;
        boxImage.type = Image.Type.Sliced;
        boxImage.color = barBackColor;

        var boxRT = (RectTransform)portraitBox.transform;
        boxRT.anchorMin = boxRT.anchorMax = new Vector2(0f, 1f);
        boxRT.pivot = new Vector2(0f, 1f);
        boxRT.anchoredPosition = new Vector2(28f, -28f);
        boxRT.sizeDelta = new Vector2(150f, 150f);

        var portraitGO = new GameObject("Portrait", typeof(RectTransform));
        portraitGO.transform.SetParent(portraitBox.transform, false);

        m_Portrait = portraitGO.AddComponent<Image>();
        m_Portrait.preserveAspect = true;
        m_Portrait.raycastTarget = false;
        Stretch(m_Portrait.rectTransform, 12f);

        // ---- ชื่อ + ภาวะ ----
        m_NameText = MakeText(inner.transform, 42, TextAnchor.UpperLeft, Color.white);
        var nameRT = m_NameText.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.offsetMin = new Vector2(198f, -84f);
        nameRT.offsetMax = new Vector2(-28f, -30f);

        m_ConditionText = MakeText(inner.transform, 30, TextAnchor.UpperLeft, Color.green);
        var condRT = m_ConditionText.rectTransform;
        condRT.anchorMin = new Vector2(0f, 1f);
        condRT.anchorMax = new Vector2(1f, 1f);
        condRT.pivot = new Vector2(0f, 1f);
        condRT.offsetMin = new Vector2(198f, -126f);
        condRT.offsetMax = new Vector2(-28f, -88f);

        m_AgeText = MakeText(inner.transform, 24, TextAnchor.UpperLeft,
                             new Color(1f, 1f, 1f, 0.7f));
        var ageRT = m_AgeText.rectTransform;
        ageRT.anchorMin = new Vector2(0f, 1f);
        ageRT.anchorMax = new Vector2(1f, 1f);
        ageRT.pivot = new Vector2(0f, 1f);
        ageRT.offsetMin = new Vector2(198f, -164f);
        ageRT.offsetMax = new Vector2(-28f, -130f);

        // ---- หลอดค่าต่างๆ ----
        float y = -206f;
        m_Health = MakeBar(inner.transform, "เลือด", healthColor, ref y, rounded);
        m_Friendship = MakeBar(inner.transform, "ความสัมพันธ์", friendshipColor, ref y, rounded);
        m_Hunger = MakeBar(inner.transform, "ความอิ่ม", hungerColor, ref y, rounded);
        m_Growth = MakeBar(inner.transform, "การเจริญเติบโต", growthColor, ref y, rounded);

        // ---- คอลัมน์เกณฑ์ด้านขวา ----
        BuildRules(inner.transform, rounded);

        // ---- ปุ่มปิด ----
        var close = MakeButton(inner.transform, "ปิด  (Esc)", new Color(0.30f, 0.28f, 0.28f, 1f),
                               220f, 58f, rounded);
        var closeRT = (RectTransform)close.transform;
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(0f, 0f);
        closeRT.pivot = new Vector2(0f, 0f);
        closeRT.anchoredPosition = new Vector2(258f, 26f);
        close.onClick.AddListener(Close);
    }

    /// <summary>กล่องอธิบายเกณฑ์ ให้คนเล่นรู้ว่าค่าแต่ละอย่างขึ้นลงยังไง</summary>
    private void BuildRules(Transform parent, Sprite rounded)
    {
        var box = new GameObject("RulesBox", typeof(RectTransform));
        box.transform.SetParent(parent, false);

        var boxImage = box.AddComponent<Image>();
        boxImage.sprite = rounded;
        boxImage.type = Image.Type.Sliced;
        boxImage.color = new Color(0.06f, 0.07f, 0.06f, 0.9f);

        var boxRT = (RectTransform)box.transform;
        boxRT.anchorMin = new Vector2(1f, 0f);
        boxRT.anchorMax = new Vector2(1f, 1f);
        boxRT.pivot = new Vector2(1f, 0.5f);
        boxRT.anchoredPosition = new Vector2(-24f, 0f);
        boxRT.sizeDelta = new Vector2(320f, -48f);

        var header = MakeText(box.transform, 26, TextAnchor.UpperCenter, borderColor);
        header.text = "เกณฑ์การเลี้ยง";

        var headerRT = header.rectTransform;
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.offsetMin = new Vector2(12f, -46f);
        headerRT.offsetMax = new Vector2(-12f, -14f);

        // ---- กรอบที่เลื่อนได้ ----
        // ข้อความยาวเกินกล่อง เลยต้องเลื่อนดูได้ ไม่งั้นบรรทัดล่างๆ จะหายไป
        var scrollGO = new GameObject("Scroll", typeof(RectTransform));
        scrollGO.transform.SetParent(box.transform, false);

        var scrollRT = (RectTransform)scrollGO.transform;
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(16f, 14f);
        scrollRT.offsetMax = new Vector2(-16f, -50f);   // เว้นที่ให้หัวข้อด้านบน

        scrollGO.AddComponent<RectMask2D>();            // ตัดส่วนที่ล้นออกนอกกรอบ

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.viewport = scrollRT;

        var body = MakeText(scrollGO.transform, 19, TextAnchor.UpperLeft,
                            new Color(1f, 1f, 1f, 0.82f));
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.lineSpacing = 1.15f;

        body.text =
            "<b>ให้อาหาร  (กด E)</b>\n"
            + "ใช้ผลผลิตในกระเป๋า 1 ชิ้น\n"
            + "ความอิ่ม เต็ม 100\n"
            + "เลือด +6   ความสัมพันธ์ +8\n"
            + "\n"
            + "<b>ผ่านไป 1 วัน</b>\n"
            + "ถ้าได้กิน\n"
            + "   เลือด +4   อิ่ม −30\n"
            + "ถ้าอดอาหาร\n"
            + "   เลือด −10   อิ่ม −45\n"
            + "   ความสัมพันธ์ −4\n"
            + "   (อดจนหิวสุด เลือด −20)\n"
            + "\n"
            + "<b>การเจริญเติบโต</b>\n"
            + "ลูกสัตว์ต้องเลี้ยง 3 วัน\n"
            + "ถึงจะโตเต็มวัย\n"
            + "ตอนเล็กยังไม่ให้ผลผลิต\n"
            + "\n"
            + "<b>ผลผลิต</b>\n"
            + "ตัวโตที่ได้กินอาหาร\n"
            + "จะออกผลผลิตเช้าวันถัดไป\n"
            + "\n"
            + "<b>ผสมพันธุ์</b>\n"
            + "ชนิดเดียวกัน 2 ตัว โตเต็มวัย\n"
            + "และได้กินอาหารทั้งคู่ในวันเดียวกัน\n"
            + "จะออกลูก 1 ตัว";

        // สูงตามเนื้อหาเอง ScrollRect จะได้รู้ว่าต้องเลื่อนแค่ไหน
        var bodyRT = body.rectTransform;
        bodyRT.anchorMin = new Vector2(0f, 1f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.pivot = new Vector2(0.5f, 1f);
        bodyRT.anchoredPosition = Vector2.zero;
        bodyRT.sizeDelta = Vector2.zero;

        var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = bodyRT;

        // คำใบ้เล็กๆ ว่าเลื่อนได้
        var hint = MakeText(box.transform, 16, TextAnchor.LowerCenter,
                            new Color(1f, 1f, 1f, 0.4f));
        hint.text = "เลื่อนล้อเมาส์เพื่อดูต่อ";

        var hintRT = hint.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.anchoredPosition = new Vector2(0f, -2f);
        hintRT.sizeDelta = new Vector2(0f, 22f);
    }

    /// <summary>หลอดค่า 1 แถว: ชื่อ + หลอด + ตัวเลข</summary>
    private Bar MakeBar(Transform parent, string label, Color color, ref float y, Sprite rounded)
    {
        const float rowHeight = 62f;

        var name = MakeText(parent, 26, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f));
        name.text = label;

        var nameRT = name.rectTransform;
        nameRT.anchorMin = nameRT.anchorMax = new Vector2(0f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(28f, y);
        nameRT.sizeDelta = new Vector2(220f, 34f);

        // พื้นหลอด
        var back = new GameObject("BarBack", typeof(RectTransform));
        back.transform.SetParent(parent, false);

        var backImage = back.AddComponent<Image>();
        backImage.sprite = rounded;
        backImage.type = Image.Type.Sliced;
        backImage.color = barBackColor;

        var backRT = (RectTransform)back.transform;
        backRT.anchorMin = backRT.anchorMax = new Vector2(0f, 1f);
        backRT.pivot = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(258f, y);
        backRT.sizeDelta = new Vector2(340f, 34f);

        // ตัวหลอด
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(back.transform, false);

        var fillImage = fill.AddComponent<Image>();
        fillImage.sprite = rounded;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = color;

        var fillRT = (RectTransform)fill.transform;
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        fillRT.offsetMin = new Vector2(3f, 3f);
        fillRT.offsetMax = new Vector2(-3f, -3f);

        // ตัวเลขท้ายหลอด
        var value = MakeText(parent, 24, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.9f));

        var valueRT = value.rectTransform;
        valueRT.anchorMin = valueRT.anchorMax = new Vector2(0f, 1f);
        valueRT.pivot = new Vector2(0f, 1f);
        valueRT.anchoredPosition = new Vector2(610f, y);
        valueRT.sizeDelta = new Vector2(120f, 34f);

        y -= rowHeight;

        return new Bar { fill = fillRT, label = value };
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
                              float width, float height, Sprite rounded)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = MakeText(go.transform, 28, TextAnchor.MiddleCenter, Color.white);
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
