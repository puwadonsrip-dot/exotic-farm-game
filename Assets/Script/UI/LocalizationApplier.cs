using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวลงมือเปลี่ยนข้อความบนจอให้ตรงกับภาษาที่เลือก
///
/// มันจะคอยกวาดดู Text ทุกตัวที่กำลังแสดงอยู่ แล้วแปลตามตารางใน <see cref="Localization"/>
/// ข้อความต้นฉบับภาษาไทยถูกจำไว้เสมอ สลับกลับมาไทยได้ทุกเมื่อ
///
/// ตัวนี้สร้างตัวเองอัตโนมัติตอนเปิดเกม ไม่ต้องไปวางใน Scene
/// </summary>
public class LocalizationApplier : MonoBehaviour
{
    /// <summary>กวาดดูทุกกี่วินาที — ถี่กว่านี้ไม่จำเป็น ข้อความไม่ได้เปลี่ยนทุกเฟรม</summary>
    private const float SweepInterval = 0.25f;

    private class Entry
    {
        /// <summary>ข้อความไทยดั้งเดิมที่โค้ดเกมใส่มา</summary>
        public string original;

        /// <summary>ข้อความที่เราเขียนทับลงไปล่าสุด</summary>
        public string shown;
    }

    private readonly Dictionary<Text, Entry> m_Seen = new Dictionary<Text, Entry>();
    private float m_Timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("LocalizationApplier");
        go.AddComponent<LocalizationApplier>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        Localization.OnChanged += RestoreAll;
    }

    private void OnDisable()
    {
        Localization.OnChanged -= RestoreAll;
    }

    private void LateUpdate()
    {
        // หา Text ตัวใหม่ๆ เป็นระยะ (อันนี้หนัก เลยไม่ทำทุกเฟรม)
        m_Timer -= Time.unscaledDeltaTime;
        if (m_Timer <= 0f)
        {
            m_Timer = SweepInterval;
            Discover();
        }

        // ส่วนนี้ต้องทำทุกเฟรม เพราะข้อความอย่างนาฬิกากับวันที่
        // ถูกเขียนทับใหม่ทุกเฟรม ถ้าตามไม่ทันจะเห็นเป็นภาษาไทยตลอด
        Apply();
    }

    /// <summary>
    /// สลับภาษา = เอาข้อความไทยดั้งเดิมใส่กลับไปก่อน
    /// แล้วปล่อยให้รอบกวาดถัดไปแปลใหม่ตามภาษาใหม่
    /// </summary>
    private void RestoreAll()
    {
        var dead = new List<Text>();

        foreach (var pair in m_Seen)
        {
            if (pair.Key == null)
            {
                dead.Add(pair.Key);
                continue;
            }

            pair.Key.text = pair.Value.original;
            pair.Value.shown = null;
        }

        foreach (var t in dead) m_Seen.Remove(t);

        m_Timer = 0f;
        Discover();
        Apply();
    }

    /// <summary>หา Text ตัวใหม่ที่เพิ่งโผล่มาบนจอ</summary>
    private void Discover()
    {
        var texts = Object.FindObjectsByType<Text>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var text in texts)
        {
            if (text == null) continue;
            if (m_Seen.ContainsKey(text)) continue;

            m_Seen[text] = new Entry();
        }

        CleanUp();
    }

    /// <summary>ไล่แปลตัวที่รู้จักแล้วทั้งหมด</summary>
    private void Apply()
    {
        foreach (var pair in m_Seen)
        {
            var text = pair.Key;
            if (text == null) continue;

            var entry = pair.Value;

            string current = text.text;
            if (string.IsNullOrEmpty(current)) continue;

            // ยังเป็นข้อความที่เราเขียนไว้เอง = ไม่มีอะไรเปลี่ยน
            if (current == entry.shown) continue;

            // ข้อความต่างไปจากที่เราเขียน = โค้ดเกมเพิ่งใส่ของใหม่มา
            entry.original = current;

            string translated = Localization.Translate(current);
            entry.shown = translated;

            if (translated != current) text.text = translated;
        }
    }

    /// <summary>ทิ้ง Text ที่ถูกทำลายไปแล้ว ไม่ให้ตารางบวมขึ้นเรื่อยๆ</summary>
    private void CleanUp()
    {
        if (m_Seen.Count < 400) return;

        var dead = new List<Text>();
        foreach (var pair in m_Seen)
            if (pair.Key == null) dead.Add(pair.Key);

        foreach (var t in dead) m_Seen.Remove(t);
    }
}
