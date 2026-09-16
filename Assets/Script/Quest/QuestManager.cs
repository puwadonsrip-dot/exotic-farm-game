using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>สถานะของเควสที่กำลังเล่นอยู่</summary>
public enum QuestState
{
    Offered,      // ยังไม่ได้รับ — ต้องไปคุยกับ NPC ก่อน
    Active,       // รับแล้ว กำลังทำอยู่
    ReadyToClaim, // ทำครบแล้ว รอกลับไปหา NPC
    AllDone       // เควสหมดแล้ว
}

/// <summary>
/// ตัวจัดการเควสหลัก (Main Quest) — ทำทีละอันเรียงกันไป
/// วางไว้บน GameObject ชื่อ "QuestManager"
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("เควสทั้งหมด เรียงตามลำดับ")]
    public List<QuestData> quests = new List<QuestData>();

    [Header("สถานะปัจจุบัน (ดูอย่างเดียว)")]
    public int currentIndex;
    public QuestState state = QuestState.Offered;
    public int progress;

    /// <summary>เรียกทุกครั้งที่เควสหรือความคืบหน้าเปลี่ยน — UI เอาไปวาดใหม่</summary>
    public event Action OnQuestChanged;

    /// <summary>เรียกตอนได้รางวัล — หน้าต่างรางวัลเอาไปโชว์</summary>
    public event Action<QuestData> OnRewardGranted;

    /// <summary>เควสที่กำลังทำอยู่ (null ถ้าจบหมดแล้ว)</summary>
    public QuestData Current =>
        (currentIndex >= 0 && currentIndex < quests.Count) ? quests[currentIndex] : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (quests.Count == 0) state = QuestState.AllDone;
    }

    private void Start()
    {
        if (FarmManager.Instance != null)
        {
            FarmManager.Instance.OnPlanted += HandlePlanted;
            FarmManager.Instance.OnHarvested += HandleHarvested;
        }
    }

    private void OnDestroy()
    {
        if (FarmManager.Instance != null)
        {
            FarmManager.Instance.OnPlanted -= HandlePlanted;
            FarmManager.Instance.OnHarvested -= HandleHarvested;
        }
    }

    private void HandlePlanted(CropData crop)
    {
        if (crop != null) Report(QuestObjective.PlantCrop, crop.cropId, 1);
    }

    private void HandleHarvested(CropData crop, int amount)
    {
        if (crop != null) Report(QuestObjective.HarvestCrop, crop.cropId, amount);
    }

    // ================= การรับ / ส่งเควส =================

    /// <summary>ตอนนี้ NPC มีอะไรจะคุยด้วยไหม</summary>
    public bool HasSomethingToSay =>
        state == QuestState.Offered || state == QuestState.ReadyToClaim;

    /// <summary>ผู้เล่นกดปุ่ม "รับเควส"</summary>
    public void AcceptCurrent()
    {
        if (state != QuestState.Offered || Current == null) return;

        progress = 0;
        state = QuestState.Active;

        // เควสที่แค่คุยก็จบ ให้ผ่านทันที
        if (Current.objective == QuestObjective.TalkOnly)
            state = Current.returnToNPC ? QuestState.ReadyToClaim : QuestState.Active;

        Debug.Log($"[Quest] รับเควส: {Current.title}");
        OnQuestChanged?.Invoke();

        if (Current.objective == QuestObjective.TalkOnly && !Current.returnToNPC)
            ClaimCurrent();
    }

    /// <summary>ผู้เล่นกลับมาส่งเควส — จ่ายรางวัลแล้วไปเควสถัดไป</summary>
    public void ClaimCurrent()
    {
        if (Current == null) return;

        var quest = Current;
        var inv = InventorySystem.Instance;

        if (inv != null)
        {
            if (quest.rewardMoney > 0)
                inv.AddMoney(quest.rewardMoney);

            if (quest.rewardItem != null && quest.rewardItemCount > 0)
                inv.Add(quest.rewardItem, quest.rewardItemCount);
        }

        Debug.Log($"[Quest] สำเร็จ: {quest.title} — ได้ {quest.rewardMoney}฿");
        OnRewardGranted?.Invoke(quest);

        currentIndex++;
        progress = 0;
        state = currentIndex < quests.Count ? QuestState.Offered : QuestState.AllDone;

        OnQuestChanged?.Invoke();
    }

    // ================= รายงานความคืบหน้า =================

    /// <summary>
    /// แจ้งว่าผู้เล่นทำอะไรไปแล้ว — ระบบอื่นเรียกตัวนี้
    /// เช่น ตอนขายพืช ให้เรียก Report(QuestObjective.SellCrop, "carrot", 3)
    /// </summary>
    public void Report(QuestObjective objective, string targetId, int amount = 1)
    {
        if (state != QuestState.Active || Current == null) return;
        if (Current.objective != objective) return;
        if (!Current.MatchesCrop(targetId)) return;

        progress += amount;

        if (progress >= Current.requiredAmount)
        {
            progress = Current.requiredAmount;
            state = Current.returnToNPC ? QuestState.ReadyToClaim : QuestState.Active;

            if (!Current.returnToNPC)
            {
                OnQuestChanged?.Invoke();
                ClaimCurrent();
                return;
            }

            Debug.Log($"[Quest] ทำครบแล้ว! กลับไปหา NPC เพื่อรับรางวัล");
        }

        OnQuestChanged?.Invoke();
    }

    // ================= ข้อความที่โชว์ในแถบซ้าย =================

    public string GetTrackerTitle()
    {
        if (state == QuestState.AllDone || Current == null) return "";
        return Current.title;
    }

    public string GetTrackerBody(string npcName)
    {
        if (state == QuestState.AllDone || Current == null)
            return "ทำเควสครบทุกอันแล้ว!";

        switch (state)
        {
            case QuestState.Offered:
                return $"ไปคุยกับ {npcName}";

            case QuestState.Active:
                return $"{Current.objectiveText}   {progress}/{Current.requiredAmount}";

            case QuestState.ReadyToClaim:
                return $"เสร็จแล้ว! กลับไปหา {npcName}";
        }
        return "";
    }

    /// <summary>ตอนนี้ควรโชว์ลูกศรชี้ไปหา NPC ไหม</summary>
    public bool ShouldPointToNPC =>
        state == QuestState.Offered || state == QuestState.ReadyToClaim;

    // ================= Save / Load =================

    public void ImportSave(int index, int savedState, int savedProgress)
    {
        currentIndex = Mathf.Clamp(index, 0, quests.Count);
        state = (QuestState)savedState;
        progress = savedProgress;
        OnQuestChanged?.Invoke();
    }
}
