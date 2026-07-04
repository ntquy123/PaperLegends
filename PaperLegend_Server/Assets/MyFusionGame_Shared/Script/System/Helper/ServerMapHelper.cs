#if UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ServerMapHelper
{
    private static readonly HashSet<string> DefinedProjectTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ground",
        "UICanvas",
        "PopupUI",
        "NetworkManager",
        "Water",
        "Light",
        "Rock",
        "Grass",
        "Swamp",
        "Puddle",
        "Rain",
        "House",
        "Tree",
        "Wall",
        "BallPlayer",
        "RingBall",
        SceneLogicConfig.PaperLegendDrumTag,
        SceneLogicConfig.PaperLegendDrumObjectiveTag,
        SceneLogicConfig.PaperLegendSpawnTag
    };

    private static readonly HashSet<string> PaperLegendAllowedMapTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ground",
        "Water",
        "Rock",
        "Grass",
        "Swamp",
        "Puddle",
        "Rain",
        "House",
        "Tree",
        "Wall",
        SceneLogicConfig.PaperLegendDrumTag,
        SceneLogicConfig.PaperLegendDrumObjectiveTag
    };

    private readonly Dictionary<string, GameSessionNetWork_Host> _mapHostTemplates = new(StringComparer.OrdinalIgnoreCase);
    private GameObject? _mapHostContainer;
    public ServerMapHelper()
    {
    }

    public void EnsureMapConfigurations(IEnumerable<(string SceneName, SceneLogicConfig Config)> bindings)
    {
        if (MapSceneConfigManager.Instance == null)
        {
            var managerGo = new GameObject("MapSceneConfigManager");
            managerGo.AddComponent<MapSceneConfigManager>();
        }

        var manager = MapSceneConfigManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("Could not initialize MapSceneConfigManager.");
            return;
        }

        bool appliedAnyBinding = false;
        if (bindings != null)
        {
            var groupedBindings = bindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.SceneName) && binding.Config != null)
                .GroupBy(binding => binding.SceneName, StringComparer.OrdinalIgnoreCase);

            foreach (var bindingGroup in groupedBindings)
            {
                var configs = bindingGroup
                    .Select(binding => binding.Config)
                    .Where(config => config != null)
                    .ToList();

                if (configs.Count == 0)
                {
                    continue;
                }

                EnsureGroundTerrainEntries(configs);
                EnsureColliderMappings(configs, bindingGroup.Key);

                MapSceneConfig mapConfig;

                if (manager.TryGetConfig(bindingGroup.Key, out var existing))
                {
                    mapConfig = existing;
                }
                else
                {
                    var configGo = new GameObject($"MapConfig_{bindingGroup.Key}");
                    UnityEngine.Object.DontDestroyOnLoad(configGo);
                    mapConfig = configGo.AddComponent<MapSceneConfig>();
                }

                mapConfig.Initialise(bindingGroup.Key, configs);
                appliedAnyBinding = true;
            }
        }

        if (!appliedAnyBinding)
        {
            string fallbackMap = GameMapHelper.ToSceneName(GameMapId.HometownHouse);
            if (!manager.HasConfig(fallbackMap))
            {
                var configGo = new GameObject($"MapConfig_{fallbackMap}");
                UnityEngine.Object.DontDestroyOnLoad(configGo);
                var config = configGo.AddComponent<MapSceneConfig>();
                config.Initialise(fallbackMap);
            }
        }
    }

    public void PrepareSceneHostTemplates(
        IEnumerable<(string SceneName, SceneLogicConfig Config)> bindings,
        GameSessionNetWork_Host? activeHost)
    {
        if (_mapHostContainer == null)
        {
            _mapHostContainer = new GameObject("ServerMapHostTemplates");
            UnityEngine.Object.DontDestroyOnLoad(_mapHostContainer);
        }

        var containerTransform = _mapHostContainer.transform;
        var toRemove = new List<GameObject>();
        foreach (Transform child in containerTransform)
        {
            if (child != null)
            {
                toRemove.Add(child.gameObject);
            }
        }

        foreach (var child in toRemove)
        {
            if (child != null)
            {
                UnityEngine.Object.Destroy(child);
            }
        }

        _mapHostTemplates.Clear();

        if (bindings != null)
        {
            var groupedBindings = bindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.SceneName) && binding.Config != null)
                .GroupBy(binding => binding.SceneName, StringComparer.OrdinalIgnoreCase);

            foreach (var bindingGroup in groupedBindings)
            {
                var selectedConfig = SelectSceneLogicConfig(bindingGroup, bindingGroup.Key);
                if (selectedConfig == null)
                {
                    Debug.LogWarning($"No valid SceneLogicConfig found for map '{bindingGroup.Key}'.");
                    continue;
                }

                var hostTemplateGo = new GameObject($"HostTemplate_{bindingGroup.Key}");
                hostTemplateGo.transform.SetParent(containerTransform, false);
                //UnityEngine.Object.DontDestroyOnLoad(hostTemplateGo);

                var hostTemplate = hostTemplateGo.AddComponent<GameSessionNetWork_Host>();
                hostTemplateGo.AddComponent<AnimatorController>();
                hostTemplateGo.SetActive(false);

                var groundEntry = EnsureGroundTerrainEntry(selectedConfig);
                if (groundEntry != null)
                {
                    string groundTag = string.IsNullOrWhiteSpace(groundEntry.tag) ? "Ground" : groundEntry.tag;
                    string? hierarchyPath = groundEntry.hierarchyPath;

                    if (string.IsNullOrWhiteSpace(hierarchyPath))
                    {
                        hierarchyPath = BuildAnchorName(bindingGroup.Key, groundTag);
                        groundEntry.hierarchyPath = hierarchyPath;
                    }

                    var groundTransform = CreateOrResolveAnchorTransform(
                        bindingGroup.Key,
                        groundTag,
                        hierarchyPath,
                        hostTemplateGo.transform);

                    var terrainCollider = EnsureTerrainCollider(groundTransform.gameObject, groundEntry, selectedConfig);
                    if (terrainCollider == null)
                    {
                        Debug.LogWarning($"Could not create TerrainCollider for Ground on map '{bindingGroup.Key}'.");
                    }
                }

                ConfigureTemplateHost(hostTemplate, bindingGroup.Key, selectedConfig);

                hostTemplate.enabled = false;
                _mapHostTemplates[bindingGroup.Key] = hostTemplate;
            }
        }

        if (activeHost != null)
        {
            GameSessionNetWork_Host.Instance = activeHost;
        }
        else
        {
            GameSessionNetWork_Host.Instance = null;
        }
    }

    public bool TryGetSceneHostTemplate(string sceneName, out GameSessionNetWork_Host host)
    {
        host = null;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (!_mapHostTemplates.TryGetValue(sceneName, out var template) || template == null)
        {
            return false;
        }

        host = template;
        return true;
    }

    public bool HasSceneHostTemplate(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return _mapHostTemplates.ContainsKey(sceneName);
    }

    private static void EnsureColliderMappings(IEnumerable<SceneLogicConfig> configs, string sceneName)
    {
        if (PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            return;
        }

        if (configs == null)
        {
            return;
        }

        foreach (var config in configs)
        {
            if (config == null)
            {
                continue;
            }

            try
            {
                var playAreaCollider = config.EnsureBoxColliderForTag("PlayArea");
                if (playAreaCollider != null)
                {
                    playAreaCollider.isTrigger = true;
                    if (playAreaCollider.size == Vector3.zero)
                    {
                        playAreaCollider.size = Vector3.one;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not ensure collider for SceneLogicConfig '{config.name}' in scene '{sceneName}': {ex.Message}");
            }
        }
    }

    private void EnsureGroundTerrainEntries(IEnumerable<SceneLogicConfig> configs)
    {
        if (configs == null)
        {
            return;
        }

        foreach (var config in configs)
        {
            EnsureGroundTerrainEntry(config);
        }
    }

    private SceneLogicConfig.BoxColliderSyncEntry? EnsureGroundTerrainEntry(SceneLogicConfig? config)
    {
        if (config == null)
        {
            return null;
        }

        var groundEntry = config.EnsureBoxColliderForTag("Ground", SceneLogicConfig.ColliderShape.Terrain);
        groundEntry.shape = SceneLogicConfig.ColliderShape.Terrain;
        groundEntry.isTrigger = false;

        ApplyDefaultGroundAssets(config, groundEntry);

        return groundEntry;
    }

    private SceneLogicConfig? SelectSceneLogicConfig(
        IEnumerable<(string SceneName, SceneLogicConfig Config)> bindings,
        string sceneName)
    {
        foreach (var binding in bindings)
        {
            if (binding.Config == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(binding.Config.sceneName) &&
                string.Equals(binding.Config.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return binding.Config;
            }
        }

        return bindings
            .Select(binding => binding.Config)
            .FirstOrDefault(config => config != null);
    }

    private void ConfigureTemplateHost(GameSessionNetWork_Host host, string sceneName, SceneLogicConfig config)
    {
        if (host == null || config == null)
        {
            return;
        }

        host.FloodEnabled = false;
        if (host.LstLocationExam == null)
        {
            host.LstLocationExam = new List<Transform>();
        }

        if (host.LstLocationStartPoint == null)
        {
            host.LstLocationStartPoint = new List<Transform>();
        }

        if (host.LstLocationGatherPoint == null)
        {
            host.LstLocationGatherPoint = new List<Transform>();
        }

        if (host.BananaSpawnPoints == null)
        {
            host.BananaSpawnPoints = new List<Transform>();
        }

        if (host.PaperLegendSpawnPoints == null)
        {
            host.PaperLegendSpawnPoints = new List<Transform>();
        }

        host.TrongDongObject = null;
        host.DrumObjectiveObject = null;
        host.LstLocationExam.Clear();
        host.LstLocationStartPoint.Clear();
        host.LstLocationGatherPoint.Clear();
        host.BananaSpawnPoints.Clear();
        host.PaperLegendSpawnPoints.Clear();

        var anchorTransforms = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        var examAnchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        var startAnchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        var bananaAnchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        var paperLegendSpawnAnchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in config.transformEntries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.tag)))
        {
            if (ShouldSkipEntryForCurrentRuleset(entry.tag))
            {
                continue;
            }

            var anchorTransform = EnsureAnchorTransform(sceneName, entry.tag, entry.hierarchyPath, host.transform, anchorTransforms);
            if (anchorTransform == null)
            {
                continue;
            }

            ApplyEntryTransform(anchorTransform, entry);
            RegisterTransformUsage(host, entry, anchorTransform, examAnchors, startAnchors, bananaAnchors, paperLegendSpawnAnchors);
        }

        foreach (var entry in config.colliderEntries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.tag)))
        {
            if (ShouldSkipEntryForCurrentRuleset(entry.tag))
            {
                continue;
            }

            var anchorTransform = EnsureAnchorTransform(sceneName, entry.tag, entry.hierarchyPath, host.transform, anchorTransforms);
            if (anchorTransform == null)
            {
                continue;
            }

            var collider = EnsureColliderComponent(anchorTransform, sceneName, entry, config);

            if (collider == null)
            {
                Debug.LogWarning($"Could not assign collider '{entry.shape}' to anchor '{BuildAnchorName(sceneName, entry.tag)}'.");
                continue;
            }

            ApplyColliderEntry(collider, entry);

            RegisterColliderUsage(host, entry, anchorTransform, collider, bananaAnchors);
        }

        host.LstLocationExam.Clear();
        foreach (var location in examAnchors
                     .Where(kvp => kvp.Value != null)
                     .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            host.LstLocationExam.Add(location.Value);
        }

        host.LstLocationStartPoint.Clear();
        host.LstLocationGatherPoint.Clear();
        foreach (var location in startAnchors
                     .Where(kvp => kvp.Value != null)
                     .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            host.LstLocationStartPoint.Add(location.Value);
            if (!host.LstLocationGatherPoint.Contains(location.Value))
            {
                host.LstLocationGatherPoint.Add(location.Value);
            }
        }

        host.BananaSpawnPoints.Clear();
        foreach (var location in bananaAnchors
                     .Where(kvp => kvp.Value != null)
                     .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            host.BananaSpawnPoints.Add(location.Value);
        }

        host.PaperLegendSpawnPoints.Clear();
        foreach (var location in paperLegendSpawnAnchors
                     .Where(kvp => kvp.Value != null)
                     .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            host.PaperLegendSpawnPoints.Add(location.Value);
        }

        if (host.PaperLegendSpawnPoints.Count > 0 && host.SpawnPlayerPoint == null)
        {
            host.SpawnPlayerPoint = host.PaperLegendSpawnPoints[0];
        }

        Debug.Log($"[ServerMapHelper][PaperLegends] Template '{sceneName}' configured {host.PaperLegendSpawnPoints.Count} spawn point(s).");
        if (PaperLegendRuntimeState.IsPaperLegendMatch &&
            host.PaperLegendSpawnPoints.Count < SceneLogicConfig.PaperLegendSpawnPointCount)
        {
            Debug.LogError($"[ServerMapHelper][PaperLegends] Template '{sceneName}' is missing '{SceneLogicConfig.PaperLegendSpawnTag}' config. Expected at least {SceneLogicConfig.PaperLegendSpawnPointCount}, got {host.PaperLegendSpawnPoints.Count}.");
        }
    }

    private Transform? EnsureAnchorTransform(
        string sceneName,
        string tag,
        string? hierarchyPath,
        Transform parent,
        Dictionary<string, Transform> anchors)
    {
        string key = BuildAnchorKey(tag, hierarchyPath);

        if (anchors.TryGetValue(key, out var existing) && existing != null)
        {
            return existing;
        }

        var anchorTransform = CreateOrResolveAnchorTransform(sceneName, tag, hierarchyPath, parent);
        anchors[key] = anchorTransform;
        return anchorTransform;
    }

    private static string BuildAnchorKey(string tag, string? hierarchyPath)
    {
        if (!string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return hierarchyPath;
        }

        return string.IsNullOrWhiteSpace(tag) ? string.Empty : tag;
    }

    private static Transform CreateOrResolveAnchorTransform(
        string sceneName,
        string tag,
        string? hierarchyPath,
        Transform parent)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        if (!string.IsNullOrWhiteSpace(hierarchyPath))
        {
            var segments = hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = parent;

            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                var next = current.Find(segment);
                if (next == null)
                {
                    var go = new GameObject(segment);
                    go.transform.SetParent(current, false);
                    next = go.transform;
                }

                current = next;
            }

            ApplyTagSafely(current.gameObject, tag);
            return current;
        }

        var anchorGo = new GameObject(BuildAnchorName(sceneName, tag));
        anchorGo.transform.SetParent(parent, false);
        ApplyTagSafely(anchorGo, tag);
        return anchorGo.transform;
    }

    private static void ApplyTagSafely(GameObject target, string? tag)
    {
        if (target == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (!IsDefinedProjectTag(tag))
        {
            Debug.LogWarning($"[ServerMapHelper] Skip undefined legacy tag '{tag}' on GameObject '{target.name}'.");
            return;
        }

        try
        {
            target.tag = tag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"Tag '{tag}' is not defined in the project. GameObject '{target.name}' will keep its current tag.");
        }
    }

    private static bool ShouldSkipEntryForCurrentRuleset(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return true;
        }

        if (!PaperLegendRuntimeState.IsPaperLegendMatch)
        {
            return false;
        }

        return !IsPaperLegendMapTag(tag);
    }

    private static bool IsPaperLegendMapTag(string tag)
    {
        return PaperLegendAllowedMapTags.Contains(tag) ||
               string.Equals(tag, SceneLogicConfig.PaperLegendSpawnTag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefinedProjectTag(string tag)
    {
        return DefinedProjectTags.Contains(tag) ||
               string.Equals(tag, SceneLogicConfig.PaperLegendSpawnTag, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAnchorName(string sceneName, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.IsNullOrWhiteSpace(sceneName) ? "SceneAnchor" : $"{sceneName}_SceneAnchor";
        }

        return string.IsNullOrWhiteSpace(sceneName) ? tag : $"{sceneName}_{tag}";
    }

    private static void ApplyEntryTransform(Transform target, SceneLogicConfig.TransformSyncEntry entry)
    {
        if (target == null || entry == null)
        {
            return;
        }

        if (entry.applyPosition)
        {
            target.position = entry.position;
        }

        if (entry.applyRotation)
        {
            target.rotation = Quaternion.Euler(entry.rotation);
        }

        if (entry.applyScale)
        {
            target.localScale = entry.localScale;
        }
    }

    private static void RegisterTransformUsage(
        GameSessionNetWork_Host host,
        SceneLogicConfig.TransformSyncEntry entry,
        Transform? transform,
        Dictionary<string, Transform> examAnchors,
        Dictionary<string, Transform> startAnchors,
        Dictionary<string, Transform> bananaAnchors,
        Dictionary<string, Transform> paperLegendSpawnAnchors)
    {
        if (host == null || transform == null || entry == null || string.IsNullOrWhiteSpace(entry.tag))
        {
            return;
        }

        var tag = entry.tag;
        var anchorKey = BuildAnchorKey(entry.tag, entry.hierarchyPath);
        var bananaKey = string.IsNullOrEmpty(anchorKey) ? tag : anchorKey;

        if (IsBananaSpawnTag(tag))
        {
            bananaAnchors[bananaKey] = transform;
            return;
        }

        if (IsPaperLegendSpawnTag(tag))
        {
            paperLegendSpawnAnchors[bananaKey] = transform;
            return;
        }

        if (IsPaperLegendDrumTag(tag))
        {
            host.TrongDongObject = transform;
            return;
        }

        if (IsPaperLegendDrumObjectiveTag(tag))
        {
            host.DrumObjectiveObject = transform;
            return;
        }

        if (string.Equals(tag, "SpawnPoint", StringComparison.OrdinalIgnoreCase))
        {
            host.SpawnPlayerPoint = transform;
            return;
        }

        if (string.Equals(tag, "SpawnBallPoint", StringComparison.OrdinalIgnoreCase))
        {
            host.SpawnBallPoint = transform;
            return;
        }

        if (string.Equals(tag, "ExamPoint", StringComparison.OrdinalIgnoreCase))
        {
            host.ExamMain = transform;
            return;
        }

        if (string.Equals(tag, "StartPointMain", StringComparison.OrdinalIgnoreCase))
        {
            host.StartPointMain = transform;
            return;
        }
 

        if (tag.StartsWith("ExamPoint_", StringComparison.OrdinalIgnoreCase))
        {
            examAnchors[tag] = transform;
            return;
        }

        if (tag.StartsWith("StartPoint_", StringComparison.OrdinalIgnoreCase))
        {
            startAnchors[tag] = transform;
        }
        if (string.Equals(tag, "ExamPoint", StringComparison.OrdinalIgnoreCase))
        {
            host.ExamMain = transform;
            return;
        }
        if (string.Equals(tag, "PlayAreaGuard", StringComparison.OrdinalIgnoreCase))
        {
            host.playAreaGuard = transform;
            return;
        }
    }

    private static void RegisterColliderUsage(
        GameSessionNetWork_Host host,
        SceneLogicConfig.BoxColliderSyncEntry entry,
        Transform anchorTransform,
        Collider collider,
        Dictionary<string, Transform> bananaAnchors)
    {
        if (host == null || entry == null || string.IsNullOrWhiteSpace(entry.tag))
        {
            return;
        }

        if (string.Equals(entry.tag, "PlayArea", StringComparison.OrdinalIgnoreCase))
        {
            if (collider is BoxCollider box)
            {
                host.playArea = box;
            }
            else if (anchorTransform != null && anchorTransform.TryGetComponent(out BoxCollider fallbackBox))
            {
                host.playArea = fallbackBox;
            }
            return;
        }

        if (string.Equals(entry.tag, "Water", StringComparison.OrdinalIgnoreCase))
        {
            if (anchorTransform == null)
            {
                Debug.LogError("[ServerMapHelper] Cannot assign WaterObject because anchorTransform is null.");
                return;
            }

            host.WaterObject = anchorTransform;

            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
            }

            Debug.Log($"[ServerMapHelper] WaterObject configured from template: {GetHierarchyPath(anchorTransform)} collider={collider?.GetType().Name ?? "null"}");
            return;
        }

        if (string.Equals(entry.tag, "Ground", StringComparison.OrdinalIgnoreCase))
        {
            if (anchorTransform != null)
            {
                var terrain = anchorTransform.GetComponent<Terrain>();
                if (terrain != null)
                {
                    host.TerrainGround = terrain;
                }
            }
        }

        if (IsPaperLegendDrumTag(entry.tag) && anchorTransform != null)
        {
            host.TrongDongObject = anchorTransform;
            Debug.Log($"[ServerMapHelper][PaperLegends] TrongDong configured from template: {GetHierarchyPath(anchorTransform)} collider={collider?.GetType().Name ?? "null"}");
            return;
        }

        if (IsPaperLegendDrumObjectiveTag(entry.tag) && anchorTransform != null)
        {
            host.DrumObjectiveObject = anchorTransform;
            if (collider != null)
                collider.isTrigger = true;

            Debug.Log($"[ServerMapHelper][PaperLegends] DrumObjective configured from template: {GetHierarchyPath(anchorTransform)} collider={collider?.GetType().Name ?? "null"} trigger={collider?.isTrigger.ToString() ?? "null"}");
            return;
        }

        if (IsBananaSpawnTag(entry.tag) && anchorTransform != null)
        {
            var anchorKey = BuildAnchorKey(entry.tag, entry.hierarchyPath);
            var bananaKey = string.IsNullOrEmpty(anchorKey) ? entry.tag : anchorKey;
            bananaAnchors[bananaKey] = anchorTransform;
        }
    }

    private static bool IsBananaSpawnTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return tag.StartsWith("BananaSpawn", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaperLegendSpawnTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(tag, SceneLogicConfig.PaperLegendSpawnTag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaperLegendDrumTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(tag, SceneLogicConfig.PaperLegendDrumTag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPaperLegendDrumObjectiveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(tag, SceneLogicConfig.PaperLegendDrumObjectiveTag, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var names = new List<string>();
        var current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private Collider? EnsureColliderComponent(
        Transform anchorTransform,
        string sceneName,
        SceneLogicConfig.BoxColliderSyncEntry entry,
        SceneLogicConfig config)
    {
        if (anchorTransform == null || entry == null)
        {
            return null;
        }

        Collider? compatibleCollider = null;

        var existingColliders = anchorTransform.GetComponents<Collider>();
        foreach (var existing in existingColliders)
        {
            if (existing == null)
            {
                continue;
            }

            if (IsColliderCompatible(existing, entry.shape))
            {
                if (compatibleCollider == null)
                {
                    compatibleCollider = existing;
                }
                else
                {
                    UnityEngine.Object.Destroy(existing);
                }
            }
            else
            {
                UnityEngine.Object.Destroy(existing);
            }
        }

        if (compatibleCollider != null)
        {
            return compatibleCollider;
        }

        try
        {
            return entry.shape switch
            {
                SceneLogicConfig.ColliderShape.Box => anchorTransform.gameObject.AddComponent<BoxCollider>(),
                SceneLogicConfig.ColliderShape.Capsule => anchorTransform.gameObject.AddComponent<CapsuleCollider>(),
                SceneLogicConfig.ColliderShape.Terrain => EnsureTerrainCollider(anchorTransform.gameObject, entry, config),
                _ => null
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not add collider '{entry.shape}' to anchor '{BuildAnchorName(sceneName, entry.tag)}': {ex.Message}");
            return null;
        }
    }

    private Collider? EnsureTerrainCollider(
        GameObject target,
        SceneLogicConfig.BoxColliderSyncEntry entry,
        SceneLogicConfig config)
    {
        if (target == null)
        {
            return null;
        }

        if (entry == null)
        {
            return null;
        }

        ApplyDefaultGroundAssets(config, entry);

        var terrain = target.GetComponent<Terrain>();
        if (terrain == null)
        {
            terrain = target.AddComponent<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogWarning($"Could not create Terrain component for '{target.name}'.");
            return null;
        }

        if (entry.terrainData != null)
        {
            terrain.terrainData = entry.terrainData;
        }

        var terrainCollider = target.GetComponent<TerrainCollider>();
        if (terrainCollider == null)
        {
            try
            {
                terrainCollider = target.AddComponent<TerrainCollider>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not add TerrainCollider to '{target.name}': {ex.Message}");
                return null;
            }
        }

        if (terrainCollider == null)
        {
            Debug.LogWarning($"TerrainCollider still does not exist on '{target.name}'.");
            return null;
        }

        if (entry.terrainData != null)
        {
            try
            {
                terrainCollider.terrainData = entry.terrainData;
            }
            catch (MissingComponentException ex)
            {
                Debug.LogWarning($"Could not assign TerrainData to TerrainCollider on '{target.name}': {ex.Message}");
                return null;
            }
        }

        terrainCollider.isTrigger = entry.isTrigger;
        if (entry.physicMaterial != null)
        {
            terrainCollider.material = entry.physicMaterial;
            terrainCollider.sharedMaterial = entry.physicMaterial;
        }

        return terrainCollider;
    }

    private static void ApplyColliderEntry(Collider collider, SceneLogicConfig.BoxColliderSyncEntry entry)
    {
        if (collider == null || entry == null)
        {
            return;
        }

        switch (entry.shape)
        {
            case SceneLogicConfig.ColliderShape.Box when collider is BoxCollider box:
                box.center = entry.center;
                box.size = entry.size;
                break;
            case SceneLogicConfig.ColliderShape.Capsule when collider is CapsuleCollider capsule:
                capsule.center = entry.center;
                capsule.radius = Mathf.Max(0f, entry.radius);
                capsule.height = Mathf.Max(entry.radius * 2f, entry.height);
                capsule.direction = Mathf.Clamp((int)entry.capsuleDirection, 0, 2);
                break;
            case SceneLogicConfig.ColliderShape.Terrain when collider is TerrainCollider terrainCollider:
                if (entry.terrainData != null)
                {
                    terrainCollider.terrainData = entry.terrainData;
                }
                break;
        }

        collider.isTrigger = entry.isTrigger;
        collider.material = entry.physicMaterial;
        collider.sharedMaterial = entry.physicMaterial;

        if (collider is TerrainCollider terrain)
        {
            terrain.material = entry.physicMaterial;
        }
    }

    private static bool IsColliderCompatible(Collider collider, SceneLogicConfig.ColliderShape shape)
    {
        return shape switch
        {
            SceneLogicConfig.ColliderShape.Box => collider is BoxCollider,
            SceneLogicConfig.ColliderShape.Capsule => collider is CapsuleCollider,
            SceneLogicConfig.ColliderShape.Terrain => collider is TerrainCollider,
            _ => false
        };
    }

    private static void ApplyDefaultGroundAssets(SceneLogicConfig? config, SceneLogicConfig.BoxColliderSyncEntry entry)
    {
        if (config == null || entry == null)
        {
            return;
        }

        var assets = config.PhysicsAssets;
        if (assets == null)
        {
            return;
        }

        entry.physicMaterial ??= assets.LoadMaterial(SceneLogicConfig.PhysicsAssetLibrary.MaterialLink.Ground);
        entry.terrainData ??= assets.LoadTerrainData(SceneLogicConfig.PhysicsAssetLibrary.TerrainLink.Ground);
    }
}
#endif
