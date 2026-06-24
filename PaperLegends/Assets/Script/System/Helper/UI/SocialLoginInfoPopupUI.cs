using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holds references to UI elements used by the social login information popup.
/// </summary>
public class SocialLoginInfoPopupUI : MonoBehaviour
{
    public TextMeshProUGUI DisplayNameText;
    public TextMeshProUGUI FriendCodeText;
    public TextMeshProUGUI EmailText;
    public Image AvatarImage;
    public Image ProviderIcon;
    public Button CloseButton;
    public Button LogoutButton;
    public Button CopyFriendCodeButton;
}
