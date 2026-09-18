using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ทำให้ลากไอเทมในช่องได้
///
/// ลากไปวางช่องอื่น   = สลับที่ / รวมกองถ้าเป็นของชนิดเดียวกัน
/// ลากไปวางถังขยะ     = ทิ้งถาวร (ถามยืนยันก่อน)
/// ลากออกนอกกระเป๋า   = วางลงพื้นในเกม เดินไปเก็บคืนได้
///
/// ติดไว้บน GameObject ของแต่ละช่อง — InventoryUI ใส่ให้เองตอนสร้างช่อง
/// </summary>
public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>ช่องที่ตัวนี้คุมอยู่ (index ใน InventorySystem.slots)</summary>
    public int slotIndex = -1;

    /// <summary>กำลังลากอยู่ไหม — ระบบอื่นเอาไปเช็คได้</summary>
    public static bool IsDragging { get; private set; }

    private static GameObject s_Ghost;
    private static Image s_GhostImage;
    private static Canvas s_GhostCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetState()
    {
        IsDragging = false;
        s_Ghost = null;
        s_GhostImage = null;
        s_GhostCanvas = null;
    }

    // ================= เริ่มลาก =================

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        var item = inv.ItemAt(slotIndex);
        if (item == null) return;          // ช่องว่าง ไม่มีอะไรให้ลาก

        IsDragging = true;
        ShowGhost(item.icon);
        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging) return;
        MoveGhost(eventData);
    }

    // ================= ปล่อย =================

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging) return;

        IsDragging = false;
        HideGhost();

        var inv = InventorySystem.Instance;
        if (inv == null) return;
        if (inv.ItemAt(slotIndex) == null) return;

        // ---- ดูว่าปล่อยลงบนอะไร ----
        var hits = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(eventData, hits);

        bool overInventoryUI = false;

        foreach (var hit in hits)
        {
            // วางทับอีกช่อง = สลับที่
            var target = hit.gameObject.GetComponentInParent<ItemDragHandler>();
            if (target != null && target.slotIndex >= 0)
            {
                if (target.slotIndex != slotIndex)
                {
                    inv.SwapSlots(slotIndex, target.slotIndex);
                    AudioManager.PlaySelect();
                }
                return;
            }

            // วางทับถังขยะ = ถามก่อนทิ้ง
            if (hit.gameObject.GetComponentInParent<InventoryTrashZone>() != null)
            {
                InventoryUI.Instance?.AskTrash(slotIndex);
                return;
            }

            // วางทับพื้นที่อื่นของหน้ากระเป๋า = ไม่ทำอะไร คืนที่เดิม
            if (hit.gameObject.GetComponentInParent<InventoryUIArea>() != null)
                overInventoryUI = true;
        }

        if (overInventoryUI) return;

        // ---- ปล่อยนอกหน้ากระเป๋า = วางลงพื้นในเกม ----
        DropToWorld(inv);
    }

    private void DropToWorld(InventorySystem inv)
    {
        var item = inv.ItemAt(slotIndex);
        int count = inv.CountAt(slotIndex);
        if (item == null || count <= 0) return;

        // สัตว์กับไข่ไม่ใช่ของที่โยนทิ้งบนพื้นเฉยๆ — ถามก่อนว่าจะเอามาเลี้ยงหรือทิ้ง
        var manager = AnimalManager.Instance;
        if (manager != null && manager.FindByItem(item, out _) != null)
        {
            InventoryUI.Instance?.AskAnimal(slotIndex);
            return;
        }

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            Debug.LogWarning("[กระเป๋า] ไม่เจอตัวละคร เลยวางของลงพื้นไม่ได้");
            return;
        }

        // วางข้างตัวละครนิดหน่อย ไม่ให้ทับตัวเอง
        Vector2 spot = (Vector2)playerGO.transform.position
                       + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.9f, -0.4f));

        int taken = inv.RemoveAt(slotIndex);
        if (taken <= 0) return;

        DroppedItem.Spawn(item, taken, spot);
        AudioManager.PlaySelect();
    }

    // ================= ภาพที่ลากตามเมาส์ =================

    private void ShowGhost(Sprite icon)
    {
        if (s_Ghost == null) CreateGhost();
        if (s_Ghost == null) return;

        s_GhostImage.sprite = icon;
        s_Ghost.SetActive(true);
        s_Ghost.transform.SetAsLastSibling();
    }

    private static void HideGhost()
    {
        if (s_Ghost != null) s_Ghost.SetActive(false);
    }

    private void CreateGhost()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        s_GhostCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        s_Ghost = new GameObject("DragGhost", typeof(RectTransform));
        s_Ghost.transform.SetParent(s_GhostCanvas.transform, false);

        s_GhostImage = s_Ghost.AddComponent<Image>();
        s_GhostImage.preserveAspect = true;
        s_GhostImage.raycastTarget = false;          // ห้ามบังการ raycast ไม่งั้นหาเป้าหมายไม่เจอ
        s_GhostImage.color = new Color(1f, 1f, 1f, 0.85f);

        var rt = (RectTransform)s_Ghost.transform;
        rt.sizeDelta = new Vector2(84f, 84f);
    }

    private void MoveGhost(PointerEventData eventData)
    {
        if (s_Ghost == null || s_GhostCanvas == null) return;

        var rt = (RectTransform)s_Ghost.transform;
        var canvasRT = (RectTransform)s_GhostCanvas.transform;

        var camera = s_GhostCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : s_GhostCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, eventData.position, camera, out var local))
            rt.localPosition = local;
    }
}
