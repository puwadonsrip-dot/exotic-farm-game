using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// NPC ที่เดินเข้าไปคุย / ขายของได้
///
/// วางไว้บน GameObject ของ NPC
/// ไม่ต้องมี Collider — ใช้วิธีวัดระยะห่างจากผู้เล่นแทน ตั้งค่าง่ายกว่า
///
/// กด E = คุย (เควส)
/// กด F = เปิดหน้าต่างขายผลผลิต
/// </summary>
public class NPCInteractable : MonoBehaviour
{
    [Header("ข้อมูล NPC")]
    public string npcName = "พี่สาวชาวบ้าน";

    [Tooltip("ตัวนี้เป็นคนแจก Quest หลักไหม")]
    public bool isQuestGiver = true;

    [Tooltip("ตัวนี้รับซื้อผลผลิตไหม")]
    public bool isBuyer = true;

    [Header("ระยะคุย")]
    [Tooltip("ผู้เล่นต้องเข้ามาใกล้แค่ไหนถึงจะคุยได้ (หน่วยเป็นช่อง) — เลือก NPC แล้วดูวงกลมเหลืองใน Scene view")]
    public float interactRange = 4f;

    [Header("บทพูดทั่วไป")]
    [Tooltip("พูดตอนเควสยังทำไม่เสร็จ")]
    [TextArea(2, 3)]
    public string[] inProgressLines = { "ทำภารกิจให้เสร็จก่อนสิ แล้วค่อยกลับมาคุยกันนะ" };

    [Tooltip("พูดตอนไม่มีเควสแล้ว")]
    [TextArea(2, 3)]
    public string[] idleLines = { "วันนี้อากาศดีนะ ฟาร์มเป็นยังไงบ้าง?" };

    /// <summary>
    /// ตอนนี้ผู้เล่นยืนใกล้ NPC อยู่ไหม
    /// PlayerToolController ใช้เช็คว่าปุ่ม E ควรเป็น "คุย" หรือ "ใช้อุปกรณ์"
    /// </summary>
    public static bool PlayerNearNPC { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetNearFlag() => PlayerNearNPC = false;

    private Transform m_Player;
    private bool m_PromptShowing;
    private bool m_WasInRange;
    private float m_NextDistanceLog;

    private void Start()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) m_Player = playerGO.transform;
        else Debug.LogWarning("[NPC] ไม่พบ GameObject ที่ติด Tag 'Player'");
    }

    private void Update()
    {
        if (m_Player == null) return;

        float distance = Vector2.Distance(transform.position, m_Player.position);
        bool inRange = distance <= interactRange;

        PlayerNearNPC = inRange;

        // บอกใน Console ตอนเข้า/ออกระยะ จะได้รู้ว่าใกล้พอหรือยัง
        if (inRange != m_WasInRange)
        {
            m_WasInRange = inRange;
            Debug.Log(inRange
                ? $"[NPC] เข้าระยะคุยกับ {npcName} แล้ว (ห่าง {distance:F1} / ระยะ {interactRange})"
                : $"[NPC] ออกจากระยะคุย {npcName}");
        }

        // ถ้ายังไม่เคยเข้าระยะเลย ให้บอกระยะห่างที่ใกล้ที่สุดทุก 2 วินาที
        if (!inRange && Time.time >= m_NextDistanceLog)
        {
            m_NextDistanceLog = Time.time + 2f;
            Debug.Log($"[NPC] ห่างจาก {npcName} อยู่ {distance:F1} ช่อง (ต้องเข้าไปใกล้กว่า {interactRange})");
        }

        bool canInteract = inRange
                           && !DialogueUI.IsOpen
                           && !InventoryUI.IsBackpackOpen
                           && !RewardPopupUI.IsOpen
                           && !ShopUI.IsOpen
                           && !CutsceneUI.IsPlaying
                           && !TitleScreenUI.IsOpen
                           && !DayTransitionUI.IsPlaying
                           && !SettingsMenuUI.IsOpen
                           && !NameEntryUI.IsOpen
                           && !AdminModeUI.IsOpen;

        UpdatePrompt(canInteract);

        if (!canInteract) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame) Talk();
        else if (isBuyer && kb.fKey.wasPressedThisFrame) OpenShop();
    }

    // ================= ป้ายบอกปุ่มล่างจอ =================

    private void UpdatePrompt(bool show)
    {
        if (DialogueUI.Instance == null) return;
        if (show == m_PromptShowing && !show) return;

        m_PromptShowing = show;

        if (!show)
        {
            DialogueUI.Instance.SetPrompt("");
            return;
        }

        string prompt = isBuyer
            ? $"[E] คุยกับ{npcName}     [F] ขายผลผลิต"
            : $"[E] คุยกับ{npcName}";

        DialogueUI.Instance.SetPrompt(prompt);
    }

    // ================= ขายของ =================

    public void OpenShop()
    {
        if (ShopUI.Instance == null)
        {
            Debug.LogWarning("[NPC] ไม่พบ ShopUI ใน Scene");
            return;
        }

        DialogueUI.Instance?.SetPrompt("");
        ShopUI.Instance.Open(npcName);
    }

    // ================= คุย =================

    public void Talk()
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("[NPC] ไม่พบ DialogueUI ใน Scene");
            return;
        }

        DialogueUI.Instance.SetPrompt("");

        var qm = QuestManager.Instance;

        if (!isQuestGiver || qm == null)
        {
            DialogueUI.Instance.Show(npcName, idleLines);
            return;
        }

        switch (qm.state)
        {
            case QuestState.Offered:
            {
                var quest = qm.Current;
                if (quest == null || quest.offerLines == null || quest.offerLines.Length == 0)
                {
                    DialogueUI.Instance.Show(npcName, idleLines);
                    return;
                }

                DialogueUI.Instance.Show(
                    npcName,
                    quest.offerLines,
                    acceptLabel: "รับเควส",
                    onAccept: qm.AcceptCurrent);
                break;
            }

            case QuestState.ReadyToClaim:
            {
                var quest = qm.Current;
                var lines = (quest != null && quest.completeLines != null && quest.completeLines.Length > 0)
                    ? quest.completeLines
                    : new[] { "เยี่ยมมาก! นี่รางวัลของเธอ" };

                DialogueUI.Instance.Show(
                    npcName,
                    lines,
                    acceptLabel: "รับรางวัล",
                    onAccept: qm.ClaimCurrent);
                break;
            }

            case QuestState.Active:
                DialogueUI.Instance.Show(npcName, inProgressLines);
                break;

            default:
                DialogueUI.Instance.Show(npcName, idleLines);
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
