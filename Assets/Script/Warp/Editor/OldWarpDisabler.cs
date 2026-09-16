using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ปิดระบบวาร์ปอันเก่า (MapTransition + FadeController)
///
/// ทั้งสองระบบทำงานซ้อนกันกับ WarpPoint + ScreenFadeUI ตัวใหม่
/// เดินชนประตูทีเดียวจะวาร์ปสองรอบ และมีม่านดำซ้อนกันสองชั้น
///
/// ตัวนี้แค่ "ปิด" ไม่ได้ลบ — ติ๊กเปิดคืนใน Inspector ได้ตลอด
///
/// เมนู: Tools → Farming → ปิดระบบวาร์ปอันเก่า
/// </summary>
public static class OldWarpDisabler
{
    [MenuItem("Tools/Farming/ปิดระบบวาร์ปอันเก่า")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int count = Disable(log);

        EditorUtility.DisplayDialog(
            "ปิดระบบวาร์ปอันเก่า",
            count > 0
                ? string.Join("\n", log) + "\n\nอย่าลืมกด Ctrl + S เพื่อบันทึกฉาก"
                : "ไม่พบระบบวาร์ปอันเก่าที่ยังเปิดอยู่",
            "โอเค");
    }

    /// <summary>ปิดให้ คืนจำนวนที่ปิดไป</summary>
    public static int Disable(List<string> log)
    {
        int count = 0;

        // ---- MapTransition ----
        foreach (var old in Object.FindObjectsByType<MapTransition>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!old.enabled) continue;

            Undo.RecordObject(old, "Disable MapTransition");
            old.enabled = false;
            EditorUtility.SetDirty(old);

            log.Add($"ปิด MapTransition บน '{old.gameObject.name}'");
            count++;
        }

        // ---- FadeController ----
        foreach (var fade in Object.FindObjectsByType<FadeController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!fade.enabled) continue;

            Undo.RecordObject(fade, "Disable FadeController");
            fade.enabled = false;
            EditorUtility.SetDirty(fade);

            log.Add($"ปิด FadeController บน '{fade.gameObject.name}'");
            count++;
        }

        if (count > 0)
            log.Add("ตัวใหม่ที่ใช้แทนคือ WarpPoint + ScreenFade");

        return count;
    }
}
