using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PaperLegendPaperArrowImpactZone : MonoBehaviour
{
    [SerializeField] private Collider impactCollider;

    private bool _activated;

    public Collider ImpactCollider
    {
        get
        {
            if (impactCollider == null)
                impactCollider = GetComponent<Collider>();

            return impactCollider;
        }
    }

    public void PrepareForLaunch()
    {
        _activated = false;
        SetZoneActive(false);
    }

    public void ServerActivate(
        PaperLegendCharacterNetworkHandler attacker,
        float damage,
        float slowPercent,
        float slowDurationSeconds)
    {
        if (_activated || attacker == null || !attacker.HasStateAuthority)
            return;

        _activated = true;
        SetZoneActive(true);

        Vector3 center = ResolveImpactCenter();
        float radius = ResolveImpactRadius();
        ApplyAreaEffects(attacker, center, radius, damage, slowPercent, slowDurationSeconds);
    }

    private void SetZoneActive(bool active)
    {
        if (impactCollider == null)
            impactCollider = GetComponent<Collider>();

        if (impactCollider != null)
            impactCollider.enabled = active;
    }

    private Vector3 ResolveImpactCenter()
    {
        if (impactCollider != null)
            return impactCollider.bounds.center;

        return transform.position;
    }

    private float ResolveImpactRadius()
    {
        if (impactCollider is SphereCollider sphere)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            return Mathf.Max(0.05f, sphere.radius * scale);
        }

        if (impactCollider != null)
        {
            Bounds bounds = impactCollider.bounds;
            return Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        return 1f;
    }

    public static void ApplyAreaEffects(
        PaperLegendCharacterNetworkHandler attacker,
        Vector3 center,
        float radius,
        float damage,
        float slowPercent,
        float slowDurationSeconds)
    {
        PaperLegendMatchNetworkHost host = PaperLegendMatchNetworkHost.Instance;
        if (host == null)
            return;

        float radiusSq = radius * radius;
        IReadOnlyList<PaperLegendCharacterNetworkHandler> players = host.GetRegisteredPlayers();
        for (int i = 0; i < players.Count; i++)
        {
            PaperLegendCharacterNetworkHandler target = players[i];
            if (target == null || !target.IsAlive || target == attacker || attacker.IsSameFaction(target))
                continue;

            Bounds bounds = target.GetWorldBounds();
            Vector3 offset = bounds.center - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSq)
                continue;

            if (damage > 0f)
                target.ServerApplyPinnedDamage(attacker, damage);

            if (slowPercent > 0f && slowDurationSeconds > 0f)
                target.ServerApplyMoveSlowDebuff(slowPercent, slowDurationSeconds);
        }
    }
}
