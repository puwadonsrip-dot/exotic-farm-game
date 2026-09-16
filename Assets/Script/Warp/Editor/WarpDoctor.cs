using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ซ่อมระบบวาร์ปทั้งหมดในคลิกเดียว
///
/// ทำ 4 อย่างเรียงกัน:
///   1. ปิดระบบวาร์ปอันเก่าที่ทำงานซ้อนกัน
///   2. ใส่ขอบเขตกล้องปลายทางให้จุดวาร์ปทุกอัน
///   3. ขยายขอบเขตที่เล็กกว่าจอกล้อง (สาเหตุที่กล้องค้างครึ่งเดียว)
///   4. ตรวจว่ามีของที่จำเป็นครบไหม
///
/// เมนู: Tools → Farming → ซ่อมระบบวาร์ปทั้งหมด
/// </summary>
public static class WarpDoctor
{
    /// <summary>เผื่อขอบให้กว้างกว่าจอกล้องเท่าไหร่</summary>
    private const float Margin = 1.15f;

    [MenuItem("Tools/Farming/ซ่อมระบบวาร์ปทั้งหมด")]
    private static void FixAll()
    {
        var log = new List<string>();

        log.Add("=== 1. ปิดระบบเก่า ===");
        int disabled = OldWarpDisabler.Disable(log);
        if (disabled == 0) log.Add("ไม่มีของเก่าที่ยังเปิดอยู่");

        log.Add("");
        log.Add("=== 2. ใส่ขอบเขตกล้อง ===");
        int linked = WarpBoundaryLinker.LinkAll(log);
        if (linked == 0) log.Add("ใส่ครบอยู่แล้ว");

        log.Add("");
        log.Add("=== 3. ตรวจขนาดขอบเขต ===");
        int resized = FixBoundarySizes(log);
        if (resized == 0) log.Add("ทุกขอบเขตใหญ่พอแล้ว");

        log.Add("");
        log.Add("=== 4. ตรวจของที่ต้องมี ===");
        CheckSetup(log);

        EditorUtility.DisplayDialog(
            "ซ่อมระบบวาร์ป",
            string.Join("\n", log) + "\n\nอย่าลืมกด Ctrl + S เพื่อบันทึกฉาก",
            "โอเค");
    }

    // ================= ขยายขอบเขตที่เล็กเกินไป =================

    /// <summary>
    /// ขอบเขตที่เล็กกว่าจอกล้อง จะทำให้ Cinemachine หนีบกล้องไว้จนขยับไม่ได้
    /// อาการคือวาร์ปไปแล้วเห็นแค่ครึ่งเดียวและกล้องไม่ยอมตามตัวละคร
    /// </summary>
    private static int FixBoundarySizes(List<string> log)
    {
        GetCameraSize(out float viewWidth, out float viewHeight);

        if (viewWidth <= 0f || viewHeight <= 0f)
        {
            log.Add("⚠ หาขนาดจอกล้องไม่ได้ ข้ามขั้นนี้");
            return 0;
        }

        log.Add($"จอกล้องกว้าง {viewWidth:F1} x สูง {viewHeight:F1} ช่อง");

        float needWidth = viewWidth * Margin;
        float needHeight = viewHeight * Margin;

        var seen = new HashSet<PolygonCollider2D>();
        int count = 0;

        foreach (var warp in Object.FindObjectsByType<WarpPoint>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var boundary = warp.destinationBoundary;
            if (boundary == null || !seen.Add(boundary)) continue;

            var size = boundary.bounds.size;
            if (size.x >= needWidth && size.y >= needHeight)
            {
                log.Add($"'{boundary.gameObject.name}' — {size.x:F1} x {size.y:F1} ผ่าน");
                continue;
            }

            float scaleX = size.x < needWidth ? needWidth / Mathf.Max(0.01f, size.x) : 1f;
            float scaleY = size.y < needHeight ? needHeight / Mathf.Max(0.01f, size.y) : 1f;

            Expand(boundary, scaleX, scaleY);

            log.Add($"'{boundary.gameObject.name}' — เล็กไป ({size.x:F1} x {size.y:F1}) "
                    + $"ขยายเป็น {boundary.bounds.size.x:F1} x {boundary.bounds.size.y:F1}");
            count++;
        }

        return count;
    }

    /// <summary>ขยายรูปหลายเหลี่ยมออกจากจุดกึ่งกลางของตัวมันเอง</summary>
    private static void Expand(PolygonCollider2D boundary, float scaleX, float scaleY)
    {
        Undo.RecordObject(boundary, "Expand Boundary");

        var points = boundary.points;
        if (points == null || points.Length == 0) return;

        Vector2 center = Vector2.zero;
        foreach (var p in points) center += p;
        center /= points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 offset = points[i] - center;
            points[i] = center + new Vector2(offset.x * scaleX, offset.y * scaleY);
        }

        boundary.points = points;
        EditorUtility.SetDirty(boundary);
    }

    /// <summary>ขนาดที่กล้องมองเห็น เป็นหน่วยช่องในโลก</summary>
    private static void GetCameraSize(out float width, out float height)
    {
        width = 0f;
        height = 0f;

        float orthoSize = 0f;

        var vcam = Object.FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        if (vcam != null) orthoSize = vcam.Lens.OrthographicSize;

        if (orthoSize <= 0f && Camera.main != null)
            orthoSize = Camera.main.orthographicSize;

        if (orthoSize <= 0f) return;

        float aspect = Camera.main != null && Camera.main.aspect > 0.1f
            ? Camera.main.aspect
            : 16f / 9f;

        height = orthoSize * 2f;
        width = height * aspect;
    }

    // ================= ตรวจของที่ต้องมี =================

    private static void CheckSetup(List<string> log)
    {
        if (Object.FindFirstObjectByType<ScreenFadeUI>(FindObjectsInactive.Include) == null)
            log.Add("⚠ ไม่มี ScreenFade — รัน 'ติดตั้งระบบ Farming' ก่อน");
        else
            log.Add("ScreenFade ครบ");

        if (Object.FindFirstObjectByType<CinemachineConfiner2D>(FindObjectsInactive.Include) == null)
            log.Add("⚠ ไม่มี CinemachineConfiner2D บนกล้อง — กล้องจะไม่ถูกจำกัดขอบเขต");
        else
            log.Add("CinemachineConfiner2D ครบ");

        var warps = Object.FindObjectsByType<WarpPoint>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        log.Add($"มีจุดวาร์ป {warps.Length} อัน");

        foreach (var warp in warps)
        {
            if (warp.destination == null)
                log.Add($"⚠ '{warp.name}' ยังไม่ได้ใส่ปลายทาง");
            else if (warp.destinationBoundary == null)
                log.Add($"⚠ '{warp.name}' ยังไม่มีขอบเขตกล้อง");
        }
    }
}
