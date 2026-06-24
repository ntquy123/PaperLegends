using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageDetailPopupUI : MonoBehaviour
{
    [Header("Message Conversation UI")]
    [SerializeField]
    private Transform messageConversationContent;
    [SerializeField]
    private ScrollRect messageConversationScrollRect;
    [SerializeField]
    private GameObject myMessageBubblePrefab;
    [SerializeField]
    private GameObject friendMessageBubblePrefab;
    [SerializeField]
    private Button sendMessageButton;
    [SerializeField]
    private TMP_InputField messageInput;

    public Transform MessageConversationContent => messageConversationContent;
    public ScrollRect MessageConversationScrollRect => messageConversationScrollRect;
    public GameObject MyMessageBubblePrefab => myMessageBubblePrefab;
    public GameObject FriendMessageBubblePrefab => friendMessageBubblePrefab;
    public Button SendMessageButton => sendMessageButton;
    public TMP_InputField MessageInput => messageInput;
}
