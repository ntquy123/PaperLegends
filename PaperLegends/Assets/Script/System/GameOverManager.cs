using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Runtime.CompilerServices;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    public GameObject EndGamePopup;
    public GameObject DrawPopup;
    public Transform EndGameResultContent; // Content of EndGame_ScrollView
    public GameObject endGameResultItemPrefab;
    [SerializeField]
    private Button luckyDrawButton;
    private bool isWinner = true;
    private readonly Dictionary<int, Sprite> resultAvatarSprites = new();
    public bool HasGameOverResults { get; private set; }
    public bool IsLuckyDrawPopupOpen { get; private set; }
    private bool isReturningToMenuForLuckyDraw;
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
    }

    private void Start()
    {
        if (luckyDrawButton != null)
        {
            // Replace the Inspector callback as well; RemoveAllListeners does not remove persistent listeners.
            luckyDrawButton.onClick = new Button.ButtonClickedEvent();
            luckyDrawButton.onClick.AddListener(ContinueToLuckyDrawInMenu);
        }
    }

    public void ShowGameOverResults(List<OverGameRequest> results)
    {
        Debug.Log($"🏁 [CLIENT] Bắt đầu xử lý ShowGameOverResults. Results count: {results?.Count ?? 0}.");
        bool wasGameOver = HasGameOverResults;
        HasGameOverResults = true;
        Time.timeScale = 0f;

        if (!wasGameOver)
        {
            var networkManager = GameManagerNetWork.Instance;
            if (networkManager != null)
            {
                // ACK và ReadyToDisconnect đã được gửi trước đó trong RpcShowOverGameResult
                // (trước khi timeScale=0). Ở đây chỉ cần ngắt kết nối runner.
                Debug.Log("🔌 [CLIENT] Ngắt kết nối runner sau khi ACK đã được gửi.");
                networkManager.CloseConnectToRunner();
            }
        }

        if (EndGamePopup != null)
            EndGamePopup.SetActive(true);

        Debug.Log("✅ [CLIENT] Đã hiển thị popup GameOver.");

        if (EndGameResultContent != null)
        {
            ClearResultAvatarSprites();
            foreach (Transform child in EndGameResultContent)
            {
                Destroy(child.gameObject);
            }

            foreach (var r in results ?? new List<OverGameRequest>())
            {
                var item = Instantiate(endGameResultItemPrefab, EndGameResultContent);
                var nameText = item.transform.Find("PlayerNameText")?.GetComponent<TMP_Text>();
                if (nameText != null) nameText.text = r.playerName;

                var marblesText = item.transform.Find("MarblesWonText")?.GetComponent<TMP_Text>();
                if (marblesText != null) marblesText.text = $"+{r.marblesWon}";

                var expText = item.transform.Find("ExpText")?.GetComponent<TMP_Text>();
                if (expText != null) expText.text = $"+{r.expGained}";

                var avatarTransform = item.transform.Find("Avatar");
                if (avatarTransform != null)
                {
                    var rawImage = avatarTransform.GetComponent<RawImage>();
                    var image = avatarTransform.GetComponent<Image>();
                    TryLoadResultAvatar(r, rawImage, image);
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(EndGameResultContent.GetComponent<RectTransform>());
        }

        int playerId = GameManagerNetWork.Instance != null ? GameManagerNetWork.Instance.loginUserModel.UserId : -1;
        var myResult = results?.Find(r => r.playerId == playerId);
        isWinner = myResult != null && myResult.StatusWin == (int)StatusWin.Win;

       // LuckyDrawManager.Instance.SetMaxDraw(isWinner ? 3 : 1);
        //LuckyDrawManager.Instance?.ResetLuckyDrawState();
    }

    private void TryLoadResultAvatar(OverGameRequest result, RawImage rawImage, Image image)
    {
        if (result == null || (rawImage == null && image == null))
            return;

        var avatarUrl = result.avatarUrl;
        if (string.IsNullOrWhiteSpace(avatarUrl))
            return;

        var avatarService = AvatarService.EnsureInstance();
        if (avatarService == null)
            return;

        avatarService.LoadAvatar(new AvatarService.AvatarRequest(AuthenticationProviderType.Anonymous, avatarUrl, string.Empty),
            texture => ApplyResultAvatarTexture(result.playerId, texture, rawImage, image),
            _ => { });
    }

    private void ApplyResultAvatarTexture(int playerId, Texture2D texture, RawImage rawImage, Image image)
    {
        if (texture == null)
            return;

        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.color = Color.white;
            return;
        }

        if (image == null)
            return;

        if (resultAvatarSprites.TryGetValue(playerId, out var existingSprite))
        {
            if (existingSprite == null || existingSprite.texture != texture)
            {
                if (existingSprite != null)
                {
                    Destroy(existingSprite);
                }

                existingSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                resultAvatarSprites[playerId] = existingSprite;
            }
        }
        else
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            resultAvatarSprites[playerId] = sprite;
        }

        image.sprite = resultAvatarSprites[playerId];
        image.color = Color.white;
    }

    private void ClearResultAvatarSprites()
    {
        foreach (var sprite in resultAvatarSprites.Values)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }

        resultAvatarSprites.Clear();
    }

    public void ResetGameOverState()
    {
        HasGameOverResults = false;
        IsLuckyDrawPopupOpen = false;
        isReturningToMenuForLuckyDraw = false;
        QuickMatchClient.Instance?.ClearActiveResultMatch();

        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        }
    }

    private void ContinueToLuckyDrawInMenu()
    {
        if (isReturningToMenuForLuckyDraw || IsLuckyDrawPopupOpen)
            return;

        StartCoroutine(ReturnToMenuAndShowLuckyDrawRoutine());
    }

    private IEnumerator ReturnToMenuAndShowLuckyDrawRoutine()
    {
        var networkManager = GameManagerNetWork.Instance;
        if (networkManager == null || networkManager.loginUserModel == null)
        {
            Debug.LogWarning("GameOverManager: Cannot continue to lucky draw because network manager or user data is missing.");
            ConfirmOverGame();
            yield break;
        }

        int playerId = networkManager.loginUserModel.UserId;
        bool winner = isWinner;
        isReturningToMenuForLuckyDraw = true;

        yield return StartCoroutine(EnsureClientDisconnectedBeforeResult(networkManager));

        if (EndGamePopup != null)
            EndGamePopup.SetActive(false);

        if (DrawPopup != null)
            DrawPopup.SetActive(false);

        Time.timeScale = 1f;
        yield return StartCoroutine(ReturnToMenuAndShowQuickMatch());

        isReturningToMenuForLuckyDraw = false;
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("GameOverManager: PopupHelper is not available after returning to menu.");
            ResetGameOverState();
            yield break;
        }

        IsLuckyDrawPopupOpen = true;
        PopupHelper.Instance.ShowLuckyDrawAfterMatchPopup(playerId, winner, OnLuckyDrawClosed);
    }

    public void CheckDrawLucky()
    {
        var networkManager = GameManagerNetWork.Instance;
        if (networkManager == null || networkManager.loginUserModel == null)
        {
            Debug.LogWarning("GameOverManager: Cannot show lucky draw popup because network manager or user data is missing.");
            return;
        }

        int playerId = networkManager.loginUserModel.UserId;
        IsLuckyDrawPopupOpen = true;
        PopupHelper.Instance.ShowLuckyDrawAfterMatchPopup(playerId, isWinner, OnLuckyDrawClosed);
    }

    public void ShowLuckyDrawAfterEarlyExit(int playerId)
    {
        QuickMatchClient.Instance?.ClearActiveResultMatch();
        if (GameManagerNetWork.Instance != null)
        {
            GameManagerNetWork.Instance.currentQuickMatchResultId = null;
        }

        if (playerId <= 0)
        {
            Debug.LogWarning("GameOverManager: Invalid playerId for lucky draw after early exit.");
            StartCoroutine(ReturnToMenuAndShowQuickMatch());
            return;
        }

        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("GameOverManager: PopupHelper is not available for lucky draw.");
            StartCoroutine(ReturnToMenuAndShowQuickMatch());
            return;
        }

        Time.timeScale = 0f;
        isWinner = false;
        IsLuckyDrawPopupOpen = true;
        PopupHelper.Instance.ShowLuckyDrawAfterMatchPopup(playerId, false, OnLuckyDrawClosedEarlyExit);
    }

    private void OnLuckyDrawClosed()
    {
        IsLuckyDrawPopupOpen = false;
        StartCoroutine(CompleteLuckyDrawInMenuRoutine());
    }

    private IEnumerator CompleteLuckyDrawInMenuRoutine()
    {
        Time.timeScale = 1f;

        if (UserInfoHandler.Instance != null)
            yield return StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());

        ResetGameOverState();
    }

    private void OnLuckyDrawClosedEarlyExit()
    {
        IsLuckyDrawPopupOpen = false;
        StartCoroutine(EarlyExitReturnRoutine());
    }
    public void ConfirmOverGame()
    {
        StartCoroutine(ConfirmOverGameRoutine());
    }

    private IEnumerator ConfirmOverGameRoutine()
    {
        var networkManager = GameManagerNetWork.Instance;
        yield return StartCoroutine(EnsureClientDisconnectedBeforeResult(networkManager));

        if (EndGamePopup != null)
            EndGamePopup.SetActive(false);

        if (DrawPopup != null)
            DrawPopup.SetActive(false);

        Time.timeScale = 1;

        yield return StartCoroutine(ReturnToMenuAndShowQuickMatch());
        ResetGameOverState();
    }

    private IEnumerator EarlyExitReturnRoutine()
    {
        var networkManager = GameManagerNetWork.Instance;
        yield return StartCoroutine(EnsureClientDisconnectedBeforeResult(networkManager));

        if (EndGamePopup != null)
            EndGamePopup.SetActive(false);

        if (DrawPopup != null)
            DrawPopup.SetActive(false);

        Time.timeScale = 1;

        yield return StartCoroutine(ReturnToMenuAndShowQuickMatch());
        ResetGameOverState();
    }

    private IEnumerator EnsureClientDisconnectedBeforeResult(GameManagerNetWork networkManager)
    {
        if (networkManager == null)
            yield break;

        Debug.Log("⏳ [CLIENT] Kiểm tra trạng thái kết nối runner trước khi chuyển màn hình kết quả.");

        if (networkManager.IsRunnerActive)
        {
            networkManager.CloseConnectToRunner();
        }

        float timeout = 5f;
        float elapsed = 0f;
        while (networkManager.IsRunnerActive && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (networkManager.IsRunnerActive)
            Debug.LogWarning("⚠️ [CLIENT] Runner vẫn active sau timeout chờ disconnect.");
        else
            Debug.Log("✅ [CLIENT] Runner đã disconnect trước khi hiển thị kết quả tiếp theo.");
    }

    private IEnumerator ReturnToMenuAndShowQuickMatch()
    {
        const string menuSceneName = "Menu";
        LoadingManager.LoadScene(menuSceneName);

        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == menuSceneName);

        var menuController = MenuController.Instance != null
            ? MenuController.Instance
            : FindObjectOfType<MenuController>();

        if (menuController != null)
        {
            //Load data infor again
        yield return StartCoroutine(UserInfoHandler.Instance.LoadPlayerInfo());
        menuController.ShowMenu(MenuActionType.QuickMatch);
        menuController.LoadPlayerInfoMenu();
        }
    }
    public void testDraw()
    {
        DrawPopup.SetActive(true);
        LuckyDrawManager.Instance?.OnClickShowDraw();
    }    
}
