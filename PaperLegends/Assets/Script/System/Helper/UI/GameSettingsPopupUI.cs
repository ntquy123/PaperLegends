using System;
using UnityEngine;
using UnityEngine.UI;

public class GameSettingsPopupUI : MonoBehaviour
{
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Button closeButton;

    private bool isInitializing;
    private Action onClose;

    public void Initialize(Action closeAction)
    {
        onClose = closeAction;
        isInitializing = true;

        float bgmVolume = PlayerPrefs.GetFloat("bgmVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 1f);
        bool vibrationEnabled = PlayerPrefs.GetInt(VibrationManager.VibrationPrefKey, 1) == 1;

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(bgmVolume);
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);
        if (vibrationToggle != null)
            vibrationToggle.SetIsOnWithoutNotify(vibrationEnabled);

        if (SoundManager.Instance != null)
        {
            if (SoundManager.Instance.bgmSource != null)
                SoundManager.Instance.bgmSource.volume = bgmVolume;
            ApplySfxVolume(sfxVolume);
        }

        RegisterListeners();

        isInitializing = false;
    }

    private void RegisterListeners()
    {
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);
            vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
        }
    }

    private void HandleClose()
    {
        onClose?.Invoke();
    }

    private void OnBgmChanged(float value)
    {
        if (SoundManager.Instance != null && SoundManager.Instance.bgmSource != null)
            SoundManager.Instance.bgmSource.volume = value;

        PlayerPrefs.SetFloat("bgmVolume", value);
        PlayerPrefs.Save();
    }

    private void OnSfxChanged(float value)
    {
        ApplySfxVolume(value);
        PlayerPrefs.SetFloat("sfxVolume", value);
        PlayerPrefs.Save();
    }

    private void ApplySfxVolume(float value)
    {
        if (SoundManager.Instance == null)
            return;

        if (SoundManager.Instance.sfxSource != null)
            SoundManager.Instance.sfxSource.volume = value;
        if (SoundManager.Instance.uiSource != null)
            SoundManager.Instance.uiSource.volume = value;
        if (SoundManager.Instance.voiceSource != null)
            SoundManager.Instance.voiceSource.volume = value;
        if (SoundManager.Instance.heartbeatSource != null)
            SoundManager.Instance.heartbeatSource.volume = value;
    }

    private void OnVibrationChanged(bool enabled)
    {
        if (isInitializing)
            return;

        if (enabled)
        {
            bool canVibrate = VibrationManager.Instance != null && VibrationManager.Instance.TryTestVibration();
            if (!canVibrate)
            {
                if (vibrationToggle != null)
                    vibrationToggle.SetIsOnWithoutNotify(false);

                PlayerPrefs.SetInt(VibrationManager.VibrationPrefKey, 0);
                PlayerPrefs.Save();

                string message = "Điện thoại chưa bật rung. Vui lòng bật rung và thử lại.";
                if (LocalizationManager.Instance != null)
                {
                    string localizedMessage = LocalizationManager.Instance.GetText("vibration_disabled_warning");
                    if (!string.IsNullOrEmpty(localizedMessage) && localizedMessage != "vibration_disabled_warning")
                        message = localizedMessage;
                }

                if (UIControllerOnline.Instance != null)
                {
                    UIControllerOnline.Instance.ShowMesByUser(message);
                }
                else
                {
                    Debug.LogWarning(message);
                }

                return;
            }
        }

        PlayerPrefs.SetInt(VibrationManager.VibrationPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (bgmSlider != null)
            bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleClose);
    }
}
