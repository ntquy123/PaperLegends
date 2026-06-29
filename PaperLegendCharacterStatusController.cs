using Fusion;
using UnityEngine;

/// <summary>
/// Shared crowd-control helper for Paper Legends characters.
/// Networked state lives on <see cref="PaperLegendCharacterNetworkHandler"/>; this class owns the rules.
/// </summary>
public sealed class PaperLegendCharacterStatusController
{
    private readonly PaperLegendCharacterNetworkHandler _character;
    private bool _stunFreezeActive;
    private bool _cachedKinematicBeforeStunFreeze;

    public PaperLegendCharacterStatusController(PaperLegendCharacterNetworkHandler character)
    {
        _character = character;
    }

    public bool IsStunned => _character.IsStunned;

    public bool BlocksFlickInput => IsStunned;

    public void ServerApplyStun(float durationSeconds)
    {
        if (_character == null || !_character.HasStateAuthority || !_character.IsAlive)
            return;

        durationSeconds = Mathf.Max(0.05f, durationSeconds);
        _character.InternalSetStunState(
            true,
            TickTimer.CreateFromSeconds(_character.Runner, durationSeconds));
        _stunFreezeActive = false;
    }

    public void ServerClearStun()
    {
        if (_character == null || !_character.HasStateAuthority)
            return;

        EndStunFreeze();
        _character.InternalSetStunState(false, TickTimer.None);
    }

    public void ServerTick(float deltaTime)
    {
        if (_character == null || !_character.HasStateAuthority || !_character.IsStunned)
            return;

        if (_character.InternalIsStunTimerExpired())
        {
            ServerClearStun();
            return;
        }

        Rigidbody rigidbody = _character.StatusRigidbody;
        if (rigidbody == null)
            return;

        if (_stunFreezeActive)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            return;
        }

        Vector3 velocity = rigidbody.linearVelocity;
        velocity *= Mathf.Clamp01(1f - _character.StatusStunVelocityDampingPerSecond * Mathf.Max(0f, deltaTime));
        rigidbody.linearVelocity = velocity;
        rigidbody.angularVelocity = Vector3.zero;

        if (velocity.sqrMagnitude <= _character.StatusStunFreezeVelocityThreshold * _character.StatusStunFreezeVelocityThreshold)
            BeginStunFreeze(rigidbody);
    }

    private void BeginStunFreeze(Rigidbody rigidbody)
    {
        if (_stunFreezeActive || rigidbody == null)
            return;

        _cachedKinematicBeforeStunFreeze = rigidbody.isKinematic;
        _stunFreezeActive = true;
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        rigidbody.isKinematic = true;
    }

    private void EndStunFreeze()
    {
        if (!_stunFreezeActive)
            return;

        Rigidbody rigidbody = _character.StatusRigidbody;
        if (rigidbody != null)
        {
            rigidbody.isKinematic = _cachedKinematicBeforeStunFreeze;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        _stunFreezeActive = false;
    }
}

public static class PaperLegendCharacterStatusEffects
{
    public static bool IsStunned(PaperLegendCharacterNetworkHandler character)
    {
        return character != null && character.IsStunned;
    }

    public static bool BlocksFlickInput(PaperLegendCharacterNetworkHandler character)
    {
        return character != null && character.StatusController.BlocksFlickInput;
    }

    public static void ServerApplyStun(PaperLegendCharacterNetworkHandler character, float durationSeconds)
    {
        character?.StatusController.ServerApplyStun(durationSeconds);
    }

    public static void ServerClearStun(PaperLegendCharacterNetworkHandler character)
    {
        character?.StatusController.ServerClearStun();
    }
}
