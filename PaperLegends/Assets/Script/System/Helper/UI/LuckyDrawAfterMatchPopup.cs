#if !UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LuckyDrawAfterMatchPopup : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField]
    private Transform cardParent;
    [SerializeField]
    private GameObject cardPrefab;
    [SerializeField]
    private TMP_Text remainingDrawText;
    [SerializeField]
    private Button closeButton;
    [SerializeField]
    private Sprite ringBallSprite;
    [SerializeField]
    private Sprite expSprite;
    [SerializeField]
    private Sprite fallbackItemSprite;
    [Header("Closed Face Backgrounds")]
    [SerializeField]
    private List<Sprite> closedFaceBackgroundSprites = new();

    private readonly List<LuckyDrawAfterMatchCard> cards = new();
    private int remainingActualDraws;
    private int totalDrawSlots;
    private int playerId;
    private bool isProcessing;
    private bool rareRewardOpened;
    private Action onClosed;
    private Tween cardAttentionTween;
    private Vector3 cardParentOriginalScale = Vector3.one;
    private GameObject _overlayCanvas;
    private LuckyDrawAfterMatchReward preclaimedReward;

    private static readonly int[] RareItemIds = new int[]
    {
        99000006, 99000007, 99000008, 99000009, 99000010,
        99000011, 99000012, 99000013, 99000014, 99000015,
    };

    public void SetOverlayCanvas(GameObject canvas)
    {
        _overlayCanvas = canvas;
    }

    private void OnDestroy()
    {
        StopCardAttentionEffect();
        if (_overlayCanvas != null)
            Destroy(_overlayCanvas);
    }

    public void Init(int playerId, bool isWinner, Action onClosed = null, LuckyDrawAfterMatchReward preclaimedReward = null)
    {
        this.playerId = playerId;
        this.onClosed = onClosed;
        this.preclaimedReward = preclaimedReward;
        remainingActualDraws = isWinner ? 3 : 1;
        totalDrawSlots = 6;
        RefreshCounter();
        BuildCards();
        //not use
       // PlayCardAttentionEffect();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            closeButton.interactable = false;
        }
    }

    private void OnCloseButtonClicked()
    {
        if (isProcessing)
            return;

        onClosed?.Invoke();

        if (_overlayCanvas != null)
        {
            var canvas = _overlayCanvas;
            _overlayCanvas = null;
            Destroy(canvas);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BuildCards()
    {
        cards.Clear();
        if (cardParent == null || cardPrefab == null)
            return;

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);

        var closedFacePool = PrepareClosedFaceSpritePool();
        for (int i = 0; i < totalDrawSlots; i++)
        {
            var cardObj = Instantiate(cardPrefab, cardParent);
            var card = cardObj.GetComponent<LuckyDrawAfterMatchCard>();
            if (card == null)
                card = cardObj.AddComponent<LuckyDrawAfterMatchCard>();

            card.Bind(OnCardClicked);
            card.SetInteractable(true);
            card.SetClosedFaceSprite(PopClosedFaceSprite(closedFacePool));
            card.SetSelected(false);
            cards.Add(card);
        }
    }

    private void OnCardClicked(LuckyDrawAfterMatchCard card)
    {
        if (card == null || card.IsOpened || isProcessing)
            return;

        if (remainingActualDraws > 0)
        {
            card.SetSelected(true);
            StartCoroutine(RunActualDraw(card));
        }
        else
        {
            RevealFake(card);
        }
    }

    private IEnumerator RunActualDraw(LuckyDrawAfterMatchCard card)
    {
        isProcessing = true;
        card.PlayShake();

        LuckyDrawAfterMatchReward reward = null;
        if (preclaimedReward != null)
        {
            reward = preclaimedReward;
            preclaimedReward = null;
            yield return new WaitForSecondsRealtime(0.35f);
        }
        else
        {
            yield return LoadingManager.Instance.RunTaskWithTimeout(
                APIManager.Instance.LuckyDrawAfterMatchAsync(playerId), r => reward = r);
        }

        card.StopShake();

        if (reward == null)
        {
            RevealRewardOnCard(card, null);
        }
        else
        {
            rareRewardOpened |= reward.isRare;
            RevealRewardOnCard(card, reward);
            remainingActualDraws = Mathf.Max(remainingActualDraws - 1, 0);
        }

        RefreshCounter();
        isProcessing = false;

        if (AllCardsOpened())
            StopCardAttentionEffect();

        if (remainingActualDraws == 0)
        {
            EnableCloseWhenDone();
            StartCoroutine(RevealRemainingFakeCards());
        }
    }

    private void RefreshCounter()
    {
        if (remainingDrawText != null)
            remainingDrawText.text = remainingActualDraws.ToString();
    }

    private void EnableCloseWhenDone()
    {
        if (closeButton != null)
            closeButton.interactable = true;
    }

    private void RevealFake(LuckyDrawAfterMatchCard card)
    {
        var fakeReward = GenerateFakeReward();
        RevealRewardOnCard(card, fakeReward);
        card.SetSelected(false);
        if (fakeReward.isRare)
            rareRewardOpened = true;

        if (AllCardsOpened() && closeButton != null)
            closeButton.interactable = true;

        if (AllCardsOpened())
            StopCardAttentionEffect();
    }

    private LuckyDrawAfterMatchReward GenerateFakeReward()
    {
        bool forceRare = !rareRewardOpened && CountUnopenedCards() <= 1;
        if (forceRare)
        {
            return new LuckyDrawAfterMatchReward
            {
                rewardType = "item",
                itemId = RareItemIds[UnityEngine.Random.Range(0, RareItemIds.Length)],
                isRare = true,
                luckyRate = 100,
            };
        }

        float roll = UnityEngine.Random.value;
        if (roll < 0.33f)
        {
            return new LuckyDrawAfterMatchReward
            {
                rewardType = "stats",
                ringBall = UnityEngine.Random.Range(1, 4),
                exp = 0,
                isRare = false,
            };
        }
        if (roll < 0.66f)
        {
            return new LuckyDrawAfterMatchReward
            {
                rewardType = "stats",
                ringBall = 0,
                exp = UnityEngine.Random.Range(50, 101),
                isRare = false,
            };
        }

        int itemId = PickWeightedItemId();
        return new LuckyDrawAfterMatchReward
        {
            rewardType = "item",
            itemId = itemId,
            isRare = false,
        };
    }

    private int PickWeightedItemId()
    {
        var weights = new List<(int id, int weight)>
        {
            (98000001, 50),
            (98000002, 30),
            (98000003, 10),
        };

        int totalWeight = 0;
        foreach (var pair in weights)
            totalWeight += pair.weight;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (var pair in weights)
        {
            if (roll < pair.weight)
                return pair.id;
            roll -= pair.weight;
        }

        return weights[0].id;
    }

    private IEnumerator RevealRemainingFakeCards()
    {
        isProcessing = true;
        foreach (var card in cards)
        {
            if (card != null && !card.IsOpened)
            {
                RevealFake(card);
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }
        isProcessing = false;

        if (AllCardsOpened())
            StopCardAttentionEffect();
    }

    private int CountUnopenedCards()
    {
        int count = 0;
        foreach (var c in cards)
        {
            if (c != null && !c.IsOpened)
                count++;
        }
        return count;
    }

    private bool AllCardsOpened()
    {
        foreach (var c in cards)
        {
            if (c != null && !c.IsOpened)
                return false;
        }
        return true;
    }

    private Sprite GetSpriteForReward(LuckyDrawAfterMatchReward reward)
    {
        if (reward == null)
            return fallbackItemSprite;

        if (reward.IsItem)
            return ItemVisualHelper.LoadSpriteByID(reward.itemId);

        if (reward.IsStats)
        {
            if (reward.ringBall > 0 && ringBallSprite != null)
                return ringBallSprite;
            if (reward.exp > 0 && expSprite != null)
                return expSprite;
        }

        return fallbackItemSprite;
    }

    private string GetDescriptionForReward(LuckyDrawAfterMatchReward reward)
    {
        if (reward == null)
            return LocalizationManager.Instance.GetText("lucky_later_infor");

        return reward switch
        {
            _ when reward.IsItem && reward.itemName != null => LocalizationManager.Instance.GetText(reward.itemName),
            _ when reward.ringBall > 0 => $"+{reward.ringBall}",
            _ when reward.exp > 0 => $"+{reward.exp} EXP",
            _ => LocalizationManager.Instance.GetText("lucky_later_infor")
        };
    }


    private void RevealRewardOnCard(LuckyDrawAfterMatchCard card, LuckyDrawAfterMatchReward reward)
    {
        if (card == null)
            return;

        if (reward == null)
        {
            card.RevealReward(fallbackItemSprite, LocalizationManager.Instance.GetText("lucky_later_infor"), false);
            return;
        }

        if (reward.IsItem)
        {
            card.RevealReward(fallbackItemSprite, GetDescriptionForReward(reward), reward.isRare, true);
            card.LoadItemRewardSprite(reward.itemId, fallbackItemSprite);
            return;
        }

        card.RevealReward(GetSpriteForReward(reward), GetDescriptionForReward(reward), reward.isRare);
    }

    private List<Sprite> PrepareClosedFaceSpritePool()
    {
        var pool = new List<Sprite>();
        foreach (var sprite in closedFaceBackgroundSprites)
        {
            if (sprite != null)
                pool.Add(sprite);
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
        }

        return pool;
    }

    private Sprite PopClosedFaceSprite(List<Sprite> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        var sprite = pool[0];
        pool.RemoveAt(0);
        return sprite;
    }

    private void PlayCardAttentionEffect()
    {
        StopCardAttentionEffect();

        if (cardParent == null)
            return;

        cardParentOriginalScale = cardParent.localScale;
        cardParent.localScale = cardParentOriginalScale;
        cardAttentionTween = cardParent
            .DOScale(cardParentOriginalScale * 1.06f, 0.55f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void StopCardAttentionEffect()
    {
        if (cardAttentionTween != null)
        {
            cardAttentionTween.Kill();
            cardAttentionTween = null;
        }

        if (cardParent != null)
            cardParent.localScale = cardParentOriginalScale;
    }
}
#endif
