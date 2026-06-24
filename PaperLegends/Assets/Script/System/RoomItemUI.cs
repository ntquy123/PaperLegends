using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class RoomItemUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text indexText;
    [SerializeField]
    private TMP_Text createPlayerName;
    [SerializeField]
    private TMP_Text roomID;
    [SerializeField]
    private TMP_Text playerCountText;
    [SerializeField]
    private TMP_Text betText;
    [SerializeField]
    private Button joinButton;
    [SerializeField]
    private Color fullPlayerCountColor = Color.red;
    private Color defaultPlayerCountColor;
    private bool defaultPlayerCountColorInitialized;
    public void Bind(int index, RoomData roomData, Action onJoin)
    {
        if (indexText)
            indexText.text = index.ToString();

        if (roomID)
            roomID.text = roomData != null ? roomData.id.ToString() : string.Empty;
        if (createPlayerName)
            createPlayerName.text = roomData != null ? roomData.createPlayerName : string.Empty;

        if (playerCountText)
        {
            int maxPlayers = roomData != null ? roomData.GetMaxPlayers() : 0;
            int currentPlayers = roomData != null ? roomData.currentPlayers : 0;
            playerCountText.text = $"{currentPlayers}/{maxPlayers}";

            if (!defaultPlayerCountColorInitialized)
            {
                defaultPlayerCountColor = playerCountText.color;
                defaultPlayerCountColorInitialized = true;
            }

            bool isFull = roomData != null && currentPlayers >= maxPlayers && maxPlayers > 0;
            playerCountText.color = isFull ? fullPlayerCountColor : defaultPlayerCountColor;
        }

        if (betText)
            betText.text = roomData != null ? roomData.bet.ToString() : string.Empty;

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            if (onJoin != null)
                joinButton.onClick.AddListener(() => onJoin());
        }
    }
}
