#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PaperLegendCharacterVisualCatalog",
    menuName = "Paper Legends/Character Visual Catalog")]
public sealed class PaperLegendCharacterVisualCatalog : ScriptableObject
{
    [SerializeField, Tooltip("Client-only visual prefabs keyed by modelId. These prefabs should not contain gameplay/network authority.")]
    private PaperLegendClientVisualModelEntry[] visualModels = Array.Empty<PaperLegendClientVisualModelEntry>();

    public PaperLegendClientVisualModelEntry[] VisualModels => visualModels ?? Array.Empty<PaperLegendClientVisualModelEntry>();

    public bool TryGetEntry(int modelId, out PaperLegendClientVisualModelEntry entry)
    {
        var entries = VisualModels;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].modelId == modelId)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = default(PaperLegendClientVisualModelEntry);
        return false;
    }

    private void OnValidate()
    {
        var seenModelIds = new HashSet<int>();
        var entries = VisualModels;

        for (int i = 0; i < entries.Length; i++)
        {
            int modelId = entries[i].modelId;
            if (modelId <= 0)
                continue;

            if (!seenModelIds.Add(modelId))
                Debug.LogWarning($"PaperLegendCharacterVisualCatalog has duplicate modelId {modelId}.", this);

            if (entries[i].visualPrefab == null && string.IsNullOrWhiteSpace(entries[i].resourcesPath))
                Debug.LogWarning($"PaperLegendCharacterVisualCatalog modelId {modelId} has no visualPrefab or resourcesPath.", this);
        }
    }
}
#endif
