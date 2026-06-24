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
}
