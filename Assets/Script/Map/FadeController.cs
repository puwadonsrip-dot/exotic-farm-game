using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    /// <summary>สร้าง FadeController อัตโนมัติตอนเกมเริ่ม ไม่ต้องวางใน Scene ด้วยตัวเอง</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("FadeController [Auto]");
        go.AddComponent<FadeController>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Auto-setup: ถ้ายังไม่ได้ assign fadeCanvasGroup ใน Inspector
        // จะสร้าง Canvas + Image + CanvasGroup ให้อัตโนมัติ
        if (fadeCanvasGroup == null)
        {
            SetupFadeCanvas();
        }
    }

    private void SetupFadeCanvas()
    {
        // สร้าง Canvas ใหม่สำหรับ fade
        GameObject canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // ให้อยู่บนสุดเสมอ

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // สร้าง Image สีดำเต็มจอ
        GameObject imageGO = new GameObject("FadeImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        Image img = imageGO.AddComponent<Image>();
        img.color = Color.black;

        RectTransform rt = imageGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // เพิ่ม CanvasGroup
        fadeCanvasGroup = imageGO.AddComponent<CanvasGroup>();


    }

    private void Start()
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogError("[FadeController] fadeCanvasGroup ยังเป็น null! กรุณา assign CanvasGroup ใน Inspector");
            return;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>จางเป็นสีดำ (alpha 0 -> 1)</summary>
    public IEnumerator FadeToBlack()
    {
        yield return StartCoroutine(Fade(0f, 1f));
    }

    /// <summary>จางกลับจากสีดำ (alpha 1 -> 0)</summary>
    public IEnumerator FadeFromBlack()
    {
        yield return StartCoroutine(Fade(1f, 0f));
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogError("[FadeController] Fade ไม่ทำงาน: fadeCanvasGroup เป็น null");
            yield break;
        }

        fadeCanvasGroup.alpha = from;
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = (to > 0.5f);
    }
}
