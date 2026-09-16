using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// รูปตัวละครทรงกลมมุมซ้ายบน
///
/// ตอนนี้มีแค่รูป — เผื่อที่ไว้ให้ใส่หลอดพลัง หลอดพลังงาน หรือค่าสเตตัสอื่นทีหลัง
/// รูปดึงมาจาก SpriteRenderer ของ Player โดยตรง เปลี่ยนชุดตัวละครเมื่อไหร่รูปก็เปลี่ยนตาม
/// </summary>
public class PlayerPortraitUI : MonoBehaviour
{
    public static PlayerPortraitUI Instance { get; private set; }

    [Header("ขนาด")]
    [Tooltip("เส้นผ่านศูนย์กลางของวงกลม")]
    public float size = 150f;

    [Tooltip("ความหนาของกรอบ")]
    public float borderThickness = 7f;

    [Tooltip("ระยะห่างจากมุมซ้ายบนของจอ")]
    public Vector2 margin = new Vector2(28f, -22f);

    [Header("แผ่นป้ายสี่เหลี่ยมข้างๆ")]
    [Tooltip("เปิด = มีกรอบสี่เหลี่ยมยาวต่อจากวงกลม (เว้นที่ไว้ใส่หลอดพลังทีหลัง)")]
    public bool showPlate = true;

    public float plateWidth = 430f;
    public float plateHeight = 104f;

    [Header("สี")]
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color backgroundColor = new Color(0.13f, 0.16f, 0.13f, 0.95f);
    public Color plateFillColor = new Color(0.10f, 0.12f, 0.10f, 0.88f);
    public Color nameColor = new Color(1f, 0.95f, 0.80f, 1f);

    [Header("การจัดรูปในกรอบ")]
    [Tooltip("ซูมรูปตัวละคร — 1 = เห็นเต็มตัวพอดี ยิ่งมากยิ่งเห็นใกล้")]
    public float zoom = 1.05f;

    [Tooltip("ขยับรูปขึ้นลง ให้ตัวละครอยู่กลางกรอบพอดี")]
    public Vector2 portraitOffset = new Vector2(0f, -4f);

    [Header("รูปที่ใช้")]
    [Tooltip("ใส่รูปเองได้ — เว้นว่าง = ดึงรูปจากตัวละครใน Scene")]
    public Sprite portraitOverride;

    [Tooltip("เปิด = รูปขยับตามท่าทางตัวละครตอนเดิน")]
    public bool followAnimation = true;

    private Image m_Portrait;
    private Text m_NameText;
    private SpriteRenderer m_PlayerRenderer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildUI();
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
            m_PlayerRenderer = playerGO.GetComponentInChildren<SpriteRenderer>();

        RefreshSprite();

        ApplyName(PlayerProfile.Name);
        PlayerProfile.OnNameChanged += ApplyName;
    }

    private void OnDestroy()
    {
        PlayerProfile.OnNameChanged -= ApplyName;
    }

    private void ApplyName(string value)
    {
        if (m_NameText != null) m_NameText.text = value;
    }

    private Text MakeText(Transform parent, int fontSize, TextAnchor anchor, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font = UIFont.Get();
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private void LateUpdate()
    {
        if (!followAnimation || portraitOverride != null) return;
        RefreshSprite();
    }

    private void RefreshSprite()
    {
        if (m_Portrait == null) return;

        var sprite = portraitOverride != null
            ? portraitOverride
            : (m_PlayerRenderer != null ? m_PlayerRenderer.sprite : null);

        if (sprite == null) return;
        if (m_Portrait.sprite == sprite) return;

        m_Portrait.sprite = sprite;
        m_Portrait.enabled = true;
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var circle = UIShapes.Circle();

        var canvasGO = new GameObject("PortraitCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;   // อยู่เหนือแถบเควส แต่ต่ำกว่าหน้าต่างต่างๆ

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        // จุดกึ่งกลางของวงกลม ใช้จัดแผ่นป้ายให้อยู่ระดับเดียวกัน
        float circleCenterY = margin.y - size * 0.5f;

        // ---- แผ่นป้ายสี่เหลี่ยมยาว (สร้างก่อน จะได้อยู่ข้างหลังวงกลม) ----
        if (showPlate)
        {
            var rounded = UIShapes.RoundedRect(26);

            var plate = new GameObject("StatusPlate", typeof(RectTransform));
            plate.transform.SetParent(canvasGO.transform, false);

            var plateBorder = plate.AddComponent<Image>();
            plateBorder.sprite = rounded;
            plateBorder.type = Image.Type.Sliced;
            plateBorder.color = borderColor;
            plateBorder.raycastTarget = false;

            var plateRT = (RectTransform)plate.transform;
            plateRT.anchorMin = plateRT.anchorMax = new Vector2(0f, 1f);
            plateRT.pivot = new Vector2(0f, 0.5f);

            // เริ่มจากกึ่งกลางวงกลม วงกลมจะได้ทับหัวแผ่นป้ายพอดี
            plateRT.anchoredPosition = new Vector2(margin.x + size * 0.5f, circleCenterY);
            plateRT.sizeDelta = new Vector2(plateWidth, plateHeight);

            var plateInner = new GameObject("Fill", typeof(RectTransform));
            plateInner.transform.SetParent(plate.transform, false);

            var fill = plateInner.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = plateFillColor;
            fill.raycastTarget = false;

            var fillRT = (RectTransform)plateInner.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(5f, 5f);
            fillRT.offsetMax = new Vector2(-5f, -5f);

            // ---- ชื่อตัวละคร ----
            // เว้นซ้ายไว้ให้วงกลมทับ เลยเริ่มข้อความหลังจากนั้น
            m_NameText = MakeText(plateInner.transform, 34, TextAnchor.MiddleLeft, nameColor);
            var nameRT = m_NameText.rectTransform;
            nameRT.anchorMin = Vector2.zero;
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = new Vector2(size * 0.5f + 22f, 0f);
            nameRT.offsetMax = new Vector2(-18f, 0f);
        }

        // ---- กรอบวงกลมด้านนอก ----
        var ring = new GameObject("Ring", typeof(RectTransform));
        ring.transform.SetParent(canvasGO.transform, false);

        var ringImage = ring.AddComponent<Image>();
        ringImage.sprite = circle;
        ringImage.color = borderColor;
        ringImage.raycastTarget = false;

        var ringRT = (RectTransform)ring.transform;
        ringRT.anchorMin = ringRT.anchorMax = new Vector2(0f, 1f);
        ringRT.pivot = new Vector2(0f, 1f);
        ringRT.anchoredPosition = margin;
        ringRT.sizeDelta = new Vector2(size, size);

        // ---- วงกลมด้านใน (ตัวตัดรูปให้เป็นวงกลม) ----
        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(ring.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = circle;
        innerImage.color = backgroundColor;
        innerImage.raycastTarget = false;

        // Mask = ทุกอย่างที่อยู่ข้างในจะถูกตัดตามรูปวงกลมนี้
        var mask = inner.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        var innerRT = (RectTransform)inner.transform;
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(borderThickness, borderThickness);
        innerRT.offsetMax = new Vector2(-borderThickness, -borderThickness);

        // ---- รูปตัวละคร ----
        var portrait = new GameObject("Portrait", typeof(RectTransform));
        portrait.transform.SetParent(inner.transform, false);

        m_Portrait = portrait.AddComponent<Image>();
        m_Portrait.preserveAspect = true;
        m_Portrait.raycastTarget = false;
        m_Portrait.enabled = false;   // ยังไม่มีรูป ซ่อนไว้ก่อน

        float inside = size - borderThickness * 2f;

        var portraitRT = (RectTransform)portrait.transform;
        portraitRT.anchorMin = portraitRT.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRT.pivot = new Vector2(0.5f, 0.5f);
        portraitRT.sizeDelta = new Vector2(inside * zoom, inside * zoom);
        portraitRT.anchoredPosition = portraitOffset;
    }
}
