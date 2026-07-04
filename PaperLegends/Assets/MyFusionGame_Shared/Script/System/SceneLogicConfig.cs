using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
//Hàm này xử lý lấy tạo độ và thông số config collider của toàn bộ object 3D có trong scene đang mở hiện tại theo TAG của chúng.
//(không bao gồm hình ảnh như material) để dùng nó tạo phiên bản ảo trên server để đồng bộ
[CreateAssetMenu(fileName = "SceneLogicConfig", menuName = "Game/Scene Logic Config")]
public class SceneLogicConfig : ScriptableObject
{
    public const string PaperLegendSpawnTag = "paper_legend_spawn";
    public const string PaperLegendDrumTag = "TrongDong";
    public const string PaperLegendDrumObjectiveTag = "DrumObjective";
    public const int PaperLegendSpawnPointCount = 4;

    private static readonly HashSet<string> IgnoredSystemTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "MainCamera",
        "Light",
        "UICanvas",
        "PopupUI"
    };

    [Serializable]
    public class TransformSyncEntry
    {
        public string tag;
        public string hierarchyPath;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 localScale = Vector3.one;
        public bool applyPosition = true;
        public bool applyRotation = true;
        public bool applyScale = true;
    }

    [Serializable]
    public class BoxColliderSyncEntry
    {
        public string tag;
        public string hierarchyPath;
        public ColliderShape shape = ColliderShape.Box;
        public Vector3 center;
        public Vector3 size = Vector3.one;
        public Vector3 transformScale = Vector3.one;
        public float radius = 0.5f;
        public float height = 2f;
        public CapsuleAxis capsuleDirection = CapsuleAxis.YAxis;
        public bool isTrigger;
        public PhysicsMaterial physicMaterial;
        public TerrainData terrainData;
    }

    [Serializable]
    public class PhysicsAssetLibrary
    {
        public enum MaterialLink
        {
            None,
            Ground,
            Rock
        }

        public enum TerrainLink
        {
            None,
            Ground
        }

        [Header("Physics Materials")]
        public PhysicsMaterial groundMaterial;

        public PhysicsMaterial rockMaterial;

        [Header("Terrain Data")]
        public TerrainData groundTerrain;

        public PhysicsMaterial? LoadMaterial(MaterialLink link)
        {
            return link switch
            {
                MaterialLink.Ground => groundMaterial,
                MaterialLink.Rock => rockMaterial,
                _ => null
            };
        }

        public TerrainData? LoadTerrainData(TerrainLink link)
        {
            return link switch
            {
                TerrainLink.Ground => groundTerrain,
                _ => null
            };
        }
    }

    public enum ColliderShape
    {
        Box,
        Capsule,
        Terrain
    }

    public enum CapsuleAxis
    {
        XAxis = 0,
        YAxis = 1,
        ZAxis = 2
    }

    [Header("Scene Binding")]
    public string sceneName = string.Empty;

    [Header("Physics Asset Management")]
    [SerializeField]
    private PhysicsAssetLibrary _physicsAssets = new PhysicsAssetLibrary();

    public PhysicsAssetLibrary PhysicsAssets
    {
        get
        {
            if (_physicsAssets == null)
            {
                _physicsAssets = new PhysicsAssetLibrary();
            }

            return _physicsAssets;
        }
    }

    [Header("Synchronized Transforms")]
    public List<TransformSyncEntry> transformEntries = new();

    [Header("Synchronized Colliders")]
    [FormerlySerializedAs("boxColliderEntries")]
    public List<BoxColliderSyncEntry> colliderEntries = new();

    public bool HasColliderForTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return colliderEntries.Any(entry =>
            entry != null &&
            !string.IsNullOrWhiteSpace(entry.tag) &&
            string.Equals(entry.tag, tag, StringComparison.OrdinalIgnoreCase));
    }

    public BoxColliderSyncEntry EnsureBoxColliderForTag(string tag, ColliderShape shape = ColliderShape.Box)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag must be provided when ensuring a collider entry.", nameof(tag));
        }

        var existing = colliderEntries.FirstOrDefault(entry =>
            entry != null &&
            !string.IsNullOrWhiteSpace(entry.tag) &&
            string.Equals(entry.tag, tag, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            return existing;
        }

        var transformEntry = transformEntries.FirstOrDefault(entry =>
            entry != null &&
            !string.IsNullOrWhiteSpace(entry.tag) &&
            string.Equals(entry.tag, tag, StringComparison.OrdinalIgnoreCase));

        var newEntry = new BoxColliderSyncEntry
        {
            tag = tag,
            hierarchyPath = transformEntry?.hierarchyPath,
            shape = shape,
            center = Vector3.zero,
            size = transformEntry?.localScale ?? Vector3.one,
            transformScale = transformEntry?.localScale ?? Vector3.one,
            isTrigger = string.Equals(tag, "PlayArea", StringComparison.OrdinalIgnoreCase)
        };

        colliderEntries.Add(newEntry);
        return newEntry;
    }

    private static bool SceneMatches(string configScene)
    {
        if (string.IsNullOrEmpty(configScene))
        {
            return true;
        }

        return string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            configScene, StringComparison.Ordinal);
    }

    public void ApplyToScene()
    {
        if (!SceneMatches(sceneName))
        {
            return;
        }

        ApplyTransformEntries();
        ApplyColliderEntries();
    }

    private void ApplyTransformEntries()
    {
#if UNITY_SERVER
        return;
#elif UNITY_EDITOR
        foreach (var entry in transformEntries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.tag))
            {
                continue;
            }

            var go = ResolveGameObject(entry);
            if (go == null)
            {
                continue;
            }

            var transform = go.transform;
            if (entry.applyPosition)
            {
                transform.position = entry.position;
            }

            if (entry.applyRotation)
            {
                transform.rotation = Quaternion.Euler(entry.rotation);
            }

            if (entry.applyScale)
            {
                transform.localScale = entry.localScale;
            }
        }
#endif
    }

    private void ApplyColliderEntries()
    {
#if UNITY_SERVER
        return;
#elif UNITY_EDITOR
        foreach (var entry in colliderEntries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.tag))
            {
                continue;
            }

            var go = ResolveGameObject(entry);
            if (go == null)
            {
                continue;
            }

            ApplyColliderSettings(go, entry);
        }
#endif
    }

    private void ApplyColliderSettings(GameObject go, BoxColliderSyncEntry entry)
    {
        if (go == null || entry == null)
        {
            return;
        }

        var transform = go.transform;
        if (transform != null)
        {
            transform.localScale = entry.transformScale;
        }

        switch (entry.shape)
        {
            case ColliderShape.Box:
            {
                var collider = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                collider.center = entry.center;
                collider.size = entry.size;
                collider.isTrigger = entry.isTrigger;
                collider.material = entry.physicMaterial;
                collider.sharedMaterial = entry.physicMaterial;
                break;
            }
            case ColliderShape.Capsule:
            {
                var collider = go.GetComponent<CapsuleCollider>() ?? go.AddComponent<CapsuleCollider>();
                collider.center = entry.center;
                collider.radius = Mathf.Max(0f, entry.radius);
                collider.height = Mathf.Max(entry.radius * 2f, entry.height);
                collider.direction = Mathf.Clamp((int)entry.capsuleDirection, 0, 2);
                collider.isTrigger = entry.isTrigger;
                collider.material = entry.physicMaterial;
                collider.sharedMaterial = entry.physicMaterial;
                break;
            }
            case ColliderShape.Terrain:
            {
                var terrain = go.GetComponent<Terrain>() ?? go.AddComponent<Terrain>();
                if (entry.terrainData != null)
                {
                    terrain.terrainData = entry.terrainData;
                }

                var collider = go.GetComponent<TerrainCollider>() ?? go.AddComponent<TerrainCollider>();
                if (entry.terrainData != null)
                {
                    collider.terrainData = entry.terrainData;
                }

                collider.isTrigger = entry.isTrigger;
                collider.material = entry.physicMaterial;
                collider.sharedMaterial = entry.physicMaterial;
                break;
            }
            default:
                Debug.LogWarning($"SceneLogicConfig: Collider shape '{entry.shape}' chưa được hỗ trợ cho '{entry.tag}'.");
                break;
        }
    }

#if UNITY_EDITOR
    public void PullFromScene()
    {
        if (!SceneMatches(sceneName))
        {
            Debug.LogWarning($"SceneLogicConfig: Scene hiện tại không khớp với cấu hình '{sceneName}' dữ liệu không được lấy");
            return;
        }

        PullTransformEntries();
        PullColliderEntries();
        EditorUtility.SetDirty(this);
    }

    private void PullTransformEntries()
    {
        var entriesByPath = new Dictionary<string, TransformSyncEntry>(StringComparer.Ordinal);
        var entriesWithPathByTag = new Dictionary<string, List<TransformSyncEntry>>(StringComparer.Ordinal);
        var legacyEntriesByTag = new Dictionary<string, Queue<TransformSyncEntry>>(StringComparer.Ordinal);

        foreach (var entry in transformEntries)
        {
            if (entry == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.tag))
            {
                if (IsIgnoredSystemTag(entry.tag))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.hierarchyPath))
                {
                    if (!entriesWithPathByTag.TryGetValue(entry.tag, out var list))
                    {
                        list = new List<TransformSyncEntry>();
                        entriesWithPathByTag[entry.tag] = list;
                    }

                    list.Add(entry);
                }
                else
                {
                    if (!legacyEntriesByTag.TryGetValue(entry.tag, out var queue))
                    {
                        queue = new Queue<TransformSyncEntry>();
                        legacyEntriesByTag[entry.tag] = queue;
                    }

                    queue.Enqueue(entry);
                }
            }

            if (!string.IsNullOrEmpty(entry.hierarchyPath))
            {
                entriesByPath[entry.hierarchyPath] = entry;
            }
        }

        var updatedEntries = new List<TransformSyncEntry>();
        var processedEntries = new HashSet<TransformSyncEntry>();

        foreach (var go in EnumerateSceneGameObjects())
        {
            var tag = go.tag;
            if (string.IsNullOrWhiteSpace(tag) || string.Equals(tag, "Untagged", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsIgnoredSystemTag(tag))
            {
                continue;
            }

            var path = BuildHierarchyPath(go.transform);
            if (!entriesByPath.TryGetValue(path, out var entry))
            {
                entry = DequeueEntry(legacyEntriesByTag, tag) ?? TakeFirstEntry(entriesWithPathByTag, tag) ?? new TransformSyncEntry();
            }
            else
            {
                RemoveMatchedEntry(entriesWithPathByTag, tag, entry);
            }

            entry.hierarchyPath = path;
            entry.tag = tag;
            UpdateTransformEntryFromGameObject(entry, go.transform);

            if (processedEntries.Add(entry))
            {
                updatedEntries.Add(entry);
            }
        }

        foreach (var entry in transformEntries)
        {
            if (entry == null || processedEntries.Contains(entry))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.tag) && IsIgnoredSystemTag(entry.tag))
            {
                processedEntries.Add(entry);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.hierarchyPath))
            {
                Debug.LogWarning($"SceneLogicConfig: Không tìm thấy GameObject với đường dẫn '{entry.hierarchyPath}' để lấy dữ liệu.");
            }
            else if (!string.IsNullOrWhiteSpace(entry.tag))
            {
                Debug.LogWarning($"SceneLogicConfig: Không tìm thấy GameObject với tag '{entry.tag}' để lấy dữ liệu.");
            }

            processedEntries.Add(entry);
            updatedEntries.Add(entry);
        }

        transformEntries.Clear();
        transformEntries.AddRange(updatedEntries);
    }

    private void PullColliderEntries()
    {
        var entriesByPath = new Dictionary<string, BoxColliderSyncEntry>(StringComparer.Ordinal);
        var entriesWithPathByTag = new Dictionary<string, List<BoxColliderSyncEntry>>(StringComparer.Ordinal);
        var legacyEntriesByTag = new Dictionary<string, Queue<BoxColliderSyncEntry>>(StringComparer.Ordinal);

        foreach (var entry in colliderEntries)
        {
            if (entry == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.tag))
            {
                if (IsIgnoredSystemTag(entry.tag))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.hierarchyPath))
                {
                    if (!entriesWithPathByTag.TryGetValue(entry.tag, out var list))
                    {
                        list = new List<BoxColliderSyncEntry>();
                        entriesWithPathByTag[entry.tag] = list;
                    }

                    list.Add(entry);
                }
                else
                {
                    if (!legacyEntriesByTag.TryGetValue(entry.tag, out var queue))
                    {
                        queue = new Queue<BoxColliderSyncEntry>();
                        legacyEntriesByTag[entry.tag] = queue;
                    }

                    queue.Enqueue(entry);
                }
            }

            if (!string.IsNullOrEmpty(entry.hierarchyPath))
            {
                entriesByPath[entry.hierarchyPath] = entry;
            }
        }

        var updatedEntries = new List<BoxColliderSyncEntry>();
        var processedEntries = new HashSet<BoxColliderSyncEntry>();

        foreach (var go in EnumerateSceneGameObjects())
        {
            var tag = go.tag;
            if (string.IsNullOrWhiteSpace(tag) || string.Equals(tag, "Untagged", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsIgnoredSystemTag(tag))
            {
                continue;
            }

            var path = BuildHierarchyPath(go.transform);
            var entryWasCreated = false;
            if (!entriesByPath.TryGetValue(path, out var entry))
            {
                entry = DequeueEntry(legacyEntriesByTag, tag) ?? TakeFirstEntry(entriesWithPathByTag, tag);

                if (entry == null && TryDetectColliderShape(go, out var shape))
                {
                    entry = new BoxColliderSyncEntry { shape = shape, transformScale = go.transform.localScale };
                    entryWasCreated = true;
                }
            }
            else
            {
                RemoveMatchedEntry(entriesWithPathByTag, tag, entry);
            }

            if (entry == null)
            {
                continue;
            }

            entry.hierarchyPath = path;
            entry.tag = tag;

            if (!PullColliderSettings(go, entry))
            {
                if (!entryWasCreated)
                {
                    processedEntries.Add(entry);
                }

                continue;
            }

            if (processedEntries.Add(entry))
            {
                updatedEntries.Add(entry);
            }
        }

        foreach (var entry in colliderEntries)
        {
            if (entry == null || processedEntries.Contains(entry))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.tag) && IsIgnoredSystemTag(entry.tag))
            {
                processedEntries.Add(entry);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.hierarchyPath))
            {
                Debug.LogWarning($"SceneLogicConfig: Không tìm thấy GameObject với đường dẫn '{entry.hierarchyPath}' để lấy dữ liệu collider.");
            }
            else if (!string.IsNullOrWhiteSpace(entry.tag))
            {
                Debug.LogWarning($"SceneLogicConfig: Không tìm thấy GameObject với tag '{entry.tag}' để lấy dữ liệu collider.");
            }

            processedEntries.Add(entry);
            updatedEntries.Add(entry);
            AssignDefaultPhysicsAssets(entry);
        }

        colliderEntries.Clear();
        colliderEntries.AddRange(updatedEntries);
    }

    private bool PullColliderSettings(GameObject go, BoxColliderSyncEntry entry)
    {
        if (go == null || entry == null)
        {
            return false;
        }

        // Ưu tiên giữ nguyên shape được cấu hình, nhưng tự động đồng bộ theo collider thực tế nếu tìm thấy.
        if (!TryPullCollider(go, entry.shape, entry))
        {
            // Nếu không tìm thấy collider với shape mong muốn, thử lần lượt các loại khác.
            foreach (ColliderShape shape in Enum.GetValues(typeof(ColliderShape)))
            {
                if (shape == entry.shape)
                {
                    continue;
                }

                if (TryPullCollider(go, shape, entry))
                {
                    AssignDefaultPhysicsAssets(entry);
                    return true;
                }
            }

            if (go.TryGetComponent(out Collider fallbackCollider))
            {
                entry.isTrigger = fallbackCollider.isTrigger;
                entry.physicMaterial = fallbackCollider.sharedMaterial;
                entry.transformScale = go.transform.localScale;
            }

            Debug.LogWarning($"SceneLogicConfig: Không thể lấy dữ liệu collider phù hợp cho '{entry.hierarchyPath}:{entry.tag}'.");
            return false;
        }

        AssignDefaultPhysicsAssets(entry);
        return true;
    }

    private bool TryPullCollider(GameObject go, ColliderShape shape, BoxColliderSyncEntry entry)
    {
        var transform = go.transform;
        if (transform == null)
        {
            return false;
        }

        switch (shape)
        {
            case ColliderShape.Box:
                if (go.TryGetComponent(out BoxCollider box))
                {
                    entry.shape = ColliderShape.Box;
                    entry.center = box.center;
                    entry.size = box.size;
                    entry.isTrigger = box.isTrigger;
                    entry.physicMaterial = box.sharedMaterial;
                    entry.transformScale = transform.localScale;
                    return true;
                }
                break;
            case ColliderShape.Capsule:
                if (go.TryGetComponent(out CapsuleCollider capsule))
                {
                    entry.shape = ColliderShape.Capsule;
                    entry.center = capsule.center;
                    entry.radius = capsule.radius;
                    entry.height = capsule.height;
                    entry.capsuleDirection = (CapsuleAxis)Mathf.Clamp(capsule.direction, 0, 2);
                    entry.isTrigger = capsule.isTrigger;
                    entry.physicMaterial = capsule.sharedMaterial;
                    entry.transformScale = transform.localScale;
                    return true;
                }
                break;
            case ColliderShape.Terrain:
                var terrain = go.GetComponent<Terrain>();
                var terrainCollider = go.GetComponent<TerrainCollider>();
                if (terrain != null || terrainCollider != null)
                {
                    entry.shape = ColliderShape.Terrain;
                    entry.terrainData = terrain != null ? terrain.terrainData : terrainCollider?.terrainData;
                    entry.center = Vector3.zero;
                    entry.size = entry.terrainData != null ? entry.terrainData.size : Vector3.zero;
                    if (terrainCollider != null)
                    {
                        entry.isTrigger = terrainCollider.isTrigger;
                        entry.physicMaterial = terrainCollider.sharedMaterial;
                    }
                    entry.transformScale = transform.localScale;
                    return true;
                }
                break;
        }

        return false;
    }

    private void AssignDefaultPhysicsAssets(BoxColliderSyncEntry entry)
    {
#if UNITY_EDITOR
        if (entry == null || string.IsNullOrWhiteSpace(entry.tag))
        {
            return;
        }

        if (string.Equals(entry.tag, "Ground", StringComparison.OrdinalIgnoreCase))
        {
            var assets = PhysicsAssets;
            if (assets != null)
            {
                entry.physicMaterial ??= assets.LoadMaterial(PhysicsAssetLibrary.MaterialLink.Ground);
                entry.terrainData ??= assets.LoadTerrainData(PhysicsAssetLibrary.TerrainLink.Ground);
            }
        }
#endif
    }

    private static IEnumerable<GameObject> EnumerateSceneGameObjects()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != null)
                {
                    yield return transform.gameObject;
                }
            }
        }
    }

    private static TEntry? DequeueEntry<TEntry>(Dictionary<string, Queue<TEntry>> source, string tag)
        where TEntry : class
    {
        if (source == null || string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        if (source.TryGetValue(tag, out var queue) && queue.Count > 0)
        {
            var entry = queue.Dequeue();
            if (queue.Count == 0)
            {
                source.Remove(tag);
            }

            return entry;
        }

        return null;
    }

    private static TEntry? TakeFirstEntry<TEntry>(Dictionary<string, List<TEntry>> source, string tag)
        where TEntry : class
    {
        if (source == null || string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        if (source.TryGetValue(tag, out var list) && list.Count > 0)
        {
            var entry = list[0];
            list.RemoveAt(0);
            if (list.Count == 0)
            {
                source.Remove(tag);
            }

            return entry;
        }

        return null;
    }

    private static void RemoveMatchedEntry<TEntry>(Dictionary<string, List<TEntry>> source, string tag, TEntry entry)
        where TEntry : class
    {
        if (source == null || entry == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (source.TryGetValue(tag, out var list))
        {
            list.Remove(entry);
            if (list.Count == 0)
            {
                source.Remove(tag);
            }
        }
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var stack = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack);
    }

    private static GameObject? ResolveGameObject(TransformSyncEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        var go = FindGameObjectByHierarchyPath(entry.hierarchyPath);
        if (go != null)
        {
            return go;
        }

        if (!string.IsNullOrWhiteSpace(entry.tag))
        {
            return GameObject.FindWithTag(entry.tag);
        }

        return null;
    }

    private static GameObject? ResolveGameObject(BoxColliderSyncEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        var go = FindGameObjectByHierarchyPath(entry.hierarchyPath);
        if (go != null)
        {
            return go;
        }

        if (!string.IsNullOrWhiteSpace(entry.tag))
        {
            return GameObject.FindWithTag(entry.tag);
        }

        return null;
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

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
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

        return null;
    }

    private static void UpdateTransformEntryFromGameObject(TransformSyncEntry entry, Transform transform)
    {
        if (entry == null || transform == null)
        {
            return;
        }

        entry.position = transform.position;
        entry.rotation = transform.rotation.eulerAngles;
        entry.localScale = transform.localScale;
    }

    private bool TryDetectColliderShape(GameObject go, out ColliderShape shape)
    {
        if (go.TryGetComponent(out BoxCollider _))
        {
            shape = ColliderShape.Box;
            return true;
        }

        if (go.TryGetComponent(out CapsuleCollider _))
        {
            shape = ColliderShape.Capsule;
            return true;
        }

        if (go.GetComponent<Terrain>() != null || go.GetComponent<TerrainCollider>() != null)
        {
            shape = ColliderShape.Terrain;
            return true;
        }

        shape = default;
        return false;
    }

    private static bool IsIgnoredSystemTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return IgnoredSystemTags.Contains(tag);
    }
#endif
}
