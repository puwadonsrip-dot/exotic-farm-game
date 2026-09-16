using UnityEngine;

/// <summary>
/// ตัวจัดการเสียงทั้งเกม
///
/// มี 2 ช่อง:
///   - เสียงบรรยากาศ  เล่นวนตลอด สลับกลางวัน/กลางคืนเองตามเวลาในเกม
///   - เสียงเอฟเฟกต์  เล่นทีละครั้งตอนทำอะไรสักอย่าง
///
/// เรียกใช้จากที่ไหนก็ได้: AudioManager.PlayTill(), AudioManager.PlayWater() ฯลฯ
/// ถ้ายังไม่มี AudioManager ในฉาก จะเงียบเฉยๆ ไม่พัง
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("ระดับเสียง")]
    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;

    [Tooltip("เวลาที่ใช้ค่อยๆ สลับเสียงบรรยากาศกลางวัน/กลางคืน (วินาที)")]
    public float ambienceFadeTime = 2.5f;

    [Header("เสียงบรรยากาศ")]
    public AudioClip ambienceDay;
    public AudioClip ambienceNight;

    [Header("เสียงตอนทำฟาร์ม")]
    [Tooltip("สุ่มเล่นทีละอัน ให้ฟังไม่ซ้ำซาก")]
    public AudioClip[] tillClips;
    public AudioClip[] waterClips;
    public AudioClip plantClip;
    public AudioClip harvestClip;

    [Header("เสียง UI")]
    public AudioClip buySellClip;
    public AudioClip selectClip;
    public AudioClip closeClip;
    public AudioClip cropReadyClip;

    private AudioSource m_Sfx;
    private AudioSource m_AmbienceA;
    private AudioSource m_AmbienceB;

    private bool m_UsingA = true;
    private bool m_WasNight;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        m_Sfx = gameObject.AddComponent<AudioSource>();
        m_Sfx.playOnAwake = false;

        m_AmbienceA = MakeAmbienceSource();
        m_AmbienceB = MakeAmbienceSource();
    }

    private AudioSource MakeAmbienceSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        return source;
    }

    private void Start()
    {
        bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
        m_WasNight = night;

        var clip = night ? ambienceNight : ambienceDay;
        if (clip == null) return;

        m_AmbienceA.clip = clip;
        m_AmbienceA.volume = ambienceVolume;
        m_AmbienceA.Play();
    }

    private void Update()
    {
        FadeAmbience();

        var cycle = DayNightCycle.Instance;
        if (cycle == null) return;

        if (cycle.IsNight == m_WasNight) return;

        m_WasNight = cycle.IsNight;
        SwitchAmbience(m_WasNight ? ambienceNight : ambienceDay);
    }

    // ================= เสียงบรรยากาศ =================

    private void SwitchAmbience(AudioClip clip)
    {
        if (clip == null) return;

        var next = m_UsingA ? m_AmbienceB : m_AmbienceA;

        next.clip = clip;
        next.volume = 0f;
        next.Play();

        m_UsingA = !m_UsingA;
    }

    /// <summary>ตัวที่กำลังใช้ดังขึ้น อีกตัวเบาลงจนเงียบ</summary>
    private void FadeAmbience()
    {
        float step = ambienceFadeTime > 0f
            ? (ambienceVolume / ambienceFadeTime) * Time.unscaledDeltaTime
            : ambienceVolume;

        var active = m_UsingA ? m_AmbienceA : m_AmbienceB;
        var fading = m_UsingA ? m_AmbienceB : m_AmbienceA;

        active.volume = Mathf.MoveTowards(active.volume, ambienceVolume, step);
        fading.volume = Mathf.MoveTowards(fading.volume, 0f, step);

        if (fading.volume <= 0.001f && fading.isPlaying) fading.Stop();
    }

    // ================= เสียงเอฟเฟกต์ =================

    private void PlayOne(AudioClip clip, float pitchJitter = 0.06f)
    {
        if (clip == null || m_Sfx == null) return;

        // เปลี่ยนระดับเสียงนิดหน่อยทุกครั้ง ฟังแล้วไม่เหมือนหุ่นยนต์
        m_Sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        m_Sfx.PlayOneShot(clip, sfxVolume);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        PlayOne(clips[Random.Range(0, clips.Length)]);
    }

    // ---- เรียกจากที่อื่นผ่านตัวนี้ ----

    public static void PlayTill() => Instance?.PlayRandom(Instance.tillClips);
    public static void PlayWater() => Instance?.PlayRandom(Instance.waterClips);
    public static void PlayPlant() => Instance?.PlayOne(Instance.plantClip);
    public static void PlayHarvest() => Instance?.PlayOne(Instance.harvestClip);

    /// <summary>เล่นเสียงที่ส่งเข้ามาโดยตรง (เสียงร้องของสัตว์ ฯลฯ)</summary>
    public void PlayAt(AudioClip clip) => PlayOne(clip);

    public static void PlayBuySell() => Instance?.PlayOne(Instance.buySellClip);
    public static void PlaySelect() => Instance?.PlayOne(Instance.selectClip, 0.02f);
    public static void PlayClose() => Instance?.PlayOne(Instance.closeClip, 0.02f);
    public static void PlayCropReady() => Instance?.PlayOne(Instance.cropReadyClip, 0f);
}
