using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// สร้างไฟส่องสว่างที่ติดเองตอนกลางคืน มีให้เลือก 3 แบบ
///
/// เมนู: Tools → Farming → ไฟกลางคืน → ...
/// สร้างแล้วลากไปวางตรงไหนก็ได้ กด Ctrl+D เพื่อก๊อปปี้เพิ่ม
/// </summary>
public static class StreetLampTool
{
    private const string LampRoot = "Assets/HappyHarvest/Art/Environment/Lamps";

    /// <summary>ค่าตั้งต้นของไฟแต่ละแบบ</summary>
    private struct LampStyle
    {
        public string name;
        public string spritePath;
        public float radius;
        public float intensity;
        public float flicker;
        public Vector2 offset;
        public Color color;
    }

    // ================= เมนู =================

    /// <summary>ค่าของไฟแต่ละแบบ — แก้ที่นี่ที่เดียว แล้วกดเมนู "ปรับความสว่างไฟทั้งหมด"</summary>
    private static readonly LampStyle[] Styles =
    {
        new LampStyle
        {
            name = "StreetLamp",
            spritePath = $"{LampRoot}/StreetLamp/Sprites/Sprite_Street_lamp.png",
            radius = 1.9f,
            intensity = 0.3f,                      // แค่เรืองๆ ที่หัวเสา ไม่ส่องทาง
            flicker = 0.05f,                       // ไฟฟ้า กะพริบน้อย
            offset = new Vector2(0f, 1.35f),
            color = new Color(1f, 0.84f, 0.55f),
        },
        new LampStyle
        {
            name = "Lantern",
            spritePath = $"{LampRoot}/LampAndLantern/Sprites/Lantern/Sprite_Lantern.png",
            radius = 3.4f,
            intensity = 1.3f,                      // ตะเกียงคือดวงที่สว่างจริง
            flicker = 0.16f,                       // ไฟจากเปลวไฟ กะพริบเยอะ
            offset = new Vector2(0f, 0.35f),
            color = new Color(1f, 0.74f, 0.40f),   // ส้มอมแดง เหมือนเปลวไฟ
        },
        new LampStyle
        {
            name = "HouseLamp",
            spritePath = $"{LampRoot}/HouseLamp/Sprites/Sprite_HouseLamp.png",
            radius = 3f,
            intensity = 1.1f,
            flicker = 0.08f,
            offset = new Vector2(0f, 0.2f),
            color = new Color(1f, 0.82f, 0.52f),
        },
    };

    [MenuItem("Tools/Farming/ไฟกลางคืน/เสาไฟข้างทาง")]
    public static void CreateStreetLamp() => Create(Styles[0]);

    [MenuItem("Tools/Farming/ไฟกลางคืน/ตะเกียง")]
    public static void CreateLantern() => Create(Styles[1]);

    [MenuItem("Tools/Farming/ไฟกลางคืน/โคมไฟติดบ้าน")]
    public static void CreateHouseLamp() => Create(Styles[2]);

    /// <summary>
    /// ปรับไฟที่วางไว้แล้วทั้งหมดให้ใช้ค่าล่าสุด
    /// ดูจากชื่อ GameObject ว่าเป็นไฟแบบไหน (รวมตัวที่ก๊อปปี้มาด้วย)
    /// </summary>
    [MenuItem("Tools/Farming/ไฟกลางคืน/ปรับความสว่างไฟทั้งหมด")]
    public static void RefreshAllLampsFromMenu()
    {
        int count = RefreshAllLamps();

        EditorUtility.DisplayDialog("ปรับความสว่างไฟ",
            count > 0
                ? $"ปรับไฟในฉากแล้ว {count} ดวง\n\nอย่าลืมกด Ctrl + S"
                : "ไม่พบไฟในฉาก",
            "โอเค");
    }

    /// <summary>ปรับไฟทุกดวงในฉากให้ใช้ค่าล่าสุด คืนจำนวนที่ปรับ</summary>
    public static int RefreshAllLamps()
    {
        var lamps = Object.FindObjectsByType<StreetLamp>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int count = 0;

        foreach (var lamp in lamps)
        {
            var style = MatchStyle(lamp.gameObject.name);
            if (style == null) continue;

            var s = style.Value;

            Undo.RecordObject(lamp, "Refresh Lamp");
            lamp.maxIntensity = s.intensity;
            lamp.lightRadius = s.radius;
            lamp.flicker = s.flicker;
            lamp.lightColor = s.color;
            EditorUtility.SetDirty(lamp);

            if (lamp.lamp != null)
            {
                Undo.RecordObject(lamp.lamp, "Refresh Lamp Light");
                lamp.lamp.color = s.color;
                lamp.lamp.pointLightInnerRadius = s.radius * 0.25f;
                lamp.lamp.pointLightOuterRadius = s.radius;
                EditorUtility.SetDirty(lamp.lamp);
            }

            count++;
        }

        return count;
    }

    /// <summary>หาว่าไฟดวงนี้เป็นแบบไหน จากชื่อ GameObject</summary>
    private static LampStyle? MatchStyle(string objectName)
    {
        foreach (var style in Styles)
            if (objectName.StartsWith(style.name)) return style;

        return null;
    }

    // ================= ตัวสร้าง =================

    private static void Create(LampStyle style)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(style.spritePath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("สร้างไม่สำเร็จ",
                "ไม่พบรูปที่\n" + style.spritePath, "เข้าใจแล้ว");
            return;
        }

        var go = new GameObject(style.name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + style.name);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Player";   // ชั้นเดียวกับตัวละคร จะได้ไม่จมพื้น

        var lampScript = go.AddComponent<StreetLamp>();
        lampScript.maxIntensity = style.intensity;
        lampScript.lightRadius = style.radius;
        lampScript.flicker = style.flicker;
        lampScript.lightOffset = style.offset;
        lampScript.lightColor = style.color;

        // ---- ไฟวงกลม ----
        var lightGO = new GameObject("Light");
        Undo.RegisterCreatedObjectUndo(lightGO, "Create Light");
        lightGO.transform.SetParent(go.transform, false);
        lightGO.transform.localPosition = style.offset;

        var light2D = lightGO.AddComponent<Light2D>();
        light2D.lightType = Light2D.LightType.Point;
        light2D.color = style.color;
        light2D.intensity = 0f;
        light2D.pointLightInnerRadius = style.radius * 0.25f;
        light2D.pointLightOuterRadius = style.radius;
        light2D.falloffIntensity = 0.6f;

        lampScript.lamp = light2D;

        CopyTargetSortingLayers(light2D);

        // วางไว้ข้างผู้เล่น
        var player = GameObject.FindGameObjectWithTag("Player");
        go.transform.position = player != null
            ? player.transform.position + new Vector3(2f, 0f, 0f)
            : Vector3.zero;

        Selection.activeGameObject = go;

        EditorUtility.DisplayDialog("สร้างแล้ว",
            $"สร้าง '{style.name}' ให้แล้ว วางไว้ข้างตัวผู้เล่น\n\n"
            + "ลากไปวางตรงที่ต้องการ แล้วกด Ctrl + D เพื่อก๊อปปี้เพิ่มได้เลย\n"
            + "ไฟจะติดเองตอนฟ้าเริ่มมืด", "โอเค");
    }

    /// <summary>
    /// ให้ไฟส่องโดนเลเยอร์เดียวกับไฟทั้งฉาก
    /// ค่านี้ไม่มี property ให้เซ็ตตรงๆ ต้องแก้ผ่าน SerializedObject
    /// </summary>
    private static void CopyTargetSortingLayers(Light2D target)
    {
        Light2D source = null;

        foreach (var light in Object.FindObjectsByType<Light2D>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light == target || light.lightType != Light2D.LightType.Global) continue;
            source = light;
            break;
        }

        if (source == null) return;

        const string field = "m_ApplyToSortingLayers";

        var from = new SerializedObject(source).FindProperty(field);
        if (from == null || !from.isArray) return;

        var toObject = new SerializedObject(target);
        var to = toObject.FindProperty(field);
        if (to == null) return;

        to.arraySize = from.arraySize;
        for (int i = 0; i < from.arraySize; i++)
            to.GetArrayElementAtIndex(i).intValue = from.GetArrayElementAtIndex(i).intValue;

        toObject.ApplyModifiedProperties();
    }
}
