using UnityEngine;

/// <summary>สิ่งที่ต้องทำให้สำเร็จใน Quest</summary>
public enum QuestObjective
{
    TalkOnly,      // แค่คุยก็จบ (ใช้เป็น Quest แนะนำตัว)
    PlantCrop,     // ปลูกพืช
    HarvestCrop,   // เก็บเกี่ยวพืช
    SellCrop,      // ขายพืช
    BuyAnimal,     // ซื้อสัตว์      (รอระบบสัตว์)
    FeedAnimal,    // ให้อาหารสัตว์  (รอระบบสัตว์)
    SellAnimal,    // ขายสัตว์       (รอระบบสัตว์)
    BreedAnimal,   // ผสมสัตว์       (รอระบบสัตว์)
    FindAnimal     // ตามหาสัตว์     (รอระบบสัตว์)
}

/// <summary>
/// ข้อมูล Quest 1 อัน
/// สร้างใหม่ได้ที่: คลิกขวาใน Project > Create > Farming > Quest Data
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "Farming/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("ข้อมูลทั่วไป")]
    public string questId = "quest_01";

    [Tooltip("ชื่อเควส โชว์หัวข้อในแถบซ้ายมือ")]
    public string title = "เควสใหม่";

    [Header("บทพูดตอนรับเควส")]
    [Tooltip("NPC จะพูดทีละบรรทัด คลิกเพื่อไปบรรทัดถัดไป")]
    [TextArea(2, 4)]
    public string[] offerLines;

    [Header("สิ่งที่ต้องทำ")]
    public QuestObjective objective = QuestObjective.TalkOnly;

    [Tooltip("ข้อความบอกภารกิจ โชว์ในแถบซ้ายมือ")]
    public string objectiveText = "ทำภารกิจให้สำเร็จ";

    [Tooltip("เจาะจงพืชชนิดไหน (เช่น carrot) — เว้นว่าง = พืชอะไรก็ได้")]
    public string targetCropId = "";

    [Tooltip("ต้องทำกี่ครั้ง")]
    public int requiredAmount = 1;

    [Tooltip("ทำเสร็จแล้วต้องกลับมาคุยกับ NPC เพื่อรับรางวัลไหม")]
    public bool returnToNPC = true;

    [Header("บทพูดตอนส่งเควส")]
    [TextArea(2, 4)]
    public string[] completeLines;

    [Header("รางวัล")]
    public int rewardMoney = 0;
    public ItemData rewardItem;
    public int rewardItemCount = 0;

    /// <summary>ภารกิจนี้เกี่ยวกับพืชชนิดที่กำหนดไว้หรือเปล่า</summary>
    public bool MatchesCrop(string cropId)
    {
        return string.IsNullOrEmpty(targetCropId) || targetCropId == cropId;
    }
}
