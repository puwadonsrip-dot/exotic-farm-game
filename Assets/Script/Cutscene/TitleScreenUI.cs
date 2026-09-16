using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าเมนูหลัก — ขึ้นเป็นอย่างแรกทันทีที่กด Play
///
/// ลำดับ:  หน้าเมนู  →  คัตซีนเปิดเกม  →  เริ่มเล่น
///
/// สร้าง UI ด้วยโค้ดทั้งหมด วางไว้บน GameObject ชื่อ "TitleScreen"
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    public static TitleScreenUI Instance { get; private set; }

    /// <summary>ตอนนี้หน้าเมนูเปิดอยู่ไหม</summary>
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsOpen = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    [Header("ภาพพื้นหลัง")]
    [Tooltip("ปล่อยว่างได้ — ตัวติดตั้งจะใส่ภาพฉากแรกให้เอง")]
    public Texture2D background;

    [Header("ชื่อเกม")]
    [Tooltip("โชว์เฉพาะตอนไม่มีภาพพื้นหลัง (ถ้ามีภาพ ในภาพมีโลโก้อยู่แล้ว)")]
    public string gameTitle = "EXOTIC FARM";

    [Header("สี")]
    public Color primaryButtonColor = new Color(0.34f, 0.55f, 0.26f, 1f);
    public Color secondaryButtonColor = new Color(0.30f, 0.28f, 0.26f, 0.95f);
    public Color quitButtonColor = new Color(0.40f, 0.24f, 0.22f, 0.95f);

    private GameObject m_Root;
    private Font m_Font;

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

        Open();
    }

    // ================= เปิด / ปิด =================

    public void Open()
    {
        m_Root.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;   // หยุดเกมไว้ก่อน ยังไม่เริ่มเล่น
    }

    private void Close()
    {
        m_Root.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;
    }

    // ================= ปุ่มต่างๆ =================

    /// <summary>เริ่มเกมใหม่ — ดูคัตซีนก่อน จบแล้วค่อยตั้งชื่อ แล้วเข้าเกม</summary>
    public void StartNewGame()
    {
        Close();

        var intro = FindFirstObjectByType<IntroCutscene>(FindObjectsInactive.Include);
        if (intro == null)
        {
            Debug.LogWarning("[Title] ไม่พบ IntroCutscene — ข้ามไปตั้งชื่อเลย");
            AskName();
            return;
        }

        intro.PlayNow(AskName);
    }

    /// <summary>เล่นต่อจากไฟล์เซฟ — ไม่ต้องดูคัตซีนหรือตั้งชื่อใหม่</summary>
    public void ContinueGame()
    {
        Close();

        if (SaveSystem.Load()) return;

        Debug.LogWarning("[Title] โหลดเซฟไม่สำเร็จ — เข้าเกมแบบเริ่มใหม่แทน");
        AskName();
    }

    /// <summary>เข้าเกมเลย ไม่ดูคัตซีน แต่ยังต้องตั้งชื่อก่อน</summary>
    public void SkipToGame()
    {
        Close();
        AskName();
    }

    /// <summary>ขึ้นหน้าตั้งชื่อ ถ้าไม่มีหน้านั้นก็เข้าเกมเลย</summary>
    private void AskName()
    {
        if (NameEntryUI.Instance == null)
        {
            Debug.LogWarning("[Title] ไม่พบ NameEntryUI — ข้ามการตั้งชื่อ");
            return;
        }

        NameEntryUI.Instance.Show(null);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ================= ปุ่มลัด =================

    private void Update()
    {
        if (!IsOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // กด Enter หรือ Space = เริ่มเกมใหม่
        if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            StartNewGame();
    }

    // ================= สร้าง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("TitleCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;   // อยู่บนสุด เหนือคัตซีน

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        m_Root = new GameObject("TitleRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var baseColor = m_Root.AddComponent<Image>();
        baseColor.color = new Color(0.06f, 0.09f, 0.07f, 1f);
        Stretch((RectTransform)m_Root.transform);

        // ---- ภาพพื้นหลัง ----
        if (background != null)
        {
            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(m_Root.transform, false);

            var raw = bgGO.AddComponent<RawImage>();
            raw.texture = background;
            raw.raycastTarget = false;

            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);

            // Envelope = ขยายให้เต็มจอ ส่วนที่เกินยอมให้ล้นออกไป ภาพจะได้ไม่ยืด
            var fitter = bgGO.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.height > 0
                ? (float)background.width / background.height
                : 1.78f;

            // ดำจางๆ ทั้งจอ ให้ภาพไม่แย่งสายตากับปุ่ม
            var shade = new GameObject("Shade", typeof(RectTransform));
            shade.transform.SetParent(m_Root.transform, false);

            var shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0.02f, 0.03f, 0.02f, 0.3f);
            shadeImage.raycastTarget = false;
            Stretch((RectTransform)shade.transform);

            // ดำเข้มขึ้นเฉพาะครึ่งล่าง ให้ปุ่มลอยเด่นออกมา
            var band = new GameObject("BottomShade", typeof(RectTransform));
            band.transform.SetParent(m_Root.transform, false);

            var bandImage = band.AddComponent<Image>();
            bandImage.color = new Color(0.02f, 0.03f, 0.02f, 0.45f);
            bandImage.raycastTarget = false;

            var bandRT = (RectTransform)band.transform;
            bandRT.anchorMin = new Vector2(0f, 0f);
            bandRT.anchorMax = new Vector2(1f, 0.46f);
            bandRT.offsetMin = Vector2.zero;
            bandRT.offsetMax = Vector2.zero;
        }

        // ---- ชื่อเกม ----
        var title = MakeText(m_Root.transform, 130, TextAnchor.MiddleCenter,
                             new Color(0.98f, 0.86f, 0.42f, 1f));
        title.text = gameTitle;

        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 0.56f);
        titleRT.anchorMax = new Vector2(1f, 0.92f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        // ขอบดำหนารอบตัวอักษร ให้ลอยเด่นออกจากภาพพื้นหลัง
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.09f, 0.06f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(4f, 4f);

        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        titleShadow.effectDistance = new Vector2(0f, -8f);

        // ---- แถวปุ่ม ----
        var menu = new GameObject("Menu", typeof(RectTransform));
        menu.transform.SetParent(m_Root.transform, false);

        var menuRT = (RectTransform)menu.transform;
        menuRT.anchorMin = new Vector2(0.5f, 0f);
        menuRT.anchorMax = new Vector2(0.5f, 0f);
        menuRT.pivot = new Vector2(0.5f, 0f);
        menuRT.anchoredPosition = new Vector2(0f, 96f);
        menuRT.sizeDelta = new Vector2(560f, 320f);

        var layout = menu.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childAlignment = TextAnchor.LowerCenter;

        // มีไฟล์เซฟ = ให้เล่นต่อได้เลย ไม่ต้องเริ่มใหม่
        if (SaveSystem.HasSave)
            MakeMenuButton(menu.transform, "เล่นต่อ", primaryButtonColor, 40, ContinueGame);

        MakeMenuButton(menu.transform, "เริ่มเกมใหม่",
                       SaveSystem.HasSave ? secondaryButtonColor : primaryButtonColor,
                       SaveSystem.HasSave ? 30 : 40, StartNewGame);

        MakeMenuButton(menu.transform, "เข้าเกมเลย  (ข้ามเรื่องราว)", secondaryButtonColor, 30, SkipToGame);
        MakeMenuButton(menu.transform, "ออกจากเกม", quitButtonColor, 30, QuitGame);

        // ---- คำใบ้ ----
        var hint = MakeText(m_Root.transform, 24, TextAnchor.LowerCenter,
                            new Color(1f, 1f, 1f, 0.55f));
        hint.text = "กด Enter เพื่อเริ่มเกมใหม่";
        var hintRT = hint.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.anchoredPosition = new Vector2(0f, 34f);
        hintRT.sizeDelta = new Vector2(0f, 34f);
    }

    /// <summary>
    /// ปุ่มมุมโค้ง มีเงาทิ้ง มีขอบ และไล่สีตอนเอาเมาส์ชี้
    ///
    /// ซ้อนกัน 3 ชั้น:  เงา (หลังสุด) → ขอบ → พื้นปุ่ม → ตัวอักษร
    /// </summary>
    private void MakeMenuButton(Transform parent, string label, Color color,
                                int fontSize, UnityEngine.Events.UnityAction action)
    {
        bool isPrimary = fontSize >= 40;
        int radius = isPrimary ? 26 : 22;
        var rounded = UIShapes.RoundedRect(radius);

        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = isPrimary ? 88f : 68f;
        layoutElement.minHeight = layoutElement.preferredHeight;

        // ---- เงาทิ้งด้านล่าง ----
        var shadow = new GameObject("Shadow", typeof(RectTransform));
        shadow.transform.SetParent(go.transform, false);

        var shadowImage = shadow.AddComponent<Image>();
        shadowImage.sprite = rounded;
        shadowImage.type = Image.Type.Sliced;
        shadowImage.color = new Color(0f, 0f, 0f, 0.4f);
        shadowImage.raycastTarget = false;

        var shadowRT = (RectTransform)shadow.transform;
        shadowRT.anchorMin = Vector2.zero;
        shadowRT.anchorMax = Vector2.one;
        shadowRT.offsetMin = new Vector2(0f, -6f);
        shadowRT.offsetMax = new Vector2(0f, -6f);

        // ---- ขอบปุ่ม (เป็นตัวรับคลิก) ----
        var border = go.AddComponent<Image>();
        border.sprite = rounded;
        border.type = Image.Type.Sliced;
        border.color = Darken(color, 0.45f);

        // ---- พื้นปุ่ม ----
        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(go.transform, false);

        var fillImage = fill.AddComponent<Image>();
        fillImage.sprite = rounded;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;
        Stretch((RectTransform)fill.transform, 4f);

        // ---- ปุ่ม + สีตอนชี้/กด ----
        var button = go.AddComponent<Button>();
        button.targetGraphic = fillImage;
        button.transition = Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Lighten(color, 0.18f);
        colors.pressedColor = Darken(color, 0.2f);
        colors.selectedColor = color;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.AddListener(action);

        // ---- ตัวอักษร + เงาอ่อนๆ ให้อ่านชัด ----
        var text = MakeText(fill.transform, fontSize, TextAnchor.MiddleCenter, Color.white);
        text.text = label;
        Stretch(text.rectTransform);

        var textShadow = text.gameObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        textShadow.effectDistance = new Vector2(0f, -2f);
    }

    private static Color Darken(Color c, float amount)
        => new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

    private static Color Lighten(Color c, float amount)
        => new Color(Mathf.Lerp(c.r, 1f, amount),
                     Mathf.Lerp(c.g, 1f, amount),
                     Mathf.Lerp(c.b, 1f, amount), c.a);

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
