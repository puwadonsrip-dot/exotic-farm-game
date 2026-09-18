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

    /// <summary>
    /// ตัวคูณเสียงรวม ปรับได้ 0–200% จากเมนูตั้งค่า
    ///
    /// ทำไมไม่ใช้ AudioListener.volume: ค่านั้น Unity ล็อกไว้ที่ 1.0 เกินไม่ได้
    /// เลยต้องมาคูณเองตรงนี้แทน — PlayOneShot รับค่าเกิน 1 ได้ และเสียงบรรยากาศ
    /// ตั้งฐานไว้ต่ำ (0.35) พอคูณ 2 ก็ยังไม่ชนเพดาน
    /// </summary>
    public const float MaxMaster = 2f;

    private const string MasterKey = "audio_master";
    private static float s_Master = 1f;

    /// <summary>ระดับเสียงรวม 0 = เงียบ, 1 = ปกติ, 2 = ดังสุด</summary>
    public static float Master
    {
        get => s_Master;
        set
        {
            s_Master = Mathf.Clamp(value, 0f, MaxMaster);
            PlayerPrefs.SetFloat(MasterKey, s_Master);
            PlayerPrefs.Save();
        }
    }

    /// <summary>อ่านค่าที่เคยตั้งไว้กลับมาตอนเปิดเกม</summary>
    public static void LoadMaster()
    {
        s_Master = Mathf.Clamp(PlayerPrefs.GetFloat(MasterKey, 1f), 0f, MaxMaster);

        // ตัวนี้ต้องเป็น 1 เสมอ ไม่งั้นมันจะไปหารเสียงซ้ำอีกชั้น
        AudioListener.volume = 1f;
    }

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
        LoadMaster();

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
        m_AmbienceA.volume = AmbienceTarget;
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

    /// <summary>ระดับเสียงบรรยากาศจริงหลังคูณเสียงรวมแล้ว (AudioSource รับได้ไม่เกิน 1)</summary>
    private float AmbienceTarget => Mathf.Clamp01(ambienceVolume * s_Master);

    /// <summary>ตัวที่กำลังใช้ดังขึ้น อีกตัวเบาลงจนเงียบ</summary>
    private void FadeAmbience()
    {
        float target = AmbienceTarget;

        float step = ambienceFadeTime > 0f
            ? (Mathf.Max(target, 0.05f) / ambienceFadeTime) * Time.unscaledDeltaTime
            : 1f;

        var active = m_UsingA ? m_AmbienceA : m_AmbienceB;
        var fading = m_UsingA ? m_AmbienceB : m_AmbienceA;

        active.volume = Mathf.MoveTowards(active.volume, target, step);
        fading.volume = Mathf.MoveTowards(fading.volume, 0f, step);

        if (fading.volume <= 0.001f && fading.isPlaying) fading.Stop();
    }

    // ================= เสียงเอฟเฟกต์ =================

    private void PlayOne(AudioClip clip, float pitchJitter = 0.06f)
    {
        if (clip == null || m_Sfx == null) return;

        // เปลี่ยนระดับเสียงนิดหน่อยทุกครั้ง ฟังแล้วไม่เหมือนหุ่นยนต์
        m_Sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

        // PlayOneShot รับค่าเกิน 1 ได้ — ตรงนี้แหละที่ทำให้ดันไปถึง 200% ได้จริง
        m_Sfx.PlayOneShot(clip, sfxVolume * s_Master);
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
