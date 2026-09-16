using UnityEngine;

/// <summary>
/// ข้อมูลสัตว์ 1 ชนิด
/// สร้างใหม่ได้ที่: คลิกขวาใน Project > Create > Farming > Animal Data
///
/// ใช้ภาพหลายเฟรมสลับกันเป็นอนิเมชั่น (ปกติ 3 เฟรม)
/// ตัวติดตั้งจะตัดจากไฟล์รวมใน Assets/Art/Animals/ ให้เอง
/// </summary>
[CreateAssetMenu(fileName = "NewAnimal", menuName = "Farming/Animal Data")]
public class AnimalData : ScriptableObject
{
    [Header("ข้อมูลทั่วไป")]
    [Tooltip("ไอดีที่ไม่ซ้ำกัน ใช้ตอน Save")]
    public string animalId = "new_animal";

    [Tooltip("ชื่อที่โชว์ในเกม")]
    public string displayName = "สัตว์แปลก";

    [Tooltip("ภาพแต่ละเฟรม สลับไปมาเป็นอนิเมชั่น")]
    public Sprite[] frames;

    [Tooltip("ขนาดตัวในเกม (1 = เท่าขนาดรูปต้นฉบับ)")]
    public float scale = 1f;

    [Header("การขยับ")]
    [Tooltip("สลับเฟรมกี่ครั้งต่อวินาทีตอนเดิน")]
    public float walkFps = 6f;

    [Tooltip("สลับเฟรมกี่ครั้งต่อวินาทีตอนยืนเฉยๆ")]
    public float idleFps = 2f;

    [Tooltip("ความเร็วเดิน (ช่องต่อวินาที)")]
    public float moveSpeed = 0.8f;

    [Tooltip("เดินถึงที่แล้วหยุดพักกี่วินาที (ต่ำสุด, สูงสุด)")]
    public Vector2 restRange = new Vector2(1.5f, 4.5f);

    [Tooltip("เด้งตัวขึ้นลงตอนเดิน — 0 = ไม่เด้ง")]
    [Range(0f, 0.3f)]
    public float hopHeight = 0.08f;

    [Header("การเลี้ยง")]
    [Tooltip("ให้อาหารแล้วได้เงินกี่บาทในวันถัดไป (ใช้ตอนยังไม่มีไอเทมผลผลิต)")]
    public int dailyIncome = 45;

    [Tooltip("ผลผลิตที่ออกมา เช่น ไข่ — ยังไม่มีก็เว้นว่างไว้ได้ ระบบจะจ่ายเป็นเงินแทน")]
    public ItemData produceItem;

    [Tooltip("ให้อาหารครบกี่วันถึงจะออกผลผลิต 1 ชิ้น")]
    public int daysPerProduce = 1;

    [Tooltip("ออกทีละกี่ชิ้น")]
    public int produceAmount = 1;

    [Tooltip("ราคาขายผลผลิต")]
    public int producePrice = 45;

    [Tooltip("ราคาซื้อตัวโตเต็มวัย — แพงกว่าเพราะใช้งานได้ทันที")]
    public int buyPrice = 400;

    [Tooltip("ราคาซื้อไข่ — ถูกกว่า แต่ต้องเลี้ยงให้โตเอง")]
    public int eggPrice = 240;

    [Tooltip("ไข่ของสัตว์ชนิดนี้ — ฟักออกมาเป็นตัวเล็กของชนิดนี้เท่านั้น")]
    public ItemData eggItem;

    [Tooltip("ตัวโตเต็มวัย — วางแล้วใช้งานได้ทันที")]
    public ItemData adultItem;

    [Header("การผสมพันธุ์")]
    [Tooltip("ผสมพันธุ์ได้ไหม")]
    public bool canBreed = true;

    [Tooltip("ลูกสัตว์ต้องโตกี่วันถึงจะเป็นตัวเต็มวัย (ให้ผลผลิตและผสมพันธุ์ได้)")]
    public int daysToAdult = 3;

    [Tooltip("ตอนเป็นลูกสัตว์ ตัวเล็กแค่ไหนเทียบกับตัวโต")]
    [Range(0.3f, 1f)]
    public float babyScale = 0.6f;

    [Header("เสียง")]
    public AudioClip[] voiceClips;

    /// <summary>เฟรมแรกที่ใช้ได้ เอาไว้โชว์เป็นไอคอน</summary>
    public Sprite Icon => frames != null && frames.Length > 0 ? frames[0] : null;

    public bool HasFrames => frames != null && frames.Length > 0;
}
