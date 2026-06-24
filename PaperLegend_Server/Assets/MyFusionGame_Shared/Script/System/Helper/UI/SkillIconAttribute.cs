using UnityEngine;

public class SkillIconAttribute : PropertyAttribute
{
    public string IconPath;

    public SkillIconAttribute(string path)
    {
        this.IconPath = path;
    }
}
