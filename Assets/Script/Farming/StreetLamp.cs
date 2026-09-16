using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// เสาไฟข้างทาง — ติดเองตอนกลางคืน ดับเองตอนเช้า
///
/// ความสว่างไล่ตามความมืดของฟ้า ไม่ใช่ติดดับแบบสวิตช์
/// มีไฟกะพริบเบาๆ ให้ดูเหมือนไฟจริง
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StreetLamp : MonoBehaviour
{
    [Header("ไฟ")]
    [Tooltip("ไฟวงกลมของเสานี้ — ตัวติดตั้งสร้างให้เอง")]
    public Light2D lamp;

    [Tooltip("ความสว่างสูงสุดตอนดึกสนิท")]
    public float maxIntensity = 1.4f;

    [Tooltip("รัศมีแสง (ช่อง)")]
    public float lightRadius = 4.5f;

    public Color lightColor = new Color(1f, 0.86f, 0.58f);

    [Header("ไฟกะพริบ")]
    [Tooltip("กะพริบแรงแค่ไหน (0 = ไม่กะพริบ)")]
    [Range(0f, 0.3f)]
    public float flicker = 0.07f;

    [Tooltip("กะพริบเร็วแค่ไหน")]
    public float flickerSpeed = 6f;

    [Header("ตำแหน่งไฟ")]
    [Tooltip("ไฟอยู่สูงจากโคนเสาเท่าไหร่ — ปรับให้ตรงหัวโคม")]
    public Vector2 lightOffset = new Vector2(0f, 1.35f);

    private float m_Seed;

    private void Awake()
    {
        // สุ่มจังหวะกะพริบของแต่ละต้นให้ไม่ตรงกัน
        m_Seed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        if (lamp == null) return;

        float darkness = GetDarkness();

        bool on = darkness > 0.03f;
        if (lamp.enabled != on) lamp.enabled = on;
        if (!on) return;

        float wobble = flicker > 0f
            ? 1f + Mathf.Sin((Time.time + m_Seed) * flickerSpeed) * flicker
            : 1f;

        lamp.intensity = maxIntensity * darkness * wobble;
    }

    /// <summary>ตอนนี้ฟ้ามืดแค่ไหน 0 = กลางวันเต็มที่ 1 = ดึกสนิท</summary>
    private float GetDarkness()
    {
        var cycle = DayNightCycle.Instance;

        // ไม่มีระบบกลางวันกลางคืน ก็เปิดไฟไว้เลย
        if (cycle == null || cycle.globalLight == null) return 1f;

        return Mathf.InverseLerp(0.95f, 0.25f, cycle.globalLight.intensity);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + (Vector3)lightOffset, lightRadius);
    }
}
