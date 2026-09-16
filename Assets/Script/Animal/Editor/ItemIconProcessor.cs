using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// เตรียมรูปไอคอนไอเทมให้พร้อมใช้
///
/// ทำ 3 อย่างกับรูปที่วาดมาเอง:
///   1. ลบพื้นหลังดำทิ้ง ถ้าไฟล์ไม่มีความโปร่งใสมาให้
///   2. ตัดขอบว่างรอบๆ ออก แล้วจัดให้เป็นสี่เหลี่ยมจัตุรัส
///      (ไฟล์ที่วาดมามักมีพื้นที่ว่างเยอะ พอย่อลงช่องเล็กๆ ตัวของจะจิ๋วมาก)
///   3. ตั้งค่านำเข้าให้เหมาะกับขนาดไอคอน
///
/// ทำครั้งเดียวต่อไฟล์ มีไฟล์ .iconready กันทำซ้ำ
///
/// เมนู: Tools → Farming → เตรียมรูปไอคอนไอเทม
/// </summary>
public static class ItemIconProcessor
{
    private const string Root = "Assets/Art/Items";
    private const string MarkerFolder = Root + "/.processed";

    /// <summary>เผื่อขอบรอบตัวของกี่ % ของด้านที่ยาวที่สุด</summary>
    private const float Padding = 0.06f;

    /// <summary>สีเข้มกว่านี้ถือว่าเป็นพื้นหลังดำ</summary>
    private const float DarkCut = 0.08f;

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    [MenuItem("Tools/Farming/เตรียมรูปไอคอนไอเทม")]
    private static void FromMenu()
    {
        var log = new List<string>();
        int done = Run(log, force: true);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("เตรียมรูปไอคอน",
            done > 0 ? string.Join("\n", log) : "ไม่พบรูปที่ต้องเตรียม",
            "โอเค");
    }

    /// <summary>เตรียมรูปทุกไฟล์ในโฟลเดอร์ไอเทม</summary>
    public static int Run(List<string> log, bool force = false)
    {
        if (!Directory.Exists(Root)) return 0;

        Directory.CreateDirectory(MarkerFolder);

        int done = 0;

        foreach (var path in Directory.GetFiles(Root))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (System.Array.IndexOf(Extensions, ext) < 0) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            string marker = $"{MarkerFolder}/{name}.done";

            if (!force && File.Exists(marker)) continue;

            if (!Process(path.Replace('\\', '/'), name, log)) continue;

            File.WriteAllText(marker, "ok");
            done++;
        }

        return done;
    }

    // ================= ตัวหลัก =================

    private static bool Process(string path, string name, List<string> log)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        // เปิด Read/Write ชั่วคราว ถึงจะอ่านพิกเซลออกมาได้
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 8192;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return false;

        var pixels = tex.GetPixels();
        int w = tex.width;
        int h = tex.height;

        bool hadAlpha = HasTransparency(pixels);

        // ไม่มีความโปร่งใส = พื้นหลังเป็นสีดำทึบ ต้องลบออก
        if (!hadAlpha) ClearDarkBackground(pixels);

        if (!FindContent(pixels, w, h, out RectInt box))
        {
            log.Add($"⚠ {name} — หาตัวของในรูปไม่เจอ");
            return false;
        }

        // จัดเป็นสี่เหลี่ยมจัตุรัส เผื่อขอบนิดหน่อย
        int side = Mathf.Max(box.width, box.height);
        side += Mathf.RoundToInt(side * Padding * 2f);

        WriteSquare(pixels, w, box, side, path);

        AssetDatabase.Refresh();
        ApplyImport(path, side);

        log.Add(hadAlpha
            ? $"{name} — ตัดขอบว่างออก เหลือ {side}x{side}"
            : $"{name} — ลบพื้นหลังดำ + ตัดขอบว่าง เหลือ {side}x{side}");

        return true;
    }

    private static bool HasTransparency(Color[] pixels)
    {
        int clear = 0;
        foreach (var c in pixels)
            if (c.a < 0.1f) clear++;

        return clear > pixels.Length * 0.02f;
    }

    /// <summary>ทำให้พิกเซลที่เข้มเกือบดำกลายเป็นโปร่งใส</summary>
    private static void ClearDarkBackground(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float brightest = Mathf.Max(c.r, Mathf.Max(c.g, c.b));

            if (brightest > DarkCut) continue;

            pixels[i] = Color.clear;
        }
    }

    /// <summary>หากรอบสี่เหลี่ยมของตัวของจริงในรูป</summary>
    private static bool FindContent(Color[] pixels, int w, int h, out RectInt box)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a < 0.1f) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (minX > maxX)
        {
            box = default;
            return false;
        }

        box = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    /// <summary>วางตัวของไว้กลางผืนสี่เหลี่ยมจัตุรัส แล้วเขียนทับไฟล์เดิม</summary>
    private static void WriteSquare(Color[] pixels, int sourceWidth, RectInt box,
                                    int side, string path)
    {
        var canvas = new Color[side * side];   // โปร่งใสหมดตั้งแต่แรก

        int offsetX = (side - box.width) / 2;
        int offsetY = (side - box.height) / 2;

        for (int y = 0; y < box.height; y++)
        {
            for (int x = 0; x < box.width; x++)
            {
                int destX = offsetX + x;
                int destY = offsetY + y;

                if (destX < 0 || destX >= side || destY < 0 || destY >= side) continue;

                canvas[destY * side + destX] = pixels[(box.y + y) * sourceWidth + (box.x + x)];
            }
        }

        var result = new Texture2D(side, side, TextureFormat.RGBA32, false);
        result.SetPixels(canvas);
        result.Apply();

        File.WriteAllBytes(path, result.EncodeToPNG());
        Object.DestroyImmediate(result);
    }

    /// <summary>
    /// ตั้งค่านำเข้า
    /// รูปวาดละเอียดใช้ Bilinear จะเนียนกว่าตอนย่อ
    /// รูปพิกเซลอาร์ตใช้ Point ถึงจะคม
    /// </summary>
    private static void ApplyImport(string path, int side)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool painted = side > 160;   // ใหญ่ขนาดนี้ = รูปวาดละเอียด ไม่ใช่พิกเซลอาร์ต

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = painted;   // รูปวาดต้องมี mipmap ไม่งั้นย่อแล้วขอบแตก
        importer.filterMode = painted ? FilterMode.Bilinear : FilterMode.Point;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }
}
