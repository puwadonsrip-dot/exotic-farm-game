using UnityEngine;

/// <summary>
/// แสดงของที่ถืออยู่ติดมือตัวละคร
///
/// อ่านจากช่องที่เลือกใน Hotbar แล้วเอาไอคอนมาวางข้างตัว
/// ขยับตำแหน่งตามทิศที่หันอยู่ และสลับหน้า/หลังให้ถูกต้อง
///
/// วางไว้บน GameObject 'Player'
/// </summary>
public class PlayerHeldItem : MonoBehaviour
{
    [Header("ขนาด")]
    [Tooltip("ของที่ถือสูงกี่ช่องในเกม")]
    public float displayHeight = 0.55f;

    [Header("ตำแหน่งตามทิศที่หัน")]
    [Tooltip("หันลง (เห็นหน้า)")]
    public Vector2 offsetDown = new Vector2(0.28f, -0.02f);

    [Tooltip("หันขึ้น (เห็นหลัง)")]
    public Vector2 offsetUp = new Vector2(-0.28f, 0.08f);

    [Tooltip("หันข้าง")]
    public Vector2 offsetSide = new Vector2(0.34f, 0.02f);

    [Header("ของที่ไม่ต้องโชว์")]
    [Tooltip("ถือมือเปล่าแล้วไม่ต้องแสดงอะไร")]
    public bool hideWhenEmpty = true;

    private SpriteRenderer m_PlayerRenderer;
    private SpriteRenderer m_HeldRenderer;
    private Animator m_Animator;
    private ItemData m_Shown;

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
        m_PlayerRenderer = GetComponentInChildren<SpriteRenderer>();

        BuildRenderer();
    }

    private void BuildRenderer()
    {
        var go = new GameObject("HeldItem");
        go.transform.SetParent(transform, false);

        m_HeldRenderer = go.AddComponent<SpriteRenderer>();
        m_HeldRenderer.enabled = false;

        if (m_PlayerRenderer != null)
            m_HeldRenderer.sortingLayerID = m_PlayerRenderer.sortingLayerID;
    }

    private void LateUpdate()
    {
        if (m_HeldRenderer == null) return;

        var item = InventorySystem.Instance != null
            ? InventorySystem.Instance.SelectedItem
            : null;

        bool show = item != null && item.icon != null
                    && (!hideWhenEmpty || item.kind != ItemKind.Feed);

        if (!show)
        {
            if (m_HeldRenderer.enabled) m_HeldRenderer.enabled = false;
            m_Shown = null;
            return;
        }

        if (item != m_Shown)
        {
            m_Shown = item;
            m_HeldRenderer.sprite = item.icon;
            m_HeldRenderer.enabled = true;

            ApplySize(item.icon);
        }

        PlaceByFacing();
    }

    /// <summary>ย่อ/ขยายให้ทุกไอเทมสูงเท่ากัน ไม่ว่าไฟล์รูปจะขนาดไหน</summary>
    private void ApplySize(Sprite sprite)
    {
        float height = sprite.bounds.size.y;
        if (height <= 0.001f) return;

        float scale = displayHeight / height;
        m_HeldRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>วางของให้อยู่ข้างมือตามทิศที่หัน</summary>
    private void PlaceByFacing()
    {
        Vector2 facing = GetFacing();

        Vector2 offset;
        bool behindPlayer = false;
        float flip = 1f;

        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
        {
            offset = offsetSide;
            flip = facing.x >= 0f ? 1f : -1f;
            offset.x *= flip;
        }
        else if (facing.y > 0f)
        {
            // หันขึ้น = เห็นหลังตัวละคร ของต้องอยู่หลังตัว
            offset = offsetUp;
            behindPlayer = true;
        }
        else
        {
            offset = offsetDown;
        }

        var held = m_HeldRenderer.transform;
        held.localPosition = offset;

        var scale = held.localScale;
        held.localScale = new Vector3(Mathf.Abs(scale.x) * flip, scale.y, 1f);

        if (m_PlayerRenderer != null)
        {
            m_HeldRenderer.sortingOrder = m_PlayerRenderer.sortingOrder + (behindPlayer ? -1 : 1);
        }
    }

    /// <summary>ทิศที่ตัวละครหันอยู่ อ่านจากค่าที่ Animator เก็บไว้</summary>
    private Vector2 GetFacing()
    {
        if (m_Animator == null) return Vector2.down;

        Vector2 dir = new Vector2(
            m_Animator.GetFloat("InputX"),
            m_Animator.GetFloat("InputY"));

        if (dir.sqrMagnitude < 0.01f)
        {
            dir = new Vector2(
                m_Animator.GetFloat("LastInputX"),
                m_Animator.GetFloat("LastInputY"));
        }

        return dir.sqrMagnitude < 0.01f ? Vector2.down : dir;
    }
}
