using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวจัดการสัตว์ในฟาร์ม
///
/// วางสัตว์ลงในคอกตอนเริ่มเกม แล้วคอยเก็บเงินให้ตอนขึ้นวันใหม่
/// ตัวไหนได้กินอาหารเมื่อวาน วันรุ่งขึ้นจะได้เงิน
/// </summary>
public class AnimalManager : MonoBehaviour
{
    public static AnimalManager Instance { get; private set; }

    [Header("สัตว์ที่มีในฟาร์ม")]
    [Tooltip("ตัวติดตั้งจะใส่ให้เองจากไฟล์ใน Assets/Script/Animal/Animals")]
    public List<AnimalData> animals = new List<AnimalData>();

    [Header("คอกสัตว์")]
    [Tooltip("จุดกึ่งกลางคอก — ลาก GameObject มาวางเพื่อกำหนดตำแหน่ง")]
    public Transform penCenter;

    [Tooltip("ขนาดคอก (กว้าง x สูง หน่วยเป็นช่อง)")]
    public Vector2 penSize = new Vector2(8f, 5f);

    [Header("การวางตัวสัตว์")]
    [Tooltip("วางสัตว์ทุกชนิดลงคอกตอนเริ่มเกม (ปิดไว้ = ต้องไปซื้อเอาเอง)")]
    public bool spawnAllAtStart;

    [Tooltip("ชั้นการวาดภาพ — ต้องเป็นชั้นเดียวกับตัวละคร ไม่งั้นสัตว์จะโดนพื้นดินบัง")]
    public string sortingLayer = "Player";

    private readonly List<AnimalInstance> m_Spawned = new List<AnimalInstance>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (spawnAllAtStart) SpawnAll();

        if (FarmManager.Instance != null)
            FarmManager.Instance.OnDayChanged += OnDayChanged;
    }

    private void OnDestroy()
    {
        if (FarmManager.Instance != null)
            FarmManager.Instance.OnDayChanged -= OnDayChanged;
    }

    // ================= วางสัตว์ =================

    [Tooltip("บังคับให้สัตว์อยู่ในคอกเท่านั้น ไม่ให้เดินไปทั่วแมพ")]
    public bool keepInsidePen = true;

    public Vector2 PenCenter => penCenter != null ? (Vector2)penCenter.position : (Vector2)transform.position;

    /// <summary>จุดนี้อยู่ในคอกไหม</summary>
    public bool IsInsidePen(Vector2 point)
    {
        var center = PenCenter;

        return Mathf.Abs(point.x - center.x) <= penSize.x * 0.5f
            && Mathf.Abs(point.y - center.y) <= penSize.y * 0.5f;
    }

    private void SpawnAll()
    {
        foreach (var animal in animals)
        {
            if (animal == null || !animal.HasFrames) continue;
            Spawn(animal);
        }

        Debug.Log($"[สัตว์] วางสัตว์ลงคอกแล้ว {m_Spawned.Count} ตัว");
    }

    /// <summary>
    /// วางสัตว์ 1 ตัวลงในคอก
    /// nextToPlayer = โผล่ข้างตัวผู้เล่นเลย (ใช้ตอนเพิ่งซื้อมา จะได้เห็นทันที)
    /// </summary>
    public AnimalInstance Spawn(AnimalData animal, bool nextToPlayer = false)
    {
        if (animal == null || !animal.HasFrames) return null;

        var center = PenCenter;
        Vector2 spot;

        if (nextToPlayer)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            spot = player != null
                ? (Vector2)player.transform.position + new Vector2(1.2f, 0f)
                : center;
        }
        else
        {
            spot = center + new Vector2(
                Random.Range(-penSize.x * 0.4f, penSize.x * 0.4f),
                Random.Range(-penSize.y * 0.4f, penSize.y * 0.4f));
        }

        var go = new GameObject(animal.displayName);
        go.transform.SetParent(transform, false);
        go.transform.position = spot;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = animal.frames[0];
        renderer.sortingLayerName = SafeSortingLayer();

        var instance = go.AddComponent<AnimalInstance>();
        instance.confineToPen = keepInsidePen;

        // ซื้อมาใหม่ให้เดินเล่นรอบจุดที่โผล่ ไม่ใช่วิ่งกลับคอกที่อาจอยู่ไกล
        instance.Setup(animal, nextToPlayer ? spot : center, penSize);

        // ทุกตัวเริ่มจากลูกสัตว์ ต้องเลี้ยงให้โตเอง
        instance.ageDays = 0;

        m_Spawned.Add(instance);

        Debug.Log($"[สัตว์] วาง {animal.displayName} ที่ ({spot.x:F1}, {spot.y:F1}) " +
                  $"ชั้น '{SafeSortingLayer()}'");

        return instance;
    }

    /// <summary>
    /// วางสัตว์ลงตรงจุดที่กำหนด (ใช้ตอนผู้เล่นเอาของจากกระเป๋ามาวาง)
    /// asAdult = ซื้อตัวโตมา ไม่ต้องเลี้ยงให้โต
    /// </summary>
    public AnimalInstance SpawnAt(AnimalData animal, Vector2 spot, bool asAdult = false)
    {
        if (animal == null || !animal.HasFrames) return null;

        var go = new GameObject(animal.displayName);
        go.transform.SetParent(transform, false);
        go.transform.position = spot;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = animal.frames[0];
        renderer.sortingLayerName = SafeSortingLayer();

        var instance = go.AddComponent<AnimalInstance>();
        instance.confineToPen = keepInsidePen;

        // ล็อคให้อยู่ในคอก = ใช้ขอบเขตคอกร่วมกันทุกตัว
        // ไม่ล็อค = เดินเล่นรอบจุดที่วางแทน
        instance.Setup(animal, keepInsidePen ? PenCenter : spot, penSize);

        // ซื้อตัวโตมา = ใช้งานได้เลย / ฟักจากไข่ = เริ่มจากตัวเล็ก
        instance.ageDays = asAdult ? animal.daysToAdult : 0;

        m_Spawned.Add(instance);

        Debug.Log($"[สัตว์] ปล่อย {animal.displayName} ที่ ({spot.x:F1}, {spot.y:F1})");
        return instance;
    }

    /// <summary>
    /// ชั้น Default ในโปรเจกต์นี้อยู่ล่างสุด สัตว์จะโดนพื้นดินบังจนมองไม่เห็น
    /// ถ้าเจอว่าตั้งเป็น Default อยู่ ให้เปลี่ยนไปใช้ชั้นเดียวกับตัวละครแทน
    /// </summary>
    private string SafeSortingLayer()
    {
        if (!string.IsNullOrEmpty(sortingLayer)
            && sortingLayer != "Default"
            && SortingLayer.NameToID(sortingLayer) != 0)
            return sortingLayer;

        foreach (var layer in SortingLayer.layers)
        {
            if (layer.name != "Player") continue;

            sortingLayer = "Player";
            return sortingLayer;
        }

        return sortingLayer;
    }

    // ================= ขึ้นวันใหม่ =================

    [Header("การผสมพันธุ์")]
    [Tooltip("มีสัตว์ชนิดเดียวกันโตเต็มวัยและได้กินอาหารครบกี่ตัว ถึงจะออกลูก")]
    public int breedPairSize = 2;

    [Tooltip("มีสัตว์ได้มากสุดกี่ตัว กันคอกแตก")]
    public int maxAnimals = 20;

    /// <summary>สรุปเหตุการณ์เมื่อคืน เอาไปโชว์บนจอข้ามวัน</summary>
    public string LastDayReport { get; private set; } = "";

    private void OnDayChanged(int day)
    {
        int money = 0;
        int produced = 0;
        int grewUp = 0;

        // นับว่าชนิดไหนมีตัวโตที่ได้กินอาหารกี่ตัว เอาไว้คิดการผสมพันธุ์
        var readyToBreed = new Dictionary<AnimalData, int>();

        // ก๊อปปี้ลิสต์ก่อน เพราะลูกที่เกิดใหม่จะถูกเพิ่มเข้า m_Spawned ระหว่างวน
        var todays = new List<AnimalInstance>(m_Spawned);

        foreach (var animal in todays)
        {
            if (animal == null) continue;

            bool wasFedAdult = animal.fedToday && animal.IsAdult;
            var result = animal.OnNewDay();

            if (result.grewUp) grewUp++;
            if (result.money > 0) money += result.money;

            if (result.produce != null && InventorySystem.Instance != null)
            {
                InventorySystem.Instance.Add(result.produce, result.produceCount);
                produced += result.produceCount;
            }

            if (!wasFedAdult || !animal.data.canBreed) continue;

            readyToBreed.TryGetValue(animal.data, out int count);
            readyToBreed[animal.data] = count + 1;
        }

        int babies = Breed(readyToBreed);

        if (money > 0) InventorySystem.Instance?.AddMoney(money);

        BuildReport(money, produced, grewUp, babies);
    }

    /// <summary>ชนิดไหนมีตัวโตที่ได้กินครบคู่ ก็ออกลูก 1 ตัว</summary>
    private int Breed(Dictionary<AnimalData, int> readyToBreed)
    {
        int babies = 0;

        foreach (var pair in readyToBreed)
        {
            if (pair.Value < breedPairSize) continue;
            if (m_Spawned.Count >= maxAnimals) break;

            var baby = Spawn(pair.Key);
            if (baby == null) continue;

            baby.ApplyScale();
            babies++;

            Debug.Log($"[สัตว์] {pair.Key.displayName} ออกลูกใหม่ 1 ตัว");
        }

        return babies;
    }

    private void BuildReport(int money, int produced, int grewUp, int babies)
    {
        var parts = new List<string>();

        if (produced > 0) parts.Add($"เก็บผลผลิตจากสัตว์ {produced} ชิ้น");
        if (money > 0) parts.Add($"สัตว์ทำเงินให้ {money}฿");
        if (grewUp > 0) parts.Add($"ลูกสัตว์โตเต็มวัย {grewUp} ตัว");
        if (babies > 0) parts.Add($"มีลูกสัตว์เกิดใหม่ {babies} ตัว");

        LastDayReport = parts.Count > 0 ? string.Join("   •   ", parts) : "";

        if (parts.Count > 0) Debug.Log("[สัตว์] " + LastDayReport);
    }

    // ================= Save / Load =================

    [System.Serializable]
    public class AnimalSave
    {
        public string animalId;
        public float x, y;
        public bool fedToday;
        public int ageDays;
        public float health;
        public float friendship;
        public float hunger;
    }

    public List<AnimalSave> ExportSave()
    {
        var list = new List<AnimalSave>();

        foreach (var animal in m_Spawned)
        {
            if (animal == null || animal.data == null) continue;

            var p = animal.transform.position;
            list.Add(new AnimalSave
            {
                animalId = animal.data.animalId,
                x = p.x,
                y = p.y,
                fedToday = animal.fedToday,
                ageDays = animal.ageDays,
                health = animal.health,
                friendship = animal.friendship,
                hunger = animal.hunger
            });
        }

        return list;
    }

    public void ImportSave(List<AnimalSave> list)
    {
        // ล้างตัวเดิมออกก่อน
        foreach (var animal in m_Spawned)
            if (animal != null) Destroy(animal.gameObject);

        m_Spawned.Clear();

        if (list == null) return;

        foreach (var saved in list)
        {
            var data = animals.Find(a => a != null && a.animalId == saved.animalId);
            if (data == null) continue;

            var instance = Spawn(data);
            if (instance == null) continue;

            instance.Teleport(new Vector2(saved.x, saved.y));
            instance.fedToday = saved.fedToday;
            instance.ageDays = saved.ageDays;
            instance.health = saved.health;
            instance.friendship = saved.friendship;
            instance.hunger = saved.hunger;
            instance.ApplyScale();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.5f);
        Gizmos.DrawWireCube(PenCenter, penSize);
    }
}
