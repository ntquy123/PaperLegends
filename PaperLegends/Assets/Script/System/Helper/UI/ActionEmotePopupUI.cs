using System;
using UnityEngine;
using UnityEngine.UI;

public class ActionEmotePopupUI : MonoBehaviour
{
    [Header("Action Buttons")]
    [SerializeField] private Button laughButton;
    [SerializeField] private Button tauntButton;
    [SerializeField] private Button angryButton;
    [SerializeField] private Button clapButton;
    [SerializeField] private Button sadButton;

    private Action closeAction;
    private Action<CharacterAnimState> onActionSelected;

    public void Initialize(Action<CharacterAnimState> onSelected, Action onClose)
    {
        onActionSelected = onSelected;
        closeAction = onClose;
        RegisterListeners();
    }

    private void RegisterListeners()
    {
        RegisterActionButton(laughButton, CharacterAnimState.EmoteLaugh);
        RegisterActionButton(tauntButton, CharacterAnimState.EmoteTaunt);
        RegisterActionButton(angryButton, CharacterAnimState.EmoteAngry);
        RegisterActionButton(clapButton, CharacterAnimState.EmoteClap);
        RegisterActionButton(sadButton, CharacterAnimState.EmoteSad);
    }

    private void RegisterActionButton(Button button, CharacterAnimState animState)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onActionSelected?.Invoke(animState);
            closeAction?.Invoke();
        });
    }

    private void OnDestroy()
    {
        if (laughButton != null)
            laughButton.onClick.RemoveAllListeners();
        if (tauntButton != null)
            tauntButton.onClick.RemoveAllListeners();
        if (angryButton != null)
            angryButton.onClick.RemoveAllListeners();
        if (clapButton != null)
            clapButton.onClick.RemoveAllListeners();
        if (sadButton != null)
            sadButton.onClick.RemoveAllListeners();
    }
}
