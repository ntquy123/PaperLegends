using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Holds scene specific configuration and is responsible for wiring the runtime
/// objects required by <see cref="GameSessionNetWork_Host"/>.
/// Each map can have its own instance of this component attached to a separate GameObject.
/// </summary>
public class MapSceneConfig : MonoBehaviour
{
    [SerializeField]
    private string sceneName = GameMapHelper.ToSceneName(GameMapId.HometownHouse);

    [SerializeField]
    private List<SceneLogicConfig> sceneConfigs = new();

    public string SceneName => sceneName;

    /// <summary>
    /// Allows initialising the configuration when instantiated from code.
    /// </summary>
    public void Initialise(string targetSceneName, IEnumerable<SceneLogicConfig>? configs = null)
    {
        sceneName = targetSceneName;

        if (configs != null)
        {
            sceneConfigs = configs.Where(config => config != null).ToList();
        }

        MapSceneConfigManager.Instance?.Register(this);
    }

    private void Awake()
    {
        MapSceneConfigManager.Instance?.Register(this);
    }
    #if UNITY_SERVER
    public bool Apply(GameSessionNetWork_Host host, NetworkObjectManager? networkManager = null)
    {
        var errors = new List<string>();

        if (host == null)
        {
            var message = "⚠️ Không thể cấu hình host vì tham chiếu host null.";
            Debug.LogWarning(message);
            errors.Add(message);
            NotifyInitializationFailed(networkManager, errors);
            return false;
        }

        var activeConfig = ApplySharedSceneConfigs();
        var configured = ConfigureHostSceneReferences(host, activeConfig, errors);

        if (!configured)
        {
            NotifyInitializationFailed(networkManager, errors);
        }

        return configured;
    }

    private SceneLogicConfig? ApplySharedSceneConfigs()
    {
        if (sceneConfigs == null || sceneConfigs.Count == 0)
        {
            Debug.LogWarning("⚠️ MapSceneConfig chưa được gán SceneLogicConfig nào để áp dụng.");
            return null;
        }

        SceneLogicConfig? matchedConfig = null;

        foreach (var config in sceneConfigs)
        {
            if (config == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sceneName) &&
                !string.IsNullOrWhiteSpace(config.sceneName) &&
                !string.Equals(config.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            config.ApplyToScene();
            matchedConfig ??= config;
        }

        if (matchedConfig == null)
        {
            foreach (var config in sceneConfigs)
            {
                if (config == null)
                {
                    continue;
                }

                config.ApplyToScene();
                matchedConfig ??= config;
            }
        }

        if (matchedConfig == null)
        {
            Debug.LogWarning($"⚠️ Không tìm thấy SceneLogicConfig phù hợp cho scene '{sceneName}'.");
        }

        return matchedConfig;
    }

    private bool ConfigureHostSceneReferences(GameSessionNetWork_Host host, SceneLogicConfig? activeConfig, List<string> errors)
    {
        bool isPaperLegends = PaperLegendRuntimeState.IsPaperLegendMatch;
        if (isPaperLegends)
        {
            return ConfigurePaperLegendHostSceneReferences(host, activeConfig, errors);
        }
        host.playArea = ResolveTriggerBoxCollider(activeConfig, "PlayArea", "thiết lập PlayArea", errors);
        host.SpawnPlayerPoint = ResolveTransform(activeConfig, "SpawnPoint", "thiết lập SpawnPlayerPoint", errors);
        host.SpawnBallPoint = ResolveTransform(activeConfig, "SpawnBallPoint", "thiết lập SpawnBallPoint", errors);
        host.ExamMain = ResolveTransform(activeConfig, "ExamPoint", "thiết lập ExamMain", errors);
        host.StartPointMain = ResolveTransform(activeConfig, "StartPointMain", "thiết lập StartPointMain", errors);
        host.playAreaGuard = ResolveTransform(activeConfig, "PlayAreaGuard", "thiết lập PlayAreaGuard", errors);
        ConfigureWaterObject(host, activeConfig, errors);
 

        var groundObject = SafeFindWithTag("Ground", "thiết lập TerrainGround", errors);
        host.TerrainGround = groundObject != null ? groundObject.GetComponent<Terrain>() : null;

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

        if (host.PaperLegendSpawnPoints == null)
        {
            host.PaperLegendSpawnPoints = new List<Transform>();
        }

        host.LstLocationExam.Clear();
        host.LstLocationStartPoint.Clear();
        host.LstLocationGatherPoint.Clear();
        host.PaperLegendSpawnPoints.Clear();

        if (activeConfig != null)
        {
            PopulateLocationList(host.LstLocationExam, activeConfig, "ExamPoint_", errors);
            PopulateLocationList(host.LstLocationStartPoint, activeConfig, "StartPoint_", errors);
            PopulateExactTaggedLocationList(host.PaperLegendSpawnPoints, activeConfig, SceneLogicConfig.PaperLegendSpawnTag, errors);
        }

        if (host.PaperLegendSpawnPoints.Count == 0)
            PopulateSceneTaggedLocationList(host.PaperLegendSpawnPoints, SceneLogicConfig.PaperLegendSpawnTag, "thiet lap Paper Legends spawn points", errors, false);

        if (host.LstLocationExam.Count == 0)
        {
            AddIfNotNull(host.LstLocationExam, SafeFindWithTag("ExamPoint_1", "thiết lập ExamPoint mặc định", errors, false)?.transform);
            AddIfNotNull(host.LstLocationExam, SafeFindWithTag("ExamPoint_2", "thiết lập ExamPoint mặc định", errors, false)?.transform);
        }

        if (host.LstLocationStartPoint.Count == 0)
        {
            AddIfNotNull(host.LstLocationStartPoint, SafeFindWithTag("StartPoint_1", "thiết lập StartPoint mặc định", errors, false)?.transform);
            AddIfNotNull(host.LstLocationStartPoint, SafeFindWithTag("StartPoint_2", "thiết lập StartPoint mặc định", errors, false)?.transform);
        }

        EnsureIndexedTaggedLocations(host.LstLocationStartPoint, "StartPoint_", 3, "thiet lap StartPoint mac dinh", errors);
        CopyLocationList(host.LstLocationStartPoint, host.LstLocationGatherPoint);
        if (host.PaperLegendSpawnPoints.Count > 0 &&
            host.PaperLegendSpawnPoints.Count < SceneLogicConfig.PaperLegendSpawnPointCount)
        {
            Debug.LogWarning($"[PaperLegends][Spawn] Chi tim thay {host.PaperLegendSpawnPoints.Count}/{SceneLogicConfig.PaperLegendSpawnPointCount} diem '{SceneLogicConfig.PaperLegendSpawnTag}' trong map.");
        }

        if (host.PaperLegendSpawnPoints.Count > 0 && host.SpawnPlayerPoint == null)
        {
            host.SpawnPlayerPoint = host.PaperLegendSpawnPoints[0];
        }

        if (host.playArea == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo PlayArea cho MapSceneConfig.");
        }

        if (host.SpawnPlayerPoint == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo SpawnPlayerPoint cho MapSceneConfig.");
        }

        if (host.SpawnBallPoint == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo SpawnBallPoint cho MapSceneConfig.");
        }

        if (host.ExamMain == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo ExamMain cho MapSceneConfig.");
        }

        if (host.StartPointMain == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo StartPointMain cho MapSceneConfig.");
        }

        if (host.playAreaGuard == null)
        {
            RegisterError(errors, "❌ Không thể khởi tạo PlayAreaGuard cho MapSceneConfig.");
        }
 

        if (host.LstLocationExam.Count == 0)
        {
            RegisterError(errors, "⚠️ Không tìm thấy bất kỳ ExamPoint nào cho MapSceneConfig.");
        }

        if (host.LstLocationStartPoint.Count == 0)
        {
            RegisterError(errors, "⚠️ Không tìm thấy bất kỳ StartPoint nào cho MapSceneConfig.");
        }
        //Có chế độ không cần vòng nên không cần kiểm tra
        //if (host.playAreaGuard == null)
        //{
        //    RegisterError(errors, "❌ Không thể khởi tạo playAreaGuard cho MapSceneConfig.");
        //}
        return errors.Count == 0;
    }

    private bool ConfigurePaperLegendHostSceneReferences(GameSessionNetWork_Host host, SceneLogicConfig? activeConfig, List<string> errors)
    {
        host.playArea = null;
        host.SpawnBallPoint = null;
        host.ExamMain = null;
        host.StartPointMain = null;
        host.playAreaGuard = null;

        ConfigureWaterObject(host, activeConfig, errors);

        var groundObject = SafeFindWithTag("Ground", "thiet lap TerrainGround Paper Legends", errors, false);
        host.TerrainGround = groundObject != null ? groundObject.GetComponent<Terrain>() : null;
        host.FloodEnabled = false;

        if (host.LstLocationExam == null)
            host.LstLocationExam = new List<Transform>();

        if (host.LstLocationStartPoint == null)
            host.LstLocationStartPoint = new List<Transform>();

        if (host.LstLocationGatherPoint == null)
            host.LstLocationGatherPoint = new List<Transform>();

        if (host.PaperLegendSpawnPoints == null)
            host.PaperLegendSpawnPoints = new List<Transform>();

        host.LstLocationExam.Clear();
        host.LstLocationStartPoint.Clear();
        host.LstLocationGatherPoint.Clear();
        host.PaperLegendSpawnPoints.Clear();

        if (activeConfig != null)
        {
            PopulateExactTaggedLocationList(host.PaperLegendSpawnPoints, activeConfig, SceneLogicConfig.PaperLegendSpawnTag, errors);
        }

        if (host.PaperLegendSpawnPoints.Count == 0)
            PopulateSceneTaggedLocationList(host.PaperLegendSpawnPoints, SceneLogicConfig.PaperLegendSpawnTag, "thiet lap Paper Legends spawn points", errors, false);

        if (host.PaperLegendSpawnPoints.Count > 0 && host.SpawnPlayerPoint == null)
        {
            host.SpawnPlayerPoint = host.PaperLegendSpawnPoints[0];
        }

        if (host.PaperLegendSpawnPoints.Count == 0)
        {
            RegisterError(errors, $"[PaperLegends][Spawn] Khong tim thay tag '{SceneLogicConfig.PaperLegendSpawnTag}' trong map.");
        }
        else if (host.PaperLegendSpawnPoints.Count < SceneLogicConfig.PaperLegendSpawnPointCount)
        {
            Debug.LogWarning($"[PaperLegends][Spawn] Chi tim thay {host.PaperLegendSpawnPoints.Count}/{SceneLogicConfig.PaperLegendSpawnPointCount} diem '{SceneLogicConfig.PaperLegendSpawnTag}' trong map.");
        }

        return errors.Count == 0;
    }
#endif
    private void PopulateExactTaggedLocationList(List<Transform> targets, SceneLogicConfig config, string tag, List<string> errors)
    {
        if (targets == null || config == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var orderedEntries = config.transformEntries
            .Where(entry => entry != null && string.Equals(entry.tag, tag, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.hierarchyPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.position.x)
            .ThenBy(entry => entry.position.z);

        foreach (var entry in orderedEntries)
        {
            var anchor = EnsureTransformFromEntry(entry, BuildFallbackName(entry.tag), $"thiet lap diem '{entry.tag}'", errors, allowCreate: true, required: false);
            if (anchor != null && !targets.Contains(anchor))
            {
                targets.Add(anchor);
            }
        }
    }

    private void PopulateSceneTaggedLocationList(List<Transform> targets, string tag, string context, List<string> errors, bool required)
    {
        if (targets == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            var objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (var obj in objects.OrderBy(obj => obj.name, StringComparer.OrdinalIgnoreCase))
            {
                AddIfNotNull(targets, obj != null ? obj.transform : null);
            }
        }
        catch (UnityException ex)
        {
            if (required)
            {
                RegisterError(errors, $"Khong tim thay tag '{tag}' cho {context}: {ex.Message}");
            }
            else
            {
                Debug.LogWarning($"Khong tim thay tag '{tag}' cho {context}: {ex.Message}");
            }
        }
    }

    private void PopulateLocationList(List<Transform> targets, SceneLogicConfig config, string tagPrefix, List<string> errors)
    {
        var orderedEntries = config.transformEntries
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.tag) && entry.tag.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.tag, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in orderedEntries)
        {
            var anchor = EnsureTransformFromEntry(entry, BuildFallbackName(entry.tag), $"thiết lập điểm '{entry.tag}'", errors, allowCreate: true, required: false);
            if (anchor != null && !targets.Contains(anchor))
            {
                targets.Add(anchor);
            }
        }
    }

    private void EnsureIndexedTaggedLocations(List<Transform> targets, string tagPrefix, int count, string context, List<string> errors)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 1; i <= count; i++)
        {
            var tag = $"{tagPrefix}{i}";
            if (targets.Any(target => MatchesIndexedTag(target, tag)))
            {
                continue;
            }

            AddIfNotNull(targets, SafeFindWithTag(tag, context, errors, false)?.transform);
        }

        targets.Sort((a, b) => GetIndexedLocationOrder(a, tagPrefix).CompareTo(GetIndexedLocationOrder(b, tagPrefix)));
    }

    private static void CopyLocationList(List<Transform> source, List<Transform> target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.Clear();
        foreach (var location in source)
        {
            AddIfNotNull(target, location);
        }
    }

    private static bool MatchesIndexedTag(Transform target, string tag)
    {
        if (target == null || string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return string.Equals(target.gameObject.tag, tag, StringComparison.OrdinalIgnoreCase)
            || target.name.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int GetIndexedLocationOrder(Transform target, string tagPrefix)
    {
        if (target == null)
        {
            return int.MaxValue;
        }

        if (TryExtractIndexedOrder(target.gameObject.tag, tagPrefix, out var order))
        {
            return order;
        }

        if (TryExtractIndexedOrder(target.name, tagPrefix, out order))
        {
            return order;
        }

        return int.MaxValue;
    }

    private static bool TryExtractIndexedOrder(string value, string tagPrefix, out int order)
    {
        order = int.MaxValue;

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(tagPrefix))
        {
            return false;
        }

        int prefixIndex = value.IndexOf(tagPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
        {
            return false;
        }

        int digitStart = prefixIndex + tagPrefix.Length;
        int digitEnd = digitStart;
        while (digitEnd < value.Length && char.IsDigit(value[digitEnd]))
        {
            digitEnd++;
        }

        return digitEnd > digitStart && int.TryParse(value.Substring(digitStart, digitEnd - digitStart), out order);
    }

#if UNITY_SERVER
    private void ConfigureWaterObject(GameSessionNetWork_Host host, SceneLogicConfig? config, List<string> errors)
    {
        if (host == null)
        {
            return;
        }

        host.WaterObject = null;

        var waterCollider = ResolveWaterCollider(config, errors);
        if (waterCollider != null)
        {
            host.WaterObject = waterCollider.transform;
            Debug.Log($"[MapSceneConfig] WaterObject configured: {host.WaterObject.name} center={waterCollider.center} size={waterCollider.size}");
        }
        else
        {
            Debug.LogError("[MapSceneConfig] WaterObject not found. Không tìm thấy GameObject tag 'Water' để gán water fallback.");
        }
    }
#endif

    private Transform? ResolveTransform(SceneLogicConfig? config, string tag, string context, List<string> errors, bool required = true)
    {
        if (config != null)
        {
            var fromConfig = EnsureTransformFromConfig(config, tag, BuildFallbackName(tag), context, errors, required);
            if (fromConfig != null)
            {
                return fromConfig;
            }
        }

        var fallback = SafeFindWithTag(tag, context, errors, required);
        return fallback != null ? fallback.transform : null;
    }

    private BoxCollider? ResolveBoxCollider(SceneLogicConfig? config, string tag, string context, List<string> errors, bool required = true)
    {
        if (config != null)
        {
            var collider = EnsureBoxColliderFromConfig(config, tag, BuildFallbackName(tag), context, errors, required);
            if (collider != null)
            {
                return collider;
            }
        }

        var fallback = SafeFindWithTag(tag, context, errors, required);
        return fallback != null ? fallback.GetComponent<BoxCollider>() : null;
    }

    private BoxCollider? ResolveTriggerBoxCollider(SceneLogicConfig? config, string tag, string context, List<string> errors, bool required = true)
    {
        var collider = ResolveBoxCollider(config, tag, context, errors, required);
        return ConfigureTriggerCollider(collider);
    }

    private BoxCollider? ResolveWaterCollider(SceneLogicConfig? config, List<string> errors)
    {
        const string tag = "Water";
        const string context = "thiết lập Water";

        if (config != null)
        {
            var entry = config.colliderEntries.FirstOrDefault(e => e != null && string.Equals(e.tag, tag, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                var targetObject = FindGameObjectByHierarchyPath(entry.hierarchyPath);
                if (targetObject == null)
                    targetObject = SafeFindWithTag(tag, context, errors, required: false);
                if (targetObject == null)
                    targetObject = GameObject.Find(BuildFallbackName(tag));

                if (targetObject != null)
                {
                    targetObject.transform.localScale = entry.transformScale;

                    var collider = targetObject.GetComponent<BoxCollider>() ?? targetObject.AddComponent<BoxCollider>();
                    collider.center = entry.center;
                    collider.size = entry.size;
                    collider.isTrigger = true;
                    collider.material = entry.physicMaterial;
                    collider.sharedMaterial = entry.physicMaterial;
                    return collider;
                }
            }
        }

        return ResolveTriggerBoxCollider(config, tag, context, errors, required: false);
    }

    private GameObject? ResolveGameObject(SceneLogicConfig? config, string tag, string context, List<string> errors, bool required = true)
    {
        if (config != null)
        {
            var transform = EnsureTransformFromConfig(config, tag, BuildFallbackName(tag), context, errors, required);
            if (transform != null)
            {
                return transform.gameObject;
            }
        }

        return SafeFindWithTag(tag, context, errors, required);
    }

    private BoxCollider? ConfigureTriggerCollider(BoxCollider? collider)
    {
        if (collider == null)
        {
            return null;
        }

        if (!collider.isTrigger)
        {
            collider.isTrigger = true;
        }

        return collider;
    }

    private Transform? EnsureTransformFromConfig(SceneLogicConfig config, string tag, string fallbackName, string context, List<string> errors, bool required)
    {
        var entry = config.transformEntries.FirstOrDefault(e => e != null && string.Equals(e.tag, tag, StringComparison.OrdinalIgnoreCase));
        return entry != null ? EnsureTransformFromEntry(entry, fallbackName, context, errors, allowCreate: false, required: required) : null;
    }

    private BoxCollider? EnsureBoxColliderFromConfig(SceneLogicConfig config, string tag, string fallbackName, string context, List<string> errors, bool required)
    {
        var entry = config.colliderEntries.FirstOrDefault(e => e != null && string.Equals(e.tag, tag, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return null;
        }

        if (entry.shape != SceneLogicConfig.ColliderShape.Box)
        {
            Debug.LogWarning($"⚠️ [MapSceneConfig] Collider '{tag}' mong đợi dạng Box nhưng cấu hình là '{entry.shape}'. Sẽ cố gắng sử dụng BoxCollider có sẵn.");
        }

        var transform = EnsureTransformFromConfig(config, tag, fallbackName, context, errors, required);
        GameObject targetObject;

        if (transform != null)
        {
            targetObject = transform.gameObject;
        }
        else
        {
            targetObject = SafeFindWithTag(tag, context, errors, required) ?? GameObject.Find(fallbackName);
        }

        if (targetObject == null)
        {
            if (required)
            {
                RegisterError(errors, $"⚠️ [MapSceneConfig] Không tìm thấy đối tượng để gán BoxCollider cho '{tag}' ({context}).");
            }

            return null;
        }

        var collider = targetObject.GetComponent<BoxCollider>() ?? targetObject.AddComponent<BoxCollider>();
        collider.center = entry.center;
        collider.size = entry.size;
        collider.isTrigger = entry.isTrigger;
        collider.material = entry.physicMaterial;
        collider.sharedMaterial = entry.physicMaterial;
        return collider;
    }

    private Transform? EnsureTransformFromEntry(SceneLogicConfig.TransformSyncEntry entry, string fallbackName, string context, List<string> errors, bool allowCreate, bool required)
    {
        if (entry == null)
        {
            if (required)
            {
                RegisterError(errors, $"⚠️ [MapSceneConfig] TransformSyncEntry cho {context} null.");
            }

            return null;
        }

        GameObject targetObject = null;

        if (!string.IsNullOrWhiteSpace(entry.tag))
        {
            targetObject = SafeFindWithTag(entry.tag, context, errors, required);
        }

        if (targetObject == null)
        {
            targetObject = GameObject.Find(fallbackName);
        }

        if (targetObject == null && allowCreate)
        {
            targetObject = new GameObject(fallbackName);
            Debug.LogWarning($"⚠️ [MapSceneConfig] Tạo mới GameObject '{fallbackName}' cho {context} vì không tìm thấy đối tượng phù hợp.");
        }

        if (targetObject == null)
        {
            if (required)
            {
                RegisterError(errors, $"⚠️ [MapSceneConfig] Không tìm thấy hoặc tạo được GameObject cho {context}.");
            }

            return null;
        }

        targetObject.name = fallbackName;

        ApplyEntryTransform(targetObject.transform, entry);

        return targetObject.transform;
    }

    private GameObject? SafeFindWithTag(string tag, string context, List<string> errors, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            var message = $"⚠️ [MapSceneConfig] Tag rỗng khi {context}.";
            Debug.LogWarning(message);
            if (required)
            {
                errors.Add(message);
            }

            return null;
        }

        try
        {
            var target = GameObject.FindWithTag(tag);
            if (target == null)
            {
                var message = $"⚠️ [MapSceneConfig] Không tìm thấy GameObject với tag '{tag}' khi {context}.";
                Debug.LogWarning(message);
                if (required)
                {
                    errors.Add(message);
                }
            }

            return target;
        }
        catch (UnityException ex)
        {
            var message = $"⚠️ [MapSceneConfig] Tag '{tag}' chưa được định nghĩa khi {context}. Chi tiết: {ex.Message}";
            Debug.LogWarning(message);
            if (required)
            {
                errors.Add(message);
            }

            return null;
        }
    }

    private void NotifyInitializationFailed(NetworkObjectManager? networkManager, List<string> errors)
    {
        if (errors == null || errors.Count == 0)
        {
            return;
        }

        networkManager?.ReportInitializationFailure("noti_map_config_failed");
        Debug.LogWarning("⛔ [MapSceneConfig] Dừng khởi tạo map do lỗi cấu hình. Chi tiết:\n - " + string.Join("\n - ", errors));
    }

    private void RegisterError(List<string> errors, string message)
    {
        Debug.LogWarning(message);
        errors.Add(message);
    }

    private static void ApplyEntryTransform(Transform target, SceneLogicConfig.TransformSyncEntry entry)
    {
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

    private static GameObject? FindGameObjectByHierarchyPath(string hierarchyPath)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return null;
        }

        var segments = hierarchyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        for (int sceneIndex = 0; sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount; sceneIndex++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!string.Equals(root.name, segments[0], StringComparison.Ordinal))
                {
                    continue;
                }

                var current = root.transform;
                var matched = true;

                for (var i = 1; i < segments.Length; i++)
                {
                    current = current.Find(segments[i]);
                    if (current == null)
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched && current != null)
                {
                    return current.gameObject;
                }
            }
        }

        return null;
    }

    private string BuildFallbackName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.IsNullOrWhiteSpace(sceneName) ? "SceneAnchor" : $"{sceneName}_SceneAnchor";
        }

        return string.IsNullOrWhiteSpace(sceneName) ? tag : $"{sceneName}_{tag}";
    }

    private static void AddIfNotNull(List<Transform> targets, Transform candidate)
    {
        if (candidate != null && !targets.Contains(candidate))
        {
            targets.Add(candidate);
        }
    }
}
