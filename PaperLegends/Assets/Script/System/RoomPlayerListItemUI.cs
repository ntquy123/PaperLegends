using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomPlayerListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private TMP_Text ringBallText;
    [SerializeField] private GameObject ownerIcon;
    [SerializeField] private Button kickButton;
    [SerializeField] private RawImage avatarRawImage;
    [SerializeField] private Image avatarImage;

    public RawImage AvatarRawImage => avatarRawImage;
    public Image AvatarImage => avatarImage;

    public void Bind(PlayerSchema player, bool isReady, bool isOwner, bool canKick, Action onKick)
    {
        if (player == null)
            return;

        if (playerNameText != null)
            playerNameText.text = player.PlayerName;
        if (levelText != null)
            levelText.text = $"Lv {player.Level}";
        if (ringBallText != null)
            ringBallText.text = $"{player.RingBall}";

        SetReady(isReady);
        SetOwner(isOwner);
        ConfigureKickButton(canKick, onKick);
    }

    public void SetReady(bool isReady)
    {
        if (readyStatusText != null)
            readyStatusText.text = isReady ? "Sẵn sàng" : "Chưa sẵn sàng";
    }

    public void SetOwner(bool isOwner)
    {
        if (ownerIcon != null)
            ownerIcon.SetActive(isOwner);
    }

    private void ConfigureKickButton(bool canKick, Action onKick)
    {
        if (kickButton == null)
            return;

        kickButton.onClick.RemoveAllListeners();
        kickButton.gameObject.SetActive(canKick);

        if (canKick && onKick != null)
        {
            kickButton.onClick.AddListener(() => onKick());
        }
    }
}
