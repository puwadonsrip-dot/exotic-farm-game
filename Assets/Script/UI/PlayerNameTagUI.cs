using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ป้ายชื่อลอยเหนือหัวตัวละคร
///
/// ใช้ Canvas แบบทับจอ แล้วคำนวณตำแหน่งตามตัวละครทุกเฟรม
/// ข้อดีคือตัวอักษรคมและขนาดคงที่ ไม่ว่ากล้องจะซูมเข้าออกแค่ไหน
/// </summary>
public class PlayerNameTagUI : MonoBehaviour
{
    [Header("ตำแหน่ง")]
    [Tooltip("ลอยสูงจากตัวละครกี่พิกเซลบนจอ")]
    public float heightAboveHead = 62f;

    [Header("ตัวอักษร")]
    public int fontSize = 26;
    public Color textColor = new Color(1f, 0.97f, 0.85f, 1f);
    public Color outlineColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("ซ่อนตอนไหน")]
    [Tooltip("ซ่อนป้ายตอนมีหน้าต่างเปิดอยู่")]
    public bool hideWhenUIOpen = true;

    private Canvas m_Canvas;
    private Text m_Text;
    private RectTransform m_TextRT;
    private Transform m_Player;
    private Camera m_Camera;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) m_Player = playerGO.transform;

        m_Camera = Camera.main;

        ApplyName(PlayerProfile.Name);
        PlayerProfile.OnNameChanged += ApplyName;
    }

    private void OnDestroy()
    {
        PlayerProfile.OnNameChanged -= ApplyName;
    }

    private void ApplyName(string value)
    {
        if (m_Text != null) m_Text.text = value;
    }

    private void LateUpdate()
    {
        if (m_Player == null || m_Text == null) return;

        if (m_Camera == null)
        {
            m_Camera = Camera.main;
            if (m_Camera == null) return;
        }

        bool hide = hideWhenUIOpen &&
                    (CutsceneUI.IsPlaying || TitleScreenUI.IsOpen || NameEntryUI.IsOpen
                     || DayTransitionUI.IsPlaying || ShopUI.IsOpen
                     || InventoryUI.IsBackpackOpen);

        if (m_Text.enabled == hide) m_Text.enabled = !hide;
        if (hide) return;

        float scale = m_Canvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        Vector2 screen = m_Camera.WorldToScreenPoint(m_Player.position);
        screen.y += heightAboveHead;

        m_TextRT.anchoredPosition = screen / scale;
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("NameTagCanvas");
        canvasGO.transform.SetParent(transform, false);

        m_Canvas = canvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 12;   // เหนือฉาก แต่ต่ำกว่า HUD อื่น

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        var go = new GameObject("NameTag", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);

        m_Text = go.AddComponent<Text>();
        m_Text.font = UIFont.Get();
        m_Text.fontSize = fontSize;
        m_Text.fontStyle = FontStyle.Bold;
        m_Text.alignment = TextAnchor.MiddleCenter;
        m_Text.color = textColor;
        m_Text.raycastTarget = false;
        m_Text.horizontalOverflow = HorizontalWrapMode.Overflow;
        m_Text.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        m_TextRT = m_Text.rectTransform;
        m_TextRT.anchorMin = m_TextRT.anchorMax = Vector2.zero;   // นับจากมุมซ้ายล่างของจอ
        m_TextRT.pivot = new Vector2(0.5f, 0.5f);
        m_TextRT.sizeDelta = new Vector2(400f, 40f);
    }
}
