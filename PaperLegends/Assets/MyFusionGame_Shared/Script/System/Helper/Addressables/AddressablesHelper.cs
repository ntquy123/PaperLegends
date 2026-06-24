using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
#if !UNITY_SERVER
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif
#endif

public static class AddressablesHelper
{
#if UNITY_SERVER
    private static bool _catalogLoaded;

    private static string CatalogUrl => ApiConfig.CatalogUrl;

    private static IEnumerator EnsureCatalog()
    {
        yield break;
    }

    public static IEnumerator EnsureCatalogLoaded()
    {
        yield return EnsureCatalog();
    }

    public static IEnumerator DownloadInitialData(System.Action<float> progressCallback = null)
    {
        progressCallback?.Invoke(1f);
        yield break;
    }

    public static IEnumerator DownloadGroup(string label, System.Action<float> progressCallback = null)
    {
        progressCallback?.Invoke(1f);
        yield break;
    }

    public static IEnumerator LoadAsset<T>(string key, System.Action<T> callback) where T : UnityEngine.Object
    {
        callback?.Invoke(null);
        yield break;
    }

    public struct AsyncOperationHandle<T> { }

    public static IEnumerator LoadAssetWithHandle<T>(string key, System.Action<T, AsyncOperationHandle<T>> callback) where T : Object
    {
        callback?.Invoke(null, default);
        yield break;
    }

    public static IEnumerator LoadSprite(string key, System.Action<Sprite> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadAudioClip(string key, System.Action<AudioClip> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadMaterial(string key, System.Action<Material> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadMesh(string key, System.Action<Mesh> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadGameObject(string key, System.Action<GameObject> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadNetworkObject(string key, System.Action<NetworkObject> callback)
    {
        yield return LoadAsset(key, callback);
    }

    public static T LoadAssetSync<T>(string key) where T : UnityEngine.Object => null;
#else
    private static bool _catalogLoaded;
    private static readonly Dictionary<string, AsyncOperationHandle> _handleCache = new();

    private static string CatalogUrl => ApiConfig.CatalogUrl;

    static AddressablesHelper()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                ReleaseAllCachedHandles();
        };
#endif
        Application.quitting += ReleaseAllCachedHandles;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        _catalogLoaded = false;
        ReleaseAllCachedHandles();
    }

    private static void ReleaseAllCachedHandles()
    {
        foreach (var handle in _handleCache.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
        _handleCache.Clear();
    }

    private static IEnumerator EnsureCatalog()
    {
        if (_catalogLoaded)
            yield break;
        var handle = Addressables.LoadContentCatalogAsync(CatalogUrl);
        yield return handle;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _catalogLoaded = true;
            Debug.Log("Catalog loaded successfully");
        }
        else
        {
            Debug.LogError($"Failed to load catalog from {CatalogUrl}: {handle.OperationException}");
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    public static IEnumerator EnsureCatalogLoaded()
    {
        yield return EnsureCatalog();
    }

    public static IEnumerator DownloadInitialData(System.Action<float> progressCallback = null)
    {
        yield return EnsureCatalog();
        string[] labels = { "ItemLabel", "EffectLabel", "SoundLabel" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            yield return DownloadGroup(labels[i], p =>
            {
                float total = (index + p) / labels.Length;
                progressCallback?.Invoke(total);
            });
        }
        //yield return DownloadGroup("CateyeGroup");
    }

    public static IEnumerator DownloadGroup(string label, System.Action<float> progressCallback = null)
    {
        yield return EnsureCatalog();
        var sizeHandle = Addressables.GetDownloadSizeAsync(label);
        yield return sizeHandle;
        if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
        {
            var downloadHandle = Addressables.DownloadDependenciesAsync(label);
            while (!downloadHandle.IsDone)
            {
                progressCallback?.Invoke(downloadHandle.PercentComplete);
                yield return null;
            }
            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to download addressables for {label}: {downloadHandle.OperationException}");
            }
        }
        else
        {
            progressCallback?.Invoke(1f);
        }
    }

    public static IEnumerator LoadAsset<T>(string key, System.Action<T> callback) where T : UnityEngine.Object
    {
        yield return EnsureCatalog();

        if (_handleCache.TryGetValue(key, out var existing) && existing.IsValid())
        {
            if (!existing.IsDone)
                yield return existing;

            callback?.Invoke(existing.Convert<T>().Result);
            yield break;
        }

        var locHandle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
        yield return locHandle;

        bool hasLocation =
            locHandle.Status == AsyncOperationStatus.Succeeded &&
            locHandle.Result != null &&
            locHandle.Result.Count > 0;

        Addressables.Release(locHandle);

        if (!hasLocation)
        {
            Debug.LogWarning($"Addressables: No location found for key='{key}' (type {typeof(T).Name}).");
            callback?.Invoke(null);
            yield break;
        }

        AsyncOperationHandle<T> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to start loading asset '{key}': {ex}");
            callback?.Invoke(null);
            yield break;
        }

        _handleCache[key] = handle;
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            callback?.Invoke(handle.Result);
        }
        else
        {
            Debug.LogWarning($"Failed to load asset {key}: {handle.OperationException}");
            if (handle.IsValid())
                Addressables.Release(handle);
            _handleCache.Remove(key);
            callback?.Invoke(null);
        }
    }


    public static IEnumerator LoadAssetWithHandle<T>(string key, System.Action<T, AsyncOperationHandle<T>> callback) where T : Object
    {
        yield return EnsureCatalog();

        if (_handleCache.TryGetValue(key, out var cached) && cached.IsValid())
        {
            if (!cached.IsDone)
                yield return cached;

            var typedCached = cached.Convert<T>();
            callback?.Invoke(typedCached.Result, typedCached);
            yield break;
        }

        var locHandle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
        yield return locHandle;

        bool hasLocation =
            locHandle.Status == AsyncOperationStatus.Succeeded &&
            locHandle.Result != null &&
            locHandle.Result.Count > 0;

        Addressables.Release(locHandle);

        if (!hasLocation)
        {
            Debug.LogWarning($"Addressables: No location found for key='{key}' (type {typeof(T).Name}).");
            callback?.Invoke(default, default);
            yield break;
        }

        AsyncOperationHandle<T> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to start loading asset '{key}': {ex}");
            callback?.Invoke(default, default);
            yield break;
        }

        _handleCache[key] = handle;
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            callback?.Invoke(handle.Result, handle);
        }
        else
        {
            Debug.LogWarning($"Failed to load asset {key}: {handle.OperationException}");
            if (handle.IsValid())
                Addressables.Release(handle);
            _handleCache.Remove(key);
            callback?.Invoke(default, handle);
        }
    }

    public static IEnumerator LoadSprite(string key, System.Action<Sprite> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadAudioClip(string key, System.Action<AudioClip> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadMaterial(string key, System.Action<Material> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadMesh(string key, System.Action<Mesh> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadGameObject(string key, System.Action<GameObject> callback) => LoadAsset(key, callback);

    public static IEnumerator LoadNetworkObject(string key, System.Action<NetworkObject> callback)
    {
        yield return LoadAsset(key, callback);
    }

    public static T LoadAssetSync<T>(string key) where T : UnityEngine.Object
    {
        if (!_catalogLoaded)
        {
            var catalog = Addressables.LoadContentCatalogAsync(CatalogUrl);
            catalog.WaitForCompletion();
            if (catalog.Status == AsyncOperationStatus.Succeeded)
                _catalogLoaded = true;
            else
                Debug.LogError($"Failed to load catalog from {CatalogUrl}: {catalog.OperationException}");
        }

        if (_handleCache.TryGetValue(key, out var cached) && cached.IsValid())
        {
            if (!cached.IsDone)
                cached.WaitForCompletion();
            return cached.Convert<T>().Result;
        }

        var handle = Addressables.LoadAssetAsync<T>(key);
        _handleCache[key] = handle;
        handle.WaitForCompletion();
        if (handle.Status == AsyncOperationStatus.Succeeded)
            return handle.Result;

        Debug.LogError($"Failed to load asset {key}: {handle.OperationException}");
        if (handle.IsValid())
            Addressables.Release(handle);
        _handleCache.Remove(key);
        return null;
    }
#endif

    public static void ReleaseAsset(string key)
    {
#if !UNITY_SERVER
        if (_handleCache.TryGetValue(key, out var handle))
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            _handleCache.Remove(key);
        }
#endif
    }
}
