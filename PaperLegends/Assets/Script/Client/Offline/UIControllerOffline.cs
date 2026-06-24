using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if !UNITY_SERVER
using DG.Tweening;
#endif

public class UIControllerOffline : MonoBehaviour
{
    [Header("SYSTEM CONFIG")]
    public static UIControllerOffline Instance;
    public TMP_InputField inputField;
    public Button startButton;
    public GameObject UIBet;
    public List<SkillType> selectedSkills = new();

    [Header("Prefab CONFIG")]
    public GameObject playerItemPrefab;
    public GameObject inforPrefab;
    public GameObject messagePrefab;
    public Transform canvasTransform;

    [Header("Panel CONFIG")]
    public Transform playerListPanel;
    public Transform InforListPanel;

    [Header("UI GAME OVER CONFIG")]
    public GameObject UIGameOVer;
    public TextMeshProUGUI ResultBall;
    public TextMeshProUGUI ResultExp;

    [Header("UI GAME PLAY CONFIG")]
    public GameObject ZoneUINeedToHide;
    public GameObject UIMove;
    public GameObject UIJoystick;
    public Sprite[] comboSprites;
    public GameObject BackgroundMesage;
    public TextMeshProUGUI showMes;

    [Header("TIME CONFIG")]
    public float baseTime = 30f;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI currentTurnText;

    public GameObject moveRangeIndicator;
    public float moveDistance = 1.5f;

    public Action OnLoseByTimeout;
    public Action OnTimeOut;

    private int betAmount = 1;

    private void Awake()
    {
        Instance = this;
        if (timerText != null)
            timerText.text = "0";
    }

    private void Start()
    {
        // Tùy chỉnh nếu cần trong tương lai
    }

    #region TURN INDICATOR
    public IEnumerator ShowTurnIndicatorRunTime(string message, float speed, int delay)
    {
#if UNITY_SERVER
        Debug.Log($"[ShowTurnIndicatorRunTime] {message}");
        yield break;
#else
        GameObject textObj = Instantiate(messagePrefab, canvasTransform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

        if (textMesh == null || textRect == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            yield break;
        }

        textMesh.text = message;

        float startX = Screen.width / 2 + moveDistance;
        float middleX = 0;
        float endX = -Screen.width / 2 - moveDistance;

        textRect.anchoredPosition = new Vector2(startX, 0);

        Tween tween = textRect.DOAnchorPosX(middleX, speed);
        yield return tween.WaitForCompletion();

        yield return new WaitForSeconds(delay);

        tween = textRect.DOAnchorPosX(endX, speed);
        yield return tween.WaitForCompletion();

        Destroy(textObj);
#endif
    }

    public void ShowTurnIndicator(string message, int speed, int delay)
    {
#if UNITY_SERVER
        Debug.Log($"[ShowTurnIndicator] {message}");
#else
        GameObject textObj = Instantiate(messagePrefab, canvasTransform);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();

        if (textMesh == null || textRect == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            return;
        }

        textMesh.text = message;

        float startX = Screen.width / 2 + moveDistance;
        float middleX = 0;
        float endX = -Screen.width / 2 - moveDistance;

        textRect.anchoredPosition = new Vector2(startX, 0);

        textRect.DOAnchorPosX(middleX, speed).SetEase(Ease.OutExpo)
            .OnComplete(() =>
            {
                DOVirtual.DelayedCall(delay, () =>
                {
                    textRect.DOAnchorPosX(endX, speed).SetEase(Ease.InExpo)
                        .OnComplete(() => Destroy(textObj));
                });
            });
#endif
    }
    #endregion

    #region MESSAGE
    public void ShowMesByUser(string mess)
    {
#if UNITY_SERVER
        Debug.Log($"[ShowMesByUser] {mess}");
#else
        RectTransform scoreRect = showMes.GetComponent<RectTransform>();
        TextMeshProUGUI scoreText = showMes.GetComponent<TextMeshProUGUI>();

        if (scoreRect == null || scoreText == null)
        {
            Debug.LogError("Prefab thiếu TextMeshProUGUI hoặc RectTransform!");
            return;
        }

        scoreText.text = mess;
        scoreRect.anchoredPosition = new Vector2(0, -300f);
        scoreRect.localScale = Vector3.one * 0.8f;

        Sequence seq = DOTween.Sequence();
        seq.Append(scoreRect.DOAnchorPosY(-180f, 0.4f).SetEase(Ease.OutBack));
        seq.Join(scoreRect.DOScale(Vector3.one, 0.4f));
        seq.AppendInterval(0.7f);
        seq.Append(scoreRect.DOAnchorPosY(-300f, 0.5f).SetEase(Ease.InBack));
#endif
    }

    public void ShowMessage(string message, float speed, float duration)
    {
        GameObject messageObject = Instantiate(messagePrefab, canvasTransform);
        TextMeshProUGUI messageText = messageObject.GetComponent<TextMeshProUGUI>();
        messageText.text = message;
        StartCoroutine(AnimateText(messageObject, speed, duration));
    }

    private IEnumerator AnimateText(GameObject messageObject, float speed, float pauseTime)
    {
        RectTransform rectTransform = messageObject.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = messageObject.AddComponent<CanvasGroup>();

        rectTransform.anchoredPosition = new Vector2(-Screen.width / 2, 0);
        rectTransform.localScale = Vector3.one * 0.8f;
        canvasGroup.alpha = 0;

        float elapsedTime = 0f;
        Vector2 targetPosition = Vector2.zero;

        while (elapsedTime < 0.5f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, speed * Time.deltaTime);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1, Time.deltaTime * 5);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, Vector3.one, Time.deltaTime * 3);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(pauseTime);

        elapsedTime = 0f;
        while (elapsedTime < 0.5f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, new Vector2(Screen.width / 2, 0), speed * Time.deltaTime);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0, Time.deltaTime * 3);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, Vector3.one * 1.1f, Time.deltaTime * 2);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(messageObject);
    }

    public void ShowComboEffect(int combo)
    {
#if UNITY_SERVER
        Debug.Log($"[ShowComboEffect] Combo {combo}");
#else
        string[] comboTexts =
        {
            "Mở hàng!",
            "Trúng phát nữa!",
            "Ăn liên tiếp!",
            "Trúng như thần!",
            "Thánh bắn bi!",
            "Trùm sân đất!"
        };
        int index = Mathf.Clamp(combo - 1, 0, comboTexts.Length - 1);
        GameObject obj = Instantiate(messagePrefab, canvasTransform);
        var rect = obj.GetComponent<RectTransform>();
        var text = obj.GetComponent<TextMeshProUGUI>();
        text.text = comboTexts[index];
        rect.anchoredPosition = Vector2.zero;
        CanvasGroup cg = obj.AddComponent<CanvasGroup>();
        cg.alpha = 0;
        Sequence seq = DOTween.Sequence();
        seq.Append(cg.DOFade(1f, 0.2f));
        seq.Join(rect.DOAnchorPosY(150f, 1f).SetEase(Ease.OutBack));
        seq.AppendInterval(0.5f);
        seq.Append(cg.DOFade(0f, 0.5f));
        seq.OnComplete(() => Destroy(obj));
#endif
    }
    #endregion

    #region OFFLINE PLAY MODES
    public void UIforView()
    {
        foreach (var itemP in NPCController.Instance.players)
        {
            itemP.playerbody.SetActive(true);
        }
        ZoneUINeedToHide.SetActive(false);
        UIMove.SetActive(false);
        UIJoystick.SetActive(false);
    }

    public void UIforPlay()
    {
        foreach (var itemP in NPCController.Instance.players)
        {
            itemP.playerbody.SetActive(false);
        }

        ZoneUINeedToHide.SetActive(true);
        UIMove.SetActive(false);
        UIJoystick.SetActive(true);
    }

    public void UIforPlayExamOrFromStartPoint()
    {
        foreach (var itemP in NPCController.Instance.players)
        {
            itemP.playerbody.SetActive(false);
        }

        ZoneUINeedToHide.SetActive(true);
        UIMove.SetActive(true);
        UIJoystick.SetActive(true);
    }
    #endregion

    #region BET
    public void ValidateInput(string value)
    {
        if (int.TryParse(value, out int result))
        {
            var playerDataInfor = NPCController.Instance.playerData;
            betAmount = Mathf.Clamp(result, 1, playerDataInfor.RingBall);
        }
        else
        {
            betAmount = 1;
            inputField.text = "1";
        }
    }

    public void PlaceBet()
    {
        NPCController.Instance.PlayGame(betAmount);
        UIBet.SetActive(false);
    }

    public void UpBet()
    {
        var playerDataInfor = NPCController.Instance.playerData;
        if (betAmount >= playerDataInfor.RingBall)
            return;

        if (betAmount == 20)
            return;

        betAmount += 1;
        inputField.text = betAmount.ToString();
    }

    public void DownBet()
    {
        if (betAmount <= 1)
            return;

        betAmount -= 1;
        inputField.text = betAmount.ToString();
    }
    #endregion

    #region PLAYER LIST
    public void ShowPlayerList()
    {
        foreach (Transform child in playerListPanel)
        {
            Destroy(child.gameObject);
        }

        List<Player> players = NPCController.Instance.players.ToList();
        if (players.Count == 0)
            return;

        int maxScore = players.Max(p => p.score);
        int currentIndex = NPCController.Instance.currentPlayerIndex;
        Player currentPlayer = players[currentIndex];

        foreach (Player player in players)
        {
            GameObject newItem = Instantiate(playerItemPrefab, playerListPanel);
            newItem.transform.Find("Avatar").GetComponent<Image>().sprite = player.avatar;
            var nameText = newItem.transform.Find("PlayerName").GetComponent<TMP_Text>();
            nameText.text = player.fullname;
            if (!player.isAI)
                nameText.color = Color.red;
            newItem.transform.Find("Score").GetComponent<TMP_Text>().text = player.score.ToString();
            player.positionShowMess = newItem.transform.Find("Mess").GetComponent<TextMeshProUGUI>();

            var comboObj = newItem.transform.Find("Image");
            if (comboObj != null)
            {
                var imgCombo = comboObj.GetComponent<Image>();
                if (player.combo > 0 && comboSprites != null && comboSprites.Length > 0)
                {
                    int idx = Mathf.Clamp(player.combo - 1, 0, comboSprites.Length - 1);
                    imgCombo.sprite = comboSprites[idx];
                    comboObj.gameObject.SetActive(true);
                }
                else
                {
                    comboObj.gameObject.SetActive(false);
                }
            }

            if (player.isDestroy)
            {
                CanvasGroup canvasGroup = newItem.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0.5f;
                canvasGroup.interactable = false;
            }

            if (player.score > 0 && player.score == maxScore && !player.isDestroy)
            {
                newItem.transform.Find("Fire").gameObject.SetActive(true);
            }
            else
            {
                newItem.transform.Find("Fire").gameObject.SetActive(false);
            }

            if (player == currentPlayer)
            {
                newItem.transform.localScale = Vector3.one * 1.2f;
                newItem.GetComponent<Image>().color = Color.white;
            }

            if (!player.isDestroy)
            {
                newItem.GetComponent<Button>().onClick.AddListener(() =>
                    CameraRotation.Instance.RotateCameraToPoint(player.ball.transform.position));
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListPanel.GetComponent<RectTransform>());
    }

    public void ShowInforList()
    {
        foreach (Transform child in InforListPanel)
        {
            Destroy(child.gameObject);
        }

        var playerDataInfor = NPCController.Instance.playerData;
        GameObject newItem = Instantiate(inforPrefab, InforListPanel);
        //newItem.transform.Find("Score").GetComponent<TMP_Text>().text = playerDataInfor.score.ToString();
        newItem.transform.Find("RingBall").GetComponent<TMP_Text>().text = playerDataInfor.RingBall.ToString();

        LayoutRebuilder.ForceRebuildLayoutImmediate(InforListPanel.GetComponent<RectTransform>());
    }
    #endregion

    #region GAME OVER
    public void PlayAgain()
    {
        CameraRotation.Instance.StopAllCoroutines();
        NPCController.Instance.StopAllCoroutines();
        BallAI.Instance.StopAllCoroutines();
        BallController.Instance.StopAllCoroutines();
        UIGameOVer.SetActive(false);
        UIBet.SetActive(true);
    }

    public void GameOver()
    {
        Player playerToUpdate = NPCController.Instance.players.Find(p => p.tagPlyer == "Player");

        UIGameOVer.SetActive(true);
        Time.timeScale = 0;
        if (playerToUpdate != null)
        {
            int exp = playerToUpdate.score * 200;
            ResultExp.text = exp.ToString();
            ResultBall.text = playerToUpdate.score.ToString();
        }
    }
    #endregion

    #region UTILS
    public void onClickRingBall()
    {
        int currentIndex = NPCController.Instance.currentPlayerIndex;
        Player currentPlayer = NPCController.Instance.players[currentIndex];
        if (currentPlayer.statusPlayer == StatusPlayer.StartPoint || currentPlayer.statusPlayer == StatusPlayer.Normal)
        {
            CameraRotation.Instance.RotateCameraToPoint(NPCController.Instance.playArea.transform.position);
        }
    }

    public void SelectSkill(int skillIndex)
    {
        int currentIndex = NPCController.Instance.currentPlayerIndex;
        Player currentPlayer = NPCController.Instance.players[currentIndex];
        IFBall ballScript = currentPlayer.ball.GetComponent<IFBall>();
        if (ballScript != null)
        {
            ballScript.SetStatusUltimate();
        }

        SkillType selectedSkill = (SkillType)skillIndex;
        if (!selectedSkills.Contains(selectedSkill))
        {
            selectedSkills.Add(selectedSkill);
        }
        SkillManager.Instance.ShowSkillUsedList();
    }
    #endregion
}
