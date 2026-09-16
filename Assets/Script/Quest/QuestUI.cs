using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แถบเควสฝั่งซ้าย + ลูกศรชี้ไปหา NPC
///
/// สร้าง UI ด้วยโค้ดทั้งหมด วางไว้บน GameObject ชื่อ "QuestManager" ได้เลย
/// </summary>
public class QuestUI : MonoBehaviour
{
    [Header("เป้าหมายที่ลูกศรจะชี้ไป")]
    [Tooltip("ลาก NPC ที่แจกเควสมาใส่ (Wizard ใส่ให้อัตโนมัติ)")]
    public Transform pointTarget;

    [Header("แถบเควสฝั่งซ้าย")]
    [Tooltip("ตำแหน่งนับจากขอบซ้าย กึ่งกลางความสูงของจอ")]
    public Vector2 panelPosition = new Vector2(28f, 0f);
    public float panelWidth = 420f;
    public Color panelColor = new Color(0.07f, 0.07f, 0.08f, 0.80f);
    public Color titleColor = new Color(1f, 0.85f, 0.35f, 1f);

    [Header("ลูกศร")]
    public Color arrowColor = new Color(1f, 0.85f, 0.25f, 0.95f);
    public float arrowSize = 56f;
    [Tooltip("ระยะห่างจากขอบจอตอน NPC อยู่นอกจอ")]
    public float edgeMargin = 90f;
    [Tooltip("ลูกศรลอยสูงจากหัว NPC เท่าไหร่ (พิกเซล)")]
    public float aboveHeadOffset = 90f;

    private Canvas m_Canvas;
    private Text m_TitleText;
    private Text m_BodyText;
    private RectTransform m_ArrowRT;
    private Image m_ArrowImage;
    private Camera m_Camera;
    private string m_NpcName = "NPC";

    private void Start()
    {
        m_Camera = Camera.main;

        var npc = pointTarget != null ? pointTarget.GetComponent<NPCInteractable>() : null;
        if (npc != null) m_NpcName = npc.npcName;

        BuildUI();

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestChanged += Redraw;
            Redraw();
        }
        else
        {
            Debug.LogError("[QuestUI] ไม่พบ QuestManager ใน Scene");
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestChanged -= Redraw;
    }

    private void LateUpdate()
    {
        UpdateArrow();
    }

    // ================= ลูกศร =================

    private void UpdateArrow()
    {
        var qm = QuestManager.Instance;

        bool show = qm != null
                    && qm.ShouldPointToNPC
                    && pointTarget != null
                    && m_Camera != null
                    && !DialogueUI.IsOpen
                    && !InventoryUI.IsBackpackOpen
                    && !CutsceneUI.IsPlaying
                    && !TitleScreenUI.IsOpen;

        if (m_ArrowImage != null && m_ArrowImage.enabled != show)
            m_ArrowImage.enabled = show;

        if (!show) return;

        float scale = m_Canvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        Vector2 targetScreen = m_Camera.WorldToScreenPoint(pointTarget.position);

        float margin = edgeMargin;
        bool onScreen = targetScreen.x > margin && targetScreen.x < Screen.width - margin
                     && targetScreen.y > margin && targetScreen.y < Screen.height - margin;

        Vector2 arrowScreen;
        if (onScreen)
        {
            // อยู่ในจอ -> ลอยเหนือหัว แล้วเด้งขึ้นลง
            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 10f;
            arrowScreen = targetScreen + Vector2.up * (aboveHeadOffset + bob);
        }
        else
        {
            // อยู่นอกจอ -> ติดขอบจอ
            arrowScreen = new Vector2(
                Mathf.Clamp(targetScreen.x, margin, Screen.width - margin),
                Mathf.Clamp(targetScreen.y, margin, Screen.height - margin));
        }

        m_ArrowRT.anchoredPosition = arrowScreen / scale;

        // หมุนให้ชี้ไปหาเป้าหมาย (รูปลูกศรชี้ขึ้นเป็นค่าเริ่มต้น)
        Vector2 dir = targetScreen - arrowScreen;
        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            m_ArrowRT.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // ================= แถบเควส =================

    private void Redraw()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        m_TitleText.text = qm.GetTrackerTitle();
        m_BodyText.text = qm.GetTrackerBody(m_NpcName);
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var font = UIFont.Get();

        var canvasGO = new GameObject("QuestCanvas");
        canvasGO.transform.SetParent(transform, false);

        m_Canvas = canvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 5;   // อยู่ใต้ Hotbar และกล่องคุย

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;   // วาดฟอนต์ละเอียด 3 เท่า -> ตัวอักษรคมขึ้น

        // ---- แถบเควสฝั่งซ้าย กลางจอ ----
        // ยึดกับกึ่งกลางความสูงของจอ มุมซ้ายบนเว้นไว้ให้รูปตัวละคร
        var panel = new GameObject("QuestPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;

        var panelRT = (RectTransform)panel.transform;
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0f, 0.5f);
        panelRT.pivot = new Vector2(0f, 0.5f);
        panelRT.anchoredPosition = panelPosition;
        panelRT.sizeDelta = new Vector2(panelWidth, 150f);

        // เส้นสีทองด้านซ้าย
        var stripe = new GameObject("Stripe", typeof(RectTransform));
        stripe.transform.SetParent(panel.transform, false);

        var stripeImage = stripe.AddComponent<Image>();
        stripeImage.color = titleColor;

        var stripeRT = (RectTransform)stripe.transform;
        stripeRT.anchorMin = new Vector2(0f, 0f);
        stripeRT.anchorMax = new Vector2(0f, 1f);
        stripeRT.pivot = new Vector2(0f, 0.5f);
        stripeRT.sizeDelta = new Vector2(6f, 0f);
        stripeRT.anchoredPosition = Vector2.zero;

        m_TitleText = MakeText(panel.transform, font, 32, TextAnchor.UpperLeft, titleColor);
        var titleRT = m_TitleText.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.offsetMin = new Vector2(22f, -54f);
        titleRT.offsetMax = new Vector2(-14f, -14f);

        m_BodyText = MakeText(panel.transform, font, 30, TextAnchor.UpperLeft, Color.white);
        m_BodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var bodyRT = m_BodyText.rectTransform;
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(22f, 12f);
        bodyRT.offsetMax = new Vector2(-14f, -60f);

        // ---- ลูกศร ----
        var arrowGO = new GameObject("QuestArrow", typeof(RectTransform));
        arrowGO.transform.SetParent(canvasGO.transform, false);

        m_ArrowImage = arrowGO.AddComponent<Image>();
        m_ArrowImage.sprite = MakeArrowSprite();
        m_ArrowImage.color = arrowColor;
        m_ArrowImage.raycastTarget = false;

        m_ArrowRT = (RectTransform)arrowGO.transform;
        m_ArrowRT.anchorMin = m_ArrowRT.anchorMax = Vector2.zero;
        m_ArrowRT.pivot = new Vector2(0.5f, 0.5f);
        m_ArrowRT.sizeDelta = new Vector2(arrowSize, arrowSize);

        m_ArrowImage.enabled = false;
    }

    /// <summary>วาดรูปสามเหลี่ยมชี้ขึ้นด้วยโค้ด ไม่ต้องมีไฟล์รูป</summary>
    private static Sprite MakeArrowSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            // t = 0 ที่ฐาน, 1 ที่ยอด
            float t = y / (float)(size - 1);
            float halfWidth = (1f - t) * (size * 0.5f);

            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - (size - 1) * 0.5f);
                tex.SetPixel(x, y, dx <= halfWidth ? Color.white : Color.clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font = font;
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
