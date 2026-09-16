using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตัดภาพสัตว์แบบเรียงหลายเฟรมออกเป็นไฟล์ทีละเฟรม
///
/// วางไฟล์ต้นฉบับไว้ที่ Assets/Art/Animals/ชื่อสัตว์.png
/// เรียงแนวตั้งหรือแนวนอนก็ได้ พื้นหลังโปร่งใสหรือทึบก็ได้ ตรวจให้เอง
///
/// ผลลัพธ์: Assets/Art/Animals/ชื่อสัตว์/Sprite_ชื่อสัตว์_0.png ...
///
/// เมนู: Tools → Farming → ตัดภาพสัตว์ใหม่
/// </summary>
public static class AnimalSheetSlicer
{
    public const string AnimalArtRoot = "Assets/Art/Animals";

    /// <summary>ชื่อไฟล์ต้นฉบับที่จะไปตามหา</summary>
    public static readonly string[] SheetNames =
    {
        "Husky", "Sharkbun", "Girasheep", "Pigbear", "Beemster"
    };

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>จุดหมุน — ให้เท้าสัตว์อยู่ที่พื้นพอดี</summary>
    private static readonly Vector2 Pivot = new Vector2(0.5f, 0.1f);

    private const float AlphaCut = 0.04f;
    private const float BackgroundTolerance = 0.16f;
    private const int ExpectedFrames = 3;

    /// <summary>ภาพสัตว์สูงกี่พิกเซล = 1 ช่องในเกม (ยิ่งน้อยตัวยิ่งใหญ่)</summary>
    private const int PixelsPerUnit = 56;

    /// <summary>
    /// ตั้งค่านำเข้าใหม่ให้ภาพที่ตัดไว้แล้ว
    /// ใช้ตอนเปลี่ยนขนาด จะได้ไม่ต้องตัดรูปใหม่ทั้งหมด
    /// </summary>
    public static int RefreshExistingImports(List<string> log)
    {
        int changed = 0;

        foreach (var name in SheetNames)
        {
            string folder = $"{AnimalArtRoot}/{name}";
            if (!Directory.Exists(folder)) continue;

            for (int i = 0; i < 12; i++)
            {
                string path = $"{folder}/Sprite_{name}_{i}.png";
                if (!File.Exists(path)) break;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)) continue;

                ApplySpriteImport(path);
                changed++;
            }
        }

        if (changed > 0)
            log.Add($"ปรับขนาดภาพสัตว์ใหม่ {changed} ไฟล์ (1 ช่อง = {PixelsPerUnit} พิกเซล)");

        return changed;
    }

    // ================= ทางเข้า =================

    [MenuItem("Tools/Farming/ตัดภาพสัตว์ใหม่")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int done = SliceAll(log, force: true);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "ตัดภาพสัตว์",
            done > 0
                ? string.Join("\n", log)
                : "ไม่พบไฟล์ต้นฉบับ\n\n"
                  + $"วางไฟล์ไว้ที่ {AnimalArtRoot}/\n"
                  + "ตั้งชื่อว่า " + string.Join(" / ", SheetNames),
            "โอเค");
    }

    public static void SliceIfNeeded(List<string> log) => SliceAll(log, force: false);

    /// <summary>
    /// ไฟล์ที่เซฟมาแล้วชื่อเป็นตัวเลข — เปลี่ยนชื่อให้อัตโนมัติ
    /// เรียงตามลำดับที่ส่งรูปเข้ามา
    /// </summary>
    private static readonly (string from, string to)[] Renames =
    {
        ("1", "Husky"),
        ("2", "Sharkbun"),
        ("3", "Girasheep"),
        ("4", "Pigbear"),
        ("5", "Beemster"),
    };

    private static void FixNames(List<string> log)
    {
        foreach (var (from, to) in Renames)
        {
            // มีไฟล์ชื่อถูกอยู่แล้วก็ไม่ต้องทำอะไร
            if (FindSheet(to) != null) continue;

            foreach (var ext in Extensions)
            {
                string path = $"{AnimalArtRoot}/{from}{ext}";
                if (!File.Exists(path)) continue;

                string error = AssetDatabase.RenameAsset(path, to);
                if (string.IsNullOrEmpty(error))
                    log.Add($"เปลี่ยนชื่อ {from}{ext} เป็น {to}{ext}");
                else
                    log.Add($"⚠ เปลี่ยนชื่อ {from}{ext} ไม่สำเร็จ: {error}");

                break;
            }
        }
    }

    private static int SliceAll(List<string> log, bool force)
    {
        FixNames(log);

        int done = 0;

        foreach (var name in SheetNames)
        {
            string sheetPath = FindSheet(name);

            if (sheetPath == null)
            {
                if (File.Exists($"{AnimalArtRoot}/{name}.webp"))
                    log.Add($"⚠ {name}.webp — Unity อ่านไฟล์ webp ไม่ได้ แปลงเป็น PNG ก่อน");

                continue;
            }

            if (!force && File.Exists($"{AnimalArtRoot}/{name}/Sprite_{name}_0.png")) continue;

            if (Slice(sheetPath, name, log)) done++;
        }

        return done;
    }

    private static string FindSheet(string name)
    {
        foreach (var ext in Extensions)
        {
            string path = $"{AnimalArtRoot}/{name}{ext}";
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // ================= ตัวหลัก =================

    private static bool Slice(string sheetPath, string name, List<string> log)
    {
        var importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
        if (importer == null)
        {
            log.Add($"⚠ {name}: อ่านไฟล์ไม่ได้");
            return false;
        }

        bool wasReadable = importer.isReadable;
        var wasCompression = importer.textureCompression;

        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
        if (sheet == null)
        {
            log.Add($"⚠ {name}: โหลดภาพไม่สำเร็จ");
            return false;
        }

        bool ok;
        try
        {
            ok = Cut(sheet, name, log);
        }
        finally
        {
            importer.isReadable = wasReadable;
            importer.textureCompression = wasCompression;
            importer.SaveAndReimport();
        }

        return ok;
    }

    private static bool Cut(Texture2D sheet, string name, List<string> log)
    {
        var pixels = sheet.GetPixels();
        int w = sheet.width;
        int h = sheet.height;

        var empty = BuildEmptyMask(pixels, w, h, out bool usedColorKey);
        if (usedColorKey) log.Add($"{name}: ลบพื้นหลังจากสีมุมภาพให้");

        bool vertical = h > w;
        var bands = FindBands(empty, w, h, vertical);

        if (bands.Count < 2 || bands.Count > 8)
        {
            log.Add($"{name}: แยกเฟรมได้ {bands.Count} — เปลี่ยนไปแบ่งเท่าๆ กัน {ExpectedFrames} ช่อง");
            bands = SplitEvenly(vertical ? h : w, ExpectedFrames);
        }

        var boxes = new List<RectInt>();
        foreach (var band in bands)
            boxes.Add(ContentBounds(empty, w, h, band, vertical));

        int cellW = 0, cellH = 0;
        foreach (var box in boxes)
        {
            cellW = Mathf.Max(cellW, box.width);
            cellH = Mathf.Max(cellH, box.height);
        }

        cellW += 4;
        cellH += 4;

        string outFolder = $"{AnimalArtRoot}/{name}";
        Directory.CreateDirectory(outFolder);

        var written = new List<string>();

        for (int i = 0; i < boxes.Count; i++)
        {
            string path = $"{outFolder}/Sprite_{name}_{i}.png";
            WriteFrame(pixels, empty, w, boxes[i], cellW, cellH, path);
            written.Add(path);
        }

        AssetDatabase.Refresh();

        foreach (var path in written) ApplySpriteImport(path);

        log.Add($"ตัดภาพ {name} ได้ {boxes.Count} เฟรม ช่องละ {cellW}x{cellH}");
        return true;
    }

    // ================= แยกพื้นหลัง =================

    private static bool[] BuildEmptyMask(Color[] pixels, int w, int h, out bool usedColorKey)
    {
        var empty = new bool[w * h];

        int transparent = 0;
        foreach (var c in pixels)
            if (c.a <= AlphaCut) transparent++;

        usedColorKey = transparent < pixels.Length * 0.02f;

        if (!usedColorKey)
        {
            for (int i = 0; i < pixels.Length; i++)
                empty[i] = pixels[i].a <= AlphaCut;

            return empty;
        }

        Color bg = AverageCorners(pixels, w, h);

        for (int i = 0; i < pixels.Length; i++)
            empty[i] = ColorClose(pixels[i], bg);

        return empty;
    }

    private static Color AverageCorners(Color[] pixels, int w, int h)
    {
        Color sum = Color.clear;
        int count = 0;

        for (int cy = 0; cy < 2; cy++)
        {
            for (int cx = 0; cx < 2; cx++)
            {
                int baseX = cx == 0 ? 0 : w - 4;
                int baseY = cy == 0 ? 0 : h - 4;

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int px = Mathf.Clamp(baseX + x, 0, w - 1);
                        int py = Mathf.Clamp(baseY + y, 0, h - 1);

                        sum += pixels[py * w + px];
                        count++;
                    }
                }
            }
        }

        return count > 0 ? sum / count : Color.white;
    }

    private static bool ColorClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < BackgroundTolerance
            && Mathf.Abs(a.g - b.g) < BackgroundTolerance
            && Mathf.Abs(a.b - b.b) < BackgroundTolerance;
    }

    // ================= หาเฟรม =================

    private struct Band { public int start; public int end; }

    private static List<Band> FindBands(bool[] empty, int w, int h, bool vertical)
    {
        int length = vertical ? h : w;
        int across = vertical ? w : h;

        var solid = new bool[length];

        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < across; j++)
            {
                int x = vertical ? j : i;
                int y = vertical ? i : j;

                if (empty[y * w + x]) continue;

                solid[i] = true;
                break;
            }
        }

        var bands = new List<Band>();
        int start = -1;

        for (int i = 0; i <= length; i++)
        {
            bool filled = i < length && solid[i];

            if (filled)
            {
                if (start < 0) start = i;
                continue;
            }

            if (start >= 0 && i - start >= 8)
                bands.Add(new Band { start = start, end = i });

            start = -1;
        }

        if (vertical) bands.Reverse();
        return bands;
    }

    private static List<Band> SplitEvenly(int length, int count)
    {
        var bands = new List<Band>();
        float size = length / (float)count;

        for (int i = 0; i < count; i++)
        {
            bands.Add(new Band
            {
                start = Mathf.RoundToInt(i * size),
                end = Mathf.RoundToInt((i + 1) * size)
            });
        }

        return bands;
    }

    private static RectInt ContentBounds(bool[] empty, int w, int h, Band band, bool vertical)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        int fromX = vertical ? 0 : band.start;
        int toX = vertical ? w : band.end;
        int fromY = vertical ? band.start : 0;
        int toY = vertical ? band.end : h;

        for (int y = fromY; y < toY; y++)
        {
            for (int x = fromX; x < toX; x++)
            {
                if (empty[y * w + x]) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (minX > maxX) return new RectInt(fromX, fromY, 1, 1);

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>วางเนื้อภาพกลางแนวนอน ชิดล่าง ทุกเฟรมขนาดเท่ากัน</summary>
    private static void WriteFrame(Color[] pixels, bool[] empty, int w, RectInt box,
                                   int cellW, int cellH, string path)
    {
        var canvas = new Color[cellW * cellH];

        int offsetX = (cellW - box.width) / 2;
        const int offsetY = 2;

        for (int y = 0; y < box.height; y++)
        {
            for (int x = 0; x < box.width; x++)
            {
                int srcIndex = (box.y + y) * w + (box.x + x);
                if (empty[srcIndex]) continue;

                int destX = offsetX + x;
                int destY = offsetY + y;

                if (destX < 0 || destX >= cellW || destY < 0 || destY >= cellH) continue;

                var color = pixels[srcIndex];
                color.a = 1f;
                canvas[destY * cellW + destX] = color;
            }
        }

        var tex = new Texture2D(cellW, cellH, TextureFormat.RGBA32, false);
        tex.SetPixels(canvas);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ApplySpriteImport(string path)
    {
        if (!File.Exists(path)) return;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
