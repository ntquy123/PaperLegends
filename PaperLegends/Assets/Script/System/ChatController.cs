using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class ChatController : MonoBehaviour {

    public static ChatController Instance;
    private const float ChatBubblePaddingX = 60f;
    private const float ChatBubblePaddingY = 50f;
    [Header("CHAT CONFIG")]
    public Transform chatContentParent;  // Parent chứa các item chat trong GridLayout
    public GameObject chatItemPrefab;    // Prefab cho mỗi item chat
    [SerializeField]
    private ScrollRect chatScrollRect;
    [SerializeField]
    private int maxVisibleMessages = 5;
    private readonly Queue<GameObject> activeChatItems = new Queue<GameObject>();
    private static readonly Dictionary<string, string> EmojiLookup = new Dictionary<string, string>
    {
        {":)", "😄"},
        {":-)", "😄"},
        {":D", "😄"},
        {":d", "😄"},
        {":cuoi:", "😄"},
        {":cười:", "😄"},
        {":laugh:", "😄"},
        {":smile:", "😄"},
        {">:(", "😠"},
        {":tucgian:", "😠"},
        {":tứcgiận:", "😠"},
        {":angry:", "😠"},
        {":khoc:", "😢"},
        {":khóc:", "😢"},
        {":cry:", "😢"},
        {":(", "😢"},
        {":-(", "😢"},
        {":'(", "😢"}
    };
    private static readonly string[] BannedViolenceTerms = new[]
    {
        // English (violent/extremist incitement)
        "kill", "killing", "murder", "massacre", "behead", "execute", "assassinate",
        "genocide", "terrorist", "terrorism", "bomb", "bombing", "shoot up",
        "stab", "slaughter", "wipe out", "burn alive", "set on fire",
        // Vietnamese (bạo lực / kích động)
        "giết", "giết chết", "sát hại", "thảm sát", "chặt đầu", "xử tử",
        "ám sát", "khủng bố", "đánh bom", "nổ bom", "xả súng", "đâm chết",
        "đồ sát", "thiêu sống", "đốt sống", "tiêu diệt hết"
    };
    private static readonly Regex NonSpacingRegex = new Regex(@"\p{Mn}+", RegexOptions.Compiled);
    private static readonly Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
    private const string BlockedMessageResponse = "chat của bạn chứa từ cấm";

    private void Awake()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (PopupHelper.Instance != null)
            PopupHelper.Instance.CloseActivePopup();
    }

    public void ShowChat(string senderName, string message)
    {
        ShowChat(senderName, message, null);
    }

    private void ShowChat(string senderName, string message, string overrideColorHex)
    {
        if (chatItemPrefab == null || chatContentParent == null)
        {
            Debug.LogWarning("ChatController: Không thể hiển thị tin nhắn do thiếu prefab hoặc parent.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
            return;

        string formattedSender = string.IsNullOrWhiteSpace(senderName) ? "Người chơi" : senderName.Trim();
        string decoratedMessage = ApplyEmojiShortcodes(message.Trim());

        GameObject chatItem = Instantiate(chatItemPrefab, chatContentParent);
        TMP_Text chatText = null;

        foreach (var textComponent in chatItem.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textComponent != null && textComponent.name == "MessageText")
            {
                chatText = textComponent;
                break;
            }
        }

        if (chatText == null)
            chatText = chatItem.GetComponent<TMP_Text>();

        if (chatText != null)
        {
            string messageLine = $"{formattedSender}: {decoratedMessage}";
            string colorHex = overrideColorHex;

            if (string.IsNullOrEmpty(colorHex) &&
                string.Equals(formattedSender, "SYSTEM", System.StringComparison.OrdinalIgnoreCase))
            {
                colorHex = "#FF4F4F";
            }
            else if (string.IsNullOrEmpty(colorHex))
            {
                string localPlayerName = ResolveLocalPlayerName();
                if (!string.IsNullOrWhiteSpace(localPlayerName) &&
                    string.Equals(formattedSender, localPlayerName, System.StringComparison.OrdinalIgnoreCase))
                {
                    colorHex = "#2ECC71";
                }
            }

            if (!string.IsNullOrEmpty(colorHex))
                messageLine = $"<color={colorHex}>{messageLine}</color>";

            chatText.text = messageLine;
            UpdateChatBubbleBackground(chatItem, chatText);
        }

        activeChatItems.Enqueue(chatItem);
        TrimChatHistory();

        if (chatContentParent is RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        AutoScrollToLatest();
    }

    public void OnChatButtonClicked()
    {
        if (PopupHelper.Instance == null)
        {
            Debug.LogWarning("ChatController: PopupHelper chưa được khởi tạo.");
            return;
        }

        PopupHelper.Instance.ShowChatInputPopup(SubmitChatMessage);
    }

    private void SubmitChatMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return;

        string sanitizedMessage = rawMessage.Trim();
        if (string.IsNullOrEmpty(sanitizedMessage))
            return;

        if (ContainsBannedViolenceLanguage(sanitizedMessage))
        {
            ShowSystemMessage(BlockedMessageResponse);
            return;
        }

        string senderName = ResolveLocalPlayerName();

        if (GameManagerNetWork.Instance == null)
        {
            Debug.LogWarning("ChatController: GameManagerNetWork chưa khởi tạo. Hiển thị tin nhắn nội bộ.");
            ShowChat(senderName, sanitizedMessage);
            return;
        }

        if (!GameManagerNetWork.Instance.ValidateNetworkObjects())
            return;

        if (GameManagerNetWork.Instance.serverRPC != null)
        {
            GameManagerNetWork.Instance.serverRPC.RpcSendChatMessage(senderName, sanitizedMessage);
        }
        else
        {
            Debug.LogWarning("ChatController: Không tìm thấy serverRPC. Tin nhắn sẽ hiển thị cục bộ.");
            ShowChat(senderName, sanitizedMessage);
        }
    }

    private string ResolveLocalPlayerName()
    {
        var loginModel = GameManagerNetWork.Instance?.loginUserModel;
        if (loginModel != null && !string.IsNullOrWhiteSpace(loginModel.Username))
            return loginModel.Username;

        return "Người chơi";
    }

    private string ApplyEmojiShortcodes(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        string result = message;
        foreach (var pair in EmojiLookup)
        {
            result = Regex.Replace(result, Regex.Escape(pair.Key), pair.Value, RegexOptions.IgnoreCase);
        }

        return result;
    }

    private bool ContainsBannedViolenceLanguage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        string normalizedRaw = NormalizeForModeration(message);
        string normalizedSpaced = $" {SpaceRegex.Replace(normalizedRaw, " ").Trim()} ";
        string normalizedCompact = normalizedSpaced.Replace(" ", string.Empty);

        foreach (string bannedTerm in BannedViolenceTerms)
        {
            string normalizedTermRaw = NormalizeForModeration(bannedTerm);
            string normalizedTermSpaced = SpaceRegex.Replace(normalizedTermRaw, " ").Trim();
            if (string.IsNullOrEmpty(normalizedTermSpaced))
                continue;

            string normalizedTermCompact = normalizedTermSpaced.Replace(" ", string.Empty);

            bool isSingleWord = normalizedTermSpaced.IndexOf(' ') < 0;
            if (isSingleWord)
            {
                if (normalizedSpaced.Contains($" {normalizedTermSpaced} "))
                    return true;
            }
            else if (normalizedSpaced.Contains($" {normalizedTermSpaced} ") ||
                     normalizedCompact.Contains(normalizedTermCompact))
            {
                return true;
            }
        }

        return false;
    }

    private string NormalizeForModeration(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string lower = input.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        string noDiacritics = NonSpacingRegex.Replace(lower, string.Empty);
        return Regex.Replace(noDiacritics, @"[^a-z0-9\s]", " ");
    }

    private void TrimChatHistory()
    {
        while (activeChatItems.Count > Mathf.Max(1, maxVisibleMessages))
        {
            GameObject oldItem = activeChatItems.Dequeue();
            if (oldItem != null)
                Destroy(oldItem);
        }
    }

    public void ShowSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        ShowChat("SYSTEM", message.Trim());
    }

    public void ShowSystemMessage(string message, string colorHex)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (string.IsNullOrWhiteSpace(colorHex))
        {
            ShowSystemMessage(message);
            return;
        }

        ShowChat("SYSTEM", message.Trim(), colorHex.Trim());
    }

    private void UpdateChatBubbleBackground(GameObject chatItem, TMP_Text chatText)
    {
        if (chatItem == null || chatText == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(chatText.rectTransform);

        Transform backgroundTransform = chatItem.transform.Find("Image");
        Image background = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
        if (background == null)
            background = chatItem.GetComponentInChildren<Image>(true);

        if (background == null)
            return;

        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.sizeDelta = new Vector2(
            chatText.preferredWidth + ChatBubblePaddingX,
            chatText.preferredHeight + ChatBubblePaddingY);
    }

    private void AutoScrollToLatest()
    {
        ScrollRect scrollRect = ResolveChatScrollRect();
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private ScrollRect ResolveChatScrollRect()
    {
        if (chatScrollRect != null)
            return chatScrollRect;

        if (chatContentParent != null)
            chatScrollRect = chatContentParent.GetComponentInParent<ScrollRect>();

        return chatScrollRect;
    }

}
