using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// สร้าง Scene หน้าปกเกมอัตโนมัติ
///
/// วิธีใช้: Tools > Farming > Setup Main Menu
///
/// ก่อนกด ต้องเอารูปหน้าปกใส่โปรเจกต์ก่อน (ลากไฟล์เข้า Unity)
/// ตั้งชื่อไฟล์ให้มีคำว่า "title" เช่น TitleScreen.png
/// </summary>
public static class MainMenuBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/project.unity";

    /// <summary>ตำแหน่งปุ่ม "เริ่มเกม" ในภาพ (สัดส่วน 0-1 นับจากมุมซ้ายล่าง)</summary>
    private static readonly Vector2 ButtonAnchorMin = new Vector2(0.350f, 0.155f);
    private static readonly Vector2 ButtonAnchorMax = new Vector2(0.650f, 0.315f);

    [MenuItem("Tools/Farming/Setup Main Menu")]
    [MenuItem("Tools/Farming/สร้างหน้าปกเกม")]
    public static void Build()
    {
        // ---------- 1. หารูปหน้าปก ----------
        var titleSprite = FindTitleSprite(out string spritePath);
        if (titleSprite == null)
        {
            EditorUtility.DisplayDialog("ยังไม่มีรูปหน้าปก",
                "หาไฟล์รูปหน้าปกไม่เจอ\n\n" +
                "วิธีทำ:\n" +
                "1. ลากไฟล์รูปหน้าปกเข้ามาใน Unity\n" +
                "2. ให้ชื่อไฟล์ 'หรือ' ชื่อโฟลเดอร์ มีคำว่า title / cover / menu\n" +
                "   เช่น Assets/Art/Ui/TitleScreen/ปกเกม.png\n" +
                "3. กดเมนูนี้อีกครั้ง", "ตกลง");
            return;
        }

        // ---------- 2. บังคับให้รูปเป็น Sprite ----------
        var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        // ---------- 3. เตรียมโฟลเดอร์ ----------
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        // ---------- 4. สร้าง Scene ใหม่ (แบบ Additive จะได้ไม่ทับ Scene ที่เปิดอยู่) ----------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        BuildCamera(scene);
        BuildEventSystem(scene);
        BuildCanvas(scene, titleSprite);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);

        // ---------- 5. ใส่ลง Build Settings (หน้าปกต้องเป็นอันแรก) ----------
        RegisterScenes();

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("สร้างหน้าปกสำเร็จ",
            $"สร้าง {ScenePath} แล้ว\n" +
            $"ใช้รูป: {Path.GetFileName(spritePath)}\n\n" +
            "ใส่ลง Build Settings ให้แล้ว (หน้าปกเป็น Scene แรก)\n\n" +
            "ลองเปิด Scene MainMenu แล้วกด Play ดูได้เลย\n" +
            "ถ้าปุ่มไม่ตรงกับรูป ปรับ Anchor ของ StartButton ได้ใน Inspector", "เยี่ยม");

        // เปิด Scene หน้าปกให้ดูเลย
        if (EditorUtility.DisplayDialog("เปิดดูเลยไหม",
                "จะเปิด Scene หน้าปกให้ดูเลยไหม\n(Scene ที่เปิดอยู่ตอนนี้จะถูกปิด — เซฟก่อนนะ)",
                "เปิดเลย", "ไว้ก่อน"))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }

    // ==================================================================

    private static Sprite FindTitleSprite(out string path)
    {
        path = null;

        // หาไฟล์รูปที่ชื่อมีคำว่า title
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/HappyHarvest")) continue;   // ข้าม Art ของเดโม

            // เช็คทั้ง path (ชื่อโฟลเดอร์ก็นับ) จะได้ไม่ต้องเปลี่ยนชื่อไฟล์
            string lower = p.ToLowerInvariant();
            if (!lower.Contains("title") && !lower.Contains("cover") && !lower.Contains("menu"))
                continue;

            path = p;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sprite != null) return sprite;

            // ยังไม่ได้ import เป็น Sprite — แปลงให้
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
        }
        return null;
    }

    private static void BuildCamera(Scene scene)
    {
        var go = new GameObject("Main Camera", typeof(Camera));
        go.tag = "MainCamera";

        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.07f, 0.05f);
        cam.orthographic = true;

        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private static void BuildEventSystem(Scene scene)
    {
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private static void BuildCanvas(Scene scene, Sprite titleSprite)
    {
        // ---- Canvas ----
        var canvasGO = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- ภาพหน้าปก ----
        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(canvasGO.transform, false);

        var bgImage = bgGO.AddComponent<Image>();
        bgImage.sprite = titleSprite;
        bgImage.raycastTarget = false;
        bgImage.preserveAspect = false;

        var bgRT = (RectTransform)bgGO.transform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // ให้ภาพเต็มจอโดยไม่บิดสัดส่วน (ล้นออกนอกจอได้)
        var fitter = bgGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = titleSprite.rect.width / titleSprite.rect.height;

        // ---- ปุ่มเริ่มเกม (โปร่งใส วางทับปุ่มที่วาดไว้ในรูป) ----
        var buttonGO = new GameObject("StartButton", typeof(RectTransform));
        buttonGO.transform.SetParent(bgGO.transform, false);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0f);   // มองไม่เห็น แต่กดได้
        buttonImage.raycastTarget = true;

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;

        var buttonRT = (RectTransform)buttonGO.transform;
        buttonRT.anchorMin = ButtonAnchorMin;
        buttonRT.anchorMax = ButtonAnchorMax;
        buttonRT.offsetMin = Vector2.zero;
        buttonRT.offsetMax = Vector2.zero;

        // ---- แผ่นแฟลชสีขาว ----
        var flashGO = new GameObject("FlashOverlay", typeof(RectTransform));
        flashGO.transform.SetParent(canvasGO.transform, false);

        var flashImage = flashGO.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.raycastTarget = false;

        var flashRT = (RectTransform)flashGO.transform;
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.offsetMin = Vector2.zero;
        flashRT.offsetMax = Vector2.zero;

        // ---- ตัวควบคุม ----
        var controllerGO = new GameObject("MainMenu");
        var controller = controllerGO.AddComponent<MainMenuController>();
        controller.background = bgRT;
        controller.flashOverlay = flashImage;
        controller.gameSceneName = Path.GetFileNameWithoutExtension(GameScenePath);

        // ต้องใช้ AddPersistentListener ไม่ใช่ AddListener
        // เพราะ AddListener จะไม่ถูกบันทึกลง Scene (หายตอนปิด Unity)
        UnityEventTools.AddPersistentListener(button.onClick, controller.OnStartGame);

        SceneManager.MoveGameObjectToScene(canvasGO, scene);
        SceneManager.MoveGameObjectToScene(controllerGO, scene);
    }

    /// <summary>ใส่ Scene ลง Build Settings — หน้าปกต้องมาก่อน</summary>
    private static void RegisterScenes()
    {
        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        if (File.Exists(GameScenePath))
            list.Add(new EditorBuildSettingsScene(GameScenePath, true));

        // เก็บ Scene อื่นที่เคยมีไว้ด้วย (ยกเว้น 2 อันข้างบน)
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path == ScenePath || s.path == GameScenePath) continue;
            list.Add(s);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
