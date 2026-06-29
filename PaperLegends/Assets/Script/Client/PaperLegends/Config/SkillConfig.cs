using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Paper Legends/Skill Config", fileName = "SkillConfig")]
public sealed class SkillConfig : ScriptableObject
{
    [Min(0)] public int heroId;
    [Min(0)] public int skillId;
    [Range(1, 4)] public int slot = 1;
    public string skillName;
    [TextArea] public string description;
    public AssetReferenceSprite iconRef;
    [Min(0f)] public float cooldown;
    public AssetReferenceAudioClip castSound;
    public AssetReferenceGameObject castVfx;
    [Tooltip("Optional direct prefab reference when the skill VFX is not addressable.")]
    public GameObject castVfxPrefab;
    [Min(0f)] public float castVfxLifetimeSeconds = 2.5f;
    [Min(0f)] public float damage;
    public bool isPassive;
}
