using UnityEngine;
using System.Collections.Generic;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    public LocalizationData englishData;
    public LocalizationData vietnameseData;

    private LocalizationData currentData;
    private Dictionary<string, string> localizedTexts = new Dictionary<string, string>();

    private readonly Dictionary<string, string> englishTexts = new();
    private readonly Dictionary<string, string> vietnameseTexts = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        LoadLanguage(PlayerPrefs.GetString("language", "vi")); // Mặc định là Tiếng Việt
    }

    public void LoadLanguage(string lang)
    {
        localizedTexts.Clear();
        if (englishTexts.Count > 0 || vietnameseTexts.Count > 0)
        {
            var src = lang == "en" ? englishTexts : vietnameseTexts;
            foreach (var pair in src)
            {
                localizedTexts[pair.Key] = pair.Value;
            }
        }
        else
        {
            currentData = lang == "en" ? englishData : vietnameseData;
            foreach (var item in currentData.texts)
            {
                localizedTexts[item.key] = item.value;
            }
        }

        PlayerPrefs.SetString("language", lang);
        PlayerPrefs.Save();

        // Cập nhật toàn bộ UI Text
        UpdateAllLocalizedTexts();
    }

    public string GetText(string key)
    {
        return localizedTexts.ContainsKey(key) ? localizedTexts[key] : key;
    }

    public void UpdateAllLocalizedTexts()
    {
        foreach (LocalizedText text in FindObjectsOfType<LocalizedText>())
        {
            text.UpdateText();
        }
    }

    public void SetLanguages(List<SysMasLanguage> languages)
    {
        englishTexts.Clear();
        vietnameseTexts.Clear();

        if (languages == null || languages.Count == 0)
        {
            Debug.LogWarning("LocalizationManager: Không có dữ liệu ngôn ngữ từ server, sử dụng dữ liệu cục bộ.");
            LoadLanguage(PlayerPrefs.GetString("language", "vi"));
            return;
        }

        foreach (var lang in languages)
        {
            englishTexts[lang.code] = lang.englishText;
            vietnameseTexts[lang.code] = lang.vietnamText;
        }

        LoadLanguage(PlayerPrefs.GetString("language", "vi"));
    }
}
