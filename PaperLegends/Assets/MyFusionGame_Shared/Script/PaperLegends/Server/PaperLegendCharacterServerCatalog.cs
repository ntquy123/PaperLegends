#if UNITY_SERVER
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PaperLegendCharacterServerCatalog",
    menuName = "Paper Legends/Character Server Catalog")]
public sealed class PaperLegendCharacterServerCatalog : ScriptableObject
{
    [SerializeField, Tooltip("Server-side paper character collider/network prefabs keyed by modelId. These are not mobile visual prefabs.")]
    private PaperLegendCharacterModelSpawnEntry[] characterModels = Array.Empty<PaperLegendCharacterModelSpawnEntry>();

    public PaperLegendCharacterModelSpawnEntry[] CharacterModels => characterModels ?? Array.Empty<PaperLegendCharacterModelSpawnEntry>();

    public bool HasAnyModelId
    {
        get
        {
            var entries = CharacterModels;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].modelId > 0)
                    return true;
            }

            return false;
        }
    }

    public bool HasAnyPrefab
    {
        get
        {
            var entries = CharacterModels;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].characterPrefab != null)
                    return true;
            }

            return false;
        }
    }

    public NetworkObject ResolvePrefab(int modelId)
    {
        var entries = CharacterModels;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].modelId == modelId && entries[i].characterPrefab != null)
                return entries[i].characterPrefab;
        }

        return null;
    }

    private void OnValidate()
    {
        var seenModelIds = new HashSet<int>();
        var entries = CharacterModels;

        for (int i = 0; i < entries.Length; i++)
        {
            int modelId = entries[i].modelId;
            if (modelId <= 0)
                continue;

            if (!seenModelIds.Add(modelId))
                Debug.LogWarning($"PaperLegendCharacterServerCatalog has duplicate modelId {modelId}.", this);
        }
    }
}
#endif
