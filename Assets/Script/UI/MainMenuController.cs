using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// หน้าปกเกม — มีอนิเมชั่นตอนกดเริ่มเกม แล้ววาร์ปซูมเข้าเกม
///
/// วางไว้บน GameObject ชื่อ "MainMenu" ใน Scene หน้าปก
/// (Wizard สร้าง Scene + ตั้งค่าให้อัตโนมัติ)
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene ที่จะเข้าไป")]
    [Tooltip("ชื่อ Scene ของตัวเกม (ต้องอยู่ใน Build Settings ด้วย)")]
    public string gameSceneName = "project";

    [Header("อ้างอิง (Wizard ใส่ให้อัตโนมัติ)")]
    [Tooltip("ภาพหน้าปก — ตัวที่จะขยับและซูม")]
    public RectTransform background;

    [Tooltip("แผ่นสีขาวคลุมจอ ใช้ทำแฟลชตอนวาร์ป")]
    public Image flashOverlay;

    [Header("อนิเมชั่นตอนอยู่เฉย ๆ")]
    [Tooltip("ภาพหน้าปกจะซูมเข้า-ออกช้า ๆ กี่เปอร์เซ็นต์")]
    public float idleZoomAmount = 0.025f;

    [Tooltip("ซูมเข้า-ออกครบ 1 รอบใช้เวลากี่วินาที")]
    public float idleZoomPeriod = 7f;

    [Header("อนิเมชั่นตอนกดปุ่ม")]
    [Tooltip("ปุ่มยุบลงเหลือกี่เท่า")]
    public float punchScale = 0.94f;

    [Tooltip("เวลาที่ใช้ยุบ-เด้งกลับ")]
    public float punchDuration = 0.22f;

    [Header("เอฟเฟกต์วาร์ป")]
    [Tooltip("ซูมเข้าไปกี่เท่าตอนวาร์ป")]
    public float warpZoom = 1.6f;

    [Tooltip("เวลาที่ใช้วาร์ปทั้งหมด")]
    public float warpDuration = 0.9f;

    [Tooltip("หน่วงก่อนโหลด Scene ถัดไป (ให้จอขาวสนิทก่อน)")]
    public float holdBeforeLoad = 0.15f;

    private bool m_Started;
    private Vector3 m_BaseScale = Vector3.one;

    private void Start()
    {
        if (background != null) m_BaseScale = background.localScale;

        if (flashOverlay != null)
        {
            var c = flashOverlay.color;
            flashOverlay.color = new Color(c.r, c.g, c.b, 0f);
            flashOverlay.raycastTarget = false;
        }

        StartCoroutine(IdleBreath());
    }

    /// <summary>ต่อกับปุ่ม "เริ่มเกม" (Wizard ต่อให้แล้ว)</summary>
    public void OnStartGame()
    {
        if (m_Started) return;
        m_Started = true;

        StopAllCoroutines();
        StartCoroutine(StartSequence());
    }

    // ================= อยู่เฉย ๆ: ซูมเข้า-ออกช้า ๆ ให้ภาพดูมีชีวิต =================

    private IEnumerator IdleBreath()
    {
        if (background == null) yield break;

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;

            // sin ให้ค่าระหว่าง -1 ถึง 1 -> แปลงเป็นสเกลรอบ ๆ ค่าเดิม
            float wave = Mathf.Sin(t / idleZoomPeriod * Mathf.PI * 2f);
            float scale = 1f + wave * idleZoomAmount;

            background.localScale = m_BaseScale * scale;
            yield return null;
        }
    }

    // ================= กดปุ่ม -> ยุบ -> วาร์ป =================

    private IEnumerator StartSequence()
    {
        yield return Punch();
        yield return Warp();

        yield return new WaitForSecondsRealtime(holdBeforeLoad);

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[MainMenu] ยังไม่ได้ตั้งชื่อ Scene ของเกมในช่อง Game Scene Name");
            yield break;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>ยุบลงแล้วเด้งกลับ ให้รู้สึกว่ากดโดน</summary>
    private IEnumerator Punch()
    {
        if (background == null) yield break;

        float half = punchDuration * 0.5f;

        // ยุบลง
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            background.localScale = m_BaseScale * Mathf.Lerp(1f, punchScale, k);
            yield return null;
        }

        // เด้งกลับเลยนิดหน่อย
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            background.localScale = m_BaseScale * Mathf.Lerp(punchScale, 1.04f, k);
            yield return null;
        }
    }

    /// <summary>ซูมเข้าไปเรื่อย ๆ พร้อมจอค่อย ๆ ขาว = ความรู้สึกวาร์ป</summary>
    private IEnumerator Warp()
    {
        float t = 0f;
        float fromScale = 1.04f;

        while (t < warpDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / warpDuration);

            // ease-in: ช้าตอนแรก แล้วพุ่งตอนท้าย
            float eased = k * k * k;

            if (background != null)
                background.localScale = m_BaseScale * Mathf.Lerp(fromScale, warpZoom, eased);

            if (flashOverlay != null)
            {
                var c = flashOverlay.color;
                // จอเริ่มขาวตอนผ่านไปครึ่งทาง
                float alpha = Mathf.Clamp01((k - 0.35f) / 0.65f);
                flashOverlay.color = new Color(c.r, c.g, c.b, alpha);
            }

            yield return null;
        }

        if (flashOverlay != null)
        {
            var c = flashOverlay.color;
            flashOverlay.color = new Color(c.r, c.g, c.b, 1f);
        }
    }
}
