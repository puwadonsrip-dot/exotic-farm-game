using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class MapTransition : MonoBehaviour
{
    /// <summary>true ขณะที่กำลัง fade/transition อยู่ — ใช้ล็อคการขยับของ Player</summary>
    public static bool IsTransitioning { get; private set; } = false;

    /// <summary>
    /// ล้างค่าทุกครั้งที่กด Play
    ///
    /// ตัวแปร static จะไม่ถูกล้างเองเมื่อกด Play ใหม่ (Unity 6 ปิด Domain Reload เป็นค่าเริ่มต้น)
    /// ถ้าครั้งก่อนกด Stop ตอนจอกำลังดำ ค่านี้จะค้างเป็น true แล้ววาร์ปไม่ได้อีกเลย
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsTransitioning = false;
    }

    [SerializeField] PolygonCollider2D mapBoundry;
    [SerializeField] Direction direction;
    [SerializeField] Transform teleportTargetPosition;
    CinemachineConfiner2D confiner;

    enum Direction { Up, Down, Left, Right, Teleport }

    private void Awake()
    {
        confiner = FindAnyObjectByType<CinemachineConfiner2D>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // ป้องกัน trigger ซ้อน ถ้ากำลัง transition อยู่ให้ข้าม
        if (IsTransitioning)
        {
            Debug.LogWarning($"[MapTransition] '{name}' ถูกชนแล้ว แต่ข้ามไปเพราะกำลังวาร์ปอยู่");
            return;
        }

        if (direction == Direction.Teleport && teleportTargetPosition == null)
        {
            Debug.LogError($"[MapTransition] '{name}' ตั้งเป็น Teleport แต่ยังไม่ได้ใส่ Teleport Target Position");
            return;
        }

        Debug.Log($"[MapTransition] เริ่มวาร์ปจาก '{name}'");
        StartCoroutine(TransitionWithFade(collision.gameObject));
    }

    private IEnumerator TransitionWithFade(GameObject player)
    {
        IsTransitioning = true;

        // 1. Fade เป็นสีดำก่อน (ผู้เล่นจะถูกล็อคโดยอัตโนมัติผ่าน IsTransitioning)
        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeToBlack();
        }
        else
        {
            Debug.LogError("[MapTransition] FadeController.Instance เป็น null! กรุณาวาง FadeController ไว้ใน Scene");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // 2. ย้ายผู้เล่นและเปลี่ยน confiner ขณะที่จอดำอยู่
        if (confiner != null)
        {
            confiner.BoundingShape2D = mapBoundry;

            // สำคัญ: Cinemachine "อบ" รูปทรงขอบเขตเก็บไว้เป็น cache
            // ถ้าเปลี่ยน BoundingShape2D เฉย ๆ มันจะยังใช้ของเก่าอยู่ กล้องเลยไม่ย้ายตาม
            // ต้องสั่งให้ล้าง cache ทุกครั้ง
            confiner.InvalidateBoundingShapeCache();
        }
        else
        {
            Debug.LogWarning("[MapTransition] ไม่พบ CinemachineConfiner2D ใน Scene");
        }

        UpdatePlayerPosition(player);

        // 3. หน่วงเล็กน้อยให้กล้องตามทัน
        yield return new WaitForSecondsRealtime(0.15f);

        // 4. Fade กลับมา
        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeFromBlack();
        }

        IsTransitioning = false;
        Debug.Log($"[MapTransition] วาร์ปเสร็จ — ผู้เล่นอยู่ที่ {player.transform.position}");
    }

    private void UpdatePlayerPosition(GameObject player)
    {
        if (direction == Direction.Teleport)
        {
            player.transform.position = teleportTargetPosition.position;
            return;
        }

        Vector3 newPos = player.transform.position;

        switch (direction)
        {
            case Direction.Up:
                newPos.y -= 2;
                break;
            case Direction.Down:
                newPos.y += 2;
                break;
            case Direction.Left:
                newPos.x += 2;
                break;
            case Direction.Right:
                newPos.x -= 2;
                break;
        }

        player.transform.position = newPos;
    }
}