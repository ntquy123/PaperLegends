using UnityEngine;
using System;
using System.Reflection;

public static class SkillHelper
{
    public static Sprite GetSkillSprite(SkillType skill)
    {
        FieldInfo field = skill.GetType().GetField(skill.ToString());
        SkillIconAttribute attribute = (SkillIconAttribute)Attribute.GetCustomAttribute(field, typeof(SkillIconAttribute));

        if (attribute != null)
        {
            return Resources.Load<Sprite>(attribute.IconPath);
        }
        return null;
    }
}
