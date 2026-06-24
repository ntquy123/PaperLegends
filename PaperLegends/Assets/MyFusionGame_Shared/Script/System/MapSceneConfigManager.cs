using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry used to keep track of <see cref="MapSceneConfig"/> instances.
/// </summary>
public class MapSceneConfigManager : MonoBehaviour
{
    public static MapSceneConfigManager? Instance { get; private set; }

    private readonly Dictionary<string, MapSceneConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

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

    public void Register(MapSceneConfig config)
    {
        if (config == null)
        {
            return;
        }

        var key = config.SceneName;
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("⚠️ MapSceneConfig không có SceneName hợp lệ.");
            return;
        }

        _configs[key] = config;
    }

    public bool TryGetConfig(string sceneName, out MapSceneConfig config)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            config = null;
            return false;
        }

        return _configs.TryGetValue(sceneName, out config);
    }

    public bool HasConfig(string sceneName)
    {
        return _configs.ContainsKey(sceneName);
    }
}
