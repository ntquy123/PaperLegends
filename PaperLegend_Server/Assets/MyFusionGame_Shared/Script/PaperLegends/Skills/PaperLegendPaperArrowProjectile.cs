using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public sealed class PaperLegendPaperArrowProjectile : NetworkBehaviour
{
    [Header("Launch")]
    [SerializeField, Min(0f)] private float minHorizontalImpulse = 2.5f;
    [SerializeField, Min(0.01f)] private float maxHorizontalImpulse = 9f;
    [SerializeField, Min(0f)] private float minUpwardImpulse = 0.15f;
    [SerializeField, Min(0f)] private float maxUpwardImpulse = 0.65f;
    [SerializeField] private AnimationCurve forceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Stop Detection")]
    [SerializeField, Min(0.01f)] private float stopVelocityThreshold = 0.18f;
    [SerializeField, Min(1)] private int stopConfirmTicks = 6;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float despawnDelayAfterImpactSeconds = 2.5f;

    [Header("Impact Zone")]
    [SerializeField] private PaperLegendPaperArrowImpactZone impactZone;
    [SerializeField] private Collider impactCollider;
    [SerializeField, Min(0.05f)] private float fallbackImpactRadius = 1f;

#if !UNITY_SERVER
    [Header("Client Visuals")]
    [SerializeField] private GameObject visualPrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField, Min(0f)] private float impactVfxLifetimeSeconds = 3f;
#endif

    [Networked, OnChangedRender(nameof(OnImpactStateChanged))] private NetworkBool HasImpacted { get; set; }
    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkRotation { get; set; }

    private PaperLegendCharacterNetworkHandler _owner;
    private float _impactDamage;
    private float _impactSlowPercent;
    private float _impactSlowDurationSeconds;
    private TickTimer _despawnTimer;
    private Rigidbody _rigidbody;
    private int _lowVelocityTicks;
    private bool _impactZonePrepared;
#if !UNITY_SERVER
    private const string EmbeddedImpactVfxRootName = "impactVfxRoot";

    private GameObject _visualInstance;
    private Transform _embeddedImpactVfxRoot;
    private bool _lastRenderedImpactState;
#endif

    public override void Spawned()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _impactZonePrepared = false;
        PrepareImpactZone();

        if (HasStateAuthority)
            PublishNetworkTransform();

#if !UNITY_SERVER
        CreateClientVisual();
        ApplyClientVisualTransform();
        _lastRenderedImpactState = HasImpacted;
#endif
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
#if !UNITY_SERVER
        if (_visualInstance != null)
            Destroy(_visualInstance);
#endif
    }

    public void ServerConfigureAndLaunch(
        PaperLegendCharacterNetworkHandler owner,
        Vector3 direction,
        float force01,
        int skillLevel,
        float impactDamage,
        float impactSlowPercent,
        float impactSlowDurationSeconds)
    {
        if (!HasStateAuthority || owner == null)
            return;

        _owner = owner;
        _impactDamage = Mathf.Max(0f, impactDamage);
        _impactSlowPercent = Mathf.Clamp01(impactSlowPercent);
        _impactSlowDurationSeconds = Mathf.Max(0f, impactSlowDurationSeconds);
        HasImpacted = false;
        _despawnTimer = TickTimer.None;
        _lowVelocityTicks = 0;

        PrepareImpactZone();

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float curvedForce = forceCurve != null ? Mathf.Clamp01(forceCurve.Evaluate(Mathf.Clamp01(force01))) : Mathf.Clamp01(force01);
        float horizontalImpulse = Mathf.Lerp(minHorizontalImpulse, maxHorizontalImpulse, curvedForce);
        float upwardImpulse = Mathf.Lerp(minUpwardImpulse, maxUpwardImpulse, curvedForce);
        Vector3 impulse = direction * horizontalImpulse + Vector3.up * upwardImpulse;

        _rigidbody.isKinematic = false;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(impulse, ForceMode.Impulse);
        PublishNetworkTransform();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (HasImpacted)
        {
            PublishNetworkTransform();

            if (_despawnTimer.Expired(Runner))
                Runner.Despawn(Object);

            return;
        }

        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontal.sqrMagnitude <= stopVelocityThreshold * stopVelocityThreshold
            && Mathf.Abs(velocity.y) <= stopVelocityThreshold)
        {
            _lowVelocityTicks++;
        }
        else
        {
            _lowVelocityTicks = 0;
        }

        if (_lowVelocityTicks < stopConfirmTicks)
        {
            PublishNetworkTransform();
            return;
        }

        ServerTriggerImpact();
        PublishNetworkTransform();
    }

    public override void Render()
    {
#if !UNITY_SERVER
        if (!HasStateAuthority)
        {
            transform.SetPositionAndRotation(NetworkPosition, SanitizeRotation(NetworkRotation));
        }

        ApplyClientVisualTransform();

        bool impacted = HasImpacted;
        if (impacted && !_lastRenderedImpactState)
            PlayClientImpactVfx();

        _lastRenderedImpactState = impacted;
#endif
    }

    private void OnImpactStateChanged()
    {
#if !UNITY_SERVER
        bool impacted = HasImpacted;
        if (impacted && !_lastRenderedImpactState)
            PlayClientImpactVfx();

        _lastRenderedImpactState = impacted;
#endif
    }

    private void ServerTriggerImpact()
    {
        if (!HasStateAuthority || HasImpacted)
            return;

        HasImpacted = true;
        _lowVelocityTicks = 0;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;

        PaperLegendPaperArrowImpactZone zone = ResolveImpactZone();
        if (zone != null)
        {
            zone.ServerActivate(_owner, _impactDamage, _impactSlowPercent, _impactSlowDurationSeconds);
        }
        else
        {
            ServerApplyFallbackImpactArea();
        }

        _despawnTimer = despawnDelayAfterImpactSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, despawnDelayAfterImpactSeconds)
            : TickTimer.None;
    }

    private void ServerApplyFallbackImpactArea()
    {
        if (!HasStateAuthority || _owner == null)
            return;

        Collider areaCollider = ResolveImpactCollider();
        Vector3 center = areaCollider != null ? areaCollider.bounds.center : transform.position;
        float radius = ResolveImpactRadius(areaCollider);
        PaperLegendPaperArrowImpactZone.ApplyAreaEffects(
            _owner,
            center,
            radius,
            _impactDamage,
            _impactSlowPercent,
            _impactSlowDurationSeconds);
    }

    private Collider ResolveImpactCollider()
    {
        if (impactCollider != null)
            return impactCollider;

        impactCollider = GetComponent<Collider>();
        return impactCollider;
    }

    private float ResolveImpactRadius(Collider areaCollider)
    {
        if (areaCollider is SphereCollider sphere)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            return Mathf.Max(0.05f, sphere.radius * scale);
        }

        if (areaCollider != null)
        {
            Bounds bounds = areaCollider.bounds;
            return Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        return Mathf.Max(0.05f, fallbackImpactRadius);
    }

    private void PublishNetworkTransform()
    {
        if (!HasStateAuthority)
            return;

        NetworkPosition = transform.position;
        NetworkRotation = SanitizeRotation(transform.rotation);
    }

    private static Quaternion SanitizeRotation(Quaternion rotation)
    {
        float sqrMagnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        if (float.IsNaN(sqrMagnitude) || float.IsInfinity(sqrMagnitude) || sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        float magnitude = Mathf.Sqrt(sqrMagnitude);
        return new Quaternion(rotation.x / magnitude, rotation.y / magnitude, rotation.z / magnitude, rotation.w / magnitude);
    }

    private void PrepareImpactZone()
    {
        if (_impactZonePrepared)
            return;

        PaperLegendPaperArrowImpactZone zone = ResolveImpactZone();
        if (zone != null)
            zone.PrepareForLaunch();

        _impactZonePrepared = true;
    }

    private PaperLegendPaperArrowImpactZone ResolveImpactZone()
    {
        if (impactZone != null)
            return impactZone;

        impactZone = GetComponentInChildren<PaperLegendPaperArrowImpactZone>(true);
        return impactZone;
    }

#if !UNITY_SERVER
    private void CreateClientVisual()
    {
        if (_visualInstance != null || visualPrefab == null)
            return;

        _visualInstance = Instantiate(visualPrefab, transform.position, transform.rotation);
        _embeddedImpactVfxRoot = FindChildRecursive(_visualInstance.transform, EmbeddedImpactVfxRootName);
        if (_embeddedImpactVfxRoot != null)
            _embeddedImpactVfxRoot.gameObject.SetActive(false);

        ApplyClientVisualTransform();
    }

    private void ApplyClientVisualTransform()
    {
        if (_visualInstance == null)
            return;

        _visualInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    private void PlayClientImpactVfx()
    {
        if (impactVfxPrefab == null)
        {
            PlayEmbeddedClientImpactVfx();
            return;
        }

        GameObject instance = Instantiate(impactVfxPrefab, transform.position, transform.rotation);
        if (impactVfxLifetimeSeconds > 0f)
            Destroy(instance, impactVfxLifetimeSeconds);
    }

    private void PlayEmbeddedClientImpactVfx()
    {
        if (_embeddedImpactVfxRoot == null)
            return;

        Transform vfxRoot = _embeddedImpactVfxRoot;
        _embeddedImpactVfxRoot = null;

        vfxRoot.SetParent(null, true);
        vfxRoot.SetPositionAndRotation(transform.position, transform.rotation);
        vfxRoot.gameObject.SetActive(true);

        if (impactVfxLifetimeSeconds > 0f)
            Destroy(vfxRoot.gameObject, impactVfxLifetimeSeconds);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildRecursive(root.GetChild(i), childName);
            if (match != null)
                return match;
        }

        return null;
    }
#endif
}
