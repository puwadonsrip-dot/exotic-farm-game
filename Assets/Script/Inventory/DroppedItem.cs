using UnityEngine;

/// <summary>
/// ของที่วางอยู่บนพื้นในโลกเกม
///
/// เกิดจากการลากไอเทมออกนอกกระเป๋า
/// เดินเข้าไปใกล้แล้วเก็บกลับได้เอง หรือคลิกเก็บก็ได้
///
/// ตัวนี้สร้างจากโค้ดล้วนๆ ไม่ต้องมี Prefab
/// </summary>
public class DroppedItem : MonoBehaviour
{
    [Header("ของที่วางอยู่")]
    public ItemData item;
    public int count = 1;

    [Header("การเก็บ")]
    [Tooltip("ผู้เล่นต้องเข้ามาใกล้แค่ไหนถึงจะเก็บอัตโนมัติ")]
    public float pickupRange = 1.1f;

    [Tooltip("หน่วงกี่วินาทีหลังวางถึงจะเก็บได้ กันเก็บกลับทันทีที่เพิ่งวาง")]
    public float armDelay = 0.8f;

    [Header("ท่าทาง")]
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.2f;

    private SpriteRenderer m_Renderer;
    private Vector2 m_Ground;
    private float m_Age;
    private Transform m_Player;

    /// <summary>
    /// ผู้เล่นเดินออกห่างไปแล้วรอบนึงหรือยัง
    ///
    /// ของที่เพิ่งวางจะตกอยู่ที่เท้าเราพอดี ถ้าเก็บอัตโนมัติทันทีมันจะเด้งกลับเข้ากระเป๋าเลย
    /// เลยต้องรอให้เดินออกไปก่อน ถึงจะเก็บคืนได้ (แต่คลิกเก็บเองได้ตลอด)
    /// </summary>
    private bool m_PlayerLeft;

    /// <summary>
    /// วางของลงพื้น — คืน null ถ้าข้อมูลไม่ครบ
    /// </summary>
    public static DroppedItem Spawn(ItemData item, int count, Vector2 position)
    {
        if (item == null || count <= 0) return null;

        var go = new GameObject($"ของที่วางไว้ — {item.displayName}");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = item.icon;
        renderer.sortingLayerName = SafeLayer();

        // ไอคอนไอเทมมักตัวใหญ่กว่าที่ควรวางบนพื้น ย่อลงหน่อย
        go.transform.localScale = Vector3.one * 0.55f;

        var dropped = go.AddComponent<DroppedItem>();
        dropped.item = item;
        dropped.count = count;
        dropped.m_Renderer = renderer;
        dropped.m_Ground = position;

        // ให้คลิกเก็บได้ด้วย
        var collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.45f;

        Debug.Log($"[ของตก] วาง {item.displayName} x{count} ที่ ({position.x:F1}, {position.y:F1})");
        return dropped;
    }

    /// <summary>ชั้นการวาดภาพ — ต้องไม่ใช่ Default ไม่งั้นของจะโดนพื้นดินบัง</summary>
    private static string SafeLayer()
    {
        foreach (var layer in SortingLayer.layers)
            if (layer.name == "Player") return "Player";

        return "Default";
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) m_Player = player.transform;
    }

    private void Update()
    {
        m_Age += Time.deltaTime;

        // ลอยขึ้นลงเบาๆ ให้สังเกตเห็นว่าเก็บได้
        float bob = Mathf.Sin(m_Age * bobSpeed) * bobHeight;
        transform.position = new Vector3(m_Ground.x, m_Ground.y + bob, transform.position.z);

        if (m_Renderer != null)
            m_Renderer.sortingOrder = Mathf.RoundToInt(-m_Ground.y * 10f) + 1;

        if (m_Age < armDelay) return;
        if (m_Player == null) return;

        float distance = Vector2.Distance(m_Player.position, m_Ground);

        // ยังไม่เคยเดินออกไป = ของยังกองอยู่เฉยๆ ไม่เด้งกลับเข้ากระเป๋า
        if (!m_PlayerLeft)
        {
            if (distance > pickupRange * 1.6f) m_PlayerLeft = true;
            return;
        }

        if (distance <= pickupRange) TryPickup();
    }

    private void OnMouseDown()
    {
        if (m_Age < armDelay) return;
        TryPickup();
    }

    /// <summary>เก็บกลับเข้ากระเป๋า — ถ้ากระเป๋าเต็มจะวางค้างไว้เหมือนเดิม</summary>
    public void TryPickup()
    {
        var inv = InventorySystem.Instance;
        if (inv == null || item == null) return;

        if (!inv.HasSpaceFor(item, count))
        {
            // กระเป๋าเต็ม — ปล่อยไว้บนพื้นก่อน ไม่ทำของหาย
            return;
        }

        inv.Add(item, count);
        AudioManager.PlaySelect();

        Debug.Log($"[ของตก] เก็บ {item.displayName} x{count} กลับเข้ากระเป๋า");
        Destroy(gameObject);
    }
}
