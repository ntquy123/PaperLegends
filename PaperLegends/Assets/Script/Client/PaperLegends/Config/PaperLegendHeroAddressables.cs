using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class PaperLegendHeroAddressables
{
    private static readonly Dictionary<string, AsyncOperationHandle> referenceHandleCache = new Dictionary<string, AsyncOperationHandle>();

    static PaperLegendHeroAddressables()
    {
        Application.quitting += ReleaseAllReferenceHandles;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        ReleaseAllReferenceHandles();
    }

    public static string BuildHeroIconAddress(int heroId)
    {
        return $"{AddressablePaths.PaperLegends.HeroIcons}/{heroId}.png";
    }

    public static string BuildHeroPortraitAddress(int heroId)
    {
        return $"{AddressablePaths.PaperLegends.HeroPortraits}/{heroId}.png";
    }

    public static string BuildHeroMaterialAddress(int heroId)
    {
        return $"{AddressablePaths.PaperLegends.HeroMaterials}/{heroId}.mat";
    }

    public static string BuildHeroPrefabAddress(int heroId)
    {
        return $"{AddressablePaths.PaperLegends.HeroPrefabs}/{heroId}.prefab";
    }

    public static string BuildSkillIconAddress(int skillId)
    {
        return $"{AddressablePaths.PaperLegends.SkillIcons}/{skillId}.png";
    }

    public static IEnumerator LoadHeroIconSpriteRoutine(int heroId, Action<Sprite> onLoaded)
    {
        HeroConfig hero = HeroConfigCatalog.ResolveHero(heroId);
        yield return LoadSpriteWithFallbackRoutine(
            hero != null ? hero.avatarIconRef : null,
            BuildHeroIconAddress(heroId),
            onLoaded);
    }

    public static IEnumerator LoadHeroPortraitSpriteRoutine(int heroId, Action<Sprite> onLoaded)
    {
        HeroConfig hero = HeroConfigCatalog.ResolveHero(heroId);
        yield return LoadSpriteWithFallbackRoutine(
            hero != null ? hero.avatarFullRef : null,
            BuildHeroPortraitAddress(heroId),
            onLoaded);
    }

    public static IEnumerator LoadSkillIconSpriteRoutine(int skillId, Action<Sprite> onLoaded)
    {
        AssetReferenceSprite iconRef = HeroConfigCatalog.ResolveSkillIconReference(skillId);
        yield return LoadSpriteWithFallbackRoutine(iconRef, BuildSkillIconAddress(skillId), onLoaded);
    }

    public static IEnumerator LoadHeroMaterialRoutine(int heroId, Action<Material> onLoaded)
    {
        HeroConfig hero = HeroConfigCatalog.ResolveHero(heroId);
        if (hero != null && IsReferenceUsable(hero.materialRef))
        {
            Material loadedMaterial = null;
            yield return LoadReferenceAssetRoutine<Material>(hero.materialRef, asset => loadedMaterial = asset);
            if (loadedMaterial != null)
            {
                onLoaded?.Invoke(loadedMaterial);
                yield break;
            }
        }

        yield return AddressablesHelper.LoadMaterial(BuildHeroMaterialAddress(heroId), onLoaded);
    }

    public static IEnumerator LoadHeroPrefabRoutine(int heroId, Action<GameObject> onLoaded)
    {
        HeroConfig hero = HeroConfigCatalog.ResolveHero(heroId);
        if (hero != null && IsReferenceUsable(hero.prefabRef))
        {
            GameObject loadedPrefab = null;
            yield return LoadReferenceAssetRoutine<GameObject>(hero.prefabRef, asset => loadedPrefab = asset);
            if (loadedPrefab != null)
            {
                onLoaded?.Invoke(loadedPrefab);
                yield break;
            }
        }

        yield return AddressablesHelper.LoadGameObject(BuildHeroPrefabAddress(heroId), onLoaded);
    }

    public static void ReleaseAllReferenceHandles()
    {
        foreach (AsyncOperationHandle handle in referenceHandleCache.Values)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        referenceHandleCache.Clear();
    }

    private static IEnumerator LoadSpriteWithFallbackRoutine(AssetReferenceSprite spriteRef, string fallbackAddress, Action<Sprite> onLoaded)
    {
        if (IsReferenceUsable(spriteRef))
        {
            Sprite loadedSprite = null;
            yield return LoadReferenceAssetRoutine<Sprite>(spriteRef, sprite => loadedSprite = sprite);
            if (loadedSprite != null)
            {
                onLoaded?.Invoke(loadedSprite);
                yield break;
            }
        }

        yield return AddressablesHelper.LoadSprite(fallbackAddress, onLoaded);
    }

    private static IEnumerator LoadReferenceAssetRoutine<T>(AssetReference reference, Action<T> onLoaded) where T : UnityEngine.Object
    {
        if (!IsReferenceUsable(reference))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        string cacheKey = ResolveReferenceCacheKey(reference);
        if (!string.IsNullOrEmpty(cacheKey) &&
            referenceHandleCache.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) &&
            cachedHandle.IsValid())
        {
            if (!cachedHandle.IsDone)
                yield return cachedHandle;

            onLoaded?.Invoke(cachedHandle.Convert<T>().Result);
            yield break;
        }

        yield return AddressablesHelper.EnsureCatalogLoaded();

        AsyncOperationHandle<T> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PaperLegends][Addressables] Failed to start loading reference '{cacheKey}': {ex.Message}");
            onLoaded?.Invoke(null);
            yield break;
        }

        if (!string.IsNullOrEmpty(cacheKey))
            referenceHandleCache[cacheKey] = handle;

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            onLoaded?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogWarning($"[PaperLegends][Addressables] Failed to load reference '{cacheKey}': {handle.OperationException}");
            if (handle.IsValid())
                Addressables.Release(handle);
            if (!string.IsNullOrEmpty(cacheKey))
                referenceHandleCache.Remove(cacheKey);
            onLoaded?.Invoke(null);
        }
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
}
