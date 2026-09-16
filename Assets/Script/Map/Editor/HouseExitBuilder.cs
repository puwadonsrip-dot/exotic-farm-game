using UnityEditor;
using UnityEngine;

/// <summary>
/// ตั้งค่าห้องในบ้าน + ทางเข้า-ออก แบบครบวงจร
///
/// วิธีใช้:
/// 1. เปิด Scene 'project' แล้วเลื่อน Scene view ไปให้เห็นห้องในบ้าน (ฝั่งขวา)
/// 2. Tools > Farming > Setup House Interior
/// 3. ของทั้งหมดจะไปโผล่ตรงกลางจอที่คุณมองอยู่ แล้วลากปรับให้พอดีห้อง
/// </summary>
public static class HouseExitBuilder
{
    private const string InteriorName = "House Interior";
    private const string ArrivalName = "Arrival Point";
    private const string ExitName = "House Exit Door";
    private const string ReturnName = "Return Point";

    /// <summary>ขนาดเริ่มต้นของกรอบกล้องในบ้าน (ลากปรับทีหลังได้)</summary>
    private static readonly Vector2 DefaultRoomSize = new Vector2(22f, 14f);

    /// <summary>จุดโผล่กลับที่ฟาร์ม อยู่ต่ำกว่าประตูบ้านเท่าไหร่</summary>
    private const float ReturnOffsetY = -1.8f;

    [MenuItem("Tools/Farming/Setup House Interior")]
    [MenuItem("Tools/Farming/ตั้งค่าห้องในบ้าน")]
    public static void Build()
    {
        // ---------- 1. หาประตูขาเข้าบ้านที่มีอยู่แล้ว ----------
        MapTransition entry = null;
        foreach (var t in Object.FindObjectsByType<MapTransition>(FindObjectsSortMode.None))
        {
            if (t.gameObject.name == ExitName) continue;
            entry = t;
            break;
        }

        if (entry == null)
        {
            EditorUtility.DisplayDialog("ไม่สำเร็จ",
                "ไม่พบประตูขาเข้าบ้าน (MapTransition) ใน Scene\n\n" +
                "เปิด Scene 'project' ก่อนแล้วลองใหม่", "ตกลง");
            return;
        }

        // ---------- 2. หาขอบเขตกล้องของฟาร์ม ----------
        PolygonCollider2D farmBounds = null;
        foreach (var poly in Object.FindObjectsByType<PolygonCollider2D>(FindObjectsSortMode.None))
        {
            if (poly.gameObject.name.ToLowerInvariant().Contains("farm"))
            {
                farmBounds = poly;
                break;
            }
        }

        if (farmBounds == null)
        {
            EditorUtility.DisplayDialog("ไม่สำเร็จ",
                "ไม่พบ GameObject ชื่อ 'Farm' ที่มี PolygonCollider2D", "ตกลง");
            return;
        }

        // ---------- 3. เอาตำแหน่งกลางจอ Scene view มาเป็นจุดตั้งต้น ----------
        var view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            EditorUtility.DisplayDialog("ไม่สำเร็จ",
                "ไม่พบหน้าต่าง Scene\n\nเปิดแท็บ Scene แล้วเลื่อนไปที่ห้องในบ้านก่อน", "ตกลง");
            return;
        }

        Vector3 center = view.pivot;
        center.z = 0f;

        // ---------- 4. ลบของเก่าที่เคยสร้างไว้ ----------
        var old = GameObject.Find(InteriorName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // ---------- 5. กรอบกล้องในบ้าน ----------
        var interiorGO = new GameObject(InteriorName);
        Undo.RegisterCreatedObjectUndo(interiorGO, "Create House Interior");
        interiorGO.transform.SetParent(entry.transform.parent, false);
        interiorGO.transform.position = center;

        var roomBounds = interiorGO.AddComponent<PolygonCollider2D>();
        roomBounds.isTrigger = true;
        roomBounds.SetPath(0, MakeRect(DefaultRoomSize));

        // ---------- 6. จุดที่ผู้เล่นโผล่ตอนเข้าบ้าน (ล่างของห้อง) ----------
        var arrivalGO = new GameObject(ArrivalName);
        Undo.RegisterCreatedObjectUndo(arrivalGO, "Create Arrival Point");
        arrivalGO.transform.SetParent(interiorGO.transform, false);
        arrivalGO.transform.localPosition = new Vector3(0f, -DefaultRoomSize.y * 0.5f + 2.2f, 0f);

        // ---------- 7. ประตูออก (ใต้จุดที่โผล่) ----------
        var exitGO = new GameObject(ExitName);
        Undo.RegisterCreatedObjectUndo(exitGO, "Create House Exit Door");
        exitGO.transform.SetParent(interiorGO.transform, false);
        exitGO.transform.localPosition = new Vector3(0f, -DefaultRoomSize.y * 0.5f + 0.6f, 0f);

        var exitBox = exitGO.AddComponent<BoxCollider2D>();
        exitBox.isTrigger = true;
        exitBox.size = new Vector2(4f, 1f);

        // ---------- 8. จุดโผล่กลับมาที่ฟาร์ม ----------
        var oldReturn = entry.transform.Find(ReturnName);
        if (oldReturn != null) Undo.DestroyObjectImmediate(oldReturn.gameObject);

        var returnGO = new GameObject(ReturnName);
        Undo.RegisterCreatedObjectUndo(returnGO, "Create Return Point");
        returnGO.transform.SetParent(entry.transform, false);
        returnGO.transform.position = entry.transform.position + new Vector3(0f, ReturnOffsetY, 0f);

        // ---------- 9. ต่อสายขาออก (ในบ้าน -> ฟาร์ม) ----------
        var exitTransition = exitGO.AddComponent<MapTransition>();
        var exitSO = new SerializedObject(exitTransition);
        exitSO.FindProperty("mapBoundry").objectReferenceValue = farmBounds;
        exitSO.FindProperty("direction").enumValueIndex = 4;   // 4 = Teleport
        exitSO.FindProperty("teleportTargetPosition").objectReferenceValue = returnGO.transform;
        exitSO.ApplyModifiedProperties();

        // ---------- 10. ต่อสายขาเข้าใหม่ (ฟาร์ม -> ห้องใหม่) ----------
        Undo.RecordObject(entry, "Rewire House Entry");
        var entrySO = new SerializedObject(entry);
        entrySO.FindProperty("mapBoundry").objectReferenceValue = roomBounds;
        entrySO.FindProperty("direction").enumValueIndex = 4;
        entrySO.FindProperty("teleportTargetPosition").objectReferenceValue = arrivalGO.transform;
        entrySO.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(interiorGO.scene);

        Selection.activeGameObject = interiorGO;
        view.FrameSelected();

        EditorUtility.DisplayDialog("ตั้งค่าห้องในบ้านสำเร็จ",
            $"สร้างที่ตำแหน่ง {center}\n" +
            $"ขนาดห้องเริ่มต้น {DefaultRoomSize.x} x {DefaultRoomSize.y}\n\n" +
            "ต่อสายให้แล้วทั้งขาเข้าและขาออก\n\n" +
            "ขั้นตอนต่อไป — ลากปรับให้พอดีห้องจริง:\n" +
            $"1. {InteriorName} = กรอบกล้อง ลากให้คลุมห้อง\n" +
            $"2. {ArrivalName} = จุดที่โผล่ตอนเข้าบ้าน\n" +
            $"3. {ExitName} = ประตูออก วางไว้ขอบล่างของห้อง\n\n" +
            "อย่าลืมกด Ctrl+S", "เข้าใจแล้ว");
    }

    /// <summary>สร้างจุด 4 มุมของสี่เหลี่ยม</summary>
    private static Vector2[] MakeRect(Vector2 size)
    {
        float w = size.x * 0.5f;
        float h = size.y * 0.5f;
        return new[]
        {
            new Vector2(-w, -h),
            new Vector2(w, -h),
            new Vector2(w, h),
            new Vector2(-w, h)
        };
    }
}
