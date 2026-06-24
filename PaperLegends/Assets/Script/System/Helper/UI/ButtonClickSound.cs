using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [SerializeField] private ButtonSfx soundType = ButtonSfx.Default;

    void Awake()
    {
        // Lấy Button trên cùng GameObject và đăng ký sự kiện
        GetComponent<Button>().onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        if (!ClientGameplayBridge.Sound.HasInstance()) return;

        switch (soundType)
        {
            case ButtonSfx.Default:
            case ButtonSfx.FindMatch:
            case ButtonSfx.CancelMatch:
                ClientGameplayBridge.Sound.PlayButton(soundType);
                break;
        }
    }
}
