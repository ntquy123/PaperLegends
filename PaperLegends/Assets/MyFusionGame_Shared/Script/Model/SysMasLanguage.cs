[System.Serializable]
public class SysMasLanguage
{
    public string code;
    public string vietnamText;
    public string englishText;
}

[System.Serializable]
public class LanguageListWrapper
{
    public System.Collections.Generic.List<SysMasLanguage> languages;
}
