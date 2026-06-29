#if !UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[Serializable]
public struct PaperLegendClientVisualModelEntry
{
    [Min(0)] public int modelId;
    public GameObject visualPrefab;

    [Tooltip("Optional Resources path without extension. Example: Character/10000001.")]
    public string resourcesPath;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PaperLegendCharacterNetworkHandler))]
public sealed class PaperLegendCharacterClientVisualSpawner : MonoBehaviour
{
    private const string DefaultVisualCatalogResourcesPath = "PaperLegends/PaperLegendCharacterVisualCatalog";

    [Header("Client Visual Catalog")]
    [SerializeField] private PaperLegendCharacterVisualCatalog visualCatalog;
    [SerializeField] private bool loadDefaultCatalogFromResources = true;
    [SerializeField] private string defaultCatalogResourcesPath = DefaultVisualCatalogResourcesPath;

    [Header("Client Visual Models")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private PaperLegendClientVisualModelEntry[] visualModels = new PaperLegendClientVisualModelEntry[0];
    [SerializeField] private bool spawnAutomatically = true;

    [Header("Fallback Resources")]
    [SerializeField] private bool loadFromResourcesWhenNoEntry = true;
    [SerializeField] private string fallbackResourcesPathFormat = "Character/{0}";

    [Header("Local Transform")]
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;
    [SerializeField] private Vector3 localScale = Vector3.one;

    [Header("Network Collider Visual")]
    [SerializeField] private bool hideNetworkRenderersWhenVisualReady = true;
    [SerializeField] private bool disableVisualColliders = true;

    [Header("Elimination Visual")]
    [SerializeField] private GameObject eliminationVfxPrefab;
    [SerializeField] private bool loadEliminationVfxFromResources = true;
    [SerializeField] private string eliminationVfxResourcesPath = "PaperLegends/VFX/PaperLegendEliminationVFX";
    [SerializeField] private bool createFallbackEliminationVfx = true;
    [SerializeField] private bool playEliminationVfxOnlyWhenCameraSeesTarget = true;
    [SerializeField, Min(0f)] private float eliminationDarkenSeconds = 2f;
    [SerializeField] private Color eliminationDarkColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [SerializeField, Min(0.1f)] private float eliminationVfxLifetimeSeconds = 3f;
    [SerializeField] private Vector3 eliminationVfxOffset = new Vector3(0f, 0.15f, 0f);

    private PaperLegendCharacterNetworkHandler _character;
    private Renderer[] _networkRenderers;
    private GameObject _spawnedVisual;
    private Coroutine _spawnRoutine;
    private Coroutine _eliminationRoutine;
    private GameObject _cachedEliminationVfxPrefab;
    private int _activeModelId;
    private int _loadingModelId;
    private int _missingModelId;
    private bool _attemptedDefaultCatalogLoad;
    private bool _attemptedEliminationVfxLoad;
    private bool _lastAliveVisible = true;
    private bool _eliminationVisualPlaying;
    private int _lastLifeStateRevision = int.MinValue;
    private readonly List<MaterialColorSnapshot> _materialColorSnapshots = new List<MaterialColorSnapshot>();

    public int ActiveModelId => _activeModelId;
    public GameObject SpawnedVisual => _spawnedVisual;

    private struct MaterialColorSnapshot
    {
        public Material Material;
        public string ColorProperty;
        public Color OriginalColor;
    }

    public static PaperLegendCharacterClientVisualSpawner EnsureFor(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return null;

        if (!character.TryGetComponent(out PaperLegendCharacterClientVisualSpawner spawner))
            spawner = character.gameObject.AddComponent<PaperLegendCharacterClientVisualSpawner>();

        spawner.Bind(character);
        spawner.RequestRefresh();
        return spawner;
    }

    public void RequestRefresh()
    {
        _missingModelId = 0;
        TryStartSpawnForCurrentModel();
    }

    private void Awake()
    {
        Bind(null);
        CacheNetworkRenderers();
    }

    private void OnEnable()
    {
        if (spawnAutomatically)
            RequestRefresh();
    }

    private void Update()
    {
        if (spawnAutomatically)
            TryStartSpawnForCurrentModel();

        bool forceVisibilityUpdate = ConsumeLifeStateRevisionChange();
        ApplyAliveVisibility(forceVisibilityUpdate);
    }

    private void OnDisable()
    {
        StopCurrentSpawnRoutine();
        StopEliminationVisual(restoreColors: true);
        ClearVisual();
        SetNetworkRenderersVisible(true);
    }

    private void OnDestroy()
    {
        StopCurrentSpawnRoutine();
        StopEliminationVisual(restoreColors: false);
        ClearVisual();
    }

    private void Bind(PaperLegendCharacterNetworkHandler character)
    {
        if (character != null)
            _character = character;

        if (_character == null)
            _character = GetComponent<PaperLegendCharacterNetworkHandler>();
    }

    private void TryStartSpawnForCurrentModel()
    {
        Bind(null);

        int modelId = _character != null ? Mathf.Max(0, _character.ResolvedVisualModelId) : 0;
        if (modelId <= 0)
            return;

        if (_activeModelId == modelId || _loadingModelId == modelId || _missingModelId == modelId)
            return;

        StopCurrentSpawnRoutine();
        ClearVisual();
        _spawnRoutine = StartCoroutine(SpawnVisualRoutine(modelId));
    }

    private IEnumerator SpawnVisualRoutine(int modelId)
    {
        _loadingModelId = modelId;
        _missingModelId = 0;

        if (TryResolveEntry(modelId, out PaperLegendClientVisualModelEntry entry))
        {
            if (entry.visualPrefab != null)
            {
                CompleteSpawn(modelId, CreateVisual(modelId, entry.visualPrefab));
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(entry.resourcesPath))
            {
                yield return LoadResourcesVisual(modelId, entry.resourcesPath);
                yield break;
            }
        }

        if (loadFromResourcesWhenNoEntry)
        {
            string fallbackPath = FormatFallbackResourcesPath(modelId);
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                yield return LoadResourcesVisual(modelId, fallbackPath);
                yield break;
            }
        }

        Debug.LogWarning($"Paper Legends client visual not configured for modelId {modelId} on {name}.", this);
        CompleteSpawn(modelId, false);
    }

    private IEnumerator LoadResourcesVisual(int modelId, string resourcesPath)
    {
        ResourceRequest request = Resources.LoadAsync<GameObject>(resourcesPath);
        yield return request;

        if (_loadingModelId != modelId)
            yield break;

        GameObject prefab = request.asset as GameObject;
        if (prefab == null)
        {
            Debug.LogWarning($"Paper Legends client visual prefab not found at Resources/{resourcesPath}.prefab for modelId {modelId}.", this);
            CompleteSpawn(modelId, false);
            yield break;
        }

        CompleteSpawn(modelId, CreateVisual(modelId, prefab));
    }

    private bool CreateVisual(int modelId, GameObject prefab)
    {
        if (prefab == null)
            return false;

        Transform parent = visualRoot != null ? visualRoot : transform;
        _spawnedVisual = Instantiate(prefab, parent, false);
        _spawnedVisual.name = $"{prefab.name}_ClientVisual";
        _spawnedVisual.transform.localPosition = localPositionOffset;
        _spawnedVisual.transform.localRotation = Quaternion.Euler(localEulerOffset);
        _spawnedVisual.transform.localScale = localScale;
        ConfigureVisualPhysics(_spawnedVisual);
        _activeModelId = modelId;

        SetNetworkRenderersVisible(false);
        return true;
    }

    private void CompleteSpawn(int modelId, bool created)
    {
        _loadingModelId = 0;
        _spawnRoutine = null;

        if (created)
            _missingModelId = 0;
        else
            _missingModelId = modelId;

        ApplyAliveVisibility();
    }

    private bool TryResolveEntry(int modelId, out PaperLegendClientVisualModelEntry entry)
    {
        if (TryFindEntry(visualModels, modelId, out entry) && HasUsableVisualSource(entry))
            return true;

        PaperLegendCharacterVisualCatalog catalog = ResolveVisualCatalog();
        if (catalog != null && catalog.TryGetEntry(modelId, out entry) && HasUsableVisualSource(entry))
            return true;

        entry = default(PaperLegendClientVisualModelEntry);
        return false;
    }

    private PaperLegendCharacterVisualCatalog ResolveVisualCatalog()
    {
        if (visualCatalog != null)
            return visualCatalog;

        if (!loadDefaultCatalogFromResources || _attemptedDefaultCatalogLoad)
            return null;

        _attemptedDefaultCatalogLoad = true;
        string path = string.IsNullOrWhiteSpace(defaultCatalogResourcesPath)
            ? DefaultVisualCatalogResourcesPath
            : defaultCatalogResourcesPath;

        visualCatalog = Resources.Load<PaperLegendCharacterVisualCatalog>(path);
        if (visualCatalog == null)
            Debug.LogWarning($"Paper Legends visual catalog not found at Resources/{path}.asset.", this);

        return visualCatalog;
    }

    private static bool TryFindEntry(PaperLegendClientVisualModelEntry[] entries, int modelId, out PaperLegendClientVisualModelEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].modelId == modelId)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = default(PaperLegendClientVisualModelEntry);
        return false;
    }

    private static bool HasUsableVisualSource(PaperLegendClientVisualModelEntry entry)
    {
        return entry.visualPrefab != null || !string.IsNullOrWhiteSpace(entry.resourcesPath);
    }

    private string FormatFallbackResourcesPath(int modelId)
    {
        if (string.IsNullOrWhiteSpace(fallbackResourcesPathFormat))
            return null;

        try
        {
            return string.Format(fallbackResourcesPathFormat, modelId);
        }
        catch (FormatException)
        {
            return fallbackResourcesPathFormat;
        }
    }

    private void CacheNetworkRenderers()
    {
        if (_networkRenderers != null)
            return;

        _networkRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void SetNetworkRenderersVisible(bool visible)
    {
        if (!hideNetworkRenderersWhenVisualReady)
            return;

        CacheNetworkRenderers();
        if (_networkRenderers == null)
            return;

        for (int i = 0; i < _networkRenderers.Length; i++)
        {
            Renderer renderer = _networkRenderers[i];
            if (renderer == null)
                continue;

            if (_spawnedVisual != null && renderer.transform.IsChildOf(_spawnedVisual.transform))
                continue;

            renderer.enabled = visible;
        }
    }

    private bool ConsumeLifeStateRevisionChange()
    {
        if (_character == null)
            return false;

        int revision = _character.LifeStateRevision;
        if (_lastLifeStateRevision == revision)
            return false;

        _lastLifeStateRevision = revision;
        return true;
    }

    private void ApplyAliveVisibility(bool forceUpdate = false)
    {
        bool aliveVisible = _character == null || _character.IsAlive;
        bool visualNeedsUpdate = _spawnedVisual != null && _spawnedVisual.activeSelf != aliveVisible;
        if (!forceUpdate && _lastAliveVisible == aliveVisible && !visualNeedsUpdate)
            return;

        _lastAliveVisible = aliveVisible;

        if (aliveVisible)
        {
            StopEliminationVisual(restoreColors: true);
            ForceShowAliveVisual();

            Debug.Log($"[PaperLegends][Visual] Showing character visual for player={(_character != null ? _character.PlayerId : 0)}.");
            return;
        }

        if (!_eliminationVisualPlaying)
            StartEliminationVisual();
    }

    private void ForceShowAliveVisual()
    {
        if (_spawnedVisual != null)
        {
            if (!_spawnedVisual.activeSelf)
                _spawnedVisual.SetActive(true);

            Renderer[] visualRenderers = _spawnedVisual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                    visualRenderers[i].enabled = true;
            }

            SetNetworkRenderersVisible(false);
            return;
        }

        SetNetworkRenderersVisible(true);
    }

    private void StartEliminationVisual()
    {
        StopEliminationVisual(restoreColors: false);

        GameObject visualTarget = _spawnedVisual != null ? _spawnedVisual : gameObject;
        if (_spawnedVisual != null)
            _spawnedVisual.SetActive(true);
        else
            SetNetworkRenderersVisible(true);

        PlayEliminationVfxIfVisible(visualTarget);
        _eliminationRoutine = StartCoroutine(EliminationVisualRoutine(visualTarget));
        Debug.Log($"[PaperLegends][Visual] Starting elimination visual for player={(_character != null ? _character.PlayerId : 0)}.");
    }

    private IEnumerator EliminationVisualRoutine(GameObject visualTarget)
    {
        _eliminationVisualPlaying = true;
        TweenDarkenVisual(visualTarget);

        float waitSeconds = Mathf.Max(0f, eliminationDarkenSeconds);
        if (waitSeconds > 0f)
            yield return new WaitForSeconds(waitSeconds);

        if (_character == null || !_character.IsAlive)
        {
            if (_spawnedVisual != null)
                _spawnedVisual.SetActive(false);
            else
                SetNetworkRenderersVisible(false);
        }

        _eliminationVisualPlaying = false;
        _eliminationRoutine = null;
        Debug.Log($"[PaperLegends][Visual] Hiding eliminated character visual for player={(_character != null ? _character.PlayerId : 0)}.");
    }

    private void StopEliminationVisual(bool restoreColors)
    {
        if (_eliminationRoutine != null)
        {
            StopCoroutine(_eliminationRoutine);
            _eliminationRoutine = null;
        }

        _eliminationVisualPlaying = false;
        KillMaterialTweens();

        if (restoreColors)
            RestoreMaterialColors();
        else
            _materialColorSnapshots.Clear();
    }

    private void TweenDarkenVisual(GameObject visualTarget)
    {
        CacheMaterialColors(visualTarget);

        float duration = Mathf.Max(0f, eliminationDarkenSeconds);
        for (int i = 0; i < _materialColorSnapshots.Count; i++)
        {
            MaterialColorSnapshot snapshot = _materialColorSnapshots[i];
            if (snapshot.Material == null || string.IsNullOrEmpty(snapshot.ColorProperty))
                continue;

            Color targetColor = Color.Lerp(snapshot.OriginalColor, eliminationDarkColor, 0.82f);
            targetColor.a = snapshot.OriginalColor.a;

            Material material = snapshot.Material;
            string colorProperty = snapshot.ColorProperty;
            DOTween
                .To(() => material.GetColor(colorProperty), value => material.SetColor(colorProperty, value), targetColor, duration)
                .SetEase(Ease.InQuad)
                .SetTarget(material);
        }
    }

    private void CacheMaterialColors(GameObject visualTarget)
    {
        _materialColorSnapshots.Clear();
        if (visualTarget == null)
            return;

        Renderer[] renderers = visualTarget.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                string colorProperty = ResolveColorProperty(material);
                if (material == null || string.IsNullOrEmpty(colorProperty))
                    continue;

                _materialColorSnapshots.Add(new MaterialColorSnapshot
                {
                    Material = material,
                    ColorProperty = colorProperty,
                    OriginalColor = material.GetColor(colorProperty)
                });
            }
        }
    }

    private void RestoreMaterialColors()
    {
        for (int i = 0; i < _materialColorSnapshots.Count; i++)
        {
            MaterialColorSnapshot snapshot = _materialColorSnapshots[i];
            if (snapshot.Material != null && !string.IsNullOrEmpty(snapshot.ColorProperty))
                snapshot.Material.SetColor(snapshot.ColorProperty, snapshot.OriginalColor);
        }

        _materialColorSnapshots.Clear();
    }

    private void KillMaterialTweens()
    {
        for (int i = 0; i < _materialColorSnapshots.Count; i++)
        {
            Material material = _materialColorSnapshots[i].Material;
            if (material != null)
                DOTween.Kill(material);
        }
    }

    private static string ResolveColorProperty(Material material)
    {
        if (material == null)
            return null;

        if (material.HasProperty("_BaseColor"))
            return "_BaseColor";

        if (material.HasProperty("_Color"))
            return "_Color";

        return null;
    }

    private void PlayEliminationVfxIfVisible(GameObject visualTarget)
    {
        if (playEliminationVfxOnlyWhenCameraSeesTarget && !IsTargetVisibleToMainCamera(visualTarget))
            return;

        GameObject prefab = ResolveEliminationVfxPrefab();
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, transform.position + eliminationVfxOffset, Quaternion.identity);
            Destroy(instance, Mathf.Max(0.1f, eliminationVfxLifetimeSeconds));
            return;
        }

        if (createFallbackEliminationVfx)
            CreateFallbackEliminationVfx(transform.position + eliminationVfxOffset);
    }

    private GameObject ResolveEliminationVfxPrefab()
    {
        if (eliminationVfxPrefab != null)
            return eliminationVfxPrefab;

        if (_cachedEliminationVfxPrefab != null)
            return _cachedEliminationVfxPrefab;

        if (!loadEliminationVfxFromResources || _attemptedEliminationVfxLoad || string.IsNullOrWhiteSpace(eliminationVfxResourcesPath))
            return null;

        _attemptedEliminationVfxLoad = true;
        _cachedEliminationVfxPrefab = Resources.Load<GameObject>(eliminationVfxResourcesPath);
        return _cachedEliminationVfxPrefab;
    }

    private bool IsTargetVisibleToMainCamera(GameObject visualTarget)
    {
        Camera camera = Camera.main;
        if (camera == null || visualTarget == null)
            return true;

        Bounds bounds = ResolveVisualBounds(visualTarget);
        Vector3 viewportPoint = camera.WorldToViewportPoint(bounds.center);
        const float viewportMargin = 0.08f;

        return viewportPoint.z > 0f &&
            viewportPoint.x >= -viewportMargin &&
            viewportPoint.x <= 1f + viewportMargin &&
            viewportPoint.y >= -viewportMargin &&
            viewportPoint.y <= 1f + viewportMargin;
    }

    private Bounds ResolveVisualBounds(GameObject visualTarget)
    {
        Renderer[] renderers = visualTarget != null ? visualTarget.GetComponentsInChildren<Renderer>(true) : null;
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one * 0.25f);

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return bounds;
    }

    private void CreateFallbackEliminationVfx(Vector3 position)
    {
        GameObject vfxObject = new GameObject("PaperLegendEliminationFallbackVFX");
        vfxObject.transform.position = position;

        ParticleSystem particleSystem = vfxObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.playOnAwake = false;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.08f, 0.06f, 0.9f),
            new Color(0.75f, 0.62f, 0.38f, 0.75f));

        var emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 18, 26)
        });

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.28f;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.material = new Material(shader);
        }

        particleSystem.Play(true);
        Destroy(vfxObject, Mathf.Max(0.1f, eliminationVfxLifetimeSeconds));
    }

    private void ConfigureVisualPhysics(GameObject visual)
    {
        if (!disableVisualColliders || visual == null)
            return;

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void StopCurrentSpawnRoutine()
    {
        if (_spawnRoutine == null)
            return;

        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
        _loadingModelId = 0;
    }

    private void ClearVisual()
    {
        if (_spawnedVisual == null)
            return;

        StopEliminationVisual(restoreColors: false);
        Destroy(_spawnedVisual);
        _spawnedVisual = null;
        _activeModelId = 0;
    }

    private void OnValidate()
    {
        if (localScale == Vector3.zero)
            localScale = Vector3.one;
    }
}
#endif
