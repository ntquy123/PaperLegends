using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PaperLegendCharacterSelectionSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image heroIconImage;
    [SerializeField] private Image emptyIconImage;
    [SerializeField] private GameObject botBadge;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject lockedIconObject;
    [SerializeField] private GameObject unlockedIconObject;

    private int playerId;

    public int PlayerId => playerId;

    public void ConfigurePlayer(int newPlayerId, string playerName, bool isBot)
    {
        playerId = newPlayerId;

        if (playerNameText != null)
            playerNameText.text = playerName ?? string.Empty;

        if (botBadge != null)
            botBadge.SetActive(isBot);

        ClearHero();
        SetStatus(isBot ? "BOT" : "Choosing");
        gameObject.SetActive(newPlayerId != 0);
    }

    public void SetHero(Sprite heroIcon, bool locked)
    {
        if (heroIconImage != null)
        {
            heroIconImage.sprite = heroIcon;
            heroIconImage.enabled = heroIcon != null;
        }

        if (emptyIconImage != null)
            emptyIconImage.enabled = heroIcon == null;

        if (selectedFrame != null)
            selectedFrame.SetActive(heroIcon != null);

        SetLocked(locked);
        SetStatus(locked ? "Locked" : heroIcon != null ? "Selected" : "Choosing");
    }

    public void ClearHero()
    {
        if (heroIconImage != null)
        {
            heroIconImage.sprite = null;
            heroIconImage.enabled = false;
        }

        if (emptyIconImage != null)
            emptyIconImage.enabled = true;

        if (selectedFrame != null)
            selectedFrame.SetActive(false);

        SetLocked(false);
    }

    public void SetStatus(string status)
    {
        if (statusText != null)
            statusText.text = status ?? string.Empty;
    }

    private void SetLocked(bool locked)
    {
        if (lockedIconObject != null)
            lockedIconObject.SetActive(locked);

        if (unlockedIconObject != null)
            unlockedIconObject.SetActive(!locked);
    }
}
