using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class FoldedPaperController : MonoBehaviour, IDragHandler
{
    [Header("Unfold Settings")]
    public float requiredDrag = 200f;
    public Vector3 foldedRotation = new Vector3(-90f, 0f, 0f);
    public Vector3 unfoldedRotation = Vector3.zero;
    public Vector3 foldedScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 unfoldedScale = Vector3.one;

    [Header("UI")]
    public TextMeshProUGUI resultText;
    public GameObject celebrationParticlePrefab;
    public AudioClip celebrationClip;

    private float dragAccum;
    private bool revealed;
    private LuckyDrawResponse result;

    private void Awake()
    {
        transform.localRotation = Quaternion.Euler(foldedRotation);
        transform.localScale = foldedScale;
        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }

    public void Init(TextMeshProUGUI text, GameObject particle, AudioClip clip)
    {
        resultText = text;
        celebrationParticlePrefab = particle;
        celebrationClip = clip;
        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }

    public void SetResult(LuckyDrawResponse r)
    {
        result = r;
        if (resultText != null)
        {
            string msg = r.IsCuli ? "Better luck next time!" : $"x{r.Quantity}";
            resultText.text = msg;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (revealed || eventData.delta.y <= 0f)
            return;

        dragAccum += eventData.delta.y;
        float t = Mathf.Clamp01(dragAccum / requiredDrag);
        transform.localRotation = Quaternion.Lerp(Quaternion.Euler(foldedRotation), Quaternion.Euler(unfoldedRotation), t);
        transform.localScale = Vector3.Lerp(foldedScale, unfoldedScale, t);

        if (t >= 1f)
        {
            revealed = true;
            if (resultText != null)
                resultText.gameObject.SetActive(true);

            if (result != null && !result.IsCuli)
            {
                if (celebrationParticlePrefab != null)
                    Instantiate(celebrationParticlePrefab, transform.position, Quaternion.identity, transform.parent);
                if (celebrationClip != null)
                    SoundManager.Instance?.sfxSource?.PlayOneShot(celebrationClip);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
