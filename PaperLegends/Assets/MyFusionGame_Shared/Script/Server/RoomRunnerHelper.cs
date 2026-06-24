using System;
using Fusion;
using UnityEngine;

public static class RoomRunnerHelper
{
    public static bool TryGetRoomRunner(NetworkBehaviour behaviour, out NetworkRunner runner, bool logError = true)
    {
        WeakReference<NetworkRunner>? cache = null;
        return TryGetRoomRunner(behaviour, ref cache, out runner, logError);
    }

    public static bool TryGetRoomRunner(NetworkBehaviour behaviour, ref WeakReference<NetworkRunner>? cache, out NetworkRunner runner, bool logError = true)
    {
        runner = null;

        if (behaviour == null)
        {
            if (logError)
            {
                Debug.LogError("❌ Không thể lấy NetworkRunner vì NetworkBehaviour truyền vào là null.");
            }

            return false;
        }

        if (cache != null && cache.TryGetTarget(out var cached) && cached != null)
        {
            runner = cached;
            return true;
        }

        var directRunner = behaviour.Runner;
        if (directRunner != null)
        {
            UpdateRunnerCache(ref cache, directRunner);
            runner = directRunner;
            return true;
        }

        var networkObject = behaviour.Object;
        if (networkObject != null)
        {
            var objectRunner = networkObject.Runner;
            if (objectRunner != null)
            {
                UpdateRunnerCache(ref cache, objectRunner);
                runner = objectRunner;
                return true;
            }
        }

        var parentRunner = behaviour.GetComponentInParent<NetworkRunner>();
        if (parentRunner != null)
        {
            UpdateRunnerCache(ref cache, parentRunner);
            runner = parentRunner;
            return true;
        }

        var scene = behaviour.gameObject.scene;
        if (scene.IsValid())
        {
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<NetworkRunner>())
            {
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    UpdateRunnerCache(ref cache, candidate);
                    runner = candidate;
                    return true;
                }
            }
        }

        if (logError)
        {
            var sceneName = scene.IsValid() ? scene.name : "Unknown";
            Debug.LogError($"❌ Không tìm thấy NetworkRunner hợp lệ cho đối tượng '{behaviour.name}' trong scene '{sceneName}'.");
        }

        return false;
    }

    public static bool EnsureRunnerDontDestroyOnLoad(NetworkBehaviour behaviour, GameObject target, ref WeakReference<NetworkRunner>? cache, bool logError = true)
    {
        if (target == null)
        {
            if (logError)
            {
                Debug.LogError($"❌ Không thể đánh dấu DontDestroyOnLoad vì GameObject mục tiêu của '{behaviour?.name}' là null.");
            }

            return false;
        }

        UnityEngine.Object.DontDestroyOnLoad(target);

        if (!TryGetRoomRunner(behaviour, ref cache, out var runner, logError))
        {
            return false;
        }

        return TryMakeRunnerDontDestroyOnLoad(runner, target, behaviour.name);
    }

    public static bool TryMakeRunnerDontDestroyOnLoad(NetworkRunner runner, GameObject target, string context, bool logWarning = true)
    {
        if (runner == null || target == null)
        {
            return false;
        }

        try
        {
            runner.MakeDontDestroyOnLoad(target);
            return true;
        }
        catch (Exception ex)
        {
            if (logWarning)
            {
                Debug.LogWarning($"⚠️ Không thể đánh dấu '{target.name}' DontDestroyOnLoad thông qua runner '{runner.name}' cho '{context}': {ex.Message}");
            }

            return false;
        }
    }

    public static void UpdateRunnerCache(ref WeakReference<NetworkRunner>? cache, NetworkRunner runner)
    {
        if (runner == null)
        {
            return;
        }

        if (cache == null)
        {
            cache = new WeakReference<NetworkRunner>(runner);
        }
        else
        {
            cache.SetTarget(runner);
        }
    }
}
