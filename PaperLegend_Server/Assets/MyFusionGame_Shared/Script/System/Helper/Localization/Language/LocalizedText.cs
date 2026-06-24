using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    public string key;
    private TMP_Text textComponent;

    void Start()
    {
        textComponent = GetComponent<TMP_Text>();
        UpdateText();
    }

    public void UpdateText()
    {
        if (textComponent == null)
        {
            return;
        }

        var manager = LocalizationManager.Instance;
        string localizedText = textComponent.text;

        if (!string.IsNullOrEmpty(key))
        {
            localizedText = key;
        }

        if (manager != null)
        {
            var fetchedText = manager.GetText(key);
            if (!string.IsNullOrEmpty(fetchedText))
            {
                localizedText = fetchedText;
            }
        }

        textComponent.text = localizedText ?? textComponent.text;
    }
}
