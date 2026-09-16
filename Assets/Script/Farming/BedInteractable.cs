using UnityEngine;

/// <summary>
/// เตียงนอน — เอาเมาส์คลิกที่เตียงเพื่อนอน ข้ามไปเช้าวันถัดไป
///
/// ใช้เมาส์แทนปุ่ม E เพราะ E ถูกใช้คุย NPC และให้อาหารสัตว์อยู่แล้ว
/// ต้องยืนใกล้เตียงถึงจะนอนได้ คลิกจากไกลๆ ไม่ได้
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BedInteractable : MonoBehaviour
{
    /// <summary>เมาส์ชี้อยู่บนเตียงไหม — PlayerToolController ใช้กันไม่ให้เผลอไถดินตอนคลิกเตียง</summary>
    public static bool MouseOverBed => Time.frameCount - s_HoverFrame <= 1;

    /// <summary>ยังคงไว้เผื่อสคริปต์อื่นเรียก — ตอนนี้เตียงไม่ใช้ปุ่ม E แล้ว</summary>
    public static bool PlayerNearBed => false;

    private static int s_HoverFrame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState() => s_HoverFrame = -10;

    [Header("ระยะที่นอนได้")]
    [Tooltip("ต้องยืนใกล้เตียงไม่เกินกี่ช่อง")]
    public float interactRange = 2.5f;

    [Header("สีตอนเอาเมาส์ชี้")]
    public Color hoverTint = new Color(1f, 0.95f, 0.7f, 1f);

    private Transform m_Player;
    private SpriteRenderer m_Renderer;
    private Color m_NormalColor;

    private void Awake()
    {
        m_Renderer = GetComponent<SpriteRenderer>();
        m_NormalColor = m_Renderer.color;

        EnsureCollider();
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) m_Player = playerGO.transform;
    }

    /// <summary>ต้องมี Collider เมาส์ถึงจะจิ้มโดน</summary>
    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;

        var box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;   // ไม่ให้ขวางทางเดินผู้เล่น

        if (m_Renderer.sprite != null)
            box.size = m_Renderer.sprite.bounds.size;
    }

    // ================= เมาส์ =================

    private void OnMouseEnter() => Hover(true);
    private void OnMouseExit() => Hover(false);

    private void OnMouseOver()
    {
        if (Busy()) return;
        s_HoverFrame = Time.frameCount;
    }

    private void Hover(bool on)
    {
        if (m_Renderer == null) return;

        m_Renderer.color = on && InRange() ? hoverTint : m_NormalColor;

        if (!on)
        {
            DialogueUI.Instance?.SetPrompt("");
            return;
        }

        if (Busy()) return;

        DialogueUI.Instance?.SetPrompt(InRange()
            ? "คลิกที่เตียงเพื่อนอน  —  ข้ามไปเช้าวันถัดไป"
            : "เดินเข้าไปใกล้เตียงก่อนถึงจะนอนได้");
    }

    private void OnMouseDown()
    {
        if (Busy()) return;

        if (!InRange())
        {
            DialogueUI.Instance?.SetPrompt("ยังอยู่ไกลเตียงเกินไป");
            return;
        }

        m_Renderer.color = m_NormalColor;
        DialogueUI.Instance?.SetPrompt("");
        Sleep();
    }

    // ================= นอน =================

    public void Sleep()
    {
        if (DayTransitionUI.Instance != null) DayTransitionUI.Instance.Sleep();
        else FarmManager.Instance?.AdvanceDay();
    }

    private bool InRange()
    {
        if (m_Player == null) return false;
        return Vector2.Distance(transform.position, m_Player.position) <= interactRange;
    }

    private static bool Busy()
    {
        return DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
            || CutsceneUI.IsPlaying || SettingsMenuUI.IsOpen
            || DayTransitionUI.IsPlaying || NameEntryUI.IsOpen
            || TitleScreenUI.IsOpen;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
