#if !UNITY_SERVER
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LuckyDrawAfterMatchCard : MonoBehaviour
{
    [SerializeField]
    private Button drawButton;
    [SerializeField]
    private Image rewardImage;
    [SerializeField]
    private TMP_Text rewardText;
    [SerializeField]
    private GameObject closedFace;
    [SerializeField]
    private GameObject openedFace;
    [SerializeField]
    private GameObject rareBadge;
    [SerializeField]
    private GameObject selectedHighlight;

    private Tween shakeSequence;

    public bool IsOpened { get; private set; }

    private void Awake()
    {
        if (drawButton == null)
            drawButton = GetComponent<Button>();
        if (rewardImage == null)
            rewardImage = GetComponentInChildren<Image>(true);
        if (rewardText == null)
            rewardText = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(OnCardClicked onClicked)
    {
        if (drawButton == null)
            return;

        drawButton.onClick.RemoveAllListeners();
        drawButton.onClick.AddListener(() =>
        {
            SoundManager.Instance?.PlayLuckyDrawClick();
            onClicked?.Invoke(this);
        });
    }

    public void SetInteractable(bool value)
    {
        if (drawButton != null)
            drawButton.interactable = value;
    }

    public void SetClosedFaceSprite(Sprite sprite)
    {
        if (closedFace == null || sprite == null)
            return;

        var closedFaceImage = closedFace.GetComponent<Image>();
        if (closedFaceImage == null)
            return;

        closedFaceImage.sprite = sprite;
        closedFaceImage.enabled = true;
    }

    public void SetSelected(bool value)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(value);
    }

    public void PlayShake()
    {
        StopShake();
        shakeSequence = transform
            .DOShakeRotation(0.35f, new Vector3(0f, 0f, 20f), vibrato: 8, randomness: 90f, fadeOut: false)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    public void StopShake()
    {
        if (shakeSequence != null)
        {
            shakeSequence.Kill();
            shakeSequence = null;
        }
        transform.rotation = Quaternion.identity;
    }

    public void LoadItemRewardSprite(int itemId, Sprite fallback)
    {
        if (rewardImage == null)
            return;

        if (fallback != null)
            rewardImage.sprite = fallback;

        StartCoroutine(AddressablesHelper.LoadSprite($"{AddressablePaths.Items.ImageItem}/{itemId}.png", s =>
        {
            if (s != null && rewardImage != null)
                rewardImage.sprite = s;
        }));
    }

    public void RevealReward(Sprite sprite, string description, bool isRare, bool forceShowRewardImage = false)
    {
        if (IsOpened)
            return;

        StopShake();
        IsOpened = true;
        SetInteractable(false);

        if (rareBadge != null)
            rareBadge.SetActive(isRare);

        if (rewardImage != null)
        {
            rewardImage.sprite = sprite;
            rewardImage.gameObject.SetActive(forceShowRewardImage || sprite != null);
        }
        if (rewardText != null)
        {
            rewardText.text = description;
            rewardText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
        }

        if (closedFace != null)
            closedFace.SetActive(true);
        if (openedFace != null)
            openedFace.SetActive(false);

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(transform.DORotate(new Vector3(0f, 90f, 0f), 0.18f).SetEase(Ease.Linear));
        seq.AppendCallback(() =>
        {
            if (closedFace != null)
                closedFace.SetActive(false);
            if (openedFace != null)
                openedFace.SetActive(true);
        });
        seq.Append(transform.DORotate(Vector3.zero, 0.18f).SetEase(Ease.Linear));
    }

    public delegate void OnCardClicked(LuckyDrawAfterMatchCard card);
}
#endif
