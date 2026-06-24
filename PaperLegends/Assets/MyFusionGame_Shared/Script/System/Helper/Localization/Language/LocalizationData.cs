using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LocalizationItem
{
    public string key;   // Tên của text (VD: "start_button")
    public string value; // Nội dung text (VD: "Start" hoặc "Bắt đầu")
}

[CreateAssetMenu(fileName = "LocalizationData", menuName = "Localization/New Localization Data")]
public class LocalizationData : ScriptableObject
{
    public List<LocalizationItem> texts;
}
