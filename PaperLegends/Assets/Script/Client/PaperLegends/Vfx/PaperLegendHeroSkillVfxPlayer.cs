using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[Serializable]
public struct PaperLegendSkillVfxEntry
{
    [Min(0)] public int skillId;
    public AssetReferenceGameObject vfxPrefab;
    [Tooltip("Optional Resources path without extension.")]
    public string resourcesPath;
    [Min(0.1f)] public float lifetimeSeconds;
    public Vector3 positionOffset;
}

[DisallowMultipleComponent]
public sealed class PaperLegendHeroSkillVfxPlayer : MonoBehaviour
{
    [SerializeField] private HeroConfig heroConfig;
    [SerializeField] private HeroVfxConfig vfxConfig;
    [SerializeField] private PaperLegendSkillVfxEntry[] skillVfxEntries = new PaperLegendSkillVfxEntry[0];
    [SerializeField] private bool createFallbackShockwave = true;
    [SerializeField, Min(0.1f)] private float fallbackLifetimeSeconds = 1.25f;
    [SerializeField] private Color fallbackStartColor = new Color(1f, 0.9f, 0.35f, 0.95f);
    [SerializeField] private Color fallbackEndColor = new Color(1f, 0.35f, 0.05f, 0f);

    private readonly Dictionary<string, GameObject> vfxPrefabCache = new Dictionary<string, GameObject>();
    private readonly List<AsyncOperationHandle<GameObject>> vfxPrefabHandles = new List<AsyncOperationHandle<GameObject>>();

    public static void PlaySkillVfx(PaperLegendCharacterNetworkHandler character, int skillId, Vector3 worldPosition, float radius)
    {
        PlaySkillVfx(character, skillId, worldPosition, radius, Vector3.zero);
    }

    public static void PlaySkillVfx(PaperLegendCharacterNetworkHandler character, int skillId, Vector3 worldPosition, float radius, Vector3 direction)
    {
        PaperLegendHeroSkillVfxPlayer player = FindForCharacter(character);
        if (player != null)
            player.EnsureHeroConfig(character);

        if (player != null && player.TryPlayConfiguredVfx(skillId, worldPosition, direction))
            return;

        if (player == null || player.createFallbackShockwave)
            PlayFallbackShockwave(worldPosition, radius, player);
    }

    public void PlayNormalAttackVfx(Vector3 worldPosition)
    {
        TryPlayCommonVfx(vfxConfig != null ? vfxConfig.normalAttackFx : null, worldPosition, Vector3.zero, fallbackLifetimeSeconds);
    }

    public void PlayFlickVfx(Vector3 worldPosition)
    {
        TryPlayCommonVfx(vfxConfig != null ? vfxConfig.flickFx : null, worldPosition, Vector3.zero, fallbackLifetimeSeconds);
    }

    public void PlayMoveVfx(Vector3 worldPosition)
    {
        TryPlayCommonVfx(vfxConfig != null ? vfxConfig.moveFx : null, worldPosition, Vector3.zero, fallbackLifetimeSeconds);
    }

    public void PlayHitVfx(Vector3 worldPosition)
    {
        TryPlayCommonVfx(vfxConfig != null ? vfxConfig.hitFx : null, worldPosition, Vector3.zero, fallbackLifetimeSeconds);
    }

    public void PlayDieVfx(Vector3 worldPosition)
    {
        TryPlayCommonVfx(vfxConfig != null ? vfxConfig.dieFx : null, worldPosition, Vector3.zero, fallbackLifetimeSeconds);
    }

    private static PaperLegendHeroSkillVfxPlayer FindForCharacter(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return null;

        return character.GetComponentInChildren<PaperLegendHeroSkillVfxPlayer>(true);
    }

    private bool TryPlayConfiguredVfx(int skillId, Vector3 worldPosition, Vector3 direction)
    {
        SkillConfig skill = heroConfig != null ? heroConfig.GetSkillById(skillId) : null;
        if (skill != null && TryPlayAddressableVfx(skill.castVfx, worldPosition, Vector3.zero, fallbackLifetimeSeconds, direction))
        {
            return true;
        }

        if (!TryResolveEntry(skillId, out PaperLegendSkillVfxEntry entry))
            return false;

        float lifetime = entry.lifetimeSeconds > 0f ? entry.lifetimeSeconds : fallbackLifetimeSeconds;
        if (TryPlayAddressableVfx(entry.vfxPrefab, worldPosition, entry.positionOffset, lifetime, direction))
            return true;

        if (!string.IsNullOrWhiteSpace(entry.resourcesPath))
        {
            GameObject prefab = Resources.Load<GameObject>(entry.resourcesPath);
            if (prefab != null)
            {
                InstantiateAndDestroy(prefab, worldPosition, entry.positionOffset, lifetime, direction);
                return true;
            }
        }

        return false;
    }

    private void EnsureHeroConfig(PaperLegendCharacterNetworkHandler character)
    {
        if (heroConfig == null && character != null)
            heroConfig = HeroConfigCatalog.ResolveHero(character.CharacterModelId);

        if (vfxConfig == null && heroConfig != null)
            vfxConfig = heroConfig.vfxConfig;
    }

    private static void InstantiateAndDestroy(GameObject prefab, Vector3 worldPosition, Vector3 offset, float lifetime, Vector3 direction)
    {
        if (prefab == null)
            return;

        GameObject instance = Instantiate(prefab, worldPosition + offset, ResolveVfxRotation(direction));
        Destroy(instance, Mathf.Max(0.1f, lifetime));
    }

    private bool TryPlayCommonVfx(AssetReferenceGameObject prefabReference, Vector3 worldPosition, Vector3 offset, float lifetime)
    {
        return TryPlayAddressableVfx(prefabReference, worldPosition, offset, lifetime, Vector3.zero);
    }

    private bool TryPlayAddressableVfx(AssetReferenceGameObject prefabReference, Vector3 worldPosition, Vector3 offset, float lifetime, Vector3 direction)
    {
        if (!IsReferenceUsable(prefabReference))
            return false;

        StartCoroutine(InstantiateAddressableVfxRoutine(prefabReference, worldPosition, offset, lifetime, direction));
        return true;
    }

    private IEnumerator InstantiateAddressableVfxRoutine(AssetReferenceGameObject prefabReference, Vector3 worldPosition, Vector3 offset, float lifetime, Vector3 direction)
    {
        string cacheKey = ResolveReferenceCacheKey(prefabReference);
        if (!string.IsNullOrEmpty(cacheKey) && vfxPrefabCache.TryGetValue(cacheKey, out GameObject cachedPrefab) && cachedPrefab != null)
        {
            InstantiateAndDestroy(cachedPrefab, worldPosition, offset, lifetime, direction);
            yield break;
        }

        yield return AddressablesHelper.EnsureCatalogLoaded();

        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(prefabReference.RuntimeKey);
        yield return handle;

        if (this == null)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            yield break;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[PaperLegends][VFX] Failed to load addressable VFX: {cacheKey}");
            if (handle.IsValid())
                Addressables.Release(handle);
            yield break;
        }

        vfxPrefabHandles.Add(handle);
        if (!string.IsNullOrEmpty(cacheKey))
            vfxPrefabCache[cacheKey] = handle.Result;

        InstantiateAndDestroy(handle.Result, worldPosition, offset, lifetime, direction);
    }

    private bool TryResolveEntry(int skillId, out PaperLegendSkillVfxEntry entry)
    {
        if (skillVfxEntries != null)
        {
            for (int i = 0; i < skillVfxEntries.Length; i++)
            {
                if (skillVfxEntries[i].skillId == skillId)
                {
                    entry = skillVfxEntries[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private static void PlayFallbackShockwave(Vector3 worldPosition, float radius, PaperLegendHeroSkillVfxPlayer source)
    {
        GameObject vfxObject = new GameObject("PaperLegendSkillShockwaveVFX");
        vfxObject.transform.position = worldPosition;

        ParticleSystem particleSystem = vfxObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.65f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            source != null ? source.fallbackStartColor : new Color(1f, 0.9f, 0.35f, 0.95f),
            source != null ? source.fallbackEndColor : new Color(1f, 0.35f, 0.05f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 48, 72)
        });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.25f, radius);
        shape.arc = 360f;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

        particleSystem.Play();
        float lifetime = source != null ? source.fallbackLifetimeSeconds : 1.25f;
        Destroy(vfxObject, lifetime);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < vfxPrefabHandles.Count; i++)
        {
            if (vfxPrefabHandles[i].IsValid())
                Addressables.Release(vfxPrefabHandles[i]);
        }

        vfxPrefabHandles.Clear();
        vfxPrefabCache.Clear();
    }

    private static bool IsReferenceUsable(AssetReference reference)
    {
        return reference != null && reference.RuntimeKeyIsValid();
    }

    private static string ResolveReferenceCacheKey(AssetReference reference)
    {
        if (reference == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(reference.AssetGUID))
            return reference.AssetGUID;

        object runtimeKey = reference.RuntimeKey;
        return runtimeKey != null ? runtimeKey.ToString() : string.Empty;
    }

    private static Quaternion ResolveVfxRotation(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }
}
