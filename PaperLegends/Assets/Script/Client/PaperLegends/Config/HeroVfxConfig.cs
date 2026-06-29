using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Paper Legends/Hero VFX Config", fileName = "HeroVfxConfig")]
public sealed class HeroVfxConfig : ScriptableObject
{
    [Min(0)] public int heroId;

    [Header("Common")]
    public AssetReferenceGameObject normalAttackFx;
    public AssetReferenceGameObject flickFx;
    public AssetReferenceGameObject moveFx;
    public AssetReferenceGameObject hitFx;
    public AssetReferenceGameObject dieFx;

    [Header("Hero 10000001")]
    [Tooltip("Spawned at each edge-bounce landing while skill 11400004 is active.")]
    public GameObject edgeBounceLandingFx;

    [Header("Hero 10000002")]
    [Tooltip("Looping aura shown while Last Stand invincibility is active.")]
    public GameObject lastStandInvincibilityFx;
}
