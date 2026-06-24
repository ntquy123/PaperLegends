using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class AvatarService : MonoBehaviour
{
    public struct AvatarRequest
    {
        public AuthenticationProviderType ProviderType;
        public string AvatarUrl;
        public string FirebaseGuid;
        public string StorageFolder;
        public bool AllowStorageFallback;

        public AvatarRequest(AuthenticationProviderType providerType, string avatarUrl, string firebaseGuid, string storageFolder = "avatars", bool allowStorageFallback = false)
        {
            ProviderType = providerType;
            AvatarUrl = avatarUrl;
            FirebaseGuid = firebaseGuid;
            StorageFolder = storageFolder;
            AllowStorageFallback = allowStorageFallback;
        }
    }

    private sealed class AvatarCallbacks
    {
        public readonly List<Action<Texture2D>> OnLoaded = new();
        public readonly List<Action<string>> OnFailed = new();
    }

    public static AvatarService Instance;

    private readonly Dictionary<string, Texture2D> avatarCache = new();
    private readonly Dictionary<string, Coroutine> activeLoads = new();
    private readonly Dictionary<string, AvatarCallbacks> pendingCallbacks = new();

    public static AvatarService EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        var host = new GameObject("AvatarService");
        Instance = host.AddComponent<AvatarService>();
        DontDestroyOnLoad(host);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TryGetCachedAvatar(string cacheKey, out Texture2D texture)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            texture = null;
            return false;
        }

        return avatarCache.TryGetValue(cacheKey, out texture) && texture != null;
    }

    public Coroutine LoadAvatar(AvatarRequest request, Action<Texture2D> onLoaded, Action<string> onFailed = null)
    {
        string cacheKey = BuildCacheKey(request);
        if (string.IsNullOrEmpty(cacheKey))
        {
            Debug.LogWarning($"[AvatarService] LoadAvatar: không có nguồn hợp lệ (url='{request.AvatarUrl}', guid='{request.FirebaseGuid}', providerType='{request.ProviderType}', allowStorageFallback={request.AllowStorageFallback}).");
            onFailed?.Invoke("Avatar request is missing a valid source.");
            return null;
        }

        if (TryGetCachedAvatar(cacheKey, out var cachedTexture))
        {
            onLoaded?.Invoke(cachedTexture);
            return null;
        }

        if (activeLoads.TryGetValue(cacheKey, out var running) && running != null)
        {
            RegisterCallbacks(cacheKey, onLoaded, onFailed);
            return running;
        }

        RegisterCallbacks(cacheKey, onLoaded, onFailed);
        var routine = StartCoroutine(LoadAvatarRoutine(request, cacheKey));
        activeLoads[cacheKey] = routine;
        return routine;
    }

    public void ClearCache()
    {
        foreach (var texture in avatarCache.Values)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        avatarCache.Clear();
        pendingCallbacks.Clear();
        activeLoads.Clear();
    }

    private static bool IsGoogleProvider(AuthenticationProviderType providerType)
    {
        return providerType == AuthenticationProviderType.GooglePlayGames || providerType == AuthenticationProviderType.Google;
    }

    private static string BuildCacheKey(AvatarRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            return request.AvatarUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.FirebaseGuid))
        {
            var folder = string.IsNullOrWhiteSpace(request.StorageFolder) ? "avatars" : request.StorageFolder.Trim();
            return $"storage://{folder}/{request.FirebaseGuid.Trim()}";
        }

        return null;
    }

    private void RegisterCallbacks(string cacheKey, Action<Texture2D> onLoaded, Action<string> onFailed)
    {
        if (!pendingCallbacks.TryGetValue(cacheKey, out var callbacks))
        {
            callbacks = new AvatarCallbacks();
            pendingCallbacks[cacheKey] = callbacks;
        }

        if (onLoaded != null)
        {
            callbacks.OnLoaded.Add(onLoaded);
        }

        if (onFailed != null)
        {
            callbacks.OnFailed.Add(onFailed);
        }
    }

    private IEnumerator LoadAvatarRoutine(AvatarRequest request, string cacheKey)
    {
        Texture2D texture = null;
        string errorMessage = null;

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            Debug.Log($"[AvatarService] Tải avatar từ URL: '{request.AvatarUrl}'.");
            yield return DownloadTextureFromUrl(request.AvatarUrl, result => texture = result, error => errorMessage = error);
        }
        else if (!string.IsNullOrWhiteSpace(request.FirebaseGuid) && (request.AllowStorageFallback || !IsGoogleProvider(request.ProviderType)))
        {
            if (UgsToFirebaseAuth.Instance == null)
            {
                Debug.LogWarning($"[AvatarService] UgsToFirebaseAuth chưa sẵn sàng (guid='{request.FirebaseGuid}', providerType='{request.ProviderType}').");
                errorMessage = "UgsToFirebaseAuth is not available.";
            }
            else
            {
                Debug.Log($"[AvatarService] Tải avatar từ Firebase Storage: guid='{request.FirebaseGuid}', folder='{request.StorageFolder}'.");
                Task<Texture2D> downloadTask;
                try
                {
                    downloadTask = UgsToFirebaseAuth.Instance.DownloadPlayerAvatarAsync(request.FirebaseGuid, default, request.StorageFolder);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    downloadTask = null;
                }

                if (downloadTask != null)
                {
                    while (!downloadTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (downloadTask.IsCanceled)
                    {
                        errorMessage = "Avatar download canceled.";
                    }
                    else if (downloadTask.IsFaulted)
                    {
                        errorMessage = downloadTask.Exception?.GetBaseException().Message;
                    }
                    else
                    {
                        texture = downloadTask.Result;
                    }
                }
            }
        }
        else
        {
            errorMessage = "No avatar source available for this provider.";
        }

        activeLoads.Remove(cacheKey);

        if (texture != null)
        {
            avatarCache[cacheKey] = texture;
            InvokeCallbacks(cacheKey, texture, null);
        }
        else
        {
            InvokeCallbacks(cacheKey, null, errorMessage);
        }
    }

    private IEnumerator DownloadTextureFromUrl(string url, Action<Texture2D> onSuccess, Action<string> onFailure)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onFailure?.Invoke("Avatar URL is empty.");
            yield break;
        }

        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                onFailure?.Invoke(request.error);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            onSuccess?.Invoke(texture);
        }
    }

    private void InvokeCallbacks(string cacheKey, Texture2D texture, string errorMessage)
    {
        if (!pendingCallbacks.TryGetValue(cacheKey, out var callbacks))
        {
            return;
        }

        pendingCallbacks.Remove(cacheKey);

        if (texture != null)
        {
            foreach (var callback in callbacks.OnLoaded)
            {
                callback?.Invoke(texture);
            }
            return;
        }

        string error = string.IsNullOrWhiteSpace(errorMessage) ? "Avatar download failed." : errorMessage;
        foreach (var callback in callbacks.OnFailed)
        {
            callback?.Invoke(error);
        }
    }
}
