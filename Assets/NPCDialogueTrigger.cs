using UnityEngine;

public class NPCDialogueTrigger : MonoBehaviour
{
    [SerializeField] private GameObject dialogueCanvas;

    private void Start()
    {
        if (dialogueCanvas != null)
            dialogueCanvas.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            dialogueCanvas.SetActive(false);
        }
    }
}