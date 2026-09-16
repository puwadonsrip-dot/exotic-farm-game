using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาดไอคอนผลผลิตสัตว์ด้วยโค้ด ใช้ชั่วคราวระหว่างที่ยังไม่มีรูปจริง
///
/// สร้างเฉพาะไฟล์ที่ยังไม่มี — พอวางรูปที่วาดเองทับ ระบบจะไม่เขียนทับให้
///
/// เมนู: Tools → Farming → สร้างไอคอนผลผลิตชั่วคราว
/// </summary>
public static class ProduceIconGenerator
{
    private const string Root = "Assets/Art/Items";
    private const int Size = 64;

    private enum Shape { Fluff, Bottle, Jar, Egg }

    private struct IconDef
    {
        public string file;
        public Shape shape;
        public Color main;
        public Color accent;
    }

    private static readonly IconDef[] Icons =
    {
        // ขนฮัสกี้ — ปุยขนสีขาวเทา
        new IconDef { file = "HuskyProduce", shape = Shape.Fluff,
                      main = new Color32(238, 240, 245, 255),
                      accent = new Color32(176, 186, 200, 255) },

        // ขนแกะยีราฟ — ปุยขนสีครีม
        new IconDef { file = "GirasheepProduce", shape = Shape.Fluff,
                      main = new Color32(245, 232, 205, 255),
                      accent = new Color32(206, 176, 128, 255) },

        // นมหมูหมี — ขวดนม ฝาชมพู
        new IconDef { file = "PigbearProduce", shape = Shape.Bottle,
                      main = new Color32(250, 250, 248, 255),
                      accent = new Color32(232, 140, 160, 255) },

        // น้ำผึ้งแฮมสเตอร์ — โถน้ำผึ้ง ฝาเหลือง
        new IconDef { file = "BeemsterProduce", shape = Shape.Jar,
                      main = new Color32(232, 160, 48, 255),
                      accent = new Color32(250, 214, 96, 255) },

        // ไข่กระต่ายฉลาม — ไข่สีฟ้ามีจุด
        new IconDef { file = "SharkbunProduce", shape = Shape.Egg,
                      main = new Color32(120, 184, 232, 255),
                      accent = new Color32(64, 120, 176, 255) },
    };

    private static readonly Color Outline = new Color32(58, 46, 40, 255);

    // ================= ทางเข้า =================

    [MenuItem("Tools/Farming/สร้างไอคอนผลผลิตชั่วคราว")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int made = Generate(log, force: true);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("สร้างไอคอนผลผลิต",
            made > 0 ? string.Join("\n", log) : "ไม่ได้สร้างอะไรเพิ่ม",
            "โอเค");
    }

    /// <summary>สร้างเฉพาะไฟล์ที่ยังไม่มี</summary>
    public static int Generate(List<string> log, bool force = false)
    {
        Directory.CreateDirectory(Root);

        int made = 0;

        foreach (var icon in Icons)
        {
            string path = $"{Root}/{icon.file}.png";
            if (!force && File.Exists(path)) continue;

            Draw(icon, path);
            made++;
        }

        if (made == 0) return 0;

        AssetDatabase.Refresh();

        foreach (var icon in Icons)
            ApplyImport($"{Root}/{icon.file}.png");

        log.Add($"วาดไอคอนผลผลิตชั่วคราวให้ {made} ไฟล์ (วาดรูปจริงทับได้ทีหลัง)");
        return made;
    }

    // ================= วาดรูป =================

    private static void Draw(IconDef icon, string path)
    {
        var pixels = new Color[Size * Size];   // โปร่งใสหมดตั้งแต่แรก

        switch (icon.shape)
        {
            case Shape.Fluff: DrawFluff(pixels, icon); break;
            case Shape.Bottle: DrawBottle(pixels, icon); break;
            case Shape.Jar: DrawJar(pixels, icon); break;
            default: DrawEgg(pixels, icon); break;
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>ก้อนขนปุย — วงกลมหลายวงซ้อนกัน</summary>
    private static void DrawFluff(Color[] pixels, IconDef icon)
    {
        Circle(pixels, 32, 26, 17, icon.main);
        Circle(pixels, 20, 34, 12, icon.main);
        Circle(pixels, 44, 34, 12, icon.main);
        Circle(pixels, 32, 40, 13, icon.main);

        // เงาด้านล่าง
        Circle(pixels, 32, 46, 10, icon.accent);
        Circle(pixels, 32, 42, 11, icon.main);

        // ไฮไลต์
        Circle(pixels, 25, 22, 5, Lighten(icon.main));
        OutlineShape(pixels);
    }

    /// <summary>ขวดนม</summary>
    private static void DrawBottle(Color[] pixels, IconDef icon)
    {
        Rect(pixels, 22, 20, 20, 34, icon.main);      // ตัวขวด
        Rect(pixels, 26, 12, 12, 10, icon.main);      // คอขวด
        Rect(pixels, 24, 8, 16, 6, icon.accent);      // ฝา
        Rect(pixels, 25, 36, 14, 16, Lighten(icon.accent));   // นมข้างใน
        Rect(pixels, 26, 24, 3, 22, Lighten(icon.main));      // ไฮไลต์
        OutlineShape(pixels);
    }

    /// <summary>โถน้ำผึ้ง</summary>
    private static void DrawJar(Color[] pixels, IconDef icon)
    {
        Rect(pixels, 18, 22, 28, 30, icon.main);
        Circle(pixels, 32, 22, 14, icon.main);
        Circle(pixels, 32, 50, 14, icon.main);
        Rect(pixels, 16, 14, 32, 10, icon.accent);    // ฝา
        Rect(pixels, 20, 11, 24, 5, Darken(icon.accent));
        Rect(pixels, 22, 28, 4, 18, Lighten(icon.main));      // ไฮไลต์
        OutlineShape(pixels);
    }

    /// <summary>ไข่มีจุด</summary>
    private static void DrawEgg(Color[] pixels, IconDef icon)
    {
        Ellipse(pixels, 32, 34, 16, 21, icon.main);

        // จุดลาย
        Circle(pixels, 25, 28, 3, icon.accent);
        Circle(pixels, 38, 34, 4, icon.accent);
        Circle(pixels, 29, 44, 3, icon.accent);

        Circle(pixels, 26, 22, 4, Lighten(icon.main));        // ไฮไลต์
        OutlineShape(pixels);
    }

    // ================= ตัวช่วยวาด =================

    private static void Set(Color[] pixels, int x, int y, Color color)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return;

        // พิกัดรูปนับจากบนลงล่าง แต่ Texture นับจากล่างขึ้นบน
        pixels[(Size - 1 - y) * Size + x] = color;
    }

    private static void Circle(Color[] pixels, int cx, int cy, int r, Color color)
        => Ellipse(pixels, cx, cy, r, r, color);

    private static void Ellipse(Color[] pixels, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;

                if (dx * dx + dy * dy > 1f) continue;
                Set(pixels, x, y, color);
            }
        }
    }

    private static void Rect(Color[] pixels, int x, int y, int w, int h, Color color)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Set(pixels, i, j, color);
    }

    /// <summary>ตีเส้นขอบดำรอบรูป ให้เห็นชัดบนพื้นหลังอะไรก็ได้</summary>
    private static void OutlineShape(Color[] pixels)
    {
        var copy = (Color[])pixels.Clone();

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (copy[(Size - 1 - y) * Size + x].a > 0.1f) continue;
                if (!HasNeighbour(copy, x, y)) continue;

                Set(pixels, x, y, Outline);
            }
        }
    }

    private static bool HasNeighbour(Color[] pixels, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || nx >= Size || ny < 0 || ny >= Size) continue;
                if (pixels[(Size - 1 - ny) * Size + nx].a > 0.1f) return true;
            }
        }
        return false;
    }

    private static Color Lighten(Color c)
        => new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f),
                     Mathf.Lerp(c.b, 1f, 0.35f), 1f);

    private static Color Darken(Color c)
        => new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, 1f);

    private static void ApplyImport(string path)
    {
        if (!File.Exists(path)) return;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
