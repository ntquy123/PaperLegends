using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomFriendInviteItemUI : MonoBehaviour
{
    [Header("Friend Invite Item")]
    [SerializeField] private Image onlineIcon;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button challengeButton;
    [SerializeField] private Button invitedButton;
    [SerializeField] private Button messageButton;
    [SerializeField] private GameObject removeButtonObject;
    [SerializeField] private GameObject acceptButtonObject;
    [SerializeField] private GameObject declineButtonObject;

    public Image OnlineIcon => onlineIcon;
    public TMP_Text PlayerNameText => playerNameText;
    public TMP_Text LevelText => levelText;
    public Button ChallengeButton => challengeButton;
    public Button InvitedButton => invitedButton;
    public Button MessageButton => messageButton;
    public GameObject RemoveButtonObject => removeButtonObject;
    public GameObject AcceptButtonObject => acceptButtonObject;
    public GameObject DeclineButtonObject => declineButtonObject;
}
