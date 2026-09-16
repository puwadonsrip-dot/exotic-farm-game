using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// สัตว์ 1 ตัวในฟาร์ม
///
/// เดินสุ่มไปมาในคอก สลับเฟรมเป็นอนิเมชั่น กลับด้านตามทิศที่เดิน
/// เดินเข้าไปใกล้แล้วกด E เพื่อให้อาหาร (ใช้ผลผลิตอะไรก็ได้ 1 ชิ้น)
/// </summary>
public class AnimalInstance : MonoBehaviour
{
    /// <summary>
    /// ตอนนี้ผู้เล่นยืนใกล้สัตว์ตัวไหนอยู่ไหม — ใช้ตัดสินว่าปุ่ม E ควรทำอะไร
    ///
    /// เก็บเป็นเลขเฟรมล่าสุดที่มีสัตว์ประกาศว่าใกล้ แทนที่จะเป็น true/false
    /// เพราะมีสัตว์หลายตัว ถ้าใช้ตัวแปร bool ตัวที่อยู่ไกลจะมาปิดของตัวที่อยู่ใกล้
    /// </summary>
    public static bool PlayerNearAnimal => Time.frameCount - s_NearFrame <= 1;

    private static int s_NearFrame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetNearFlag()
    {
        s_NearFrame = -10;
        s_HoverFrame = -10;
        Pet = null;
    }

    [Header("ข้อมูล")]
    public AnimalData data;

    [Tooltip("กินอาหารไปแล้วในวันนี้หรือยัง")]
    public bool fedToday;

    [Tooltip("อายุกี่วันแล้ว — ลูกสัตว์เกิดมาที่ 0")]
    public int ageDays;

    [Header("ค่าสถานะ (0-100)")]
    [Range(0f, 100f)] public float health = 100f;
    [Range(0f, 100f)] public float friendship = 20f;
    [Range(0f, 100f)] public float hunger = 70f;   // ยิ่งมากยิ่งอิ่ม

    /// <summary>ภาวะของสัตว์ตอนนี้ — ดูจากค่าที่แย่ที่สุดก่อน</summary>
    public string Condition
    {
        get
        {
            if (health < 30f) return "ป่วย";
            if (hunger < 25f) return "หิวมาก";
            if (health < 60f) return "อ่อนแอ";
            if (hunger < 50f) return "เริ่มหิว";
            if (friendship >= 80f) return "รักคุณมาก";
            if (friendship >= 50f) return "เชื่องแล้ว";
            return "สบายดี";
        }
    }

    /// <summary>สีของภาวะ เอาไปใช้กับข้อความบนหน้าต่าง</summary>
    public Color ConditionColor
    {
        get
        {
            if (health < 30f || hunger < 25f) return new Color(0.95f, 0.42f, 0.38f);
            if (health < 60f || hunger < 50f) return new Color(0.95f, 0.78f, 0.35f);
            return new Color(0.55f, 0.90f, 0.55f);
        }
    }

    /// <summary>ความคืบหน้าการเติบโต 0-1</summary>
    public float GrowthProgress
    {
        get
        {
            if (data == null || data.daysToAdult <= 0) return 1f;
            return Mathf.Clamp01(ageDays / (float)data.daysToAdult);
        }
    }

    /// <summary>โตเต็มวัยหรือยัง — ตัวเล็กยังให้ผลผลิตและผสมพันธุ์ไม่ได้</summary>
    public bool IsAdult => data == null || ageDays >= data.daysToAdult;

    [Header("ขอบเขตการเดิน")]
    public Vector2 penCenter;
    public Vector2 penSize = new Vector2(6f, 4f);

    [Tooltip("บังคับให้อยู่ในคอกเท่านั้น ออกไปนอกรั้วไม่ได้")]
    public bool confineToPen = true;

    [Header("ระยะที่ผู้เล่นให้อาหารได้")]
    public float interactRange = 1.8f;

    [Header("การเดินตาม")]
    [Tooltip("ตามมาใกล้ผู้เล่นแค่ไหนแล้วหยุด")]
    public float followDistance = 1.4f;

    [Tooltip("ตอนเดินตาม เร็วขึ้นกี่เท่า จะได้ไม่ถูกทิ้ง")]
    public float followSpeedMultiplier = 2.2f;

    /// <summary>สัตว์ตัวที่กำลังเดินตามผู้เล่นอยู่ (มีได้ทีละตัว)</summary>
    public static AnimalInstance Pet { get; private set; }

    /// <summary>เมาส์ชี้อยู่บนตัวสัตว์ไหม — กันไม่ให้เผลอไถดินตอนคลิกสัตว์</summary>
    public static bool MouseOverAnimal => Time.frameCount - s_HoverFrame <= 1;

    private static int s_HoverFrame = -10;

    public bool IsPet => Pet == this;

    private SpriteRenderer m_Renderer;
    private Transform m_Player;

    private Vector2 m_Target;
    private float m_RestLeft;
    private float m_FrameTimer;
    private int m_Frame;
    private float m_HopPhase;

    /// <summary>ตำแหน่งบนพื้นจริงๆ ไม่รวมความสูงที่เด้งตอนเดิน</summary>
    private Vector2 m_Ground;

    private Transform m_Heart;
    private float m_HeartLeft;

    private bool m_InRange;
    private bool m_FacingRight = true;

    /// <summary>ตั้งขนาดตัวตามอายุและทิศที่หัน — ลูกสัตว์ตัวเล็กกว่า</summary>
    public void ApplyScale()
    {
        if (data == null) return;

        float size = IsAdult ? data.scale : data.scale * data.babyScale;
        transform.localScale = new Vector3(m_FacingRight ? size : -size, size, 1f);
    }

    // ================= เริ่มต้น =================

    private void Awake()
    {
        m_Renderer = GetComponent<SpriteRenderer>();
        if (m_Renderer == null) m_Renderer = gameObject.AddComponent<SpriteRenderer>();

        // ต้องมี Collider เมาส์ถึงจะคลิกโดน
        if (GetComponent<Collider2D>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (m_Renderer.sprite != null) box.size = m_Renderer.sprite.bounds.size;
        }
    }

    // ================= คลิกเพื่อสั่งให้ตาม =================

    private void OnMouseOver()
    {
        if (data == null) return;
        s_HoverFrame = Time.frameCount;

        // คลิกขวา = เปิดหน้าต่างดูรายละเอียด
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
        if (AnimalInfoUI.Instance == null) return;

        AnimalInfoUI.Instance.Show(this);
    }

    private void OnMouseDown()
    {
        if (data == null) return;

        bool busy = DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
                    || CutsceneUI.IsPlaying || SettingsMenuUI.IsOpen
                    || DayTransitionUI.IsPlaying || NameEntryUI.IsOpen
                    || TitleScreenUI.IsOpen || AdminModeUI.IsOpen;

        if (busy) return;
        if (!m_InRange)
        {
            DialogueUI.Instance?.SetPrompt($"เดินเข้าไปใกล้{data.displayName}ก่อน");
            return;
        }

        ToggleFollow();
    }

    /// <summary>สลับระหว่างเดินตามผู้เล่นกับเดินเล่นเอง — ตามได้ทีละตัว</summary>
    public void ToggleFollow()
    {
        if (IsPet)
        {
            Pet = null;
            DialogueUI.Instance?.SetPrompt($"{data.displayName} กลับไปเดินเล่นแล้ว");
            return;
        }

        Pet = this;
        m_RestLeft = 0f;

        DialogueUI.Instance?.SetPrompt($"{data.displayName} จะเดินตามคุณไปแล้ว");
        ShowHeart();
    }

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) m_Player = playerGO.transform;

        if (data != null)
        {
            ApplyScale();
            if (data.HasFrames) m_Renderer.sprite = data.frames[0];
        }

        m_Ground = transform.position;
        PickNewTarget();
    }

    /// <summary>ย้ายตัวไปตำแหน่งใหม่ (ใช้ตอนโหลดเซฟ) — ต้องอัปเดตตำแหน่งพื้นด้วย</summary>
    public void Teleport(Vector2 position)
    {
        m_Ground = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    /// <summary>ตั้งค่าตอนสร้างตัวจาก AnimalManager</summary>
    public void Setup(AnimalData animal, Vector2 center, Vector2 size)
    {
        data = animal;
        penCenter = center;
        penSize = size;
    }

    // ================= ทุกเฟรม =================

    private void LateUpdate()
    {
        // ตัวที่อยู่ล่างกว่าให้วาดทับตัวที่อยู่บนกว่า เหมือนมองจากมุมสูง
        if (m_Renderer != null)
            m_Renderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f);
    }

    private void Update()
    {
        if (data == null) return;

        UpdateMovement();
        UpdateAnimation();
        UpdateInteraction();
        UpdateHeart();
    }

    private void UpdateMovement()
    {
        // เดินตามผู้เล่น
        if (IsPet && m_Player != null)
        {
            FollowPlayer();
            return;
        }

        // พักอยู่
        if (m_RestLeft > 0f)
        {
            m_RestLeft -= Time.deltaTime;
            return;
        }

        Vector2 toTarget = m_Target - m_Ground;

        // ถึงที่หมายแล้ว พักก่อนแล้วค่อยหาที่ใหม่
        if (toTarget.sqrMagnitude < 0.02f)
        {
            m_RestLeft = Random.Range(data.restRange.x, data.restRange.y);
            PickNewTarget();
            return;
        }

        Vector2 step = toTarget.normalized * data.moveSpeed * Time.deltaTime;
        m_Ground += step;

        // หันหน้าไปทางที่เดิน (ภาพต้นฉบับหันขวา)
        if (Mathf.Abs(step.x) > 0.0001f)
        {
            m_FacingRight = step.x > 0f;
            ApplyScale();
        }

        ApplyPosition(moving: true);
    }

    /// <summary>
    /// เอาตำแหน่งบนพื้นบวกกับความสูงที่เด้ง แล้วค่อยวางลงบนฉาก
    ///
    /// สำคัญ: ต้องเก็บตำแหน่งพื้นแยกไว้ (m_Ground) ห้ามอ่านกลับจาก transform
    /// ไม่งั้นความสูงที่เด้งจะถูกบวกทบกันทุกเฟรม แล้วสัตว์จะลอยขึ้นฟ้า
    /// </summary>
    private void ApplyPosition(bool moving)
    {
        // กันออกนอกรั้ว — บีบตำแหน่งพื้นให้อยู่ในกรอบคอกเสมอ
        if (confineToPen) m_Ground = ClampToPen(m_Ground);

        float hop = 0f;

        if (moving && data.hopHeight > 0f)
        {
            m_HopPhase += Time.deltaTime * data.walkFps * 2f;
            hop = Mathf.Abs(Mathf.Sin(m_HopPhase)) * data.hopHeight;
        }

        transform.position = new Vector3(m_Ground.x, m_Ground.y + hop, transform.position.z);
    }

    /// <summary>ดึงตำแหน่งกลับเข้าในกรอบคอก</summary>
    private Vector2 ClampToPen(Vector2 point)
    {
        float halfX = Mathf.Max(0.2f, penSize.x * 0.5f);
        float halfY = Mathf.Max(0.2f, penSize.y * 0.5f);

        return new Vector2(
            Mathf.Clamp(point.x, penCenter.x - halfX, penCenter.x + halfX),
            Mathf.Clamp(point.y, penCenter.y - halfY, penCenter.y + halfY));
    }

    /// <summary>เดินตามผู้เล่น หยุดเมื่อใกล้พอ ไม่เดินทับตัวผู้เล่น</summary>
    private void FollowPlayer()
    {
        Vector2 toPlayer = (Vector2)m_Player.position - m_Ground;

        // ใกล้พอแล้ว ยืนรอเฉยๆ
        if (toPlayer.magnitude <= followDistance)
        {
            m_RestLeft = 0.1f;
            ApplyPosition(moving: false);
            return;
        }

        float speed = data.moveSpeed * followSpeedMultiplier;
        m_Ground += toPlayer.normalized * speed * Time.deltaTime;

        if (Mathf.Abs(toPlayer.x) > 0.01f)
        {
            m_FacingRight = toPlayer.x > 0f;
            ApplyScale();
        }

        // เดินตามอยู่ = นับว่ากำลังขยับ อนิเมชั่นจะได้เล่นเร็ว
        m_RestLeft = 0f;
        ApplyPosition(moving: true);
    }

    private void PickNewTarget()
    {
        m_Target = penCenter + new Vector2(
            Random.Range(-penSize.x * 0.5f, penSize.x * 0.5f),
            Random.Range(-penSize.y * 0.5f, penSize.y * 0.5f));
    }

    private void UpdateAnimation()
    {
        if (!data.HasFrames) return;

        bool moving = m_RestLeft <= 0f;
        float fps = moving ? data.walkFps : data.idleFps;
        if (fps <= 0f) return;

        m_FrameTimer += Time.deltaTime;
        if (m_FrameTimer < 1f / fps) return;

        m_FrameTimer = 0f;
        m_Frame = (m_Frame + 1) % data.frames.Length;
        m_Renderer.sprite = data.frames[m_Frame];
    }

    // ================= ให้อาหาร =================

    private void UpdateInteraction()
    {
        if (m_Player == null) return;

        float distance = Vector2.Distance(transform.position, m_Player.position);
        bool inRange = distance <= interactRange;

        m_InRange = inRange;
        if (!inRange) return;

        s_NearFrame = Time.frameCount;

        bool busy = DialogueUI.IsOpen || ShopUI.IsOpen || InventoryUI.IsBackpackOpen
                    || CutsceneUI.IsPlaying || SettingsMenuUI.IsOpen
                    || DayTransitionUI.IsPlaying || NameEntryUI.IsOpen
                    || NPCInteractable.PlayerNearNPC || AdminModeUI.IsOpen;

        if (busy) return;

        ShowPrompt();

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) Feed();
    }

    private void ShowPrompt()
    {
        if (DialogueUI.Instance == null) return;

        string follow = IsPet ? "คลิกซ้าย: หยุดตาม" : "คลิกซ้าย: ให้เดินตาม";

        DialogueUI.Instance.SetPrompt(fedToday
            ? $"{data.displayName} อิ่มแล้ววันนี้     {follow}     คลิกขวา: ดูรายละเอียด"
            : $"[E] ให้อาหาร{data.displayName}     {follow}     คลิกขวา: ดูรายละเอียด");
    }

    /// <summary>ให้อาหาร — ใช้ผลผลิตอะไรก็ได้ในกระเป๋า 1 ชิ้น</summary>
    public void Feed()
    {
        if (fedToday) return;

        var inv = InventorySystem.Instance;
        if (inv == null) return;

        var food = FindFood(inv);
        if (food == null)
        {
            DialogueUI.Instance?.SetPrompt("ไม่มีอาหารในกระเป๋า — เก็บเกี่ยวพืชมาก่อน");
            return;
        }

        if (!inv.Remove(food, 1)) return;

        fedToday = true;
        hunger = 100f;
        friendship = Mathf.Min(100f, friendship + 8f);
        health = Mathf.Min(100f, health + 6f);

        ShowHeart();
        PlayVoice();

        Debug.Log($"[สัตว์] ให้ {food.displayName} กับ {data.displayName} แล้ว");
    }

    /// <summary>หาผลผลิตชิ้นแรกในกระเป๋าที่เอาไปให้สัตว์กินได้</summary>
    private static ItemData FindFood(InventorySystem inv)
    {
        foreach (var slot in inv.slots)
        {
            if (slot.IsEmpty) continue;
            if (slot.item.kind != ItemKind.Produce) continue;

            return slot.item;
        }
        return null;
    }

    private void PlayVoice()
    {
        if (data.voiceClips == null || data.voiceClips.Length == 0) return;
        if (AudioManager.Instance == null) return;

        var clip = data.voiceClips[Random.Range(0, data.voiceClips.Length)];
        AudioManager.Instance.PlayAt(clip);
    }

    // ================= หัวใจตอนอิ่ม =================

    private void ShowHeart()
    {
        if (m_Heart == null) CreateHeart();

        m_Heart.gameObject.SetActive(true);
        m_HeartLeft = 1.4f;
    }

    private void CreateHeart()
    {
        var go = new GameObject("Heart");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = UIShapes.Circle();
        renderer.color = new Color(1f, 0.35f, 0.45f, 1f);
        renderer.sortingLayerID = m_Renderer.sortingLayerID;
        renderer.sortingOrder = m_Renderer.sortingOrder + 1;

        go.transform.localScale = Vector3.one * 0.25f;
        m_Heart = go.transform;
    }

    private void UpdateHeart()
    {
        if (m_Heart == null || m_HeartLeft <= 0f) return;

        m_HeartLeft -= Time.deltaTime;

        // ลอยขึ้นแล้วจางหาย
        m_Heart.localPosition += Vector3.up * Time.deltaTime * 0.5f;

        var renderer = m_Heart.GetComponent<SpriteRenderer>();
        var color = renderer.color;
        color.a = Mathf.Clamp01(m_HeartLeft / 1.4f);
        renderer.color = color;

        if (m_HeartLeft > 0f) return;

        m_Heart.localPosition = new Vector3(0f, 0.9f, 0f);
        m_Heart.gameObject.SetActive(false);
    }

    // ================= ขึ้นวันใหม่ =================

    /// <summary>ผลของการขึ้นวันใหม่ของสัตว์ตัวนี้</summary>
    public struct DayResult
    {
        public ItemData produce;    // ผลผลิตที่ออก (null = ไม่ออก)
        public int produceCount;
        public int money;           // เงิน (ใช้ตอนยังไม่มีไอเทมผลผลิต)
        public bool grewUp;         // เพิ่งโตเป็นตัวเต็มวัยวันนี้
    }

    private int m_ProduceCounter;

    /// <summary>ขึ้นวันใหม่ — โตขึ้น 1 วัน และออกผลผลิตถ้าเมื่อวานได้กิน</summary>
    public DayResult OnNewDay()
    {
        var result = new DayResult();
        if (data == null) return result;

        bool wasAdult = IsAdult;
        ageDays++;

        if (!wasAdult && IsAdult)
        {
            result.grewUp = true;
            ApplyScale();
        }

        // ---- ค่าสถานะประจำวัน ----
        if (fedToday)
        {
            // ได้กินเมื่อวาน = สุขภาพดีขึ้น สนิทขึ้น
            hunger = Mathf.Max(35f, hunger - 30f);
            health = Mathf.Min(100f, health + 4f);
        }
        else
        {
            // อดอาหาร = หิวจัดและอ่อนแอลง ความสนิทลดด้วย
            hunger = Mathf.Max(0f, hunger - 45f);
            health = Mathf.Max(0f, health - (hunger <= 0f ? 20f : 10f));
            friendship = Mathf.Max(0f, friendship - 4f);
        }

        // ตัวเล็กยังไม่ให้ผลผลิต ต้องโตก่อน
        if (fedToday && IsAdult)
        {
            m_ProduceCounter++;

            if (m_ProduceCounter >= Mathf.Max(1, data.daysPerProduce))
            {
                m_ProduceCounter = 0;

                if (data.produceItem != null)
                {
                    result.produce = data.produceItem;
                    result.produceCount = Mathf.Max(1, data.produceAmount);
                }
                else
                {
                    // ยังไม่มีไอเทมผลผลิต ก็จ่ายเป็นเงินไปก่อน
                    result.money = data.dailyIncome;
                }
            }
        }

        fedToday = false;
        return result;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.4f);
        Gizmos.DrawWireCube(penCenter, penSize);
    }
}
