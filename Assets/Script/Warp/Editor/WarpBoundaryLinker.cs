using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตั้งขอบเขตกล้องปลายทางให้จุดวาร์ปอัตโนมัติ
///
/// หา 2 วิธี เรียงตามความน่าเชื่อถือ:
///   1. ลอกจากระบบวาร์ปอันเก่า (MapTransition) ที่อยู่ตำแหน่งเดียวกัน
///      อันนี้แม่นที่สุด เพราะคนตั้งเองไว้แล้ว
///   2. ถ้าไม่มีของเก่า ค่อยดูว่าจุดที่ไปโผล่อยู่ในรูปทรงของอันไหน
///
/// เมนู: Tools → Farming → ตั้งขอบเขตกล้องให้จุดวาร์ป
/// </summary>
public static class WarpBoundaryLinker
{
    /// <summary>จุดวาร์ปใหม่ต้องอยู่ใกล้ของเก่าไม่เกินกี่ช่อง ถึงจะถือว่าเป็นประตูเดียวกัน</summary>
    private const float MatchDistance = 6f;

    [MenuItem("Tools/Farming/ตั้งขอบเขตกล้องให้จุดวาร์ป")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int count = LinkAll(log);

        EditorUtility.DisplayDialog(
            "ตั้งขอบเขตกล้องให้จุดวาร์ป",
            count > 0
                ? string.Join("\n", log) + "\n\nอย่าลืมกด Ctrl + S เพื่อบันทึกฉาก"
                : (log.Count > 0 ? string.Join("\n", log) : "ไม่มีจุดวาร์ปที่ต้องตั้งค่า"),
            "โอเค");
    }

    public static int LinkAll(List<string> log)
    {
        var warps = Object.FindObjectsByType<WarpPoint>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (warps.Length == 0)
        {
            log.Add("ไม่พบจุดวาร์ปในฉาก");
            return 0;
        }

        var oldDoors = ReadOldDoors();
        if (oldDoors.Count > 0)
            log.Add($"เจอประตูของระบบเก่า {oldDoors.Count} อัน ใช้ค่าจากของเก่าเป็นหลัก");

        var boundaries = Object.FindObjectsByType<PolygonCollider2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        GetMinimumSize(out float minWidth, out float minHeight);

        int count = 0;

        foreach (var warp in warps)
        {
            if (warp.destination == null)
            {
                log.Add($"⚠ '{warp.name}' ยังไม่ได้ใส่ปลายทาง");
                continue;
            }

            // ---- วิธีที่ 1: ลอกจากประตูเก่าที่อยู่ตำแหน่งเดียวกัน ----
            var door = MatchOldDoor(oldDoors, warp.transform.position);
            var found = door?.boundary;
            string how = door != null ? $"ลอกจากประตูเก่า '{door.Value.name}'" : "";

            // จุดที่ผู้เล่นจะไปยืน — ใช้ของระบบเก่าที่ตั้งไว้ถูกแล้ว
            if (door != null && door.Value.arrival != null && warp.arrivalPoint != door.Value.arrival)
            {
                Undo.RecordObject(warp, "Link Arrival Point");
                warp.arrivalPoint = door.Value.arrival;
                EditorUtility.SetDirty(warp);

                log.Add($"'{warp.name}' → จุดยืน '{door.Value.arrival.name}'");
                count++;
            }

            // ---- วิธีที่ 2: ดูว่าจุดที่ไปโผล่อยู่ในรูปทรงไหน ----
            if (found == null)
            {
                Vector2 exit = warp.arrivalPoint != null
                    ? (Vector2)warp.arrivalPoint.position
                    : (Vector2)warp.destination.position + warp.exitOffset;

                found = FindContaining(boundaries, exit, minWidth, minHeight);
                how = "หาจากจุดที่ไปโผล่";
            }

            if (found == null)
            {
                log.Add($"⚠ '{warp.name}' — หาขอบเขตปลายทางไม่เจอ ต้องใส่เอง");
                continue;
            }

            if (warp.destinationBoundary == found)
            {
                log.Add($"'{warp.name}' → '{found.gameObject.name}' (ถูกอยู่แล้ว)");
                continue;
            }

            Undo.RecordObject(warp, "Link Warp Boundary");
            warp.destinationBoundary = found;
            EditorUtility.SetDirty(warp);

            log.Add($"'{warp.name}' → '{found.gameObject.name}'  ({how})");
            count++;
        }

        return count;
    }

    // ================= อ่านค่าจากระบบเก่า =================

    private struct OldDoor
    {
        public Vector3 position;
        public PolygonCollider2D boundary;
        public Transform arrival;
        public string name;
    }

    /// <summary>
    /// ดึงค่า mapBoundry ออกจาก MapTransition ทุกอัน
    /// ตัวแปรเป็น private เลยต้องอ่านผ่าน SerializedObject
    /// </summary>
    private static List<OldDoor> ReadOldDoors()
    {
        var doors = new List<OldDoor>();

        foreach (var old in Object.FindObjectsByType<MapTransition>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(old);

            var boundary = serialized.FindProperty("mapBoundry")?.objectReferenceValue
                as PolygonCollider2D;

            // จุดที่ระบบเก่าย้ายผู้เล่นไปยืน — ในฉากคือ 'Arrival Point' กับ 'Return Point'
            var arrival = serialized.FindProperty("teleportTargetPosition")?.objectReferenceValue
                as Transform;

            if (boundary == null && arrival == null) continue;

            doors.Add(new OldDoor
            {
                position = old.transform.position,
                boundary = boundary,
                arrival = arrival,
                name = old.gameObject.name
            });
        }

        return doors;
    }

    /// <summary>หาประตูเก่าที่อยู่ใกล้จุดวาร์ปนี้ที่สุด</summary>
    private static OldDoor? MatchOldDoor(List<OldDoor> doors, Vector3 position)
    {
        OldDoor? best = null;
        float bestDistance = MatchDistance;

        foreach (var door in doors)
        {
            float distance = Vector2.Distance(door.position, position);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = door;
        }

        return best;
    }

    // ================= หาจากรูปทรงจริง =================

    /// <summary>
    /// หาขอบเขตที่ครอบจุดนี้
    /// ใช้ OverlapPoint ซึ่งเช็ครูปทรงจริง ไม่ใช่กรอบสี่เหลี่ยมหยาบๆ
    /// และต้องใหญ่พอที่กล้องจะขยับได้ ไม่งั้นจะไปเจอกำแพงหรือ Blocker แทน
    /// </summary>
    private static PolygonCollider2D FindContaining(PolygonCollider2D[] boundaries,
                                                    Vector2 point,
                                                    float minWidth, float minHeight)
    {
        PolygonCollider2D best = null;
        float bestArea = float.MaxValue;

        foreach (var boundary in boundaries)
        {
            if (boundary == null) continue;

            var size = boundary.bounds.size;
            if (size.x < minWidth || size.y < minHeight) continue;   // เล็กเกินไป ไม่ใช่ขอบเขตแมพ

            if (!boundary.OverlapPoint(point)) continue;

            float area = size.x * size.y;
            if (area >= bestArea) continue;

            bestArea = area;
            best = boundary;
        }

        return best;
    }

    /// <summary>ขอบเขตต้องใหญ่กว่าจอกล้องถึงจะใช้ได้</summary>
    private static void GetMinimumSize(out float width, out float height)
    {
        float orthoSize = 0f;

        var vcam = Object.FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        if (vcam != null) orthoSize = vcam.Lens.OrthographicSize;

        if (orthoSize <= 0f && Camera.main != null)
            orthoSize = Camera.main.orthographicSize;

        if (orthoSize <= 0f)
        {
            width = 0f;
            height = 0f;
            return;
        }

        float aspect = Camera.main != null && Camera.main.aspect > 0.1f
            ? Camera.main.aspect
            : 16f / 9f;

        height = orthoSize * 2f;
        width = height * aspect;
    }
}
