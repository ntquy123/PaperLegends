using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemElementDisplay : MonoBehaviour
{
    public Image elementIcon;
    public Sprite[] elementSprites;

    /// <summary>
    /// Maps each <see cref="ElementalType"/> to the index it should use in
    /// <see cref="elementSprites"/>. The enum values themselves are server IDs
    /// and therefore unsuitable as array indices.
    /// </summary>
    private static readonly Dictionary<ElementalType, int> ElementSpriteIndex =
        new()
        {
            { ElementalType.Fire, 0 },
            { ElementalType.Water, 1 },
            { ElementalType.Earth, 2 },
            { ElementalType.Smoke, 3 },
            { ElementalType.Life, 4 },
            { ElementalType.Steam, 5 }
        };

    /// <summary>
    /// Check whether a sprite has been configured for the given element.
    /// </summary>
    public static bool HasSpriteFor(ElementalType element) => ElementSpriteIndex.ContainsKey(element);

    /// <summary>
    /// Verify that sprite mappings exist for all provided elements. Logs a warning
    /// for any missing entries so issues can be caught during development.
    /// </summary>
    public static void VerifySpriteMappings(IEnumerable<ElementalType> elements)
    {
        foreach (var element in elements)
        {
            if (!HasSpriteFor(element))
            {
                Debug.LogWarning($"No sprite configured for element {element}");
            }
        }
    }

    public void SetElement(ElementalType element)
    {
        if (elementIcon != null && elementSprites != null &&
            ElementSpriteIndex.TryGetValue(element, out int index) &&
            index >= 0 && index < elementSprites.Length)
        {
            elementIcon.sprite = elementSprites[index];
        }
    }
}
