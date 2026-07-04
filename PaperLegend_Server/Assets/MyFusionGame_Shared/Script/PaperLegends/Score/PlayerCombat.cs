using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PaperLegendCharacterNetworkHandler character;

    public PaperLegendCharacterNetworkHandler Character
    {
        get
        {
            if (character == null)
                character = GetComponent<PaperLegendCharacterNetworkHandler>();

            return character;
        }
    }

    public PlayerRef PlayerRef => Character != null && Character.Object != null
        ? Character.Object.InputAuthority
        : PlayerRef.None;

    public int PlayerId => Character != null ? Character.PlayerId : 0;

    public bool ServerApplyDamage(PlayerCombat attacker, float amount)
    {
        if (!HasStateAuthority || Character == null || attacker == null || attacker.Character == null)
            return false;

        // Replace or wrap this method if a future HP system no longer lives on PaperLegendCharacterNetworkHandler.
        return Character.ServerApplyPinnedDamage(attacker.Character, amount);
    }

    public bool ServerRegisterKill(PlayerCombat victim)
    {
        if (!HasStateAuthority || Character == null || victim == null || victim.Character == null)
            return false;

        return PaperLegendMatchNetworkHost.Instance != null
            && PaperLegendMatchNetworkHost.Instance.ReportCharacterElimination(Character, victim.Character);
    }
}
