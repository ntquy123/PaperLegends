using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperLegendCharacterInvincibilityAura : MonoBehaviour
{
    [SerializeField] private PaperLegendCharacterNetworkHandler target;
    [SerializeField] private GameObject invincibilityVfxPrefab;
    [SerializeField] private bool autoCreateDefaultAura = true;
    [SerializeField, Min(0.1f)] private float auraRadius = 1.25f;
    [SerializeField, Min(8)] private int auraSegments = 48;
    [SerializeField, Min(0.001f)] private float auraHeightOffset = 0.08f;
    [SerializeField] private Color innerAuraColor = new Color(1f, 0.92f, 0.35f, 0.55f);
    [SerializeField] private Color outerAuraColor = new Color(1f, 0.55f, 0.1f, 0.2f);
    [SerializeField, Min(0.1f)] private float pulseSpeed = 3.5f;
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.12f;

    private GameObject _auraRoot;
    private LineRenderer _innerRing;
    private LineRenderer _outerRing;
    private GameObject _spawnedVfxInstance;
    private GameObject _resolvedVfxPrefab;
    private bool _wasInvincible;

    public static PaperLegendCharacterInvincibilityAura EnsureFor(PaperLegendCharacterNetworkHandler character)
    {
        if (character == null)
            return null;

        PaperLegendCharacterInvincibilityAura aura = character.GetComponentInChildren<PaperLegendCharacterInvincibilityAura>(true);
        if (aura == null)
            aura = character.gameObject.AddComponent<PaperLegendCharacterInvincibilityAura>();

        aura.Bind(character);
        return aura;
    }

    public void Bind(PaperLegendCharacterNetworkHandler character)
    {
        target = character;
        _resolvedVfxPrefab = null;
        EnsureAura();
        RefreshImmediate();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponentInParent<PaperLegendCharacterNetworkHandler>();

        EnsureAura();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            SetVisible(false);
            DestroySpawnedVfx();
            _wasInvincible = false;
            return;
        }

        bool active = target.IsInvincibleAtOneHealth;
        if (active && !_wasInvincible)
            EnsureSpawnedVfx();

        if (!active && _wasInvincible)
            DestroySpawnedVfx();

        _wasInvincible = active;

        if (UsesPrefabVfx())
        {
            SetVisible(false);
            if (active)
                UpdateSpawnedVfxTransform();
            return;
        }

        EnsureAura();
        SetVisible(active);
        if (!active)
            return;

        UpdateAuraTransform();
        AnimateAura();
    }

    private void OnDestroy()
    {
        DestroySpawnedVfx();

        if (_auraRoot != null)
            Destroy(_auraRoot);
    }

    public void RefreshImmediate()
    {
        if (target == null)
        {
            SetVisible(false);
            DestroySpawnedVfx();
            return;
        }

        bool active = target.IsInvincibleAtOneHealth;
        if (active)
            EnsureSpawnedVfx();
        else
            DestroySpawnedVfx();

        _wasInvincible = active;

        if (UsesPrefabVfx())
        {
            SetVisible(false);
            if (active)
                UpdateSpawnedVfxTransform();
            return;
        }

        SetVisible(active);
        UpdateAuraTransform();
    }

    private bool UsesPrefabVfx()
    {
        ResolveVfxPrefab();
        return _resolvedVfxPrefab != null;
    }

    private void ResolveVfxPrefab()
    {
        if (_resolvedVfxPrefab != null)
            return;

        if (invincibilityVfxPrefab != null)
        {
            _resolvedVfxPrefab = invincibilityVfxPrefab;
            return;
        }

        if (target == null)
            return;

        HeroConfig heroConfig = HeroConfigCatalog.ResolveHero(target.CharacterModelId);
        if (heroConfig != null && heroConfig.vfxConfig != null && heroConfig.vfxConfig.lastStandInvincibilityFx != null)
            _resolvedVfxPrefab = heroConfig.vfxConfig.lastStandInvincibilityFx;
    }

    private void EnsureSpawnedVfx()
    {
        ResolveVfxPrefab();
        if (_resolvedVfxPrefab == null)
            return;

        if (_spawnedVfxInstance != null)
            return;

        _spawnedVfxInstance = Instantiate(_resolvedVfxPrefab, transform);
        _spawnedVfxInstance.transform.localPosition = Vector3.zero;
        _spawnedVfxInstance.transform.localRotation = Quaternion.identity;
        UpdateSpawnedVfxTransform();
    }

    private void DestroySpawnedVfx()
    {
        if (_spawnedVfxInstance == null)
            return;

        Destroy(_spawnedVfxInstance);
        _spawnedVfxInstance = null;
    }

    private void UpdateSpawnedVfxTransform()
    {
        if (_spawnedVfxInstance == null || target == null)
            return;

        _spawnedVfxInstance.transform.position = target.transform.position;
    }

    private void EnsureAura()
    {
        if (!autoCreateDefaultAura || UsesPrefabVfx() || (_auraRoot != null && _innerRing != null && _outerRing != null))
            return;

        if (_auraRoot != null)
            return;

        _auraRoot = new GameObject("PaperLegendInvincibilityAura_Runtime");
        _auraRoot.transform.SetParent(transform, false);

        _innerRing = CreateRing("InnerAura", 0.055f, innerAuraColor);
        _outerRing = CreateRing("OuterAura", 0.085f, outerAuraColor);
    }

    private LineRenderer CreateRing(string name, float width, Color color)
    {
        GameObject ringObject = new GameObject(name);
        ringObject.transform.SetParent(_auraRoot.transform, false);

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = auraSegments;
        ring.widthMultiplier = width;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        ring.material = new Material(shader);
        ring.startColor = color;
        ring.endColor = color;

        Vector3[] points = new Vector3[auraSegments];
        for (int i = 0; i < auraSegments; i++)
        {
            float angle = i / (float)auraSegments * Mathf.PI * 2f;
            points[i] = new Vector3(Mathf.Cos(angle) * auraRadius, auraHeightOffset, Mathf.Sin(angle) * auraRadius);
        }

        ring.SetPositions(points);
        return ring;
    }

    private void SetVisible(bool visible)
    {
        if (_auraRoot != null)
            _auraRoot.SetActive(visible);
    }

    private void UpdateAuraTransform()
    {
        if (_auraRoot == null || target == null)
            return;

        _auraRoot.transform.position = target.transform.position;
    }

    private void AnimateAura()
    {
        if (_auraRoot == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude;
        _auraRoot.transform.localScale = Vector3.one * pulse;

        if (_innerRing != null)
        {
            Color inner = innerAuraColor;
            inner.a = innerAuraColor.a * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * pulseSpeed * 1.3f));
            _innerRing.startColor = inner;
            _innerRing.endColor = inner;
        }

        if (_outerRing != null)
        {
            Color outer = outerAuraColor;
            outer.a = outerAuraColor.a * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
            _outerRing.startColor = outer;
            _outerRing.endColor = outer;
        }
    }
}
