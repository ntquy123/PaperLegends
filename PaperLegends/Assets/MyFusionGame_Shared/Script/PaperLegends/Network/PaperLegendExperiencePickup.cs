using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class PaperLegendExperiencePickup : NetworkBehaviour
{
    [Header("Experience")]
    [SerializeField, Min(1)] private int experienceAmount = 30;
    [SerializeField, Min(0f)] private float respawnSeconds = 10f;

    [Header("Pickup")]
    [SerializeField] private bool forceTriggerColliders = true;
    [SerializeField] private bool destroyOnCollect;

    [Networked] public int ExperienceAmount { get; private set; }
    [Networked] public NetworkBool IsAvailable { get; private set; }
    [Networked] private TickTimer RespawnTimer { get; set; }

    private Collider[] _colliders;
    private Renderer[] _renderers;

    public override void Spawned()
    {
        CacheComponents();

        if (HasStateAuthority)
        {
            ExperienceAmount = Mathf.Max(1, experienceAmount);
            IsAvailable = true;
            RespawnTimer = TickTimer.None;
        }

        ApplyAvailability(IsAvailable);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (IsAvailable)
            return;

        if (RespawnTimer.Expired(Runner))
            SetAvailable(true);
    }

    public override void Render()
    {
        ApplyAvailability(IsAvailable);
    }

    public void Configure(int amount, float respawnDelaySeconds)
    {
        if (!HasStateAuthority)
            return;

        experienceAmount = Mathf.Max(1, amount);
        respawnSeconds = Mathf.Max(0f, respawnDelaySeconds);
        ExperienceAmount = experienceAmount;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority || !IsAvailable)
            return;

        var character = other != null ? other.GetComponentInParent<PaperLegendCharacterNetworkHandler>() : null;
        if (character == null || !character.IsAlive)
            return;

        character.ServerGrantExperience(ExperienceAmount, PaperLegendExperienceSource.Pickup);
        Collect();
    }

    private void Collect()
    {
        if (destroyOnCollect)
        {
            Runner?.Despawn(Object);
            return;
        }

        SetAvailable(false);
        RespawnTimer = respawnSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, respawnSeconds)
            : TickTimer.None;
    }

    private void SetAvailable(bool available)
    {
        IsAvailable = available;
        ApplyAvailability(available);
    }

    private void ApplyAvailability(bool available)
    {
        CacheComponents();

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = available;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = available;
        }
    }

    private void CacheComponents()
    {
        if (_colliders == null || _colliders.Length == 0)
            _colliders = GetComponentsInChildren<Collider>(true);

        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        if (!forceTriggerColliders || _colliders == null)
            return;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].isTrigger = true;
        }
    }
}
