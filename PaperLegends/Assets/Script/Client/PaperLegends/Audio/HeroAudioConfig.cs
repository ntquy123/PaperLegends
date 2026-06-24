using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Game/Hero Audio Config", fileName = "HeroAudioConfig")]
public sealed class HeroAudioConfig : ScriptableObject
{
    [Min(0)] public int heroId;

    [Header("Common")]
    public AssetReferenceAudioClip normalAttack;
    public AssetReferenceAudioClip flick;
    public AssetReferenceAudioClip move;
    public AssetReferenceAudioClip hit;
    public AssetReferenceAudioClip die;
}
