using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// จัดการรูปไอคอนไข่สัตว์
///
/// เซฟรูปมาชื่อเป็นตัวเลข (1.png ถึง 5.png) ก็ได้ ตัวนี้จะเปลี่ยนชื่อให้เอง
/// แล้วตั้งค่านำเข้าให้เป็นสไปรท์คมๆ พร้อมใช้
///
/// จับคู่ตามสีไข่กับสีตัวสัตว์:
///   1 ขาว-ม่วง-เขียว  -> ฮัสกี้หิมะ (ตัวขาวเทา)
///   2 ชมพู-เหลือง-ฟ้า -> หมูหมี (หน้าหมูสีชมพู)
///   3 ม่วง-เหลือง     -> แฮมสเตอร์ผึ้ง (ลายเหลืองดำ)
///   4 แดง-ฟ้า-เหลือง  -> ยีราฟขนแกะ (ตัวส้มอมแดง)
///   5 ฟ้า-ส้ม         -> กระต่ายฉลาม (ตัวสีน้ำเงิน)
/// </summary>
public static class EggIconImporter
{
    public const string ItemArtRoot = "Assets/Art/Items";

    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    /// <summary>ชื่อไฟล์ที่เซฟมา -> ชื่อที่ระบบต้องการ</summary>
    private static readonly (string from, string to)[] Renames =
    {
        ("1", "HuskyEgg"),
        ("2", "PigbearEgg"),
        ("3", "BeemsterEgg"),
        ("4", "GirasheepEgg"),
        ("5", "SharkbunEgg"),
    };

    /// <summary>ชื่อไอคอนทั้งหมดที่ต้องตั้งค่านำเข้า</summary>
    private static readonly string[] IconNames =
    {
        "HuskyEgg", "SharkbunEgg", "GirasheepEgg", "PigbearEgg", "BeemsterEgg",
        "HuskyProduce", "SharkbunProduce", "GirasheepProduce",
        "PigbearProduce", "BeemsterProduce",
    };

    [MenuItem("Tools/Farming/จัดการรูปไอคอนไข่")]
    private static void FromMenu()
    {
        var log = new List<string>();
        Run(log);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("จัดการรูปไอคอน",
            log.Count > 0
                ? string.Join("\n", log)
                : $"ยังไม่พบรูปใน {ItemArtRoot}\n\nเซฟรูปไข่ลงโฟลเดอร์นี้ก่อน",
            "โอเค");
    }

    /// <summary>เปลี่ยนชื่อไฟล์ที่เซฟมาเป็นตัวเลข แล้วตั้งค่านำเข้าให้ทุกไอคอน</summary>
    public static void Run(List<string> log)
    {
        if (!Directory.Exists(ItemArtRoot)) return;

        FixNames(log);

        int fixedCount = 0;
        foreach (var name in IconNames)
            if (ApplyImport(name)) fixedCount++;

        if (fixedCount > 0)
            log.Add($"ตั้งค่านำเข้าไอคอนแล้ว {fixedCount} ไฟล์");
    }

    private static void FixNames(List<string> log)
    {
        foreach (var (from, to) in Renames)
        {
            // มีไฟล์ชื่อถูกอยู่แล้วก็ไม่ต้องทำอะไร
            if (Find(to) != null) continue;

            string path = Find(from);
            if (path == null) continue;

            string error = AssetDatabase.RenameAsset(path, to);

            log.Add(string.IsNullOrEmpty(error)
                ? $"เปลี่ยนชื่อ {Path.GetFileName(path)} เป็น {to}.png"
                : $"⚠ เปลี่ยนชื่อ {Path.GetFileName(path)} ไม่สำเร็จ: {error}");
        }
    }

    private static string Find(string name)
    {
        foreach (var ext in Extensions)
        {
            string path = $"{ItemArtRoot}/{name}{ext}";
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>ตั้งค่าให้เป็นสไปรท์คมๆ เหมาะกับไอคอนในกระเป๋า</summary>
    private static bool ApplyImport(string name)
    {
        string path = Find(name);
        if (path == null) return false;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;   // พิกเซลอาร์ตต้องคม
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!changed) return false;

        importer.SaveAndReimport();
        return true;
    }
}
