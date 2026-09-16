using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// เมนูตั้งค่า — ปุ่มฟันเฟืองมุมขวาล่าง
///
/// แบ่งเป็น 4 หัวข้อ: ตั้งค่า / ควบคุม / เซฟ / รีสตาร์ท
/// กด Esc เปิดปิดก็ได้ (ตอนไม่มีหน้าต่างอื่นเปิดอยู่)
///
/// ปุ่ม Tab = ปลดเมาส์ออกไปกดอย่างอื่นนอกเกม กดซ้ำเพื่อกลับมาเล่น
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    public static SettingsMenuUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    /// <summary>เมาส์ถูกปลดออกไปใช้กับหน้าต่างอื่นอยู่ไหม</summary>
    public static bool CursorFree { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsOpen = false;
        CursorFree = false;
        if (Time.timeScale <= 0f) Time.timeScale = 1f;
    }

    private const string VolumeKey = "ExoticFarm.Volume";

    [Header("สี")]
    public Color panelColor = new Color(0.10f, 0.12f, 0.10f, 0.98f);
    public Color borderColor = new Color(0.85f, 0.70f, 0.32f, 1f);
    public Color tabActiveColor = new Color(0.34f, 0.52f, 0.28f, 1f);
    public Color tabIdleColor = new Color(0.20f, 0.20f, 0.19f, 0.95f);
    public Color buttonColor = new Color(0.28f, 0.42f, 0.58f, 1f);
    public Color dangerColor = new Color(0.55f, 0.26f, 0.22f, 1f);

    private enum Tab { Settings, Controls, Save, Restart }

    private GameObject m_Root;
    private RectTransform m_Content;
    private Text m_StatusText;
    private Text m_CursorHint;
    private Font m_Font;
    private Sprite m_Rounded;

    private readonly List<Button> m_TabButtons = new List<Button>();
    private readonly List<GameObject> m_ContentItems = new List<GameObject>();
    private Tab m_Tab = Tab.Settings;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        m_Font = UIFont.Get();
        m_Rounded = UIShapes.RoundedRect(22);

        BuildUI();

        m_Root.SetActive(false);
        IsOpen = false;

        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        ApplyCursor();
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

        m_StatusText.text = "";
        ShowTab(m_Tab);
        ApplyCursor();
    }

    public void Close()
    {
        m_Root.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;
        ApplyCursor();
    }

    // ================= เมาส์ =================

    public void ToggleCursor()
    {
        CursorFree = !CursorFree;
        ApplyCursor();
    }

    private void ApplyCursor()
    {
        // อิสระ = ออกไปนอกหน้าต่างเกมได้ / ล็อค = อยู่ในหน้าต่างเกมเท่านั้น
        bool free = CursorFree || IsOpen;

        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Confined;
        Cursor.visible = true;

        if (m_CursorHint != null)
            m_CursorHint.enabled = CursorFree && !IsOpen;
    }

    // ================= ปุ่มลัด =================

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Tab = ปลด / ล็อคเมาส์
        if (kb.tabKey.wasPressedThisFrame && !IsOpen && !AdminModeUI.IsOpen)
            ToggleCursor();

        if (!kb.escapeKey.wasPressedThisFrame) return;

        if (IsOpen)
        {
            Close();
            return;
        }

        // เปิดเมนูได้เฉพาะตอนไม่มีหน้าต่างอื่นค้างอยู่
        bool busy = DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
                    || RewardPopupUI.IsOpen || CutsceneUI.IsPlaying
                    || TitleScreenUI.IsOpen || NameEntryUI.IsOpen
                    || DayTransitionUI.IsPlaying || AnimalInfoUI.IsOpen
                    || EncyclopediaUI.IsOpen || AdminModeUI.IsOpen;

        if (!busy) Open();
    }

    // ================= เนื้อหาแต่ละหัวข้อ =================

    private void ShowTab(Tab tab)
    {
        m_Tab = tab;

        for (int i = 0; i < m_TabButtons.Count; i++)
        {
            var img = m_TabButtons[i].targetGraphic as Image;
            if (img != null) img.color = (int)tab == i ? tabActiveColor : tabIdleColor;
        }

        foreach (var item in m_ContentItems) Destroy(item);
        m_ContentItems.Clear();

        switch (tab)
        {
            case Tab.Settings: BuildSettingsTab(); break;
            case Tab.Controls: BuildControlsTab(); break;
            case Tab.Save: BuildSaveTab(); break;
            default: BuildRestartTab(); break;
        }
    }

    private void BuildSettingsTab()
    {
        AddHeading("ตั้งค่าทั่วไป");

        // ---- เสียง ----
        AddLabel($"ระดับเสียง:  {Mathf.RoundToInt(AudioListener.volume * 100f)}%");

        var volumeRow = AddRow();
        AddRowButton(volumeRow, "− เบาลง", buttonColor, () => ChangeVolume(-0.1f));
        AddRowButton(volumeRow, "+ ดังขึ้น", buttonColor, () => ChangeVolume(0.1f));
        AddRowButton(volumeRow, "ปิดเสียง", tabIdleColor, () => SetVolume(0f));

        AddSpace(14f);

        // ---- หน้าจอ ----
        AddLabel(Screen.fullScreen ? "โหมดจอ:  เต็มจอ" : "โหมดจอ:  หน้าต่าง");

        var screenRow = AddRow();
        AddRowButton(screenRow, "สลับเต็มจอ / หน้าต่าง", buttonColor, () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            ShowTab(Tab.Settings);
        });

        AddSpace(14f);
        AddLabel($"ชื่อตัวละคร:  {PlayerProfile.Name}");
    }

    private void BuildControlsTab()
    {
        AddHeading("ปุ่มควบคุม");

        AddLabel("W A S D  /  ลูกศร      เดิน");
        AddLabel("คลิกซ้าย  (กดค้างได้)     ไถ ปลูก รดน้ำ เก็บเกี่ยว");
        AddLabel("Space  หรือ  E          ใช้อุปกรณ์ (ปุ่มสำรอง)");
        AddLabel("1 - 8                   เลือกช่องอุปกรณ์");
        AddLabel("I                       เปิดกระเป๋า");
        AddLabel("E  (ตอนใกล้ NPC)         คุยกับ NPC");
        AddLabel("F  (ตอนใกล้ NPC)         เปิดร้าน ซื้อ-ขาย");
        AddLabel("N                       นอน ข้ามไปวันถัดไป");
        AddLabel("Tab                     ปลด / ล็อคเมาส์");
        AddLabel("Esc                     เปิดเมนูนี้ / ปิดหน้าต่าง");
    }

    private void BuildSaveTab()
    {
        AddHeading("บันทึกเกม");

        AddLabel("บันทึกจะจำตำแหน่งที่ตัวละครยืนอยู่ตอนกด");
        AddLabel("พร้อมกับวัน ของในกระเป๋า เงิน แปลงผัก และเควส");
        AddSpace(16f);

        AddLabel(SaveSystem.Describe());
        AddSpace(16f);

        var row = AddRow();
        AddRowButton(row, "บันทึกตอนนี้", tabActiveColor, () =>
        {
            m_StatusText.text = SaveSystem.Save()
                ? "บันทึกเรียบร้อยแล้ว"
                : "บันทึกไม่สำเร็จ ดูรายละเอียดใน Console";
            ShowTab(Tab.Save);
        });

        if (SaveSystem.HasSave)
        {
            AddRowButton(row, "โหลดเซฟล่าสุด", buttonColor, () =>
            {
                if (SaveSystem.Load())
                {
                    m_StatusText.text = "โหลดเซฟแล้ว";
                    Close();
                }
                else m_StatusText.text = "โหลดไม่สำเร็จ";
            });
        }
    }

    private void BuildRestartTab()
    {
        AddHeading("เริ่มใหม่");

        if (SaveSystem.HasSave)
        {
            AddLabel("กลับไปจุดที่บันทึกไว้ล่าสุด");
            AddLabel(SaveSystem.Describe());
            AddSpace(12f);

            var row = AddRow();
            AddRowButton(row, "วาร์ปกลับจุดเซฟ", tabActiveColor, () =>
            {
                if (SaveSystem.Load())
                {
                    m_StatusText.text = "กลับมาที่จุดเซฟแล้ว";
                    Close();
                }
                else m_StatusText.text = "โหลดไม่สำเร็จ";
            });

            AddSpace(22f);
        }
        else
        {
            AddLabel("ยังไม่มีไฟล์เซฟ — ไปกดบันทึกในหัวข้อ 'เซฟ' ก่อน");
            AddSpace(22f);
        }

        AddLabel("เริ่มใหม่ทั้งหมด จะลบไฟล์เซฟและเริ่มจากวันที่ 1");
        AddLabel("ของในกระเป๋า เงิน และเควสจะหายหมด");
        AddSpace(12f);

        var dangerRow = AddRow();
        AddRowButton(dangerRow, "เริ่มใหม่ทั้งหมด", dangerColor, RestartFromScratch);
    }

    private void RestartFromScratch()
    {
        SaveSystem.Delete();
        PlayerProfile.Forget();
        IntroCutscene.ForgetSeen();

        Time.timeScale = 1f;
        IsOpen = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ChangeVolume(float delta) => SetVolume(AudioListener.volume + delta);

    private void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);

        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();

        ShowTab(Tab.Settings);
    }

    // ================= ตัวช่วยสร้างเนื้อหา =================

    private void AddHeading(string text)
    {
        var t = MakeText(m_Content, 36, TextAnchor.UpperLeft, borderColor);
        t.text = text;

        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;

        m_ContentItems.Add(t.gameObject);
    }

    private void AddLabel(string text)
    {
        var t = MakeText(m_Content, 26, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.88f));
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;

        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = text.Contains("\n") ? 66f : 36f;

        m_ContentItems.Add(t.gameObject);
    }

    private void AddSpace(float height)
    {
        var go = new GameObject("Space", typeof(RectTransform));
        go.transform.SetParent(m_Content, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        m_ContentItems.Add(go);
    }

    private Transform AddRow()
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(m_Content, false);

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 62f;
        le.minHeight = 62f;

        m_ContentItems.Add(go);
        return go.transform;
    }

    private void AddRowButton(Transform row, string label, Color color,
                              UnityEngine.Events.UnityAction action)
    {
        var button = MakeButton(row, label, color, 0f, 58f);

        var le = button.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 44f + label.Length * 13f;
        le.minWidth = le.preferredWidth;

        button.onClick.AddListener(action);
    }

    // ================= สร้างโครง UI =================

    private void BuildUI()
    {
        var canvasGO = new GameObject("SettingsCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.dynamicPixelsPerUnit = 3f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- ปุ่มฟันเฟือง มุมขวาล่าง ----
        var gearGO = new GameObject("GearButton", typeof(RectTransform));
        gearGO.transform.SetParent(canvasGO.transform, false);

        var gearImage = gearGO.AddComponent<Image>();
        gearImage.sprite = UIShapes.Gear();
        gearImage.color = new Color(0.92f, 0.88f, 0.78f, 0.9f);

        var gearButton = gearGO.AddComponent<Button>();
        gearButton.targetGraphic = gearImage;
        gearButton.onClick.AddListener(Toggle);

        var gearColors = gearButton.colors;
        gearColors.normalColor = new Color(1f, 1f, 1f, 0.85f);
        gearColors.highlightedColor = Color.white;
        gearColors.pressedColor = new Color(0.75f, 0.75f, 0.7f, 1f);
        gearButton.colors = gearColors;

        var gearRT = (RectTransform)gearGO.transform;
        gearRT.anchorMin = gearRT.anchorMax = new Vector2(1f, 0f);
        gearRT.pivot = new Vector2(1f, 0f);
        gearRT.anchoredPosition = new Vector2(-26f, 26f);
        gearRT.sizeDelta = new Vector2(76f, 76f);

        // ---- คำใบ้ตอนปลดเมาส์ ----
        m_CursorHint = MakeText(canvasGO.transform, 24, TextAnchor.LowerCenter,
                                new Color(1f, 0.95f, 0.7f, 0.9f));
        m_CursorHint.text = "เมาส์อิสระอยู่ — กด Tab เพื่อกลับมาเล่น";
        var hintRT = m_CursorHint.rectTransform;
        hintRT.anchorMin = new Vector2(0f, 1f);
        hintRT.anchorMax = new Vector2(1f, 1f);
        hintRT.pivot = new Vector2(0.5f, 1f);
        hintRT.anchoredPosition = new Vector2(0f, -14f);
        hintRT.sizeDelta = new Vector2(0f, 34f);
        m_CursorHint.enabled = false;

        // ---- หน้าต่างเมนู ----
        m_Root = new GameObject("SettingsRoot", typeof(RectTransform));
        m_Root.transform.SetParent(canvasGO.transform, false);

        var dim = m_Root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
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
        frameRT.sizeDelta = new Vector2(1060f, 620f);

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(frame.transform, false);

        var innerImage = inner.AddComponent<Image>();
        innerImage.sprite = m_Rounded;
        innerImage.type = Image.Type.Sliced;
        innerImage.color = panelColor;
        Stretch((RectTransform)inner.transform, 6f);

        // ---- แถบหัวข้อฝั่งซ้าย ----
        var tabs = new GameObject("Tabs", typeof(RectTransform));
        tabs.transform.SetParent(inner.transform, false);

        var tabsRT = (RectTransform)tabs.transform;
        tabsRT.anchorMin = new Vector2(0f, 0f);
        tabsRT.anchorMax = new Vector2(0f, 1f);
        tabsRT.pivot = new Vector2(0f, 1f);
        tabsRT.anchoredPosition = new Vector2(24f, -24f);
        tabsRT.sizeDelta = new Vector2(250f, -48f);

        var tabsLayout = tabs.AddComponent<VerticalLayoutGroup>();
        tabsLayout.spacing = 12f;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = false;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;

        AddTabButton(tabs.transform, "ตั้งค่า", Tab.Settings);
        AddTabButton(tabs.transform, "ควบคุม", Tab.Controls);
        AddTabButton(tabs.transform, "เซฟ", Tab.Save);
        AddTabButton(tabs.transform, "รีสตาร์ท", Tab.Restart);

        // ---- เนื้อหาฝั่งขวา ----
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(inner.transform, false);

        m_Content = (RectTransform)content.transform;
        m_Content.anchorMin = new Vector2(0f, 0f);
        m_Content.anchorMax = new Vector2(1f, 1f);
        m_Content.pivot = new Vector2(0f, 1f);
        m_Content.offsetMin = new Vector2(296f, 86f);
        m_Content.offsetMax = new Vector2(-28f, -24f);

        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childAlignment = TextAnchor.UpperLeft;

        // ---- ข้อความแจ้งผล ----
        m_StatusText = MakeText(inner.transform, 26, TextAnchor.LowerLeft,
                                new Color(0.65f, 0.9f, 0.6f, 1f));
        var statusRT = m_StatusText.rectTransform;
        statusRT.anchorMin = new Vector2(0f, 0f);
        statusRT.anchorMax = new Vector2(1f, 0f);
        statusRT.pivot = new Vector2(0f, 0f);
        statusRT.anchoredPosition = new Vector2(296f, 26f);
        statusRT.sizeDelta = new Vector2(-320f, 34f);

        // ---- ปุ่มปิด ----
        var close = MakeButton(inner.transform, "ปิด  (Esc)", tabIdleColor, 200f, 56f);
        var closeRT = (RectTransform)close.transform;
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 0f);
        closeRT.pivot = new Vector2(1f, 0f);
        closeRT.anchoredPosition = new Vector2(-24f, 22f);
        close.onClick.AddListener(Close);
    }

    private void AddTabButton(Transform parent, string label, Tab tab)
    {
        var button = MakeButton(parent, label, tabIdleColor, 0f, 66f);

        var le = button.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 66f;
        le.minHeight = 66f;

        button.onClick.AddListener(() => ShowTab(tab));
        m_TabButtons.Add(button);
    }

    // ================= ตัวช่วยพื้นฐาน =================

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private Button MakeButton(Transform parent, string label, Color color, float width, float height)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.sprite = m_Rounded;
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
