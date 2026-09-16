using System;
using UnityEngine;

/// <summary>
/// ข้อมูลตัวละครของผู้เล่น — ตอนนี้มีแค่ชื่อ
/// เก็บไว้ใน PlayerPrefs เปิดเกมใหม่ก็ยังจำได้
/// </summary>
public static class PlayerProfile
{
    private const string NameKey = "ExoticFarm.PlayerName";
    public const int MaxNameLength = 14;

    /// <summary>ชื่อที่ใช้ตอนยังไม่ได้ตั้ง</summary>
    public const string DefaultName = "ชาวไร่";

    private static string s_Name;

    /// <summary>เรียกทุกครั้งที่ชื่อเปลี่ยน — UI เอาไปอัปเดต</summary>
    public static event Action<string> OnNameChanged;

    public static string Name
    {
        get
        {
            if (string.IsNullOrEmpty(s_Name))
                s_Name = PlayerPrefs.GetString(NameKey, DefaultName);

            return string.IsNullOrEmpty(s_Name) ? DefaultName : s_Name;
        }
    }

    /// <summary>ตั้งชื่อใหม่ — ตัดช่องว่างหัวท้ายและจำกัดความยาวให้เอง</summary>
    public static void SetName(string value)
    {
        value = (value ?? "").Trim();

        if (value.Length > MaxNameLength)
            value = value.Substring(0, MaxNameLength);

        if (string.IsNullOrEmpty(value)) value = DefaultName;

        s_Name = value;
        PlayerPrefs.SetString(NameKey, value);
        PlayerPrefs.Save();

        Debug.Log($"[Player] ตั้งชื่อตัวละครเป็น '{value}'");
        OnNameChanged?.Invoke(value);
    }

    /// <summary>เคยตั้งชื่อไปแล้วหรือยัง</summary>
    public static bool HasName => PlayerPrefs.HasKey(NameKey);

    /// <summary>ล้างชื่อทิ้ง (ใช้ตอนเริ่มเกมใหม่)</summary>
    public static void Forget()
    {
        s_Name = null;
        PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.Save();
    }
}
