using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// จุดวาร์ป — เดินเข้ามาแล้วย้ายไปโผล่ที่จุดปลายทาง พร้อมจอมืดเข้าออก
///
/// วิธีใช้: สร้างจุดวาร์ป 2 อัน แล้วชี้ Destination หากันและกัน
/// เมนู: Tools → Farming → สร้างจุดวาร์ปคู่ใหม่
/// </summary>
public class WarpPoint : MonoBehaviour
{
    [Header("ปลายทาง")]
    [Tooltip("ลากจุดวาร์ปอีกอันมาใส่ ผู้เล่นจะไปโผล่ตรงนั้น")]
    public Transform destination;

    [Tooltip("จุดที่ผู้เล่นจะไปยืนพอดี — ใส่แล้วจะใช้จุดนี้แทน Destination + Exit Offset")]
    public Transform arrivalPoint;

    [Tooltip("โผล่ห่างจากจุดปลายทางเท่าไหร่ (ใช้ตอนไม่ได้ใส่ Arrival Point)")]
    public Vector2 exitOffset = new Vector2(0f, -1.2f);

    [Header("การเข้าใช้")]
    [Tooltip("ผู้เล่นต้องเข้ามาใกล้แค่ไหน")]
    public float radius = 0.9f;

    [Tooltip("เปิด = ต้องกด E ถึงจะวาร์ป / ปิด = เดินชนแล้ววาร์ปเลย")]
    public bool requireKeyPress;

    [Tooltip("ข้อความบอกตอนเข้าใกล้ — เว้นว่าง = ไม่ต้องบอก")]
    public string prompt = "";

    [Header("ขอบเขตกล้องปลายทาง")]
    [Tooltip("ลาก PolygonCollider2D ที่เป็นขอบเขตของแมพปลายทางมาใส่ "
             + "เว้นว่างได้ถ้าวาร์ปในแมพเดียวกัน")]
    public PolygonCollider2D destinationBoundary;

    /// <summary>กันวาร์ปวนไปมาทันทีหลังโผล่</summary>
    private static float s_CooldownUntil;

    private Transform m_Player;

    /// <summary>
    /// พร้อมทำงานหรือยัง
    ///
    /// จุดวาร์ปจะทำงานได้ก็ต่อเมื่อผู้เล่น "เดินออกไปนอกวงแล้วเดินกลับเข้ามาใหม่"
    /// กันไม่ให้วาร์ปซ้ำตอนโผล่มาใกล้ๆ จุดวาร์ปอีกฝั่ง หรือตอนกดปุ่มเดินค้างไว้
    /// </summary>
    private bool m_Armed;

    /// <summary>สั่งให้หยุดทำงานจนกว่าผู้เล่นจะเดินออกไปแล้วกลับเข้ามาใหม่</summary>
    public void Disarm() => m_Armed = false;

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) m_Player = playerGO.transform;
    }

    private void Update()
    {
        if (m_Player == null || destination == null) return;
        if (ScreenFadeUI.IsFading || Time.unscaledTime < s_CooldownUntil) return;

        bool busy = DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
                    || CutsceneUI.IsPlaying || SettingsMenuUI.IsOpen
                    || DayTransitionUI.IsPlaying || NameEntryUI.IsOpen
                    || TitleScreenUI.IsOpen || AnimalInfoUI.IsOpen
                    || AdminModeUI.IsOpen;

        if (busy) return;

        float distance = Vector2.Distance(transform.position, m_Player.position);

        // ออกไปนอกวงแล้ว = พร้อมทำงานรอบหน้า
        if (distance > radius)
        {
            m_Armed = true;
            return;
        }

        // ยังไม่เคยเดินออกไปเลย = ยังไม่ให้ทำงาน (กันวาร์ปซ้ำ)
        if (!m_Armed) return;

        if (requireKeyPress)
        {
            if (!string.IsNullOrEmpty(prompt))
                DialogueUI.Instance?.SetPrompt(prompt);

            var kb = Keyboard.current;
            if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        }

        Warp();
    }

    public void Warp()
    {
        if (destination == null || m_Player == null) return;

        m_Armed = false;

        // ปลายทางก็ต้องปิดไว้ด้วย เพราะผู้เล่นจะไปโผล่ใกล้ๆ มัน
        var other = destination.GetComponent<WarpPoint>();
        if (other != null) other.Disarm();

        if (ScreenFadeUI.Instance == null)
        {
            MovePlayer();   // ไม่มีระบบจอมืด ก็ย้ายดื้อๆ
            return;
        }

        DialogueUI.Instance?.SetPrompt("");
        ScreenFadeUI.Instance.Play(MovePlayer);
    }

    /// <summary>จุดที่ผู้เล่นจะไปยืน</summary>
    private Vector3 ArrivalPosition => arrivalPoint != null
        ? arrivalPoint.position
        : destination.position + (Vector3)exitOffset;

    private void MovePlayer()
    {
        Vector3 target = ArrivalPosition;
        Vector3 delta = target - m_Player.position;   // ต้องเก็บก่อนย้าย

        // ย้ายขอบเขตกล้องไปแมพใหม่ก่อน ไม่งั้นกล้องจะติดอยู่ที่แมพเดิม
        MoveCameraBoundary();

        m_Player.position = target;

        // Rigidbody จำตำแหน่งเก่าไว้ ต้องบอกมันด้วย ไม่งั้นจะโดนดึงกลับ
        var body = m_Player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = target;
            body.linearVelocity = Vector2.zero;
        }

        SnapCamera(delta);

        // กันเด้งกลับทันทีเพราะไปโผล่ในระยะของจุดปลายทาง
        s_CooldownUntil = Time.unscaledTime + 1f;

        Debug.Log($"[วาร์ป] ย้ายไปที่ ({target.x:F1}, {target.y:F1})");
    }

    /// <summary>
    /// เปลี่ยนขอบเขตที่กล้องเดินตามได้ ให้เป็นของแมพปลายทาง
    ///
    /// Cinemachine เก็บรูปทรงขอบเขตไว้เป็น cache
    /// เปลี่ยนค่าเฉยๆ ไม่พอ ต้องสั่งล้าง cache ด้วย ไม่งั้นกล้องจะยังติดอยู่ที่แมพเดิม
    /// </summary>
    private void MoveCameraBoundary()
    {
        if (destinationBoundary == null) return;

        var confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner == null)
        {
            Debug.LogWarning("[วาร์ป] ไม่พบ CinemachineConfiner2D — กล้องจะไม่ย้ายขอบเขต");
            return;
        }

        confiner.BoundingShape2D = destinationBoundary;
        confiner.InvalidateBoundingShapeCache();

        StartCoroutine(SuspendConfinerDamping(confiner));
    }

    /// <summary>
    /// ปิดการหน่วงของ Confiner ชั่วคราวตอนวาร์ป
    ///
    /// Confiner ตั้ง Damping 0.5 กับ Slowing Distance 5 ไว้ ซึ่งดีตอนเดินชนขอบแมพปกติ
    /// แต่ตอนวาร์ป กล้องต้องกระโดดข้ามแมพ การหน่วงจะดึงมันให้ค่อยๆ ไถลตาม
    /// เลยเห็นกล้องค้างอยู่ข้างๆ ไม่ยอมมาอยู่กลางตัวละคร
    ///
    /// ปิดไว้จนกว่าจะวาร์ปเสร็จ แล้วค่อยคืนค่าเดิม
    /// </summary>
    private System.Collections.IEnumerator SuspendConfinerDamping(CinemachineConfiner2D confiner)
    {
        float savedDamping = confiner.Damping;
        float savedSlowing = confiner.SlowingDistance;

        confiner.Damping = 0f;
        confiner.SlowingDistance = 0f;

        // รอให้กล้องเข้าที่ก่อน (ใช้เวลาจริง เผื่อเกมกำลังหยุดอยู่)
        yield return new WaitForSecondsRealtime(0.5f);

        confiner.Damping = savedDamping;
        confiner.SlowingDistance = savedSlowing;
    }

    /// <summary>
    /// บอกกล้องว่าเป้าหมายถูกวาร์ป ให้กระโดดตามไปเลย
    ///
    /// ปกติกล้องจะเลื่อนตามแบบหน่วงๆ (damping) ซึ่งสวยตอนเดิน
    /// แต่ตอนวาร์ปมันจะไล่ตามไม่ทัน พอจอสว่างขึ้นเลยเห็นกล้องค้างอยู่กลางทาง
    /// OnTargetObjectWarped สั่งให้กล้องเลื่อนตามทันทีโดยไม่หน่วง
    /// </summary>
    private void SnapCamera(Vector3 delta)
    {
        var cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            cam.OnTargetObjectWarped(m_Player, delta);
        }

        // ล้างประวัติการหน่วงของกล้องทุกตัว ให้เริ่มนับใหม่จากตำแหน่งปัจจุบัน
        // ไม่งั้นกล้องจะค่อยๆ ไถลจากที่เดิมไปที่ใหม่ ซึ่งเห็นชัดตอนวาร์ประยะไกล
        CinemachineCore.ResetCameraState();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, radius);

        if (destination == null) return;

        // เส้นโยงไปหาปลายทาง จะได้เห็นว่าคู่กับอันไหน
        Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.35f);
        Gizmos.DrawLine(transform.position, destination.position);

        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.7f);
        Gizmos.DrawWireSphere(ArrivalPosition, 0.3f);
    }
}
