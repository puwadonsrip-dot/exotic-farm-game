using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ตัดภาพพืชแบบเรียงเป็นแถวออกเป็นไฟล์ทีละระยะ
///
/// วางไฟล์ต้นฉบับไว้ที่ Assets/Art/Crops/ชื่อพืช.png (หรือ .jpg / .jpeg ก็ได้)
/// เช่น Assets/Art/Crops/Pumpkin.png  หรือ  Assets/Art/Crops/Tomato.jpg
///
/// พื้นหลังจะโปร่งใสหรือทึบก็ได้ — ถ้าทึบจะดูดสีมุมภาพมาเป็นสีพื้นหลังแล้วลบให้เอง
/// เรียงแนวตั้งหรือแนวนอนก็ได้ ตรวจให้เอง
///
/// ผลลัพธ์เข้าไปอยู่ที่ Assets/Art/Crops/ชื่อพืช/
///   Sprite_ชื่อพืช_00.png ... = ระยะการเติบโต
///   Sprite_ชื่อพืช_icon.png    = ผลผลิต (เฟรมรองสุดท้าย)
///   Sprite_ชื่อพืช_seedbag.png = ถุงเมล็ด (เฟรมสุดท้าย)
///
/// เมนู: Tools → Farming → ตัดภาพพืชใหม่
/// </summary>
public static class CropSheetSlicer
{
    public const string CropArtRoot = "Assets/Art/Crops";

    /// <summary>ชื่อไฟล์ต้นฉบับที่จะไปตามหา (ไม่ต้องมีนามสกุล)</summary>
    private static readonly string[] SheetNames = { "Pumpkin", "Tomato" };

    /// <summary>นามสกุลที่รับได้ — Unity อ่าน .webp ไม่ได้ เลยไม่มีในลิสต์</summary>
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>จุดหมุนของสไปรท์ — ให้ต้นไม้ยืนบนดินพอดี</summary>
    private static readonly Vector2 Pivot = new Vector2(0.5f, 0.25f);

    /// <summary>อัลฟ่าต่ำกว่านี้ถือว่าโปร่งใส</summary>
    private const float AlphaCut = 0.04f;

    /// <summary>สีต่างจากพื้นหลังน้อยกว่านี้ถือว่าเป็นพื้นหลัง (ใช้ตอนภาพไม่มีความโปร่งใส)</summary>
    private const float BackgroundTolerance = 0.16f;

    /// <summary>คาดว่าภาพหนึ่งมีกี่เฟรม — ใช้ตอนแยกเฟรมเองไม่สำเร็จ</summary>
    private const int ExpectedFrames = 8;

    // ================= ทางเข้า =================

    [MenuItem("Tools/Farming/ตัดภาพพืชใหม่")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int done = SliceAll(log, force: true);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "ตัดภาพพืช",
            done > 0
                ? string.Join("\n", log)
                : "ไม่พบไฟล์ต้นฉบับ\n\n"
                  + $"วางไฟล์ไว้ที่ {CropArtRoot}/\n"
                  + "ตั้งชื่อว่า Pumpkin หรือ Tomato\n"
                  + "นามสกุล .png .jpg .jpeg ก็ได้",
            "โอเค");
    }

    /// <summary>ตัดให้เฉพาะตัวที่ยังไม่เคยตัด — ตัวติดตั้งเรียกตัวนี้</summary>
    public static void SliceIfNeeded(List<string> log) => SliceAll(log, force: false);

    /// <summary>
    /// ไฟล์ที่เซฟมาแล้วชื่อไม่ตรง — เปลี่ยนชื่อให้อัตโนมัติ
    /// (ตอนเซฟจากแชทบางทีได้ชื่อเป็นตัวเลขมา)
    /// </summary>
    private static readonly (string from, string to)[] Renames =
    {
        ("11", "Tomato"),
    };

    private static void FixNames(List<string> log)
    {
        foreach (var (from, to) in Renames)
        {
            // มีไฟล์ชื่อถูกอยู่แล้วก็ไม่ต้องทำอะไร
            if (FindSheet(to) != null) continue;

            foreach (var ext in Extensions)
            {
                string path = $"{CropArtRoot}/{from}{ext}";
                if (!File.Exists(path)) continue;

                string error = AssetDatabase.RenameAsset(path, to);
                if (string.IsNullOrEmpty(error))
                    log.Add($"เปลี่ยนชื่อไฟล์ {from}{ext} เป็น {to}{ext} ให้แล้ว");
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
                // เจอ .webp = Unity อ่านไม่ได้ ต้องบอกให้ชัด ไม่ใช่เงียบไป
                if (File.Exists($"{CropArtRoot}/{name}.webp"))
                {
                    log.Add($"⚠ {name}.webp — Unity อ่านไฟล์ webp ไม่ได้\n"
                            + "    เปิดไฟล์ด้วย Paint แล้ว Save as เป็น PNG ก่อน");
                }
                continue;
            }

            // มีไฟล์ระยะแรกอยู่แล้ว = เคยตัดไปแล้ว
            if (!force && File.Exists($"{CropArtRoot}/{name}/Sprite_{name}_00.png")) continue;

            if (Slice(sheetPath, name, log)) done++;
        }

        return done;
    }

    /// <summary>หาไฟล์ต้นฉบับ ลองทีละนามสกุล</summary>
    private static string FindSheet(string name)
    {
        foreach (var ext in Extensions)
        {
            string path = $"{CropArtRoot}/{name}{ext}";
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

        // เปิด Read/Write ชั่วคราว ถึงจะอ่านพิกเซลออกมาได้
        bool wasReadable = importer.isReadable;
        var wasCompression = importer.textureCompression;
        int wasMaxSize = importer.maxTextureSize;

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
            importer.maxTextureSize = wasMaxSize;
            importer.SaveAndReimport();
        }

        return ok;
    }

    private static bool Cut(Texture2D sheet, string name, List<string> log)
    {
        var pixels = sheet.GetPixels();
        int w = sheet.width;
        int h = sheet.height;

        // สร้างตารางว่า "พิกเซลไหนคือพื้นหลัง" ใช้ได้ทั้งภาพโปร่งใสและภาพพื้นทึบ
        var empty = BuildEmptyMask(pixels, w, h, out bool usedColorKey);

        if (usedColorKey)
            log.Add($"{name}: ภาพไม่มีพื้นโปร่งใส — ลบพื้นหลังจากสีมุมภาพให้แทน");

        bool vertical = h > w;

        var bands = FindBands(empty, w, h, vertical);

        // แยกเฟรมได้จำนวนแปลกๆ = ใช้วิธีแบ่งเท่าๆ กันแทน
        if (bands.Count < 4 || bands.Count > 12)
        {
            log.Add($"{name}: แยกเฟรมอัตโนมัติได้ {bands.Count} เฟรม "
                    + $"— เปลี่ยนไปแบ่งเท่าๆ กัน {ExpectedFrames} ช่องแทน");

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

        cellW += 4;   // เผื่อขอบนิดหน่อย
        cellH += 4;

        string outFolder = $"{CropArtRoot}/{name}";
        Directory.CreateDirectory(outFolder);

        var written = new List<string>();

        for (int i = 0; i < boxes.Count; i++)
        {
            string fileName = FrameFileName(name, i, boxes.Count);
            string path = $"{outFolder}/{fileName}";

            WriteFrame(pixels, empty, w, box: boxes[i], cellW, cellH, path);
            written.Add(path);
        }

        AssetDatabase.Refresh();

        foreach (var path in written)
            ApplySpriteImport(path, cellH);

        log.Add($"ตัดภาพ {name} ได้ {boxes.Count} เฟรม "
                + $"(ระยะเติบโต {boxes.Count - 2} + ผลผลิต + ถุงเมล็ด) ช่องละ {cellW}x{cellH}");
        return true;
    }

    private static string FrameFileName(string name, int index, int total)
    {
        if (index == total - 1) return $"Sprite_{name}_seedbag.png";
        if (index == total - 2) return $"Sprite_{name}_icon.png";
        return $"Sprite_{name}_{index:00}.png";
    }

    // ================= แยกพื้นหลังออกจากตัวภาพ =================

    /// <summary>
    /// คืนตารางว่าพิกเซลไหนเป็นพื้นหลัง
    /// ถ้าภาพมีความโปร่งใสอยู่แล้วก็ใช้อัลฟ่า
    /// ถ้าไม่มี (เช่นไฟล์ jpg) จะดูดสีมุมภาพมาเป็นสีพื้นหลังแล้วเทียบสี
    /// </summary>
    private static bool[] BuildEmptyMask(Color[] pixels, int w, int h, out bool usedColorKey)
    {
        var empty = new bool[w * h];

        // ภาพนี้มีพื้นโปร่งใสไหม
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

        // ไม่มีพื้นโปร่งใส -> เอาสีที่พบบ่อยที่สุดตามมุมทั้งสี่เป็นสีพื้นหลัง
        Color bg = AverageCorners(pixels, w, h);

        for (int i = 0; i < pixels.Length; i++)
            empty[i] = ColorClose(pixels[i], bg);

        return empty;
    }

    private static Color AverageCorners(Color[] pixels, int w, int h)
    {
        Color sum = Color.clear;
        int count = 0;

        // อ่านสี่มุม มุมละ 4x4 พิกเซล
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

    private struct Band { public int start; public int end; }   // end = ไม่รวม

    /// <summary>ไล่ไปตามแกนยาว หาแถวที่ว่างทั้งเส้น = ช่องว่างระหว่างเฟรม</summary>
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

            // เฟรมต้องหนาพอสมควร กันจุดหลงๆ ในภาพ
            if (start >= 0 && i - start >= 6)
                bands.Add(new Band { start = start, end = i });

            start = -1;
        }

        // แกน Y ของ Texture นับจากล่างขึ้นบน แต่ภาพเรียงจากบนลงล่าง
        if (vertical) bands.Reverse();

        return bands;
    }

    /// <summary>แบ่งเท่าๆ กัน ใช้ตอนแยกเฟรมเองไม่สำเร็จ</summary>
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

    /// <summary>หากรอบสี่เหลี่ยมของเนื้อภาพจริงในเฟรมนั้น</summary>
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

    /// <summary>
    /// เขียนเฟรมลงไฟล์ วางเนื้อภาพกลางแนวนอนและชิดล่าง
    /// ทุกเฟรมขนาดเท่ากัน ต้นไม้จะได้งอกจากจุดเดียวกัน ไม่ลอยไปมา
    /// พิกเซลที่เป็นพื้นหลังจะถูกทำให้โปร่งใส
    /// </summary>
    private static void WriteFrame(Color[] pixels, bool[] empty, int w, RectInt box,
                                   int cellW, int cellH, string path)
    {
        var canvas = new Color[cellW * cellH];   // โปร่งใสหมดตั้งแต่แรก

        int offsetX = (cellW - box.width) / 2;
        const int offsetY = 2;                    // ชิดล่าง เว้นขอบ 2 พิกเซล

        for (int y = 0; y < box.height; y++)
        {
            for (int x = 0; x < box.width; x++)
            {
                int srcIndex = (box.y + y) * w + (box.x + x);
                if (empty[srcIndex]) continue;    // พื้นหลัง = ปล่อยให้โปร่งใส

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

    /// <summary>ตั้งค่านำเข้าให้เป็นสไปรท์ ขนาดเท่ากับพืชเดิมของเกม</summary>
    private static void ApplySpriteImport(string path, int cellH)
    {
        if (!File.Exists(path)) return;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;      // พิกเซลอาร์ตต้องคมๆ
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        // ให้ 1 เฟรม = สูง 1 ช่องพอดี
        importer.spritePixelsPerUnit = Mathf.Max(1, cellH);

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Pivot;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
