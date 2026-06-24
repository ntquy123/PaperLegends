using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameSettingsController : MonoBehaviour
{
    public Slider bgmSlider;
    public Slider sfxSlider;
    public TMP_Dropdown languageDropdown;
    public Toggle vibrationToggle;

    private bool _isInitializing;
    private string _currentLanguageCode;

    private void Start()
    {
        _isInitializing = true;

        float bgmVolume = PlayerPrefs.GetFloat("bgmVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 1f);
        string language = PlayerPrefs.GetString("language", "vi");
        bool vibrationEnabled = PlayerPrefs.GetInt(VibrationManager.VibrationPrefKey, 1) == 1;

        if (bgmSlider != null)
            bgmSlider.value = bgmVolume;
        if (sfxSlider != null)
            sfxSlider.value = sfxVolume;
        if (languageDropdown != null)
        {
            if (languageDropdown.value != (language == "en" ? 0 : 1))
                languageDropdown.SetValueWithoutNotify(language == "en" ? 0 : 1);
        }
        if (vibrationToggle != null)
            vibrationToggle.SetIsOnWithoutNotify(vibrationEnabled);

        _currentLanguageCode = language;

        if (SoundManager.Instance != null)
        {
            if (SoundManager.Instance.bgmSource != null)
                SoundManager.Instance.bgmSource.volume = bgmVolume;
            ApplySfxVolume(sfxVolume);
        }

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);

        _isInitializing = false;
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

    private void OnLanguageChanged(int index)
    {
        if (_isInitializing)
            return;

        string code = index == 0 ? "en" : "vi";
        if (code == _currentLanguageCode)
            return;

        void ApplyLanguageChange()
        {
            if (LocalizationManager.Instance != null)
            {
                LocalizationManager.Instance.LoadLanguage(code);
            }
            else
            {
                PlayerPrefs.SetString("language", code);
                PlayerPrefs.Save();
            }

            _currentLanguageCode = code;

            if (LoadingManager.Instance != null)
            {
                LoadingManager.LoadScene("Menu");
            }
            else
            {
                SceneManager.LoadScene("Menu");
            }
        }

        if (PopupHelper.Instance != null)
        {
            string message = "Thay đổi ngôn ngữ sẽ tải lại menu. Bạn có muốn tiếp tục?";

            if (LocalizationManager.Instance != null)
            {
                string localizedMessage = LocalizationManager.Instance.GetText("confirm_change_language");
                if (!string.IsNullOrEmpty(localizedMessage) && localizedMessage != "confirm_change_language")
                    message = localizedMessage;
            }

            PopupHelper.Instance.ShowPopupOut(message, ApplyLanguageChange);
        }
        else
        {
            ApplyLanguageChange();
        }
    }

    private void OnVibrationChanged(bool enabled)
    {
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

                if (PopupHelper.Instance != null)
                    PopupHelper.Instance.ShowPopupOut(message, null);

                return;
            }
        }

        PlayerPrefs.SetInt(VibrationManager.VibrationPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
